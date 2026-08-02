// The Units panel: everything the scenario places, listed and locatable.
//
// Three different anchorings, shown honestly rather than uniformly. Civilians
// and infrastructure name a cell. Armies name a province, and are drawn on its
// town — a label, not a position. Fleets name a `zone` record, which the map
// does not share a numbering with, so they can be listed but never located.

import * as api from './api.js';

const $ = (id) => document.getElementById(id);

const state = { units: null, tab: 'civilians', filter: '', onLocate: null };

const TABS = [
  ['civilians', 'Civilians'],
  ['armies', 'Armies'],
  ['ships', 'Fleets'],
  ['infrastructure', 'Infrastructure'],
];

export async function load() {
  state.units = await api.getUnits();
  return state.units.present;
}

export function setLocateHandler(fn) { state.onLocate = fn; }

export function present() {
  return Boolean(state.units && state.units.present);
}

export function strandedCount() {
  if (!present()) return 0;
  return [...state.units.civilians, ...state.units.infrastructure]
    .filter((u) => u.stranded).length;
}

function cellText(cell, width) {
  if (cell === null || cell === undefined) return '—';
  return `(${cell % width}, ${Math.floor(cell / width)})`;
}

function row(label, detail, unit, width) {
  const el = document.createElement('div');
  el.className = `row-entry${unit && unit.stranded ? ' stranded' : ''}`;

  const name = document.createElement('span');
  name.style.flex = '1 1 auto';
  name.textContent = label;

  const where = document.createElement('span');
  where.className = 'row-id';
  where.style.width = 'auto';
  where.textContent = detail;

  el.append(name, where);
  if (unit && unit.cell !== null && unit.cell !== undefined) {
    el.style.cursor = 'pointer';
    el.onclick = () => state.onLocate
      && state.onLocate(unit.cell % width, Math.floor(unit.cell / width));
  }
  return el;
}

function matches(text) {
  return !state.filter || text.toLowerCase().includes(state.filter.toLowerCase());
}

function render(width) {
  const body = $('unitsBody');
  body.replaceChildren();

  for (const button of $('unitsTabs').children) {
    button.classList.toggle('on', button.dataset.tab === state.tab);
  }
  if (!present()) {
    body.innerHTML = '<div class="panel-note">No .scn file alongside this map, ' +
      'so there is nothing placed to show.</div>';
    return;
  }

  const stranded = strandedCount();
  if (stranded) {
    const warn = document.createElement('div');
    warn.className = 'panel-note';
    warn.style.color = '#ff8a80';
    warn.textContent = `${stranded} record${stranded === 1 ? '' : 's'} ` +
      'stranded on cells that are no longer usable land. They are marked red ' +
      'on the map.';
    body.append(warn);
  }

  const filter = document.createElement('input');
  filter.type = 'text';
  filter.className = 'filter';
  filter.placeholder = 'filter…';
  filter.value = state.filter;
  filter.oninput = () => { state.filter = filter.value; render(width); };
  body.append(filter);

  const list = document.createElement('div');
  list.className = 'rows';
  const units = state.units;

  if (state.tab === 'civilians') {
    for (const unit of units.civilians) {
      const label = `${unit.typeName}${unit.ownerName ? ` — ${unit.ownerName}` : ''}`;
      if (!matches(label)) continue;
      list.append(row(
        unit.stranded ? `${unit.typeName} — stranded at sea` : label,
        cellText(unit.cell, width), unit, width));
    }
  } else if (state.tab === 'armies') {
    for (const unit of units.armies) {
      const label = `${unit.count} × ${unit.typeName} — ${unit.provinceName || `province ${unit.province}`}`;
      if (!matches(label)) continue;
      list.append(row(label, cellText(unit.cell, width), unit, width));
    }
  } else if (state.tab === 'ships') {
    const note = document.createElement('div');
    note.className = 'panel-note';
    note.textContent = 'Fleets sit in a sea zone, and the map numbers its ' +
      'oceans differently from the scenario, so they cannot be shown on the ' +
      'map — only listed.';
    body.append(note);
    for (const unit of units.ships) {
      const label = `${unit.count} × ${unit.typeName} — ${unit.countryName || `country ${unit.country}`}`;
      const where = unit.zoneName || `zone ${unit.zone}`;
      if (!matches(`${label} ${where}`)) continue;
      list.append(row(label, where, null, width));
    }
  } else {
    for (const unit of units.infrastructure) {
      const kind = { port: 'Port', rail: 'Railway', deve: 'Development' }[unit.tag];
      const label = unit.tag === 'deve' ? `${kind} level ${unit.level}` : kind;
      if (!matches(label)) continue;
      list.append(row(
        unit.stranded ? `${label} — stranded at sea` : label,
        cellText(unit.cell, width), unit, width));
    }
  }

  if (!list.children.length) {
    list.innerHTML = '<div class="muted" style="padding:6px">nothing matches</div>';
  }
  body.append(list);
}

export function buildTabs(width) {
  const bar = $('unitsTabs');
  bar.replaceChildren();
  for (const [id, label] of TABS) {
    const button = document.createElement('button');
    button.textContent = label;
    button.dataset.tab = id;
    button.onclick = () => { state.tab = id; state.filter = ''; render(width); };
    bar.append(button);
  }
}

export function show(width) { render(width); }

export async function refresh(width) {
  await load();
  if ($('unitsPanel') && !$('unitsPanel').hidden) render(width);
}

export function current() { return state.units; }

/** The scenario records on one cell, for the cell inspector. */
export function describeAt(cell, width) {
  if (!present() || cell === null) return [];
  const out = [];
  for (const unit of state.units.civilians) {
    if (unit.cell === cell) {
      out.push(`${unit.typeName}${unit.ownerName ? ` (${unit.ownerName})` : ''}`);
    }
  }
  for (const unit of state.units.armies) {
    if (unit.cell === cell) out.push(`${unit.count} × ${unit.typeName}`);
  }
  for (const unit of state.units.infrastructure) {
    if (unit.cell !== cell) continue;
    out.push(unit.tag === 'deve'
      ? `Development level ${unit.level}`
      : { port: 'Port', rail: 'Railway' }[unit.tag]);
  }
  return out;
}
