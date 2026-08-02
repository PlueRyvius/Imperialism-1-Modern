// Canvas renderer for an odd-r offset hex grid.
//
// Geometry must agree exactly with src/imperialism_format/derive.py: odd rows
// are shifted half a cell right, and direction bit 0 is NE proceeding
// clockwise. If you change one, change both.

export const DIR_OFFSETS = [
  // even rows            NE       E       SE       SW       W        NW
  [[0, -1], [1, 0], [0, 1], [-1, 1], [-1, 0], [-1, -1]],
  // odd rows
  [[1, -1], [1, 0], [1, 1], [0, 1], [-1, 0], [0, -1]],
];

export const TERRAIN_COLOURS = {
  0: '#1d3f63',   // ocean
  1: '#8fb45f',   // clear
  2: '#e0e4bc',   // cotton
  3: '#b9a469',   // cattle ranch
  4: '#c9b06a',   // horse ranch
  5: '#e3cf76',   // grain farm
  6: '#9fd06f',   // orchard
  7: '#b6c07a',   // wool hill
  8: '#a8905f',   // hill
  9: '#776c5d',   // mountain
  10: '#5f7f5c',  // swamp
  11: '#e6d295',  // desert
  12: '#dde5e8',  // tundra
  13: '#3f6f3c',  // forest
  14: '#d1502f',  // town
  15: '#5f8f4c',  // scrub forest
  16: '#ff3b30',  // capital
};

// Glyphs standing in for the original tile art, which lives in the .gob
// archives we have not decoded. Keyed by the RESOURCE table in constants.py.
export const RESOURCE_ICONS = {
  0: '🌸',   // cotton
  1: '🐑',   // wool
  2: '🌲',   // forest
  3: '🪨',   // coal
  4: '🔩',   // iron
  5: '🐎',   // horses
  6: '🛢',   // oil
  17: '🌾',  // grain
  18: '🍎',  // fruit
  19: '🐟',  // fish
  20: '🐄',  // cattle
  21: '💎',  // gems
  22: '🥇',  // gold
};

// Terrain glyphs, keyed by the TERRAIN_TYPE table in constants.py. Ocean is
// deliberately absent: it is half the map and the blue says it already.
export const TERRAIN_ICONS = {
  1: '🌿',   // clear
  2: '🌸',   // cotton
  3: '🐄',   // cattle ranch
  4: '🐎',   // horse ranch
  5: '🌾',   // grain farm
  6: '🍎',   // orchard
  7: '🐑',   // wool hill
  8: '⛰',   // hill
  9: '🏔',   // mountain
  10: '🐸',  // swamp
  11: '🏜',  // desert
  12: '❄',   // tundra
  13: '🌲',  // forest
  14: '🏘',  // town
  15: '🌳',  // scrub forest
  16: '🏛',  // capital
};

const ICON_FONT = '"Segoe UI Emoji", "Noto Color Emoji", "Apple Color Emoji", system-ui';

// Badge geometry, as fractions of the hex radius. A pointy-top hex is only
// sqrt(3) ~ 1.73 radii wide, so three badges have to be this small to fit
// across it without spilling onto the neighbouring cells.
const BADGE_RADIUS = 0.28;
const BADGE_SLOTS = [-0.58, 0, 0.58];   // terrain, resource A, resource B

// Things the scenario places, and how each is marked. Positions are fractions
// of the hex radius, chosen to clear the badge row along the top.
//
// An army is drawn on its province's town — a label for the province, not a
// position. Fleets are absent on purpose: a ship names a `zone` record and the
// map's ocean cells use a different numbering, so a fleet cannot be located.
export const SCENARIO_MARKS = {
  civi: { colour: '#c77dff', at: [0.0, 0.10], shape: 'circle', label: 'Civilians' },
  army: { colour: '#e0564a', at: [0.0, -0.05], shape: 'square', label: 'Armies' },
  port: { colour: '#4fc3f7', at: [-0.48, 0.5], shape: 'circle', label: 'Ports' },
  rail: { colour: '#e8e8e8', at: [0.0, 0.62], shape: 'circle', label: 'Rail' },
  deve: { colour: '#ffd83d', at: [0.48, 0.5], shape: 'diamond', label: 'Development' },
};

function buildUnitIndex(units) {
  const index = new Map();
  const add = (cell, entry) => {
    if (cell === null || cell === undefined) return;
    if (!index.has(cell)) index.set(cell, []);
    index.get(cell).push(entry);
  };
  if (!units || !units.present) return index;
  for (const unit of units.civilians) add(unit.cell, unit);
  for (const unit of units.armies) add(unit.cell, unit);
  for (const unit of units.infrastructure) add(unit.cell, unit);
  return index;
}

const NATION_HUES = 23;

// Below ~3px a hex is smaller than its own outline; above ~64 a legacy map is
// well past the point where more magnification tells you anything.
const MIN_RADIUS = 3;
const MAX_RADIUS = 64;

export class HexRenderer {
  constructor(canvas, map) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.map = map;
    this.radius = 11;
    this.originX = 0;
    this.originY = 0;
    this.layers = {
      terrainIcons: true, resources: true, provinces: false, nations: false,
      rivers: true, rail: true, towns: true, scenario: true,
      grid: false, dirty: false,
    };
    this.dirty = new Set();
    this.hover = null;
    // Cell indices the .scn points at, by tag. Read-only here: placing them is
    // the forces/economy phase; for now you can see them and the validator can
    // complain about them.
    this.scenarioCells = buildUnitIndex(map.units);
    // Which unit kinds are drawn; toggled from the Layers list.
    this.unitLayers = new Set(Object.keys(SCENARIO_MARKS));
  }

  get hexW() { return Math.sqrt(3) * this.radius; }
  get rowH() { return 1.5 * this.radius; }

  centre(x, y) {
    return [
      this.originX + x * this.hexW + (y & 1 ? this.hexW / 2 : 0) + this.hexW / 2,
      this.originY + y * this.rowH + this.radius,
    ];
  }

  // Nearest-centre lookup. Exact for our purposes: we only need to pick the
  // cell under the cursor, and testing the 3x3 candidate block around the
  // rough row/column guess is cheaper than inverse-transforming the axial
  // coordinates and rounding.
  cellAt(px, py) {
    const { width, height } = this.map;
    const gy = Math.round((py - this.originY - this.radius) / this.rowH);
    let best = null;
    let bestDist = Infinity;
    for (let y = gy - 1; y <= gy + 1; y++) {
      if (y < 0 || y >= height) continue;
      const gx = Math.round(
        (px - this.originX - this.hexW / 2 - (y & 1 ? this.hexW / 2 : 0)) / this.hexW);
      for (let x = gx - 1; x <= gx + 1; x++) {
        const wx = this.map.wrapX ? ((x % width) + width) % width : x;
        if (wx < 0 || wx >= width) continue;
        const [cx, cy] = this.centre(x, y);
        const d = (cx - px) ** 2 + (cy - py) ** 2;
        if (d < bestDist) { bestDist = d; best = [wx, y]; }
      }
    }
    return bestDist <= this.radius ** 2 ? best : null;
  }

  path(cx, cy) {
    const ctx = this.ctx;
    ctx.beginPath();
    for (let k = 0; k < 6; k++) {
      const a = (Math.PI / 3) * k;
      const px = cx + this.radius * Math.sin(a);
      const py = cy - this.radius * Math.cos(a);
      k ? ctx.lineTo(px, py) : ctx.moveTo(px, py);
    }
    ctx.closePath();
  }

  // Midpoint of the edge facing `dir`, used to stroke rivers, rail and
  // borders on the correct side of the hex.
  edge(cx, cy, dir) {
    const a = (Math.PI / 3) * dir + Math.PI / 6;
    return [cx + this.radius * Math.sin(a) * 0.86, cy - this.radius * Math.cos(a) * 0.86];
  }

  fitTo(viewW, viewH) {
    const { width, height } = this.map;
    const r = Math.min(viewW / ((width + 1) * Math.sqrt(3)), viewH / (height * 1.5 + 1));
    this.radius = Math.max(MIN_RADIUS, r);
    this.originX = 0;
    this.originY = 0;
  }

  panBy(dx, dy) {
    this.originX += dx;
    this.originY += dy;
  }

  /**
   * Scale about a screen point, keeping whatever is under it in place.
   *
   * Every cell centre is `origin + radius * f(x, y)` — the offset from the
   * origin is linear in the radius — so holding one point fixed needs no
   * inverse hex lookup, just the same ratio applied to the origin.
   */
  zoomAt(px, py, factor) {
    const wanted = this.radius * factor;
    const radius = Math.min(MAX_RADIUS, Math.max(MIN_RADIUS, wanted));
    if (radius === this.radius) return false;
    const ratio = radius / this.radius;
    this.originX = px - (px - this.originX) * ratio;
    this.originY = py - (py - this.originY) * ratio;
    this.radius = radius;
    return true;
  }

  resize() {
    const dpr = window.devicePixelRatio || 1;
    const { clientWidth: w, clientHeight: h } = this.canvas.parentElement;
    this.canvas.width = w * dpr;
    this.canvas.height = h * dpr;
    this.canvas.style.width = `${w}px`;
    this.canvas.style.height = `${h}px`;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    return [w, h];
  }

  draw() {
    const ctx = this.ctx;
    const { width, height, fields } = this.map;
    const [w, h] = [this.canvas.width, this.canvas.height];
    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = '#0c0f14';
    ctx.fillRect(0, 0, w, h);

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const i = y * width + x;
        const [cx, cy] = this.centre(x, y);
        if (cx < -this.radius || cy < -this.radius) continue;

        this.path(cx, cy);
        ctx.fillStyle = this.fillFor(i);
        ctx.fill();

        if (this.layers.grid && this.radius > 5) {
          ctx.strokeStyle = 'rgba(255,255,255,0.07)';
          ctx.lineWidth = 0.5;
          ctx.stroke();
        }
        if (this.layers.dirty && this.dirty.has(i)) {
          ctx.strokeStyle = '#ffd83d';
          ctx.lineWidth = 1.5;
          ctx.stroke();
        }

        this.drawOverlays(i, x, y, cx, cy);
      }
    }

    // Where a dragged marker would land: green if it can, red if it cannot.
    if (this.dragTarget) {
      const [tx, ty, ok] = this.dragTarget;
      const [dx, dy] = this.centre(tx, ty);
      this.path(dx, dy);
      ctx.strokeStyle = ok ? '#7bd88f' : '#ff3b30';
      ctx.lineWidth = 3;
      ctx.stroke();
    }

    if (this.hover) {
      const [cx, cy] = this.centre(this.hover[0], this.hover[1]);
      this.path(cx, cy);
      ctx.strokeStyle = '#fff';
      ctx.lineWidth = 2;
      ctx.stroke();
    }
  }

  fillFor(i) {
    const f = this.map.fields;
    if (this.layers.nations && f.terrain[i] !== 0) {
      const n = f.nation_zone_a[i];
      return `hsl(${(n * 360) / NATION_HUES}deg 55% 45%)`;
    }
    if (this.layers.provinces && f.terrain[i] !== 0) {
      const p = f.province[i];
      return `hsl(${(p * 137.5) % 360}deg 45% ${38 + (p % 5) * 5}%)`;
    }
    return TERRAIN_COLOURS[f.terrain[i]] || '#ff00ff';
  }

  drawOverlays(i, x, y, cx, cy) {
    const ctx = this.ctx;
    const f = this.map.fields;
    const small = this.radius < 6;

    if (this.layers.rivers && f.river[i]) {
      this.strokeMask(f.river[i], cx, cy, '#4fc3f7', Math.max(1, this.radius * 0.22));
    }
    if (this.layers.rail && f.rail[i]) {
      this.strokeMask(f.rail[i], cx, cy, '#2b2b2b', Math.max(1, this.radius * 0.16));
    }
    if (this.layers.nations && f.national_border[i]) {
      this.strokeMask(f.national_border[i], cx, cy, '#ffffff', Math.max(1, this.radius * 0.12));
    }
    if (this.layers.provinces && f.province_border[i]) {
      this.strokeMask(f.province_border[i], cx, cy, 'rgba(0,0,0,0.55)',
                      Math.max(1, this.radius * 0.1));
    }
    if (small) return;

    if (this.layers.towns && f.town_type[i]) {
      const capital = f.town_type[i] === 35;
      ctx.fillStyle = capital ? '#ffe14d' : '#f5f5f5';
      ctx.beginPath();
      ctx.arc(cx, cy, this.radius * (capital ? 0.42 : 0.3), 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = '#22160a';
      ctx.lineWidth = 1;
      ctx.stroke();
    }
    // Three fixed slots across the hex: terrain, then the two resource slots.
    // Fixed rather than packed, so a badge does not shift sideways when its
    // neighbour appears or disappears.
    const badgeY = cy - this.radius * 0.42;
    const terrainIcon = TERRAIN_ICONS[f.terrain[i]];
    const showResources = this.layers.resources;
    const a = showResources && f.resource_a[i] !== 255 ? f.resource_a[i] : null;
    const b = showResources && f.resource_b[i] !== 255 ? f.resource_b[i] : null;

    // Developed terrain (a ranch, farm, orchard) always carries the resource it
    // exploits, so its glyph would just repeat the resource badge. Show it once.
    const duplicate = terrainIcon && terrainIcon === RESOURCE_ICONS[a];

    if (this.layers.terrainIcons && terrainIcon && !duplicate) {
      this.drawBadge(terrainIcon, f.terrain[i], cx + this.radius * BADGE_SLOTS[0], badgeY);
    }
    if (a !== null) {
      this.drawBadge(RESOURCE_ICONS[a], a, cx + this.radius * BADGE_SLOTS[1], badgeY);
    }
    if (b !== null) {
      this.drawBadge(RESOURCE_ICONS[b], b, cx + this.radius * BADGE_SLOTS[2], badgeY);
    }
    if (this.layers.scenario) this.drawScenarioMarks(i, cx, cy);
  }

  /** Mark what the scenario places on this cell. */
  drawScenarioMarks(index, cx, cy) {
    const here = this.scenarioCells.get(index);
    if (!here) return;
    const ctx = this.ctx;
    for (const unit of here) {
      const mark = SCENARIO_MARKS[unit.tag];
      if (!mark || !this.unitLayers.has(unit.tag)) continue;
      const [dx, dy] = mark.at;
      const x = cx + this.radius * dx;
      const y = cy + this.radius * dy;
      const r = Math.max(1.4, this.radius * 0.15);

      ctx.beginPath();
      if (mark.shape === 'square') ctx.rect(x - r, y - r, r * 2, r * 2);
      else if (mark.shape === 'diamond') {
        ctx.moveTo(x, y - r); ctx.lineTo(x + r, y);
        ctx.lineTo(x, y + r); ctx.lineTo(x - r, y); ctx.closePath();
      } else ctx.arc(x, y, r, 0, Math.PI * 2);

      // A stranded record is the thing you most need to spot, so it is drawn
      // in alarm colours rather than its own.
      ctx.fillStyle = unit.stranded ? '#ff3b30' : mark.colour;
      ctx.fill();
      if (this.radius > 7) {
        ctx.strokeStyle = unit.stranded ? '#fff' : 'rgba(0,0,0,0.6)';
        ctx.lineWidth = unit.stranded ? 1.5 : 1;
        ctx.stroke();
      }
      if (unit.tag === 'army' && unit.count && this.radius > 11) {
        ctx.fillStyle = '#fff';
        ctx.font = `${Math.round(this.radius * 0.4)}px system-ui`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(String(unit.count), x, y + this.radius * 0.02);
      }
    }
  }

  /** Rebuild the placed-record index after the scenario or map changes. */
  setUnits(units) {
    this.scenarioCells = buildUnitIndex(units);
  }

  /** Everything the scenario places on a cell, for the inspector. */
  unitsAt(x, y) {
    return this.scenarioCells.get(y * this.map.width + x) || [];
  }

  /** A glyph on a dark disc, falling back to `id` when we have no glyph. */
  drawBadge(icon, id, cx, cy) {
    const ctx = this.ctx;
    const r = this.radius * BADGE_RADIUS;

    // A dark disc behind the glyph: emoji carry their own colours, which get
    // lost against pale terrain like desert and tundra.
    ctx.fillStyle = 'rgba(16,18,22,0.72)';
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fill();

    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    if (icon) {
      // Colour emoji ignore fillStyle, but some of these glyphs fall back to
      // monochrome text presentation — and those would inherit the disc's
      // near-black and disappear into it.
      ctx.fillStyle = '#ffe9a8';
      ctx.font = `${Math.round(r * 1.25)}px ${ICON_FONT}`;
      ctx.fillText(icon, cx, cy + r * 0.06);
    } else {
      // An id we have no glyph for still has to be visible rather than silently
      // rendering as an empty badge.
      ctx.fillStyle = '#ffe9a8';
      ctx.font = `${Math.round(r * 1.1)}px system-ui`;
      ctx.fillText(String(id).slice(0, 2), cx, cy);
    }
  }

  strokeMask(mask, cx, cy, colour, lineWidth) {
    const ctx = this.ctx;
    ctx.strokeStyle = colour;
    ctx.lineWidth = lineWidth;
    ctx.lineCap = 'round';
    for (let d = 0; d < 6; d++) {
      if (!(mask & (1 << d))) continue;
      const [ex, ey] = this.edge(cx, cy, d);
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.lineTo(ex, ey);
      ctx.stroke();
    }
  }
}
