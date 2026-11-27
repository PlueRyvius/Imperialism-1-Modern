# Transport screen: layout note

The format is the one in [`docs/look-design.md`](https://github.com/PlueRyvius/Imperialism-1-Modern/blob/main/docs/look-design.md)
("The layout-note format"). Measured on 27 Nov 2026 by the PC Dev (claude-joined-1)
by script, not by eye; the Architect's decisions on it are at the end.

**Measured from:** `C:\qf-test\evidence\original-s1\O30-transport.png`, **on ETHERCAT
only** (a 1920x1080 capture of the original game; the 640x480 game frame sits at
offset (640,300), at 1x). The PC copy of this note is
`C:\qf-test\evidence\look\transport-layout.md`, on ETHERCAT.

**Background:** [`assets/original/pictuniv/1021.png`](https://github.com/PlueRyvius/Imperialism-1-Modern/blob/main/assets/original/pictuniv/1021.png)
(640x480): 277,990 of the frame's 307,200 pixels equal it exactly.

**Art folder:** [`assets/original/`](https://github.com/PlueRyvius/Imperialism-1-Modern/tree/main/assets/original)
at 7f062d1 (a sparse clone on ETHERCAT, `C:\qf-test\work\look\repo`).

**Method:** a C# (System.Drawing) script compares the frame with each PNG, magenta
FF00FF transparent. A score is the fraction of a template's opaque pixels matching
the frame within tolerance.

**Target canvas:** the approved mockup (`C:\qf-test\evidence\mockups\transport-target-1920.png`,
on ETHERCAT only) places the scene at 2x with its top-left at (320,60); every pixel
outside the scene is #0E0905.

All coordinates are 1x frame coordinates, origin top-left.

| name | x | y | w | h | art | shows |
|---|---|---|---|---|---|---|
| `canvas` | 0 | 0 | 640 | 480 | — | whole scene, drawn at 2x at (320,60) on #0E0905 |
| `ledger` | 0 | 0 | 640 | 480 | `pictuniv/1021.png` | static; already carries every row in a greyed (inactive) look |
| `tab.transport` | 270 | 8 | 14 | 18 | `pictuniv/9456.png` | static (selected state, inferred) |
| `tab.industry` | 298 | 8 | 14 | 18 | `pictuniv/9453.png` | static |
| `tab.trade` | 326 | 8 | 14 | 18 | `pictuniv/9451.png` | static |
| `tab.diplomacy` | 355 | 8 | 14 | 18 | `pictuniv/9449.png` | static |
| `date` | 46 | 9 | 77 | 16 | — | current date; cream #E3E0B8 / #E3E082 text, brown #904813 / #9C5213 / #A25B13 and black shading |
| `treasury` | 157 | 9 | 48 | 16 | — | treasury figure; same text style as the date |
| `hottext` | 484 | 5 | 136 | 29 | — | two lines, right-aligned at x=620: the hovered element's name, then a second line; same style plus #6E3F19 / #5B0000 |
| `title.left` | 162 | 86 | 88 | 15 | — | left-page heading, ink #5E4A30 |
| `title.right` | 416 | 86 | 60 | 16 | — | right-page heading, ink #5E4A30 |
| `row.left` | 70 | 118 | 224 | 30 | `pictuniv/4001.png` … `4010.png` | pasted over the ledger only when the row is active; icons, "&", slider rim, gold track and arrow ends are baked in |
| `row.right` | 325 | 118 | 224 | 30 | `pictuniv/4011.png` … `4018.png` | as `row.left`, second column |
| `row.icon` | 70 | 118 | 32 | 30 | in strip | static, part of the strip |
| `slider.left` | 154 | 128 | 13 | 10 | in strip | static left arrow end (strip-local 84,10); lowers the slider |
| `slider.rim` | 167 | 130 | 113 | 1 | in strip | static dark red rim, #93340C / #800000 / #6E3F1A |
| `slider.edge` | 166 | 131 | 1 | 4 | — | plain fill #000000 |
| `slider.carried` | 167 | 131 | var | 4 | — | plain fill #0058A2, from x=167 to the fill point |
| `slider.spare` | var | 131 | var | 4 | — | plain fill #CABC6C, from the fill point to x=279 |
| `slider.track` | 167 | 131 | 113 | 4 | — | interior = carried + spare, 113 px (x 167..279) |
| `slider.highlight` | 166 | 135 | 115 | 1 | — | plain fill #FFFFFF |
| `slider.underline` | 166 | 136 | 115 | 2 | — | plain fill: green #5FBA4B or red #FA0500 |
| `slider.right` | 281 | 128 | 11 | 10 | in strip | static right arrow end (strip-local 211,10); raises the slider |
| `slider.handle` | — | — | — | — | — | none on the original; see decision D1 |
| `row.figure` | 254 | 140 | 38 | 8 | — | "a / b": black anti-aliased text, right-aligned at x=291 |
| `capacity.row` | 325 | 397 | 224 | 30 | `pictuniv/4019.png` | static frame of the capacity row |
| `capacity.bar` | 418 | 410 | 113 | 4 | — | plain fill #FA0500, the used share |
| `capacity.figure` | 499 | 421 | 48 | 9 | — | "used / capacity", black, right-aligned at x=546 |
| `capacity.label` | — | — | — | — | ? | none drawn by the game; presumably part of 4019 |
| `endturn` | — | — | — | — | `pictenu/1011.png` | not on this screen; see decision D3 |

## Repeats

Row pitch `dx=0 dy=31`, order top to bottom as on O30.

- **Left column** (x=70): 10 slots, k=0..9, y = 118 + 31k, art 4001 … 4010 in that order.
- **Right column** (x=325): 8 slots, k=0..7, y = 118 + 31k, art 4011 … 4018 in that
  order. Slot k=8 (y=366) is empty; the capacity row 4019 is at k=9 (y=397).
- **Per row k** (add 31k): rim 130, bar 131..134, white line 135, underline 136..137,
  figure 140..147.
- **Right column x:** the slider sits at dx=+251 from the left column (edge 417,
  interior 418..530, highlight and underline 417..531, arrow ends at about 405 and
  532); the figure at dx=+255 (right-aligned at 546); the strip at dx=+255 (325).
  Its slider frame starts at strip-local x 80, not 84 as on the left.
- **Inactive rows:** nothing is pasted and the ledger's own greyed row shows. Which
  rows were active on O30 is shot state, not layout.

## Icons

Baked into the strips. The 32x24 `pictuniv/7xx` files are near-identical copies, none
an exact pixel match: 719 → 4001 (.961), 700 → 4011 (.954), 704 → 4008 (.938),
703 → 4007 (.880), 701 → 4011 (.859), 702 → 4004 (.846), 705 → 4016 (.784),
718 → 4002 (.646, weak). Build from the strips, not the 7xx files.

Non-paper shapes per strip (strip-local x,y,w,h): 4001 `0,0,32,30` · "&" `37,14,7,7` ·
`47,4,31,23`; 4002 `0,0,12,30` · `31,4,25,22`; 4003 `25,2,31,23`; 4004 `33,4,17,23`;
4005 `26,8,31,15`; 4006 `33,4,15,24`; 4007 `0,0,15,30` · `27,5,28,23`; 4008
`0,0,29,30` · `29,7,25,20`; 4009 `0,0,55,30`; 4010 `0,0,88,30` · `133,21,46,9`; 4011
`3,7,23,20` · "&" `34,12,7,7` · `47,4,25,24`; 4012–4019 not separable from the
slider frame. The slider frame is at strip-local `84,10,138,10` in 4001–4010 and
`80,0,144,30` in 4011.

## Confidence

1. **Exact pixel matches:** the ledger 1021, the tab icons 9456, 9453, 9451, 9449.
   Which tab is which screen, and 9456 being the selected state of a 9455/9456 pair,
   are guesses.
2. **Strips (size match plus high score):** 4001 .886, 4002 .900, 4003 .900, 4004
   .881, 4007 .905, 4008 .886, 4011 .894, 4016 .952, 4019 .912. The missing fraction
   is the live overlay drawn on top. Inactive slots score .61–.68 and are placed by
   pitch. That the ledger holds greyed rows is an inference (lower chroma, about 27
   against 34).
3. **Slider pieces:** rim, gold track and arrow ends are strip pixels, measured
   exactly; the fills, white line and underline are plain fills with measured colours
   and sizes. `pictuniv/4020`–`4023` (12x11) do not match the arrows (.30–.40), nor do
   9457/9458.
4. **Text:** positions are bounding boxes of changed pixels. Font and heading wording
   are not identified.
5. **End Turn:** `pictenu/1011` (112x19) is not on O30 at .9 or above, nor in the mockup.

## The approved mockup against the original

| | original (O30) | approved mockup |
|---|---|---|
| track | 4 px fill, 1 px white line, 2 px underline at y 131..137 | 7 px track y 129..136, rim y 128, underline about 136.5..138.5 |
| handle | none | round knob, radius 4.5, #ECD696 outlined #6E4614 |
| name label | none | the good's name under each bar at x0, y about 141 + 31k |
| figure | right-aligned at 291 / 546 | right-aligned at 279 / 529 |
| right slider | 418–530 | 418–529 |

## Decisions (Architect, 27 Nov 2026)

- **D1. Geometry is the original's; two approved additions stay.** Andy's ruling is
  the original's exact layout, so every position, size and colour above is built as
  measured on O30, the track and figure included. The **round handle** and the **name
  under each bar** are kept from the mockup Andy approved ("This looks perfect"), and
  the look-design doc names the handle; each is one node, so dropping either later is
  a one-line change. The Transport PR lists both for Andy. *Chosen.*
- **D2. A row is active when its commodity has a slider.** The strip is pasted for
  every commodity in `TransportPreview.Sliders`, and the ledger's greyed row shows for
  the rest. *Chosen*: the original's own rule for activating a row is not traced.
- **D3. No End Turn on Transport.** The original has none there; End Turn stays where
  the client shell already puts it. The look-design doc's Transport target is
  corrected to match.
- **D4. Build from the strips.** The icons, rim and arrow ends come from
  `pictuniv/4001`–`4019`; the 7xx icons and 4020–4023 are not used. Which strip is
  which commodity is identified by the Transport PR by viewing the strips, and added
  to this note.
- **D5. Tabs.** The four tab icons are drawn as static art; whether they navigate is
  the Transport PR's choice and is listed in it, since which tab is which is a guess.

## Strip identification (Graphic Dev, 27 Nov 2026)

Viewed all nineteen original PNGs together, then inspected 4009 and 4015
individually. These are art identities, not quantities or a new allocation rule.
The steel roll, fabric rolls and fuel pump identifications are inferred from the
icons; the office-PC comparison must confirm their names through hot text.

| strip | commodity key(s) | icon |
|---|---|---|
| 4001 | `commodity.fish`, `commodity.livestock` | fish + cattle |
| 4002 | `commodity.fruit` | fruit |
| 4003 | `commodity.grain` | grain |
| 4004 | `commodity.timber` | tree |
| 4005 | `commodity.lumber` | boards |
| 4006 | `commodity.furniture` | chair |
| 4007 | `commodity.coal` | black lumps |
| 4008 | `commodity.iron` | brown ore |
| 4009 | `commodity.steel` | metal roll (inferred) |
| 4010 | `commodity.hardware` | hammer |
| 4011 | `commodity.cotton`, `commodity.wool` | cotton + sheep |
| 4012 | `commodity.fabric` | fabric rolls (inferred) |
| 4013 | `commodity.clothing` | clothes |
| 4014 | `commodity.oil` | black drop |
| 4015 | `commodity.fuel` | fuel pump (inferred) |
| 4016 | `commodity.horses` | horse |
| 4017 | `commodity.gold` | gold bars |
| 4018 | `commodity.gems` | gems |
| 4019 | — | shared capacity, ship + locomotive |

## Implementation blockers found during the Transport review

**MISSING: shared-row editing contract.** Strip 4001 has one track for fish and
livestock, and 4011 has one for cotton and wool. Presentation currently returns
one `TransportSliderPreview` per individual commodity, and
`OrdersDraft.SetTransport(CommodityId, long)` edits one commodity at a time.
The existing opening-default test explicitly recognises these two shared rows;
it sums the two commodity quantities only to compare the original's figures.
It supplies no rule for splitting a subsequent shared-slider click.

An exact interactive screen needs a decision about how a shared increment or
decrement changes the individual orders, their priority, and the bound when one
commodity is exhausted. Summing a read-only figure cannot provide that setter.
Drawing two controls over the same track would hide an existing edit, and
silently choosing a split would change data handling. This needs the Architect's
behaviour contract before client implementation, under the owner's visual-role
boundary. No such adapter or game-rule change has been made.

**MISSING: placement for unmatched sliders.** The current screen ensures every
warehouse commodity has a draft row, including `commodity.paper`,
`commodity.canned-food` and `commodity.armaments`. No corresponding strip appears
in 4001–4018. Decision D2 and the exact eighteen-slot layout therefore need a
policy for these entries (and any custom-world commodities); hiding or relocating
editable orders is not settled by the layout note.

**MISSING: available quantity and demand status.** `TransportSliderPreview`
provides `Requested`, `Committed` and `IsShort`, but no countryside availability
or worker/mill demand status. `IsShort` compares committed with requested, not
worker/mill demand. The authorised read-only-field exception can address these
once the row grouping is settled. `TransportDefault.Inputs` already supplies
expected gather and demand inputs; its expected gather is an upper bound, not
an exact connectivity-aware forecast. No new view fields have been added yet.

## Decisions on the blockers (Architect, 27 Nov 2026)

These settle the three MISSING items above. They are presentation contracts:
Core, content and every game number are unchanged, and the draft still submits one
transport order per commodity exactly as today.

- **D6. A ledger row may hold two commodities.** Presentation gains a row view,
  one per ledger row. `fish`+`livestock` and `cotton`+`wool` are each one row,
  applied only when both keys exist in the world; every other commodity is a row of
  its own. A row's figures are the sums of its members'. *Chosen*: the strips
  show the pairs, and nothing else in our model joins them.
- **D7. A click on a shared row moves one unit of one member.** Raise adds one to
  the member with the most expected-but-unordered (`Available − ordered`), ties to
  the lower commodity id. Lower removes one from the member with the most ordered,
  ties to the higher commodity id. A drag to a total repeats single steps. The
  opening fill from `TransportDefault` is kept as it is: nothing is re-split until
  the player clicks. *Chosen*: deterministic, never orders a member past what it is
  expected to yield, and leaves the measured opening fill alone.
- **D8. No raise past the shared capacity.** A raise is refused when the draft's
  ordered total has reached the transport capacity, and on a row when it has
  reached the row's `Available`. That is the original's slider behaviour. With the
  total never above capacity, the order in which Core trims rows can't change the
  result, so the rebuilt screen has **no move up / move down controls**. The
  draft's order stays `TransportDefault`'s, food first. *Chosen.*
- **D9. Only gatherable goods have a row.** A slotted row is active, with its strip
  pasted and editable, when its `Available` is above zero. Otherwise the ledger's
  greyed row shows and does not respond. This supersedes D2. Commodities with no
  strip (`paper`, `canned-food`, `armaments`) get no row: Extraction never gathers
  them, so a slider for them could carry nothing, and their draft entries stay at
  zero, untouched. A gatherable commodity with no slot, which no shipped content
  has, takes the empty right-hand slot k=8 with the slider drawn and no strip. Any
  beyond that one are not shown, and the PR lists this as a limit. *Chosen.*
- **D10. The figure and the underline.** The row figure is "ordered / available".
  `Available` is `TransportDemandInputs.Available`, an upper bound and labelled
  as one. The underline is green when the ordered amount covers the row's demand,
  as `TransportDefault` computes it (exposed from there, not restated), and red
  otherwise. *Chosen*: the original's own meaning for the colour is not traced.
- **D11. What the contract settled while it was built** (claude-2, accepted by the
  Architect). The row edits take the screen's current `TransportPreview`, because
  the draft knows neither capacity nor Available. Produced goods that have a strip
  (lumber, furniture, steel, hardware, fabric, clothing, fuel) keep their row and
  show greyed while nothing is available; this follows D9. A pair with one member
  missing stays on the pair's strip. Only a member that a row raise adds is placed
  beside its partner, so the opening fill's order stands. Demand is capped at
  Available. A greyed row that still orders something may be lowered, so the
  player can clear it. **Known limit:** a row's Available counts deposits only and
  leaves out port fishing, so a country that fishes only from ports sees a greyed
  fish row. It is unchanged because counting port fishing would change the opening
  fill, which matches the original on GB s1 apart from issue #92. The PR lists it.
