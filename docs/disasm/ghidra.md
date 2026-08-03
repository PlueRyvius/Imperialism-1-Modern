# Decompiling the original binary with Ghidra

`tools/alf/` indexes a W32Dasm assembly listing. That is the right tool for
addresses, call graphs and crash resolution, and the wrong one for recovering a
formula — the labour-cost hunt failed in it, and there are
[seven engine defaults](../formulas/_index.md#the-seven-engine-defaults) that
cannot come from anywhere else.

This is a report on what a decompiler actually produces here, written before
building any workflow on it.

**Nothing here ships game data.** The Ghidra project is a derived work of a
copyrighted executable, exactly like the `.alf` index, and lives outside the
repository at `F:/ghidra-projects`. So do the decompiled samples quoted below.

## Setup

Ghidra 12.1.2, headless. Full analysis of `Imperialism.exe` took **121 seconds**.

Two things cost time and are worth writing down:

- **The install path must not contain spaces.** `analyzeHeadless.bat` fails with
  `'F:\Claude' is not recognized` and no other explanation. Ghidra lives at
  `F:/ghidra` for that reason alone.
- **Python scripts need PyGhidra**, which is a separate `pip install` of the
  bundled wheels. `.java` GhidraScripts need nothing — Ghidra compiles them on
  the fly. The scripts in `F:/ghidra-scripts` are Java for that reason.

JDK 21 was already installed, which is what Ghidra 12 wants.

```
F:/ghidra/support/analyzeHeadless.bat F:/ghidra-projects imperialism \
    -import E:/Imperialism/Imperialism.exe

F:/ghidra/support/analyzeHeadless.bat F:/ghidra-projects imperialism \
    -process Imperialism.exe -noanalysis \
    -scriptPath F:/ghidra-scripts -postScript DecompileTargets.java
```

## What the output is actually like

**Good, and better than expected.** A mid-sized function comes out legible.
This is `004B4390`, inside `UCity.cpp`:

```c
iVar4 = 0;  sVar3 = 0;
psVar6 = (short *)(param_1 + 0x5c);
do {
    sVar1 = thunk_FUN_00550d80(iVar4);
    if (sVar1 == 0) { sVar3 = sVar3 + *psVar6; }
    iVar4 = iVar4 + 1;  psVar6 = psVar6 + 1;
} while (iVar4 < 0xe);
...
iVar4 = FUN_005e83f0();
iVar4 = iVar4 % (int)sVar3 + 1;
```

Readable without effort: a **weighted random selection over fourteen things**,
summing weights from an array of `short` at structure offset `0x5C`, skipping
entries a predicate rejects, then drawing against the total. In the assembly
that is a hundred lines of unreadable ops.

Calling conventions survive (`__thiscall`, `__fastcall`), structure offsets and
strides are visible, and globals are cross-referenced, so `DAT_006a20f8` links
to the known nation tables.

**Bad, and roughly as expected.** Large functions are hard work. `004DAF30` in
`UCountry.cpp` opens with a raw SEH prologue (`unaff_FS_OFFSET`), thirty-odd
stack locals with generated names, and dispatches through
`(**(code **)(local_c0 + 0xa0))()`. Legible, but nothing is named and no
structure is recovered — every field access is a raw offset.

There is no free lunch: the decompiler recovers *control flow and arithmetic*
well, and *meaning* not at all.

## Ghidra's functions are not `tools/alf/`'s spans

This bit an assumption immediately. `004B3080` is listed by `tools/alf/` as
`UCity.cpp`'s main body, 4,607 bytes. Ghidra says it is a **7-byte vtable
setter**:

```c
void __fastcall FUN_004b3080(undefined4 *param_1)
{ *param_1 = &PTR_LAB_0066fec4; return; }
```

Both are right. The ALF figure is an **attributed address range** covering many
functions, not one function. Anything reading `module-map.md` as a function list
will target constructor stubs.

| | count |
|---|---|
| Ghidra functions | 8,111 |
| …excluding thunks | **5,699** |
| ALF attributed ranges | 3,034 |

## The label import names half the binary — and it is the wrong half

**2,807 of 5,699 non-thunk functions — 49.3%** — fall inside a range
`tools/alf/` has attributed to an original `.cpp`. That number looks good and is
misleading, which a calibration run established the hard way.

Every filename comes from a compiled-in `assert()` string, and asserts cluster
in view code. All 55 recovered files, by anchor count:

| file | anchors |
|---|---|
| `USmallViews.cpp` | 92 |
| `UCityViews.cpp` | 73 |
| `UViewMgr.cpp` | 53 |
| `UCityDialogs.cpp` | 44 |
| … | |
| **`UCity.cpp`** | **3** |
| **`UCountry.cpp`** | **1** |
| **`UCountryAuto.cpp`** | **1** |

There is no `UIndustry.cpp`, `UProduction.cpp` or `UEconomy.cpp` in the list at
all. The gameplay math does not assert, so it is not named. `_index.md` warned
about exactly this and the warning deserved more weight than the headline
percentage.

So the labels are worth having for navigation, and they will not lead to a
formula.

## The calibration run

The honest test of a decompiler is whether it can recover a number we already
know. Labour costs **2** per clothing cycle — the manual says so and every
shipped recipe agrees — so that number should be findable in the production
accounting if anything is.

It was not, and the reason is worth recording.

**`UCity.cpp` is not the economy module.** All 21 of its Ghidra functions
decompile cleanly, and what they compute is weighted averages and a weighted
random pick over a dense fourteen-entry enumeration, reading counts from an
array of `short` at object offset `0x5C` and attributes through typed getters
into a static table of 36-byte records at `~0x698100`. Whatever those fourteen
things are, the records hold costs in the hundreds to low thousands and
percentages descending from 100 to 30 — entity statistics, not industry capacity
and not labour.

`_index.md` listed `UCity.cpp` as "economy, dig here". That pointer is wrong and
has been corrected.

**`UCityDialogs.cpp` is not it either.** It was the better bet — the manual says
the labour total decrements as you drag a production slider, so the number is
UI-facing, and UI is what gets labelled. Its 26 functions decompile to rectangle
arithmetic and screen-position lookup tables. It draws the dialog; it does not
model it.

So the tool works and the map does not reach the code. That is a different
failure from "the decompiler cannot do this", and a much cheaper one to have
learnt on a question with a known answer.

## Verdict

Use Ghidra for recovering behaviour; keep `tools/alf/` for addresses, xrefs and
crash resolution. They answer different questions and the ALF index is what
makes the Ghidra database navigable.

Set expectations at "a competent C programmer's reconstruction with every name
stripped". Reading a formula out of it is real work — but it is work that can
succeed, which is not true of the assembly.

## Open

- **Find the economy code without the module map**, since the map does not cover
  it. Structure is the lead now, not filenames: production reads a seven-entry
  capacity array, and the seven engine defaults are written once at new-game
  setup. Searching decompiled output for those shapes is the next thing to try,
  and it is untested.
- Find where a new game is initialised. That is where the seven engine defaults
  live, and nothing in the corpus can point at it — a fair start carries no
  `ware`, `cash`, `deve`, `tech`, `tran`, `rail` or `rela` records at all.
- Decide whether the Ghidra database is worth scripting against repeatedly or
  whether one-off decompiles are enough. Do not build a pipeline before there is
  a second real question to ask it.
