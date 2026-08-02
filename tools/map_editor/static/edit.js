// Tools: turn pointer input into batches of field edits.
//
// No tool writes a derived byte. The server recomputes borders, coastlines and
// adjacency after every batch and tells us which cells actually moved.

import { DIR_OFFSETS } from './render.js';

const OPPOSITE = [3, 4, 5, 0, 1, 2];  // NE<->SW, E<->W, SE<->NW

// Painting terrain without a matching underlay leaves the map looking wrong in
// the original renderer, so each terrain carries its base.
export const TERRAIN_UNDERLAY = {
  0: 5, 1: 0, 2: 7, 3: 0, 4: 0, 5: 7, 6: 7, 7: 2, 8: 2,
  9: 3, 10: 4, 11: 6, 12: 6, 13: 1, 14: 0, 15: 1, 16: 0,
};

export function neighbour(map, x, y, dir) {
  const [dx, dy] = DIR_OFFSETS[y & 1][dir];
  let nx = x + dx;
  const ny = y + dy;
  if (map.wrapX) nx = ((nx % map.width) + map.width) % map.width;
  if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) return null;
  return [nx, ny];
}

/** Direction from (x,y) to an adjacent cell, or null if they do not touch. */
export function directionTo(map, x, y, tx, ty) {
  for (let d = 0; d < 6; d++) {
    const n = neighbour(map, x, y, d);
    if (n && n[0] === tx && n[1] === ty) return d;
  }
  return null;
}

/** Cells within `radius` steps of (x,y), by breadth-first walk over neighbours. */
export function disc(map, x, y, radius) {
  let frontier = [[x, y]];
  const seen = new Map([[`${x},${y}`, [x, y]]]);
  for (let step = 0; step < radius; step++) {
    const next = [];
    for (const [cx, cy] of frontier) {
      for (let d = 0; d < 6; d++) {
        const n = neighbour(map, cx, cy, d);
        if (!n) continue;
        const key = `${n[0]},${n[1]}`;
        if (!seen.has(key)) { seen.set(key, n); next.push(n); }
      }
    }
    frontier = next;
  }
  return [...seen.values()];
}

/** Contiguous run of cells matching the terrain at the seed. */
export function floodRegion(map, x, y, limit = 4000) {
  const target = map.fields.terrain[y * map.width + x];
  const seen = new Set([`${x},${y}`]);
  const out = [];
  const queue = [[x, y]];
  while (queue.length && out.length < limit) {
    const [cx, cy] = queue.shift();
    out.push([cx, cy]);
    for (let d = 0; d < 6; d++) {
      const n = neighbour(map, cx, cy, d);
      if (!n) continue;
      const key = `${n[0]},${n[1]}`;
      if (seen.has(key)) continue;
      if (map.fields.terrain[n[1] * map.width + n[0]] !== target) continue;
      seen.add(key);
      queue.push(n);
    }
  }
  return out;
}

function edits(cells, fields) {
  const out = [];
  for (const [x, y] of cells) {
    for (const [field, value] of Object.entries(fields)) {
      out.push({ x, y, field, value });
    }
  }
  return out;
}

/**
 * Paint terrain, keeping the resource consistent with it.
 *
 * Developed land (a farm, ranch, orchard, managed forest) *is* the resource it
 * exploits — the two are never set independently in the original data — so
 * painting one sets the other. Painting plain terrain over developed land drops
 * that resource, since it was only there because of the improvement.
 *
 * A resource on undeveloped ground is left alone: that is a real state, a
 * deposit waiting to be worked, not an inconsistency.
 *
 * `developed` is the terrain -> resource map served by /api/tables.
 */
export function paintTerrain(map, cells, terrain, developed = {}) {
  const fields = { terrain, terrain_underlay: TERRAIN_UNDERLAY[terrain] ?? 0 };

  // Ocean and land carry mutually exclusive attributes; clear what no longer
  // applies rather than leaving stale resources or provinces behind.
  if (terrain === 0) {
    Object.assign(fields, {
      province: 65535, resource_a: 255, resource_b: 255,
      rail: 0, river: 0, town_type: 0,
    });
    return edits(cells, fields);
  }

  // Developed land holds exactly the one resource it works, so any stacked
  // secondary deposit goes with the improvement either way.
  const mandated = developed[terrain];
  if (mandated !== undefined) {
    return edits(cells, { ...fields, resource_a: mandated, resource_b: 255 });
  }

  // Per-cell, because whether the old resource was terrain-mandated depends on
  // what each cell used to be. Terrain that merely *permits* deposits (hill,
  // mountain) keeps them, so repainting hill to mountain preserves the coal.
  const cleared = { ...fields, resource_a: 255, resource_b: 255 };
  return cells.flatMap(([x, y]) => {
    const wasDeveloped = developed[map.fields.terrain[y * map.width + x]] !== undefined;
    return edits([[x, y]], wasDeveloped ? cleared : fields);
  });
}

/**
 * Stamp a resource into one of the two slots.
 *
 * Clearing the primary clears the secondary too: `resource_b` set on its own
 * appears nowhere in the original data and reads as a stacked deposit whose
 * base deposit has gone missing.
 */
export function paintResource(cells, resource, slot = 'resource_a') {
  if (slot === 'resource_a' && resource === 255) {
    return edits(cells, { resource_a: 255, resource_b: 255 });
  }
  return edits(cells, { [slot]: resource });
}
/** The cells of a brush that are actually land. */
function landOnly(map, cells) {
  return cells.filter(([x, y]) => map.fields.terrain[y * map.width + x] !== 0);
}

// Ownership is a property of land. A brush that crosses a coastline must not
// stamp the sea: an ocean cell's province is always 65535, and its "nation"
// byte holds a *sea zone* id, so writing a country id there is not a wrong
// owner but a wrong kind of value.
export const paintProvince = (map, cells, province) =>
  edits(landOnly(map, cells), { province });

export const paintNation = (map, cells, nation) =>
  edits(landOnly(map, cells), { nation_zone_a: nation, nation_zone_b: nation });

export function placeTown(cells, townType) {
  const terrain = townType === 35 ? 16 : 14;
  return edits(cells, { town_type: townType, terrain,
                        terrain_underlay: TERRAIN_UNDERLAY[terrain] });
}

/**
 * Draw a path of `river` or `rail` along a chain of adjacent cells, setting
 * the direction bit at both ends of every step so the two cells agree.
 */
export function drawPath(map, chain, field, erase = false) {
  const masks = new Map();
  const key = (x, y) => `${x},${y}`;
  const current = (x, y) => {
    const k = key(x, y);
    if (!masks.has(k)) masks.set(k, map.fields[field][y * map.width + x]);
    return masks.get(k);
  };
  for (let i = 0; i + 1 < chain.length; i++) {
    const [ax, ay] = chain[i];
    const [bx, by] = chain[i + 1];
    const d = directionTo(map, ax, ay, bx, by);
    if (d === null) continue;
    masks.set(key(ax, ay), erase ? current(ax, ay) & ~(1 << d)
                                 : current(ax, ay) | (1 << d));
    masks.set(key(bx, by), erase ? current(bx, by) & ~(1 << OPPOSITE[d])
                                 : current(bx, by) | (1 << OPPOSITE[d]));
  }
  return [...masks.entries()].map(([k, value]) => {
    const [x, y] = k.split(',').map(Number);
    return { x, y, field, value };
  });
}
