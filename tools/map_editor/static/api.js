// Thin wrapper over the editor server. Nothing here knows about map bytes:
// we send field edits and receive decoded cells.

async function req(path, options) {
  const res = await fetch(path, options);
  const body = await res.json();
  if (!res.ok || body.error) {
    // Callers need to tell "you have unsaved work" apart from a real failure,
    // so the status and payload travel with the error.
    const err = new Error(body.error || res.statusText);
    err.status = res.status;
    err.body = body;
    throw err;
  }
  return body;
}

function decodeField(field) {
  if (field.enc === 'raw') return field.data;
  const bin = atob(field.data);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export async function loadMap() {
  const raw = await req('/api/map');
  const fields = {};
  for (const [name, packed] of Object.entries(raw.fields)) {
    fields[name] = decodeField(packed);
  }
  return { ...raw, fields };
}

export const loadTables = () => req('/api/tables');
export const getScenario = () => req('/api/scenario');
export const getInfo = () => req('/api/info');
export const getUnits = () => req('/api/units');
export const getState = () => req('/api/state');
export const getDiff = () => req('/api/diff');
export const getIssues = () => req('/api/validate');

const post = (path, body) => req(path, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body || {}),
});

export const sendEdits = (edits, label, acceptStranding = false) =>
  post('/api/edit', { edits, label, acceptStranding });
export const moveUnit = (uid, x, y) => post('/api/units/move', { uid, x, y });
export const deleteUnit = (uid) => post('/api/units/delete', { uid });
export const addUnit = (tag, x, y, value) => post('/api/units/add', { tag, x, y, value });
export const editScenario = (edits, label) => post('/api/scenario/edit', { edits, label });
export const editInfo = (edits, label) => post('/api/info/edit', { edits, label });
export const undo = () => post('/api/undo');
export const redo = () => post('/api/redo');
export const save = () => post('/api/save');

// These put a native OS file dialog on screen and do not return until it is
// dismissed, so they can sit open for as long as the person takes.
export const browseSave = () => post('/api/browse/save');
export const browseOpen = (discardChanges = false) =>
  post('/api/browse/open', { discardChanges });
