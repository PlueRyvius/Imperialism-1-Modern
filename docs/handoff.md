# Handoff — where this stands

Written at the end of a long session. Read `CLAUDE.md` first; this covers what
that file cannot: what was just built, what is unresolved, and how the work has
actually been going.

## Immediate next step

**`s5` has been regenerated with two fixes and is again untested in-game.**
It is "Ilvane and the World, 1820", from the keyword `Pippin`. The user
launches it and reports back.

Two rounds of crash-hunting happened here, both at fault offset `0x0011465C` →
`0x0051465C` in `UMap.cpp`, which indexes the **7-slot** Great Power table at
`006A4370` unguarded and dereferences the result. See the disasm README for
that table and its 24-slot nation-id neighbour; telling the two apart is
usually the whole diagnosis, because a minor's id is valid for one and off the
end of the other.

1. **`army` records were invented** — random type over the era roster, random
   count 1–6 — where the game's own generator writes a fixed three-role
   pattern. Produced a type-3 record, a value in no shipped scenario. Fixed;
   `s5`'s army block is now identical in shape to `s11`/`s15`.
   *This was a real defect but not the crash.* The next launch faulted at the
   same offset.
2. **Works were being placed on minor nations' cells.** This was it. All nine
   originals put every `deve`/`rail`/`port`/`civi` on a cell owned by a Great
   Power — 700-odd records, not one exception — because the engine resolves a
   work's cell to an owner and indexes that 7-slot table with it. `s5` had 113
   `deve`, 40 `rail` and 28 `port` on minors' cells. Fixed in `_place_works`.

Lesson worth keeping: fix 1 was found by diffing field *values* against
shipped data, and it was the wrong lead. Fix 2 needed the diff to be over a
*derived* property — the owner of the cell a record points at — which no
field-value sweep would ever surface. When a value diff comes up thin, diff
what the values **resolve to**.

**`s5` was in the test corpus.** `tests/originals.py` excluded `s0` but not
`s5`, which is where generated worlds get written — so "generated values stay
inside what the originals use" was partly comparing our output against itself,
and could not see a novel value in `s5` because `s5` was one of the originals.
Now on `WORKING_SCENARIOS`. Anything else written into a live `Scenario`
folder belongs there immediately.

**`s5` then loaded and played.** Three faults are fixed and three cosmetic /
gameplay defects were reported from that launch and fixed in turn:

3. **No docks.** The zone table ends with one port city **per country, all 23**
   — seven ordinary harbour names for the powers, then sixteen "<country> City"
   for the minors. We wrote only the sixteen, so every Great Power, including
   the played one, had nowhere for ships to anchor. `PORT_ZONES` is now 23.
4. **The shore looked wrong.** Bytes 1 and 11 were written as zero on every
   cell. Byte 11 turned out to be fully derivable and is now exact on all nine
   originals — see `derived-bytes.md`, it had been written off at "~7% best
   fit". Generated worlds had no water-side shoreline art at all.
5. **A country split across the east-west seam.** The grid wraps, but the
   game's own worlds put **no land in either edge column** and the historical
   maps none in the west. Growing a continent across the seam leaves a nation
   in two halves that cannot be walked between. `world.EDGE_SEA_MARGIN`.

Chasing 5 surfaced a separate latent bug: sinking unusable land can bisect a
country, and `build._split_fragments` already repaired it — but the politics
test asserted the blob invariant one stage too early, where it held only by
luck of the geography. That assertion now runs against the finished world.

6. **No port at the capital.** The root cause was not the `port` record at all:
   **every capital in every shipped scenario is coastal or on a river, all 184
   of them**, and `place_towns` was siting every town by `_most_interior` — as
   far from the coast as it could get. All 23 capitals came out landlocked, so
   no nation had a dock. Capitals now take the largest *coastal* province and
   sit on a sheltered shore cell within it; villages keep the interior rule,
   which matches the shipped mix.

   Fixing that exposed three more constraints on `port` records, each verified
   silent across all nine originals: on water, never on high ground, and never
   on a capital cell. Ports are Great-Power-only, 0–3 apiece, and mostly on
   ordinary coastal ground rather than towns.

**The `port -> terrain` finding came from `preflight`, immediately.** It was
also the tool's own correction of me: I had earlier dismissed `port -> town-type
35` as a thin lead, and it was the real thing. A finding being weakly supported
by a nine-file corpus is not the same as it being wrong.

## Rivers — done

`src/imperialism_format/generate/rivers.py`. Ten rivers per world, springing
from inland mountains and running to the sea, matching the shipped generated
worlds on every measure taken: 84-108 cells against their 90-95, 5% of land
against 5%, ten heads and ten mouths, average course 8-11 cells against ~9.
Every port meets a river on the other side; no course dangles.

The format decode is in `file-formats.md`. Three things are worth carrying:

- **The 60-degree rule.** The sixteen through-flow values cover nine of the
  fifteen direction pairs, and the six missing are exactly the 60-degree turns.
  That is the whole constraint on carving a course, and it fell out of counting
  which values exist rather than from any documentation.
- **Height is a property of the edge**, not the cell. East high must meet west
  high. All four combinations exist, so no course is ever blocked.
- **The community notes are wrong twice** — 49 is a west head, 55 a south-west
  mouth — and the shipped data says so plainly. Both are pinned by tests.

Rivers are cut **before** towns are sited, which is what closed the landlocked
capital gap: a country with no coast of its own now seats its capital on a
river. Across six keywords, zero landlocked capitals, and the coastal/river
split for Great Powers (4-6 coastal, 1-3 river) matches `s9` and `s15` exactly.
Ports may sit on a river too, as 3 of `s9`'s 13 and 27 of `s1`'s 49 do.

The editors were a *guide*, not a source: `MapCreator5`'s readme says outright
that it cannot make rivers and they must be drawn by hand in `MapEditor`, and
`MAPEDIT.C` is 68 lines of struct and I/O. There is no algorithm in there to
copy — the course-carving is ours, the format is theirs, and the format is the
part that was hard.

**A `MapCreator5` claim that does not hold:** its readme describes a province-id
allocation of 27 ids per major power, then 22/22/22, 10x12, 8. Checked against
shipped data it fails badly — 141 of 213 provinces out of range on `s1`, all 120
on `s9`. It is that program's own convention for avoiding collisions, not the
engine's rule, and it is another form of the trap `CLAUDE.md` already warns
about: province ownership is not derivable from the province id.

Still open from `preflight`: `deve` on cotton, which the corpus never develops
though cotton is a legitimate resource terrain, and `pnam -> town-type 33`,
which is a corpus gap rather than a divergence — the historical maps have no
minor capitals at all and the generated ones name almost no provinces.

Regenerate it with:

```bash
python tools/generate_scenario.py --seed Pippin \
    --template E:/Imperialism/Scenario/s1.map --out E:/Imperialism/Scenario/s5
```

## The community editor tools are a real asset

`C:\Users\Ryvius\Desktop\Imp1 editors and utilities` holds a set of old
third-party Imperialism editors — and, more usefully, **their documentation and
C source**: `Map editor/MapDecode.rtf` is a byte-by-byte `.map` format note,
`ImpEdit v2/` has `ImpMap.doc`, `ImpScen.doc`, `ImpTech.doc`, `ImpUnits.doc`
and the C for a decompiler/compiler pair, and `Map Creator 5/` has more.

`MapDecode.rtf` alone supplied the river direction table, the resource ids, and
the island codes in byte 1 — and the hint that byte 11 pairs with byte 1 on
ocean cells, which is what cracked byte 11 completely.

**Treat it as a lead source, not as truth.** It is right about a great deal and
wrong in places: it calls byte 28 "unused - always set to int 0" when the engine
demonstrably reads bit 2 of it, and it lists only town types 34 and 35, missing
33. Every claim taken from it has been checked against all nine originals
first, and the ones that failed that check were dropped. Still unmined:
`ImpScen.doc` and `ImpUnits.doc`, which may settle the ship/zone question.

## Tooling built for this loop

Three things now automate what was done by hand above:

- **`python -m tools.alf.crash`** — newest fault from the Application log,
  image base added, module and disassembly resolved, one command. `--list N`
  to see recent faults, `--offset 0x...` to resolve one without an event log.
- **`python tools/preflight.py s5`** — diffs a scenario against the shipped
  corpus and reports what it contains that no shipped file does. Run it before
  launching. It finds both 2026-08-01 defects unprompted, and `tests/
  test_preflight.py` pins that so the tool cannot quietly stop finding them.
- **`validate.check_cross_file`** now rejects a work on a minor's cell, so the
  editor and the generator both catch that class before the game does.

`preflight.py` earns its keep through two ideas worth preserving. It separates
**codes** (a small closed vocabulary, where a novel value means something) from
**indices** into the map (hundreds of scattered values, where novelty is
noise) — without that split the one real signal drowns in five false ones. And
it sweeps **projections**: what each reference *resolves to* in the map, not
just the number stored. The crash was invisible to any value sweep because
every value involved was individually ordinary.

Its findings are leads, not verdicts. Nine scenarios are a thin corpus — the
leave-one-out test shows each original is the sole source of some values — so
it reports and ranks rather than failing. Rules certain enough to fail on
belong in `validate.py`.

Two leads it currently reports on `s5`, both looked at and neither chased:
`deve` on cotton (terrain 2), which the corpus never develops though cotton is
a legitimate resource terrain; and `port`/`rail` on a capital (town-type 35),
which the corpus never does either. Both are thin, dominated by `s1`.

## The crash-hunting loop that works

This is the most valuable thing to carry forward. Each in-game crash has been
diagnosed the same way, and each landed in a *different* engine module:

1. Read the Windows Application log for `Imperialism.exe`, take the **fault
   offset**.
2. Add `0x400000` (the image base) and resolve it:
   `python -m tools.alf.query addr 0x00XXXXXX --context 20`. The disassembly
   index is built from the user's `Imperialism.alf`; rebuild with
   `python tools/alf/index.py --alf E:/Imperialism/Imperialism.alf --exe E:/Imperialism/Imperialism.exe`.
3. The module name (from `assert` filenames) says which subsystem: `UMapper.cpp`
   and `UMap.cpp` for map data, `UOcean.cpp` for sea zones.
4. Then **diff our output against the shipped files** for anything they never
   contain. That is what found both generator crashes, and it found the second
   one *before* it cost a test cycle.

Faults so far have all been `call dword ptr [eax]` on a nil object — the engine
looks something up, gets nothing, and calls a method on it. `UMap.cpp` even
carries a compiled-out `"Nil Pointer"` assert on that path.

## Format decodes made this session

All three were long-standing unknowns, all verified across all ten maps.

- **The `.map` trailer is a province table.** 384 slots indexed by province id
  (so 384 is the format's province cap), each holding that province's town cell
  as a big-endian u16 at offset 4, 65535 when unused. The other 196 bytes per
  record are still unread, and part of the tail varies even in *unused* slots —
  the signature of uninitialised memory. See `file-formats.md`.
- **`town_type` 33 is a minor nation's capital.** Sixteen per generated world,
  one per minor; the historical maps use none, giving all 23 countries a type-35
  capital instead.
- **A ship's `zone` id is not the map's ocean byte.** Unrelated numbering
  spaces: the English Channel is `zone` record 14 but ocean byte 48. This
  corrects a claim that was in `scenario-semantics.md`.

## Generation status

`src/imperialism_format/generate/` — modelled on measurements of the five
shipped worlds the game's own generator produced (`s9`-`s12`, `s15`), not
invented. `docs/world-generation.md` does not exist; the numbers live in the
module docstrings with their provenance.

Done: geography (latitude-banded, 9 landmasses, ~30% land), politics (7 powers
of 8 provinces, 16 minors of 4, hard 20-cell province cap), scenario `.scn` and
`.inf`, and a CLI.

**Not done: G4, the generator panel in the editor.** The plan for it is in the
user's plan file. The home-country locking it needs is already built and tested
(`politics.assign_countries(locked=...)`) — a locked country keeps every cell
through a full regeneration.

## Open questions, in rough priority order

1. **Does `s5` load?** Everything else is downstream of this.
1b. **Derived masks with values no shipped map contains.** `land_coastline`
   23, 27, 50, 54 and `province_border` 27, 58 in `s5`. These come out of
   `derive.py`, so either the masks are wrong or generated coastlines have
   shapes the originals never drew. The province table itself is clean —
   checked against `s9`/`s11`/`s15` and it matches exactly, including the
   33/34/35 town-type split, so the s1-template worry below is not affecting
   the town field.

2. **The other 196 bytes of each province-table record.** Offsets ~58-65,
   ~130-135 and ~158-190 carry something. Generated maps currently inherit a
   real map's table and rewrite only the town field. A better template would be
   `s9` (120 provinces, ids 0-119) rather than `s1` (213, sparse to 348), since
   our layout matches `s9` exactly — worth trying if `s5` misbehaves.
3. **How a `ship`'s zone id maps to the map's oceans.** Until this is known,
   generated worlds have no navies. The `port` records may be the way in: zone
   ids 40-62 are port cities and `port` records name coastal cells.
4. **Small inland lakes** in some generated worlds (2-7 cells). No shipped map
   has any. Valid data, but a visible difference.
5. **Dragging units in the editor** reportedly does not work; hardened against
   every cause I could find but never reproduced. The toast on pickup is the
   diagnostic — if it appears, the grab worked and the problem is the drop.

## Working conventions

- **`s0` is the user's testing scenario; `s1` is the pristine reference.** They
  edit `s0`, launch it, and revert when it breaks. `tests/originals.py` excludes
  `s0` by name and any scenario carrying a `.bak`.
- `IMP_SCENARIO_DIR=E:/Imperialism/Scenario python -m pytest` runs the suite
  against the real originals without copying game data into the repo.
- **Never write to the user's `Scenario` folder except deliberately.** Testing
  through the editor once painted into `s0.map`; the one-shot `.bak` saved it.
  Use temp copies.
- The user tests anything needing eyes or a real mouse. Give numbered steps and
  say what to look for — that has worked well.

## The rule that keeps earning its keep

**A rule that fires on shipped data is a wrong rule, not a bad map.** It has
caught eleven so far: three map validation rules fitted against a single map,
four cross-file rules that assumed a `.scn` names everything the map references,
and the rest in generation. Before believing any new invariant, check it is
silent across all ten scenarios.

Corollary that also keeps paying: when something is measured from one file,
say so, and re-measure when more files appear. `land_coastline` looked like a
95.9% rule until nine more maps showed it is exact on eight of them.
