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

## The label import is worth building

The question that decides it: how many of Ghidra's functions fall inside a range
`tools/alf/` has already attributed to an original `.cpp`?

**2,807 of 5,699 — 49.3%.** By ALF's own confidence grading:

| confidence | functions |
|---|---|
| `high` (contains an assert naming the file) | 567 |
| `medium` (between two same-file anchors) | 731 |
| `low` (inferred from callers) | 1,509 |

So pushing the module map into Ghidra as namespaces would name roughly half the
binary's functions by the source file they came from, with 567 of them resting
on a compiled-in `assert()` string rather than inference. `UCity.cpp` becomes a
browsable module instead of an address range.

That is worth doing, and it is the obvious next step here.

## Verdict

Use Ghidra for recovering behaviour; keep `tools/alf/` for addresses, xrefs and
crash resolution. They answer different questions and the ALF index is what
makes the Ghidra database navigable.

Set expectations at "a competent C programmer's reconstruction with every name
stripped". Reading a formula out of it is real work — but it is work that can
succeed, which is not true of the assembly.

## Open

- Push `module-map.md` into Ghidra as namespaces, and re-export to confirm the
  49.3% lands where it should.
- Find where a new game is initialised. That is where the seven engine defaults
  live, and nothing in the corpus can point at it — a fair start carries no
  `ware`, `cash`, `deve`, `tech`, `tran`, `rail` or `rela` records at all.
- Decide whether the Ghidra database is worth scripting against repeatedly or
  whether one-off decompiles are enough. Do not build a pipeline before there is
  a second real question to ask it.
