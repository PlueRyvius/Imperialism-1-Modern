# Disassembly index (`tools/alf/`)

Turns the 59 MB W32Dasm listing of the original `Imperialism.exe` into a
queryable SQLite index, and uses the `assert()` filenames the retail build
leaks to partition the address space by original C++ source file.

**Nothing here ships game data.** The `.alf` listing and the `.exe` are
copyrighted (Ubisoft) and live outside the repository; the tools read them from
an absolute path given at runtime. The index they produce contains the original
instruction stream verbatim, so it is a derived copyrighted work and is
**never** committed — it defaults to a path outside the tree, and `*.sqlite` /
`*.alfdb` are gitignored as a backstop. `docs/disasm/module-map.md` is safe to
commit because it holds only addresses, sizes and filenames.

## Definitive original-game data

[definitive-original-data.md](definitive-original-data.md) and
[definitive-original-data.json](definitive-original-data.json) preserve the
evidence-backed values recovered from the original executable and resource
archive. Direct observations are labeled definitive; unresolved semantic
interpretations remain marked candidate. The original copyrighted sources and
disassembly listing remain outside the repository.

**What of it is in the engine, and what is not.** The technology table's order and
prices, the thirteen ship classes, the production recipes and labour rate, the
worker-conversion costs and the raw-resource yield curves are all shipped —
`docs/formulas/technology.md`, `trade.md`, `production.md` and `migration.md` record
what each one changed. The army tables, the map geometry, the town-development rules
and everything marked `candidate` are recorded and unused, because nothing in this
engine models them yet. Do not promote a `candidate` into content without tracing
its selector first; `wanted-values.md` explains why, with two worked examples of a
plausible fingerprint confirming the wrong table.

## Building the index

```sh
python tools/alf/index.py \
    --alf "C:/path/to/Imperialism.alf" \
    --exe "C:/path/to/Imperialism.exe"
```

* `--exe` is optional but **strongly recommended**: without it you lose the
  vtable-pointer function detection and 13 of the 55 source filenames.
* The index goes to `$IMP_ALF_DB`, else `~/.cache/imperialism/imperialism-alf.sqlite`.
  Override with `--db`.
* `--rebuild` discards any existing index; `--modules-only` skips the file scan
  and just re-runs function/module derivation (seconds, not minutes).
* The scan is **resumable**: progress is committed every 100 000 source lines
  along with the last line number, and re-running picks up from there. It is
  also **idempotent** — every insert is an upsert keyed on address.

A full run takes well under a minute on a modern machine and produces roughly
1.17 M instruction rows.

Regenerate the module map afterwards:

```sh
python tools/alf/module_map.py --out docs/disasm/module-map.md
```

## Querying

```sh
python -m tools.alf.query addr 0x004B45C6 --context 40   # disassembly in context
python -m tools.alf.query xrefs 0x004B45D3               # in/out references
python -m tools.alf.query calls-into 0x004057A4          # who calls this
python -m tools.alf.query func --name UCity              # functions in a module
python -m tools.alf.query func --at 0x004B4400           # function containing an address
python -m tools.alf.query strings --grep Memory          # string references
python -m tools.alf.query imports --grep RegOpen         # Win32 import call sites
python -m tools.alf.query modules --ranges               # attribution summary
python -m tools.alf.query stats
```

`tools/alf/query.py` also works as a script if you would rather not use `-m`.

## Resolving a crash

`tools/alf/crash.py` runs the whole loop — read the Windows Application log,
add the image base, resolve the address, name the module — in one command:

```sh
python -m tools.alf.crash                     # the newest fault
python -m tools.alf.crash --list 10           # what has crashed recently
python -m tools.alf.crash --offset 0x11465c   # resolve one by hand, no event log
```

**The offset in the log is not an address.** It is relative to the module base,
so `Fault offset: 0x0011465C` at the usual `0x400000` is `0x0051465C`. The tool
prints the arithmetic, because getting it wrong lands you in an unrelated
function that still looks plausible. A fault inside a DLL is reported but not
resolved: its offset belongs to that DLL, not to `Imperialism.exe`.

## Where the world generator lives

**`UMap.cpp`, roughly `0050F860`-`00528B5F`.** Found by locating the RNG rather
than by guessing: the game inlines a Borland LCG whose multiplier `015A4E35`
(22695477) appears at 242 sites, and they cluster hard —

| sites | module |
|---|---|
| 75 | `UMap.cpp` |
| 16 | `UMapper.cpp` |
| 4 | `UOcean.cpp` |
| 3 | `UArmyMgr.cpp` |
| 144 | unattributed, mostly `005C4C60`-`005D4200` (a second cluster, likely battle AI) |

Within `UMap.cpp` they concentrate in a handful of large functions —
`005267F0`-`00528B5F` (9,072 bytes, 25 RNG sites) is the biggest, with
`005114B0`, `005108D0`, `00525A30` and `0050F860` behind it. The same block
holds 173 references to `0000006C` (108, the map width), so it is walking the
grid.

This matters because the generator here is the authority on questions the
project has otherwise had to infer from five sample outputs: coastline shaping,
what happens at the east-west seam, landmass and province layout. Reading it is
a real reverse-engineering task, not a quick look — but it is the ground truth,
and `docs/handoff.md` lists the open questions it would settle.

To find a subsystem this way generally: pick a behaviour that must use
randomness, then cluster the RNG sites by module.

## Two global object tables worth knowing

Crash sites repeatedly land on one of two adjacent arrays of object pointers.
Telling them apart is usually the whole diagnosis, because **they are indexed
by different id spaces**:

| Address | Slots | Indexed by |
|---|---|---|
| `006A4310` | 24 | nation id, 0–22 (the map's nation byte), `FFFF` = none |
| `006A4370` | 7 | Great Power slot, 0–6 |

The 7 is measured, not assumed: the walk at `004A344E` steps by 4 and stops at
`006A438C`, giving `(0x6A438C - 0x6A4370) / 4 = 7`. The 24-slot table starts
`0x60` earlier.

The trap: an index into the 24-slot table is valid for a minor nation and
**runs off the end of the 7-slot table**. Sites reading `006A4310` tend to
guard (`cmp ax, FFFF` at `005142A6`); sites reading `006A4370` often do not —
`0051465C` in `UMap.cpp` indexes it from a `movsx`-ed word and dereferences
the result immediately, which is where a bad owner id surfaces as
`call dword ptr [eax]` on nil. That path carries the compiled-out
`"Nil Pointer"` assert.

Related: the cell record is 36 bytes, so `[map+0x0C] + cell*36 + N` is cell
byte `N`. `005145F2` reads bit 2 of byte 28 — one of the bytes the format docs
still call unused.

## The W32Dasm listing format

The file is a fixed-column *report*, not a machine format. Reverse-engineered
structure:

1. **Header sections** — `MENU INFORMATION`, `DIALOG INFORMATION`,
   `IMPORTED FUNCTIONS`, `IMPORT MODULE DETAILS`, `EXPORTED FUNCTIONS`. Each
   uses its own ad-hoc layout. The object table at the very top is where the
   section addresses come from:

   | object | RVA | file offset | size |
   | --- | --- | --- | --- |
   | `.text` | `00001000` | `00000400` | `0023D000` |
   | `.rdata` | `0023E000` | `0023D400` | `00053800` |
   | `.data` | `00292000` | `00290C00` | `0000F400` |
   | `.idata` | `002AA000` | `002A0000` | `00003C00` |
   | `.rsrc` | `002AE000` | `002A3C00` | `00027C00` |
   | `.reloc` | `002D6000` | `002CB800` | `00030800` |
   | `.patch` | `00307000` | `002FC000` | `00001000` |

2. **`+++ ASSEMBLY CODE LISTING +++`** then
   `//***** Start of Code in Object .text *****`, followed by the disassembly.

3. **Disassembled lines** look like:

   ```
   :004B45C6 68185F6900              push 00695F18
   ```

   `:` + 8 upper-case hex address, space, raw bytes as upper-case hex padded to
   24 columns, then the text. **Do not split on a hard column** — encodings
   longer than 12 bytes overflow the field. Split on "two or more spaces"
   instead; raw bytes are always upper-case hex and mnemonics are always
   lower-case (or `Call`/`BYTE`/`DWORD`), so this is unambiguous.

4. **Alignment filler** shares that layout:
   `:005190E4 00000000                BYTE  4 DUP(0)`. It is stored with
   `is_data = 1`.

5. **Annotation blocks** start with `*` in column 1 and describe the **next**
   disassembled line. Multi-value blocks continue on `|` lines and end with a
   bare `|`:

   ```
   * Referenced by a CALL at Addresses:
   |:004E739E   , :004E8424   , :004E84B6
   |
   :004014A6 E995700E00              jmp 004E8540
   ```

   Observed kinds and their counts in this listing:

   | annotation | count |
   | --- | ---: |
   | `Referenced by a (U)nconditional or (C)onditional Jump at Address(es)` | 36 751 |
   | `Possible Reference to Dialog` | 3 101 |
   | `Possible StringData Ref from Data Obj ->"…"` | 2 997 |
   | `Reference To: MODULE.Symbol, Ord:NNNNh` | 2 985 |
   | `Referenced by a CALL at Address(es)` | 3 134 |
   | `Possible StringData Ref from Code Obj ->"…"` | 166 |
   | `Possible Reference to Menu` / `String Resource ID=…` | ~250 |
   | `Possible Indirect StringData Ref from Data Obj ->"…"` | 22 |

   Jump sources carry a `(C)`/`(U)` suffix; call sources do not.

6. **End sentinel**: `:FFFFFFFF    End Of Listing`.

### Gotchas

* **Only `.text` and `.patch` are disassembled.** `.data` and `.rdata` never
  appear as listing lines, so string *contents* exist only inside annotation
  text, and vtables are invisible to the listing entirely. That is why the
  tools also read the `.exe` directly.
* **The listing jumps** from `0063DFF6` straight to `00707000` (`.patch`).
  Address order is monotonic but not contiguous.
* **More than half the listing is padding.** `int 03` accounts for 576 975 of
  the 1 167 847 disassembled lines, nearly all of it a single enormous run from
  `0063C000` to the end of `.text`.
* **`Call` vs `call`.** W32Dasm capitalises indirect calls
  (`Call dword ptr [006AABE4]`) and leaves direct ones lower-case. Mnemonics
  are stored as-is; compare case-insensitively.
* **W32Dasm's back-annotations are incomplete.** It records "referenced by a
  CALL at …" only for labels it resolved. The indexer therefore also emits a
  forward `call_direct` edge from every direct `call <addr>` it parses.
* **Strings are useless as anchors here.** There are only 3 172 string
  references in the whole 2.3 MB of code, because essentially all game text
  lives in the separate `.gob` resource archives.

## How module attribution works

The retail build shipped with `assert()` enabled. MSVC's assert macro expands
to `push <line>; push <"D:\Ambit\...\File.cpp">; call <handler>`, and all 552
of those call sites funnel into a single handler at `004057A4`. The 55 filename
literals are the only symbolic information the stripped binary leaks.

Attribution pipeline (`tools/alf/modules.py`):

1. **Locate the literals.** Scan `.data` in the `.exe` for `D:\Ambit\…\*.cpp|h`
   and record their virtual addresses. Cross-checked against W32Dasm's
   annotations, which cover 42 of the 55.
2. **Find the referencing code.** Match on the *operand value*, not on the
   annotation — the push operand is always the literal's address, whereas
   W32Dasm annotates only some sites. This yields 552 anchors.
3. **Resolve incremental-link thunks.** Almost every `call` in this binary
   targets a five-byte `jmp` stub in a 2 131-entry table at the bottom of
   `.text`. Without resolving these, "who calls X" answers nothing. Synthetic
   `call_thunk` edges bypass the stub.
4. **Detect function starts** from four unioned signals: call targets, thunk
   destinations, `.data`/`.rdata` dwords pointing into `.text` (C++ vtable
   slots — ~10 400 of them, 99.6 % landing exactly on an instruction boundary),
   and `push ebp; mov ebp, esp` prologues plus post-padding addresses.
   Deliberately *not* "anything after a `ret`": this build omits frame pointers
   and pads only sporadically, so that rule nearly doubles the function count
   by splitting at every early return.
5. **Colour the functions**, recording how each decision was made:
   * `high` — the function contains an assert naming that file.
   * `medium` — the function lies between two `high` anchors naming the same
     file (linker locality), or shares a vtable with an attributed function.
   * `low` — no anchor, but every known caller resolves to a single module.
   * `none` — unattributed.

Results are in [`module-map.md`](module-map.md). Roughly 56 % of `.text` by
bytes gets some attribution; treat `low` as a hint, not a fact.

### Known limitations

* Function boundaries are heuristic. A function reached only through a jump
  table, with no vtable slot and no direct call, is absorbed into its
  predecessor — a handful of "functions" are implausibly large as a result.
* `medium` interpolation assumes the linker emitted each translation unit
  contiguously. That is normally true for this toolchain but is not guaranteed,
  especially around COMDAT-folded template code.
* Some files (`McAppUI.h`, `QuickDraw.h`, `UOcean.h`, `Toy.h`) are headers.
  Their "module" is really inlined code that could have come from any
  translation unit including them, which is why `McAppUI.h` appears as the
  single largest "module".
