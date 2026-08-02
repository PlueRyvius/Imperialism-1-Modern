// Wiring: pointer input -> tool -> server -> repaint.

import * as api from './api.js';
import { HexRenderer, RESOURCE_ICONS, SCENARIO_MARKS, TERRAIN_COLOURS,
         TERRAIN_ICONS } from './render.js';
import * as tools from './edit.js';
import * as scenario from './scenario.js';
import * as units from './units.js';

const $ = (id) => document.getElementById(id);

const state = {
  map: null, tables: null, renderer: null,
  tool: 'terrain', brush: 1, value: 1, provinceId: 0,
  drag: null, chain: null, pan: null, busy: false, unitDrag: null, unitChoice: 'civi:0',
  dirty: 0, dialogOpen: false, slot: 'resource_a',
};

const TOOLS = [
  ['terrain', 'Terrain', 'Click or drag to paint. Shift-click floods the region.'],
  ['resource', 'Resource', 'Stamps a resource on land cells.'],
  ['province', 'Province', 'Assigns the province id below to painted cells.'],
  ['nation', 'Nation', 'Assigns ownership. Borders redraw automatically.'],
  ['town', 'Town', 'Places a village or capital.'],
  ['river', 'River', 'Drag across adjacent cells to trace. Right-drag erases.'],
  ['rail', 'Rail', 'Drag across adjacent cells to trace. Right-drag erases.'],
  ['unit', 'Units', 'Drag a marker to move it. Click bare ground to place the '
    + 'selected kind. Right-click a marker to remove it.'],
];

// What the Units tool can place. Civilian types come from the scenario's own
// era, so a 1820 map does not offer units it has never heard of.
function unitPalette() {
  const roster = (units.current() || {}).rosters || {};
  const out = {};
  for (const [id, name] of Object.entries(roster.civilian || {})) {
    out[`civi:${id}`] = name;
  }
  out['port:0'] = 'Port';
  out['rail:0'] = 'Railway';
  for (const level of [1, 2, 3]) out[`deve:${level}`] = `Development level ${level}`;
  return out;
}

/** A modal with more than two answers, which `confirm` cannot express. */
function ask(title, bodyHtml, choices) {
  return new Promise((resolve) => {
    $('askTitle').textContent = title;
    $('askBody').innerHTML = bodyHtml;
    const bar = $('askButtons');
    bar.replaceChildren();
    for (const [value, label, primary] of choices) {
      const button = document.createElement('button');
      button.textContent = label;
      if (primary) button.classList.add('on');
      button.onclick = () => { $('ask').hidden = true; resolve(value); };
      bar.append(button);
    }
    $('ask').hidden = false;
  });
}

const LAYERS = [
  // Terrain colour is always drawn; this toggles the terrain *glyphs*. (The
  // old "Terrain" checkbox controlled nothing — the fill has no off switch.)
  ['terrainIcons', 'Terrain icons'],
  ['resources', 'Resources'], ['provinces', 'Provinces'],
  ['nations', 'Nations'], ['rivers', 'Rivers'], ['rail', 'Rail'],
  ['towns', 'Towns'], ['scenario', 'Scenario objects'],
  ['grid', 'Grid'], ['dirty', 'Changed cells'],
];

// Fields the server recomputes; marked in the inspector so it is clear which
// bytes you do not own.
const DERIVED = new Set([
  'national_border', 'province_border', 'land_coastline', 'like_cell_adjacency',
]);

function toast(msg) {
  const el = $('toast');
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => el.classList.remove('show'), 1800);
}

// --- palette --------------------------------------------------------------

function paletteFor(tool) {
  const t = state.tables;
  if (tool === 'terrain') return t.terrain;
  if (tool === 'resource') return { ...t.resource, 255: 'none' };
  if (tool === 'nation') return t.countries;
  if (tool === 'town') return { 34: 'village', 35: 'capital', 0: 'none' };
  if (tool === 'unit') return unitPalette();
  return null;
}

function refreshPalette() {
  const entries = paletteFor(state.tool);
  const sel = $('palette');
  sel.style.display = entries ? '' : 'none';
  $('provinceWrap').style.display = state.tool === 'province' ? '' : 'none';
  $('slotWrap').style.display = state.tool === 'resource' ? '' : 'none';
  if (!entries) return;
  sel.innerHTML = '';
  for (const [k, label] of Object.entries(entries)) {
    const opt = document.createElement('option');
    opt.value = k;
    const icon = { resource: RESOURCE_ICONS, terrain: TERRAIN_ICONS }[state.tool]?.[k];
    opt.textContent = state.tool === 'unit' ? label
      : `${icon ? `${icon} ` : ''}${k} — ${label}`;
    sel.append(opt);
  }
  // The Units tool addresses kinds by name ("civi:2"), not by a byte value.
  if (state.tool === 'unit') state.unitChoice = sel.value;
  else state.value = Number(sel.value);
}

// --- edits ----------------------------------------------------------------

function editsFor(cells) {
  const v = state.value;
  switch (state.tool) {
    case 'terrain':
      return tools.paintTerrain(state.map, cells, v, state.tables.developedTerrain);
    case 'resource': return tools.paintResource(cells, v, state.slot);
    case 'province': return tools.paintProvince(state.map, cells, state.provinceId);
    case 'nation': return tools.paintNation(state.map, cells, v);
    case 'town': return tools.placeTown(cells, v);
    default: return [];
  }
}

/** Fold returned cells back into the flat field arrays and repaint. */
function absorb(cells) {
  const { fields, width } = state.map;
  for (const cell of cells) {
    const i = cell.y * width + cell.x;
    for (const name of Object.keys(fields)) {
      if (name in cell) fields[name][i] = cell[name];
    }
    if (cell.dirty) state.renderer.dirty.add(i);
    else state.renderer.dirty.delete(i);
  }
  state.renderer.draw();
}

async function send(edits, label) {
  if (!edits.length || state.busy) return;
  state.busy = true;
  try {
    let result;
    try {
      result = await api.sendEdits(edits, label);
    } catch (err) {
      if (err.status !== 409) throw err;
      const choice = await askAboutStranding(err.body.stranded);
      if (choice === 'cancel') return;
      // Carry first, so the records are already safe when the paint lands.
      if (choice === 'carry') await carryAll(err.body.stranded);
      if (choice === 'delete') await deleteAll(err.body.stranded);
      result = await api.sendEdits(edits, label, true);
    }
    absorb(result.cells);
    // Painting can strand a record, or free one — the markers have to follow.
    await units.refresh(state.map.width);
    state.renderer.setUnits(units.current());
    state.renderer.draw();
    await refreshState();
  } catch (err) {
    toast(err.message);
  } finally {
    state.busy = false;
  }
}

function askAboutStranding(stranded) {
  const rows = stranded.map((r) => {
    const where = r.carryTo
      ? `could move to (${r.carryTo[0]}, ${r.carryTo[1]})`
      : '<b>nowhere to move it</b>';
    return `${r.label} at (${r.at[0]}, ${r.at[1]}) — ${where}`;
  }).join('<br>');
  const anyHomeless = stranded.some((r) => !r.carryTo);
  return ask(
    `This would strand ${stranded.length} record${stranded.length === 1 ? '' : 's'}`,
    `${rows}<br><br>The game reads these as positions on land. Leaving one in ` +
    'open water is what crashes it.',
    [
      ['carry', anyHomeless ? 'Move what can move' : 'Move them to land', true],
      ['delete', 'Delete them'],
      ['accept', 'Paint anyway'],
      ['cancel', 'Cancel'],
    ]);
}

async function carryAll(stranded) {
  for (const record of stranded) {
    if (!record.carryTo) continue;
    await api.moveUnit(record.uid, record.carryTo[0], record.carryTo[1]);
  }
}

async function deleteAll(stranded) {
  for (const record of stranded) await api.deleteUnit(record.uid);
}

async function refreshState() {
  const s = await api.getState();
  $('undo').disabled = !s.canUndo;
  $('redo').disabled = !s.canRedo;
  $('currentPath').textContent = s.path;
  state.dirty = s.dirty;

  // Name each file that has unsaved work, so a scenario edit is not invisible
  // just because the map is untouched.
  const files = s.dirtyFiles || {};
  const pending = [];
  if (files.map) pending.push(`${files.map} cell${files.map === 1 ? '' : 's'}`);
  if (files.scenario) pending.push('scenario');
  if (files.info) pending.push('briefing');
  $('fileInfo').textContent = pending.length ? `changed: ${pending.join(', ')}` : 'no changes';
}

// --- opening ---------------------------------------------------------------

/**
 * The dialog is a native window owned by the server process, so it can sit
 * open indefinitely. Buttons are disabled while it is up: the page is still
 * live and painting into a map that is about to be replaced would be lost.
 */
async function withDialog(button, run) {
  if (state.dialogOpen) return null;
  state.dialogOpen = true;
  const label = button.textContent;
  button.textContent = 'waiting…';
  button.disabled = true;
  try {
    return await run();
  } finally {
    state.dialogOpen = false;
    button.disabled = false;
    button.textContent = label;
  }
}

async function openMap() {
  const result = await withDialog($('open'), async () => {
    try {
      return await api.browseOpen();
    } catch (err) {
      if (err.status !== 409) { toast(err.message); return null; }
      const ok = confirm(
        `${err.body.dirty} cell${err.body.dirty === 1 ? '' : 's'} changed since ` +
        `the last save.\n\nOpening another map discards those edits. Continue?`);
      if (!ok) return null;
      try {
        return await api.browseOpen(true);
      } catch (retry) { toast(retry.message); return null; }
    }
  });
  if (!result || result.cancelled) return;
  // The document was replaced wholesale; reload rather than trying to patch
  // the renderer's view of a map that no longer exists.
  location.reload();
}

// --- pointer --------------------------------------------------------------

function cellFromEvent(ev) {
  const r = state.renderer.canvas.getBoundingClientRect();
  return state.renderer.cellAt(ev.clientX - r.left, ev.clientY - r.top);
}

function isPathTool() {
  return state.tool === 'river' || state.tool === 'rail';
}

const MIDDLE_BUTTON = 1;

function onDown(ev) {
  if (ev.button === MIDDLE_BUTTON) {
    ev.preventDefault();
    state.pan = { x: ev.clientX, y: ev.clientY };
    state.renderer.canvas.setPointerCapture?.(ev.pointerId);
    state.renderer.canvas.style.cursor = 'grabbing';
    return;
  }
  const cell = cellFromEvent(ev);
  if (!cell) return;
  ev.preventDefault();

  if (state.tool === 'unit') { onUnitDown(ev, cell); return; }

  if (isPathTool()) {
    state.chain = { cells: [cell], erase: ev.button === 2 };
    return;
  }
  if (ev.shiftKey && state.tool === 'terrain') {
    const region = tools.floodRegion(state.map, ...cell);
    toast(`flood: ${region.length} cells`);
    send(editsFor(region), 'flood');
    return;
  }
  state.drag = new Set();
  paintAt(cell);
}

function paintAt(cell) {
  const key = `${cell[0]},${cell[1]}`;
  if (state.drag.has(key)) return;
  const cells = tools.disc(state.map, cell[0], cell[1], state.brush - 1);
  for (const c of cells) state.drag.add(`${c[0]},${c[1]}`);
  send(editsFor(cells), state.tool);
}

function onMove(ev) {
  if (state.pan) {
    state.renderer.panBy(ev.clientX - state.pan.x, ev.clientY - state.pan.y);
    state.pan = { x: ev.clientX, y: ev.clientY };
    state.renderer.draw();
    return;
  }
  const cell = cellFromEvent(ev);
  state.renderer.hover = cell;
  showCell(cell);
  state.renderer.draw();
  if (!cell) return;

  if (state.unitDrag) {
    const [sx, sy] = state.unitDrag.startedAt;
    // A few pixels of slop so a click is never mistaken for a drag.
    if (Math.abs(ev.clientX - sx) > 3 || Math.abs(ev.clientY - sy) > 3) {
      state.unitDrag.moved = true;
    }
    state.unitDrag.to = cell;
    state.renderer.dragTarget = state.unitDrag.unit
      ? [...cell, canHold(...cell)] : null;
    state.renderer.draw();
    return;
  }

  if (state.chain) {
    const last = state.chain.cells[state.chain.cells.length - 1];
    if (last[0] === cell[0] && last[1] === cell[1]) return;
    if (tools.directionTo(state.map, last[0], last[1], cell[0], cell[1]) === null) return;
    state.chain.cells.push(cell);
  } else if (state.drag) {
    paintAt(cell);
  }
}

function onUp() {
  if (state.unitDrag) { onUnitUp(); return; }
  if (state.pan) {
    state.pan = null;
    state.renderer.canvas.style.cursor = '';
    return;
  }
  if (state.chain) {
    const { cells, erase } = state.chain;
    state.chain = null;
    if (cells.length > 1) {
      send(tools.drawPath(state.map, cells, state.tool, erase), state.tool);
    }
  }
  state.drag = null;
}

// Trackpads report pixels, mice report lines, and some browsers report pages.
// Normalise to something roughly per-notch before scaling.
const WHEEL_SCALE = { 0: 1, 1: 16, 2: 400 };

function onWheel(ev) {
  ev.preventDefault();
  const delta = ev.deltaY * (WHEEL_SCALE[ev.deltaMode] ?? 1);
  if (!delta) return;
  const r = state.renderer.canvas.getBoundingClientRect();
  const factor = Math.exp(-delta / 400);
  if (!state.renderer.zoomAt(ev.clientX - r.left, ev.clientY - r.top, factor)) return;

  // The cell under the cursor can change as hexes grow or shrink beneath it.
  const cell = cellFromEvent(ev);
  state.renderer.hover = cell;
  showCell(cell);
  state.renderer.draw();
}

// --- the Units tool -------------------------------------------------------

function canHold(x, y) {
  const i = y * state.map.width + x;
  return state.map.fields.terrain[i] !== 0 && state.map.fields.province[i] !== 65535;
}

async function refreshUnits() {
  await units.refresh(state.map.width);
  state.renderer.setUnits(units.current());
  state.renderer.draw();
  await refreshState();
}

async function onUnitDown(ev, cell) {
  const here = state.renderer.unitsAt(...cell).filter((u) => u.tag !== 'army');

  if (ev.button === 2) {
    if (!here.length) return;
    const unit = here[0];
    const what = unit.typeName || { port: 'Port', rail: 'Railway', deve: 'Development' }[unit.tag];
    if (!confirm(`Remove ${what} at (${cell[0]}, ${cell[1]})?`)) return;
    try { await api.deleteUnit(unit.uid); await refreshUnits(); toast(`removed ${what}`); }
    catch (err) { toast(err.message); }
    return;
  }
  // Capture the pointer so every move and the release come back here even if
  // the cursor strays over the status bar or outside the canvas mid-drag.
  try { state.renderer.canvas.setPointerCapture(ev.pointerId); } catch { /* ignore */ }
  state.unitDrag = {
    unit: here.length ? here[0] : null,
    from: cell,
    pointerId: ev.pointerId,
    startedAt: [ev.clientX, ev.clientY],
    moved: false,
  };
  if (here.length) {
    // Armies are excluded above: their marker shows a province, not a place,
    // so dragging one would imply a precision the data does not have.
    const what = here[0].typeName
      || { port: 'Port', rail: 'Railway', deve: 'Development' }[here[0].tag];
    toast(`moving ${what} — drop it on land`);
  }
}

/** Place the selected kind. Runs on release, so a press that turns into a
 *  drag never leaves a stray unit behind where you started. */
async function placeUnit(cell) {
  if (!canHold(...cell)) { toast('needs land with a province'); return; }
  const [tag, value] = (state.unitChoice || 'civi:0').split(':');
  try {
    await api.addUnit(tag, cell[0], cell[1], Number(value));
    await refreshUnits();
  } catch (err) { toast(err.message); }
}

async function onUnitUp() {
  const drag = state.unitDrag;
  state.unitDrag = null;
  state.renderer.dragTarget = null;
  if (!drag) return;
  try { state.renderer.canvas.releasePointerCapture(drag.pointerId); } catch { /* ignore */ }

  const target = drag.to || drag.from;
  const sameCell = target[0] === drag.from[0] && target[1] === drag.from[1];

  if (!drag.unit) {
    // Pressed bare ground: a click places, a drag was just a stray gesture.
    if (!drag.moved) await placeUnit(drag.from);
    else state.renderer.draw();
    return;
  }
  if (sameCell) { state.renderer.draw(); return; }
  try {
    await api.moveUnit(drag.unit.uid, target[0], target[1]);
    await refreshUnits();
  } catch (err) {
    toast(err.message);
    state.renderer.draw();
  }
}

// --- inspector ------------------------------------------------------------

function showCell(cell) {
  const box = $('cellInfo');
  if (!cell) { box.textContent = 'hover a cell'; return; }
  const [x, y] = cell;
  const i = y * state.map.width + x;
  const f = state.map.fields;
  const name = (table, v) => state.tables[table][v] ?? v;

  const rows = [
    ['x, y', `${x}, ${y}`],
    ['index', i],
    ['terrain', `${f.terrain[i]} ${name('terrain', f.terrain[i])}`],
    ['underlay', `${f.terrain_underlay[i]} ${name('underlay', f.terrain_underlay[i])}`],
    ['resource A', `${f.resource_a[i]} ${name('resource', f.resource_a[i])}`],
    ['resource B', `${f.resource_b[i]} ${name('resource', f.resource_b[i])}`],
    ['province', f.province[i]],
    ['nation', `${f.nation_zone_a[i]} ${name('countries', f.nation_zone_a[i]) ?? ''}`],
    ['town', `${f.town_type[i]} ${name('town', f.town_type[i])}`],
    ['river', maskText(f.river[i])],
    ['rail', maskText(f.rail[i])],
    ['national_border', maskText(f.national_border[i])],
    ['province_border', maskText(f.province_border[i])],
    ['land_coastline', maskText(f.land_coastline[i])],
    ['like_cell_adjacency', maskText(f.like_cell_adjacency[i])],
  ];
  const placed = units.describeAt(i, state.map.width);
  const placedHtml = placed.length
    ? `<div class="hint" style="margin-bottom:6px;color:var(--accent)">` +
      `${placed.join('<br>')}</div>`
    : '';

  box.innerHTML = placedHtml + '<table class="bytes">' + rows.map(([k, v]) =>
    `<tr class="${DERIVED.has(k) ? 'derived' : ''}"><td>${k}</td><td>${v}</td></tr>`
  ).join('') + '</table><div class="hint">· recomputed for you</div>';
}

function maskText(mask) {
  if (!mask) return '0';
  const dirs = state.tables.directions.filter((_, d) => mask & (1 << d));
  return `${mask} ${dirs.join(',')}`;
}

// --- validation -----------------------------------------------------------

/**
 * Repairs are ordinary cell edits, so they go through the normal edit path:
 * undoable, re-derived, and counted as unsaved work like anything else. A fix
 * is never applied on its own — you press the button.
 */
async function applyFixes(issues) {
  const edits = [];
  const seen = new Set();
  for (const issue of issues) {
    for (const edit of issue.fix || []) {
      // Last write wins per cell+field; re-validation catches any bad merge.
      const key = `${edit.x},${edit.y},${edit.field}`;
      if (seen.has(key)) edits[edits.findIndex((e) => `${e.x},${e.y},${e.field}` === key)] = edit;
      else { seen.add(key); edits.push(edit); }
    }
  }
  if (!edits.length) return;
  await send(edits, 'fix');
  await runChecks();
}

function locate(x, y) {
  if (x < 0) return;
  state.renderer.hover = [x, y];
  showCell(state.renderer.hover);
  state.renderer.draw();
}

async function runChecks() {
  const box = $('issues');
  box.textContent = 'checking…';
  const { issues } = await api.getIssues();
  if (!issues.length) {
    box.innerHTML = '<span class="muted">no problems found</span>';
    return;
  }
  const fixable = issues.filter((i) => i.fix && i.fix.length);
  box.replaceChildren();

  const summary = document.createElement('div');
  summary.className = 'hint';
  summary.textContent = `${issues.length} issue${issues.length === 1 ? '' : 's'}` +
    (fixable.length ? `, ${fixable.length} fixable` : ', none fixable automatically');
  box.append(summary);

  if (fixable.length) {
    const all = document.createElement('button');
    all.textContent = `Fix ${fixable.length} automatically`;
    all.style.width = '100%';
    all.style.marginBottom = '6px';
    all.onclick = () => applyFixes(fixable);
    box.append(all);
  }

  for (const issue of issues.slice(0, 200)) {
    const el = document.createElement('div');
    el.className = `issue ${issue.severity}`;
    const where = issue.x < 0 ? '' : `(${issue.x}, ${issue.y}) `;
    const line = document.createElement('div');
    line.textContent = `${where}${issue.message}`;
    line.onclick = () => locate(issue.x, issue.y);
    el.append(line);

    if (issue.fix && issue.fix.length) {
      const button = document.createElement('button');
      button.textContent = 'Fix';
      button.style.marginTop = '4px';
      button.onclick = (e) => { e.stopPropagation(); applyFixes([issue]); };
      el.append(button);
    } else if (issue.why) {
      // Say what is needed rather than just refusing.
      const why = document.createElement('div');
      why.className = 'muted';
      why.style.marginTop = '3px';
      why.textContent = issue.why;
      el.append(why);
    }
    box.append(el);
  }
  if (issues.length > 200) {
    const more = document.createElement('div');
    more.className = 'muted';
    more.textContent = `…and ${issues.length - 200} more`;
    box.append(more);
  }
}

// --- setup ----------------------------------------------------------------

function buildControls() {
  const toolBox = $('tools');
  for (const [id, label, hint] of TOOLS) {
    const b = document.createElement('button');
    b.textContent = label;
    b.dataset.tool = id;
    b.onclick = () => {
      state.tool = id;
      for (const other of toolBox.children) other.classList.toggle('on', other === b);
      $('toolHint').textContent = hint;
      refreshPalette();
    };
    toolBox.append(b);
  }
  toolBox.firstChild.click();

  const layerBox = $('layers');
  for (const [id, label] of LAYERS) {
    const wrap = document.createElement('label');
    wrap.className = 'check';
    const cb = document.createElement('input');
    cb.type = 'checkbox';
    cb.checked = state.renderer.layers[id];
    cb.onchange = () => {
      state.renderer.layers[id] = cb.checked;
      state.renderer.draw();
    };
    wrap.append(cb, label);
    layerBox.append(wrap);
  }

  $('brush').oninput = (e) => {
    state.brush = Number(e.target.value);
    $('brushLabel').textContent = state.brush;
  };
  $('palette').onchange = (e) => {
    if (state.tool === 'unit') state.unitChoice = e.target.value;
    else state.value = Number(e.target.value);
  };
  for (const b of $('slots').children) {
    b.onclick = () => {
      state.slot = b.dataset.slot;
      for (const other of $('slots').children) other.classList.toggle('on', other === b);
    };
  }
  $('provinceId').oninput = (e) => { state.provinceId = Number(e.target.value); };

  // A step can land in the map, the .scn or the .inf, so apply whichever the
  // server says moved.
  const stepBack = async (call) => {
    const result = await call();
    absorb(result.cells);
    if (result.scenario || result.info) scenario.refresh();
    await units.refresh(state.map.width);
    state.renderer.setUnits(units.current());
    state.renderer.draw();
    refreshState();
  };
  $('undo').onclick = () => stepBack(api.undo);
  $('redo').onclick = () => stepBack(api.redo);
  const settle = (saved) => {
    toast(`saved ${saved}`);
    state.renderer.dirty.clear();
    state.renderer.draw();
    refreshState();
  };
  $('save').onclick = async () => {
    try {
      const written = (await api.save()).saved;
      settle(written.length
        ? written.map((p) => p.split(/[\/]/).pop()).join(', ')
        : 'nothing to save');
    } catch (err) { toast(err.message); }
  };
  $('saveAs').onclick = async () => {
    const result = await withDialog($('saveAs'), async () => {
      try { return await api.browseSave(); } catch (err) { toast(err.message); return null; }
    });
    if (result && !result.cancelled) {
      settle(result.saved.map((p) => p.split(/[\/]/).pop()).join(', '));
    }
  };
  $('open').onclick = openMap;
  $('check').onclick = runChecks;

  scenario.buildTabs();
  scenario.setChangeHandler(refreshState);
  $('scenario').onclick = () => {
    $('scenarioPanel').hidden = false;
    scenario.render();
  };
  $('scenarioClose').onclick = () => { $('scenarioPanel').hidden = true; };

  units.buildTabs(state.map.width);
  units.setLocateHandler((x, y) => {
    $('unitsPanel').hidden = true;
    locate(x, y);
  });
  $('units').onclick = () => {
    $('unitsPanel').hidden = false;
    units.show(state.map.width);
  };
  $('unitsClose').onclick = () => { $('unitsPanel').hidden = true; };

  // One checkbox per unit kind, so a busy map can be quietened selectively.
  const unitBox = $('unitLayers');
  for (const [tag, mark] of Object.entries(SCENARIO_MARKS)) {
    const wrap = document.createElement('label');
    wrap.className = 'check';
    const cb = document.createElement('input');
    cb.type = 'checkbox';
    cb.checked = true;
    cb.onchange = () => {
      if (cb.checked) state.renderer.unitLayers.add(tag);
      else state.renderer.unitLayers.delete(tag);
      state.renderer.draw();
    };
    const swatch = document.createElement('span');
    swatch.style.cssText =
      `width:9px;height:9px;border-radius:2px;background:${mark.colour};` +
      'display:inline-block;flex:0 0 auto';
    wrap.append(cb, swatch, mark.label);
    unitBox.append(wrap);
  }

  window.addEventListener('keydown', (e) => {
    if (!(e.ctrlKey || e.metaKey)) return;
    if (e.key === 'z') { e.preventDefault(); $('undo').click(); }
    if (e.key === 'y' || (e.key === 'Z' && e.shiftKey)) { e.preventDefault(); $('redo').click(); }
    if (e.key === 's') { e.preventDefault(); $('save').click(); }
  });
}

async function main() {
  const [map, tables] = await Promise.all([
    api.loadMap(), api.loadTables(), scenario.load(), units.load()]);
  state.map = map;
  state.tables = tables;

  const canvas = $('map');
  const renderer = new HexRenderer(canvas, map);
  state.renderer = renderer;

  // Fit once. Later resizes keep whatever zoom you have set rather than
  // yanking the view back to the whole map.
  // Wait for a stage with real dimensions before fitting: a page that loads
  // into a collapsed or zero-sized pane would otherwise latch onto a
  // meaningless scale and never recover.
  let fitted = false;
  const relayout = () => {
    const [w, h] = renderer.resize();
    if (!fitted && w > 0 && h > 0) { renderer.fitTo(w, h); fitted = true; }
    renderer.draw();
  };
  window.addEventListener('resize', relayout);

  buildControls();
  relayout();

  canvas.addEventListener('pointerdown', onDown);
  canvas.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
  canvas.addEventListener('wheel', onWheel, { passive: false });
  canvas.addEventListener('contextmenu', (e) => e.preventDefault());
  // Browsers open their autoscroll widget on middle *mousedown*, which
  // pointerdown cannot suppress on its own.
  canvas.addEventListener('mousedown', (e) => {
    if (e.button === MIDDLE_BUTTON) e.preventDefault();
  });
  canvas.addEventListener('pointercancel', () => {
    if (state.unitDrag) { state.unitDrag = null; renderer.dragTarget = null; }
    state.pan = null;
    renderer.draw();
  });
  // Pointer capture routes the release to the canvas, so listen there too.
  canvas.addEventListener('pointerup', onUp);
  canvas.addEventListener('pointerleave', () => {
    if (state.pan || state.unitDrag) return;
    renderer.hover = null;
    renderer.draw();
  });

  $('status').textContent =
    `${map.width}×${map.height} · ${map.path.split(/[\\/]/).pop()}` +
    (map.wrapX ? ' · wraps E–W' : '') + ' · scroll to zoom, middle-drag to pan';
  await refreshState();
}

main().catch((err) => { $('status').textContent = `error: ${err.message}`; });
