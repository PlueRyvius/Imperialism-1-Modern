# Map editor

A browser-based editor for the original game's `.map` files. Output is meant to
be loaded by the real 1997 `Imperialism.exe`, so it works within the legacy
108x60 profile and preserves everything about the file it does not understand.

## Starting it

**Double-click `Map Editor.bat`.** It asks which `.map` file to open, then
launches your browser. You can also drag a `.map` file onto the `.bat` to skip
the question.

A console window appears and stays open — that window *is* the editor. Closing
it stops the editor; the browser tab is only the interface.

From a terminal, if you prefer:

```bash
python tools/map_editor/server.py fixtures/local_only/s1.map
```

The map path is optional there too — leave it off and you get the same file
dialog. Opens on `http://127.0.0.1:8731/`. Bind is localhost-only.

Options: `--port`, `--no-browser`, and `--no-wrap` for a map that should not
join up east-to-west.

The mouse wheel zooms about the cursor — whatever is under the pointer stays
put — and holding the middle button drags the view around.

## How it is put together

The browser renders and takes input. It never sees or builds map bytes. It
sends field edits — `{x, y, field, value}` — and the server applies them to a
`MapFile` through the verified `imperialism_format` library and replies with
the cells that changed.

That split is the point. All format knowledge stays in the one implementation
that round-trips every original file byte-for-byte, and the bytes nobody has
decoded yet (the 384-record trailer, the `unused_*` fields) survive editing
because nothing ever reconstructs a cell from scratch.

| File | Role |
|---|---|
| `Map Editor.bat` | double-clickable launcher |
| `server.py` | stdlib HTTP server; routing and the JSON wire format |
| `session.py` | open document, undo/redo, dirty tracking, save |
| `dialogs.py` | native Open/Save dialogs, run in a throwaway subprocess |
| `validate.py` | consistency rules, all silent on unmodified originals |
| `static/render.js` | canvas hex renderer and layer compositing |
| `static/edit.js` | tools — brushes, flood fill, path drawing |
| `static/app.js` | input wiring |

No build step and no dependencies beyond the standard library. The file dialogs
use `tkinter`, which ships with the python.org Windows installer; on Linux it
may need a separate `python3-tk` package.

## Derived bytes

Repainting one hex invalidates the direction masks of up to six neighbours.
The server recomputes them via `imperialism_format.derive` after every batch,
which is why an edit usually reports seven changed cells. `national_border`,
`province_border`, `land_coastline` and `like_cell_adjacency` are handled this
way; the inspector marks them with a `·` so it is clear you do not own them.

Rivers and rail are authored, not derived — drawing a path sets the direction
bit at *both* ends of every step so the two cells agree. Bytes whose rules we
could not establish are never written. See `docs/derived-bytes.md`.

## Scenario editing

A scenario is four files sharing a stem, and opening the `.map` opens all of
them. **Scenario…** switches to the identity and briefing editor:

| tab | edits |
|---|---|
| Countries | `cnam` names, and `cash` for the seven playable powers |
| Provinces | `pnam` names — 213 of them, with a filter |
| Zones & ports | `zone` names, split at id 40 into sea and port cities |
| Briefing | the whole `.inf`: title, overview, seven briefings, playability |
| Records | a census of all 24 tags, plus the start year |

They share **one undo stack**: Ctrl+Z walks back through your edits in the
order you made them, whichever file each landed in. Save writes only the files
that changed, each with its own one-shot `.bak`.

Records are addressed by **id, not position** — `zone` records are not stored
in id order and `pnam` ids are sparse (0-348 for 213 provinces), so an index
would be a trap.

Only fields whose meaning is established are editable. `flag`, `tclr`, `coun`
and `tyer` appear in the record census but not as controls: `flag` is one
record per scenario with values 0-3 that track the campaign rather than the
year, and `tclr`'s single field holds a country id, not a colour. Guessing at
them would write plausible nonsense into a working scenario.

The start year is stored as **turns from 1815** and shown as a calendar year —
verified against the titles: `s0`=5→1820, `s3`=33→1848, `s1`=67→1882.

### Repairs

**Run checks** lists problems and offers to fix the ones with a single correct
answer — per issue, or all at once. Repairs are ordinary cell edits, so they
are undoable, re-derive borders and coastlines, and count as unsaved work like
anything else. A batch undoes as one step.

Repairable, because the format allows exactly one answer:

| problem | repair |
|---|---|
| ocean cell keeping a province id | set it to 65535 |
| ocean cell carrying a land resource, or rail | clear it |
| developed terrain with the wrong resource | set the resource the terrain implies |
| a stacked second resource with no primary | clear the orphan |

Not repairable, and deliberately so — each says what it needs from you instead:

- **land with no province** — there are 213 and no way to tell which
- **land owned by an unknown country** — ownership is a design decision
- **an unrecognised terrain, town or resource value** — nothing to fall back to
- **a `.scn` record stranded at sea** — restoring the land and moving the
  record produce different maps

That last one is the case that matters most, and it is exactly the one a tool
must not guess at. Automating it would quietly rewrite your intent, which is
worse than the error it replaced.

### Units

**Units…** lists everything the scenario places — civilians, armies, fleets and
infrastructure — and clicking a row locates it on the map. The cell inspector
names whatever sits on the hovered hex, and each kind has its own map marker
with a layer toggle.

The three kinds are anchored differently, and the editor is honest about it:

- **Civilians and infrastructure** name a cell outright.
- **An army** names a *province*. Its marker sits on that province's town —
  all 213 provinces have one — which is a label, not a position.
- **A fleet** names a `zone` record, and the map numbers its oceans in an
  unrelated space (English Channel is `zone` 14 but ocean byte 48). Fleets are
  therefore **listed but never drawn**. See `docs/scenario-semantics.md`.

A civilian's owner is not stored: it is whoever owns the ground it stands on.
Moving one across a border changes its side, and a civilian on a cell with no
province has no owner at all — which is what the red markers mean.

### Placing them

The **Units** tool: drag a marker to move it, click bare ground to place the
kind chosen in the palette, right-click a marker to remove it. Armies are not
draggable — their marker is a province label, not a position.

The only placement rule is **land with a province**, because it is the only one
the shipped data actually keeps: 46 of 134 ports touch no ocean, so "ports must
be coastal" would have been an invention.

### Stranding

An edit that would leave a record pointing at unusable ground is **refused
before it is applied**, listing what it would strand and offering to move each
one to the nearest land in its own province, delete them, or paint anyway.

Refusing first rather than reporting afterwards matters twice over: the map and
the `.scn` are never simultaneously wrong, and the carry destination is worked
out while the cell still knows which province it belonged to — once it is sea,
that is gone.

### Cross-file checks

`deve`, `rail`, `port` and `civi` hold linear cell indices, so repainting the
map can leave a port in open water. The validator catches that, and it is the
reason the four files are held together rather than opened separately. The
**Scenario objects** layer marks those cells so you can see them; placing them
is a later phase.

There is deliberately no check that an id the map uses has a name record. Name
records are optional labels, not a registry — `s9` names one province but puts
armies in 120, and `s1`'s map uses sea-zone ids up to 78 while naming only
0-62. Four such rules were written and deleted after they fired on shipped
data.

## Terrain and resources

A cell's `terrain` byte encodes both landform *and* development. Seven terrains
are developed land — cotton, cattle ranch, horse ranch, grain farm, orchard,
wool hill, forest — and each carries exactly the resource it works; across all
1,245 such cells in `s1` the pairing never breaks. So painting one sets the
other, and painting plain terrain over developed land drops that resource. The
mapping is `DEVELOPED_TERRAIN_RESOURCE` in `constants.py`, served over
`/api/tables` so the paint tools and the validator cannot disagree.

The implication runs one way only. A resource on undeveloped ground is a normal
state — `s1` has fruit sitting on clear land at (63, 15), waiting for a Farmer —
so painting terrain never disturbs an unworked deposit.

Cells have **two** resource slots. The second stacks onto the first: `s1` uses
it on two mountains, coal + gold and iron + gems. The Resource tool has a
Primary/Secondary selector, and both slots show as separate badges. Clearing
the primary clears the secondary too, since a stacked deposit with no base
appears nowhere in the original data. Beyond that, stacking is unconstrained —
two cells is far too little evidence to rule combinations out.

## Opening and saving

**Save writes over the file you opened.** The first save copies it to
`<name>.map.bak` and never touches that copy again, so your starting point
survives — but everything after lands on the real file. Use **Save As** if you
want the original left alone; it retargets the session at the new file, so
later saves follow it.

**Open…** and **Save As…** open ordinary Windows Explorer dialogs. The server
runs on your own machine, so it can put a real file dialog on screen instead of
making you navigate a list inside the page — and since you picked the file by
hand, there is nothing to sandbox. Both start in the folder of the map you
currently have open.

Opening a different map discards the current undo history, so the editor asks
before doing it when you have unsaved edits — the check happens *before* the
dialog appears, rather than after you have already chosen a file.

While a dialog is up the buttons read "waiting…" and are disabled. The dialog
is a native window, so it will sit there as long as you need; if it somehow
never comes back, the request gives up after five minutes rather than wedging
the editor. If it opens behind the browser window, it is set topmost, so check
your taskbar.

## Safety

- Saving copies the file to `<name>.map.bak` first, once per file, so repeated
  saves cannot erase what you started from.
- Opening and saving without editing is byte-exact — enforced by
  `tests/test_map_editor.py`.
- The validator never rewrites your map on its own. **Run checks** offers
  repairs; nothing is applied until you press a button.

## Not yet done

Scenario (`.scn`) editing. `session.py` is deliberately shaped around
"document plus undo stack" rather than anything cell-specific so that layer can
reuse it, but none of it is written.

**The in-game check has not been run.** Nothing here has been loaded by the
real `Imperialism.exe` yet. Byte-exact round-tripping proves the file is
well-formed, not that the game accepts an *edited* one. Change one hex, drop it
into an install, and confirm before trusting the tool with real work.
