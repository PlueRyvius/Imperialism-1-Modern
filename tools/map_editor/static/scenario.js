// The scenario panel: the .scn identity records and the .inf briefing text.
//
// Only fields whose meaning is established are editable — country, province and
// zone names, starting cash, and the year. `flag`, `tclr` and `coun` are shown
// in the record census but not exposed, because we do not know what they mean.

import * as api from './api.js';

const $ = (id) => document.getElementById(id);

const state = { scenario: null, info: null, tab: 'countries', onChange: null };

const TABS = [
  ['countries', 'Countries'],
  ['provinces', 'Provinces'],
  ['zones', 'Zones & ports'],
  ['briefing', 'Briefing'],
  ['records', 'Records'],
];

// Sea zones and port cities share the `zone` tag; ids below this are water.
const FIRST_PORT_ZONE = 40;

export async function load() {
  [state.scenario, state.info] = await Promise.all([api.getScenario(), api.getInfo()]);
  return state.scenario.present;
}

export function present() {
  return Boolean(state.scenario && state.scenario.present);
}

export function setChangeHandler(fn) {
  state.onChange = fn;
}

function announce() {
  if (state.onChange) state.onChange();
}

// --- edit plumbing --------------------------------------------------------

async function editRecord(tag, id, field, value) {
  try {
    const result = await api.editScenario([{ tag, id, field, value }]);
    state.scenario = result.scenario;
    announce();
    return true;
  } catch (err) {
    window.alert(err.message);
    render();
    return false;
  }
}

async function editInfo(field, value, id) {
  try {
    const result = await api.editInfo([{ field, value, id }]);
    state.info = result.info;
    announce();
  } catch (err) {
    window.alert(err.message);
    render();
  }
}

/** Commit on blur or Enter rather than per keystroke. */
function commitOn(input, apply) {
  let last = input.value;
  const fire = async () => {
    if (input.value === last) return;
    const wanted = input.value;
    const ok = await apply(wanted);
    last = ok === false ? last : wanted;
    if (ok === false) input.value = last;
  };
  input.onblur = fire;
  input.onkeydown = (e) => {
    if (e.key === 'Enter' && input.tagName !== 'TEXTAREA') { e.preventDefault(); input.blur(); }
    if (e.key === 'Escape') { input.value = last; input.blur(); }
  };
}

// --- rendering ------------------------------------------------------------

function nameRows(entries, tag, { filter = '', label = (e) => `${e.id}` } = {}) {
  const wrap = document.createElement('div');
  wrap.className = 'rows';
  const needle = filter.trim().toLowerCase();
  let shown = 0;
  for (const entry of entries) {
    if (needle && !`${entry.id} ${entry.name}`.toLowerCase().includes(needle)) continue;
    shown++;
    const row = document.createElement('div');
    row.className = 'row-entry';
    const id = document.createElement('span');
    id.className = 'row-id';
    id.textContent = label(entry);
    const input = document.createElement('input');
    input.type = 'text';
    input.value = entry.name || '';
    commitOn(input, (value) => editRecord(tag, entry.id, 'name', value));
    row.append(id, input);
    wrap.append(row);
  }
  if (!shown) {
    wrap.innerHTML = '<div class="muted" style="padding:6px">nothing matches</div>';
  }
  return wrap;
}

function filterBox(placeholder, onInput) {
  const input = document.createElement('input');
  input.type = 'text';
  input.placeholder = placeholder;
  input.className = 'filter';
  input.value = state.filter || '';
  input.oninput = () => { state.filter = input.value; onInput(input.value); };
  return input;
}

function renderCountries(body) {
  const s = state.scenario;
  const cash = new Map(s.cash.map((c) => [c.id, c.amount]));

  const head = document.createElement('div');
  head.className = 'panel-note';
  head.textContent =
    `${s.countries.length} countries. Starting cash exists only for the ` +
    `${s.cash.length} playable powers.`;
  body.append(head);

  const wrap = document.createElement('div');
  wrap.className = 'rows';
  for (const country of s.countries) {
    const row = document.createElement('div');
    row.className = 'row-entry';
    const id = document.createElement('span');
    id.className = 'row-id';
    id.textContent = country.id;

    const name = document.createElement('input');
    name.type = 'text';
    name.value = country.name || '';
    commitOn(name, (value) => editRecord('cnam', country.id, 'name', value));

    row.append(id, name);
    if (cash.has(country.id)) {
      const money = document.createElement('input');
      money.type = 'number';
      money.className = 'cash';
      money.value = cash.get(country.id);
      money.min = 0;
      commitOn(money, (value) => editRecord('cash', country.id, 'amount', Number(value)));
      row.append(money);
    } else {
      const gap = document.createElement('span');
      gap.className = 'cash muted';
      gap.textContent = '—';
      row.append(gap);
    }
    wrap.append(row);
  }
  body.append(wrap);
}

function renderProvinces(body) {
  const s = state.scenario;
  const note = document.createElement('div');
  note.className = 'panel-note';
  note.textContent =
    `${s.provinces.length} provinces. Ids are sparse — they run 0-${
      s.provinces.length ? s.provinces[s.provinces.length - 1].id : 0
    } in allocated runs, so an id is not an index.`;
  body.append(note);

  const list = document.createElement('div');
  body.append(filterBox('filter by name or id…', () => {
    list.replaceChildren(nameRows(s.provinces, 'pnam', { filter: state.filter }));
  }));
  list.append(nameRows(s.provinces, 'pnam', { filter: state.filter || '' }));
  body.append(list);
}

function renderZones(body) {
  const s = state.scenario;
  const sea = s.zones.filter((z) => z.id < FIRST_PORT_ZONE);
  const ports = s.zones.filter((z) => z.id >= FIRST_PORT_ZONE);

  const note = document.createElement('div');
  note.className = 'panel-note';
  note.textContent =
    'Sea zones and port cities share the same tag; ids below ' +
    `${FIRST_PORT_ZONE} are water. The map may reference zone ids that were ` +
    'never named, which is normal.';
  body.append(note);

  for (const [heading, entries] of [['Sea zones', sea], ['Port cities', ports]]) {
    const title = document.createElement('h3');
    title.textContent = `${heading} (${entries.length})`;
    body.append(title, nameRows(entries, 'zone'));
  }
}

function renderBriefing(body) {
  if (!state.info || !state.info.present) {
    body.innerHTML = '<div class="panel-note">No .inf file alongside this map.</div>';
    return;
  }
  const info = state.info;

  const note = document.createElement('div');
  note.className = 'panel-note';
  note.textContent = 'What the player reads when picking this scenario. ' +
    'Use ^^ for a paragraph break, as the original does.';
  body.append(note);

  const title = document.createElement('input');
  title.type = 'text';
  title.value = info.title;
  commitOn(title, (value) => editInfo('title', value));
  body.append(labelled('Title', title));

  const overview = document.createElement('textarea');
  overview.rows = 6;
  overview.value = info.overview;
  commitOn(overview, (value) => editInfo('overview', value));
  body.append(labelled('Overview', overview));

  const countries = state.scenario.present ? state.scenario.countries : [];
  info.country_sections.forEach((text, index) => {
    const area = document.createElement('textarea');
    area.rows = 4;
    area.value = text;
    commitOn(area, (value) => editInfo('country', value, index));
    const who = countries[index] ? `${index} — ${countries[index].name}` : `${index}`;
    body.append(labelled(`Briefing ${who}`, area));
  });

  const meta = document.createElement('div');
  meta.className = 'meta-row';
  info.metadata.forEach((value, index) => {
    const input = document.createElement('input');
    input.type = 'number';
    input.value = value;
    input.title = index < 7
      ? `Country ${index}: difficulty, or -1 for unplayable`
      : 'Default player country';
    commitOn(input, (raw) => {
      const next = [...state.info.metadata];
      next[index] = Number(raw);
      return editInfo('metadata', next);
    });
    meta.append(input);
  });
  body.append(labelled('Playability — seven difficulty codes then the default player',
                       meta));
}

function renderRecords(body) {
  const counts = state.scenario.counts || {};
  const note = document.createElement('div');
  note.className = 'panel-note';
  note.textContent = 'Everything in the .scn. Only the tags above are editable; ' +
    'the rest are shown so you can see what a scenario contains.';
  body.append(note);

  const year = state.scenario.year;
  if (year && year.turns !== null) {
    const input = document.createElement('input');
    input.type = 'number';
    input.value = year.turns;
    input.min = 0;
    commitOn(input, (value) => editRecord('year', null, 'turns', Number(value)));
    const hint = document.createElement('span');
    hint.className = 'muted';
    hint.textContent = ` turns from 1815 → ${year.calendar}`;
    const row = document.createElement('div');
    row.append(input, hint);
    body.append(labelled('Start year', row));
  }

  const table = document.createElement('table');
  table.className = 'bytes';
  for (const [tag, n] of Object.entries(counts).sort()) {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td>${tag}</td><td>${n}</td>`;
    table.append(tr);
  }
  body.append(table);
}

function labelled(text, control) {
  const wrap = document.createElement('label');
  wrap.className = 'field';
  const span = document.createElement('span');
  span.textContent = text;
  wrap.append(span, control);
  return wrap;
}

export function render() {
  const body = $('scenarioBody');
  if (!body) return;
  body.replaceChildren();

  for (const button of $('scenarioTabs').children) {
    button.classList.toggle('on', button.dataset.tab === state.tab);
  }
  if (!present()) {
    body.innerHTML =
      '<div class="panel-note">No .scn file alongside this map, so there is ' +
      'no scenario to edit. Country names, briefings and starting cash all ' +
      'live in the companion files.</div>';
    return;
  }
  ({
    countries: renderCountries,
    provinces: renderProvinces,
    zones: renderZones,
    briefing: renderBriefing,
    records: renderRecords,
  }[state.tab])(body);
}

export function buildTabs() {
  const bar = $('scenarioTabs');
  bar.replaceChildren();
  for (const [id, label] of TABS) {
    const button = document.createElement('button');
    button.textContent = label;
    button.dataset.tab = id;
    button.onclick = () => { state.tab = id; state.filter = ''; render(); };
    bar.append(button);
  }
}

export async function refresh() {
  await load();
  render();
}
