# Industry layout

28 November 2026: roles, positions and draw order traced from the original's code.
M = measured code/data or screenshot geometry; I = inferred association. Displayed
stage and access rules below are chosen, not recovered game rules.
The source trace and original screenshots are ETHERCAT/server-only and cannot be linked.
No game data or original screenshots are committed here.

The 640×480 canvas uses original `pictuniv/6000.png`, centred at whole-number scale
on #0E0905 (2× at 1920×1080). Text uses crisp Lora and Fraunces Bold at final scale.
All coordinates below are frame pixels, top-left origin. All tile canvases and masks
are 250×165. `pictuniv` is the source unless `pictpaid` is stated.

## Building slots

Slot, name and position are M (table 0x00696198, name list 10009). The tile formula
`7000 + 16·L + slot` is M; the meaning of L is untraced. Chosen: use the stage seen
in play, L3 for slots 0–6, L2 for Shipyard, L1 for Armory, University, Railyard and
Capitol; Power Plant uses 7027. Panel association `9200 + slot` is I; the warehouse
9215 and Capitol 9216 exceptions are M from their windows.

| Slot | Building (M) | Position (M) | Art / mask | Panel | Our role / shows |
|---|---|---|---|---|---|
| 0 | Textile Mill | 200,235 | 7048 L3 | 9200 I | facility.textile-mill |
| 1 | Clothing Factory | 340,300 | 7049 L3 | 9201 I | facility.clothing-factory |
| 2 | Steel Mill | 281,184 | 7050 L3 | 9202 I | facility.steel-mill |
| 3 | Metalworks | 340,266 | 7051 L3 | 9203 I | facility.metal-works; both recipes |
| 4 | Lumber Mill | 87,286 | 7052 L3 | 9204 I | facility.lumber-mill; both recipes |
| 5 | Furniture Factory | 230,310 | 7053 L3 | 9205 I | facility.furniture-factory |
| 6 | Oil Refinery | 340,139 | pictpaid/7054 L3 | 9206 I | facility.oil-refinery |
| 7 | Shipyard | 240,35 | pictpaid/7039 L2 | 9207 absent | existing Armoury navigation |
| 8 | Armory | 50,220 | 7024 L1 | 9208 absent | existing Armoury navigation |
| 9 | Trade School | 50,107 | background; mask 7109 | 9209 I | trade-school; hottext, order panel follow-up |
| 10 | University | 50,35 | 7026 L1 | 9210 absent | university; hottext only; MISSING panel art |
| 11 | Power Plant | 340,139 | pictpaid/7027 | 9211 I | facility.power-plant; existing BurnFuel control |
| 12 | Food Processing | 82,35 | background; mask 7112 | 9212 I | facility.food-processing |
| 13 | Warehouse | 300,35 | background; mask 7113 | 9215 M | warehouse |
| 14 | Railyard | 340,44 | 7030 L1 | 9214 I | RailyardPreview.RequestedPoints |
| 15 | Capitol | 150,95 | 7031 L1 | 9216 M | RecruitmentPreview.Requested |

Draw order M (0x00696178): **12 13 7 10 14 15 9 6 11 2 3 8 0 1 4 5**.
Hover/click resolves the reverse order, as 0x004BAFA0 does. Hit geometry tests each opaque, non-magenta pixel of the chosen tile, or
the non-white pixels of the L0 mask for slots 9/12/13. These three
buildings are already painted into background 6000; their masks are not drawn.

Chosen: a facility target/tile shows only at positive or unlimited country capacity.
Shipyard, Armory, University, Railyard and Capitol always show. Warehouse and Trade
School retain their background targets. Where both Refinery and Power Plant are
held, only Refinery draws: the frame arrows reach the Power Plant's 9211 fuel panel.
Frame arrows also reach absent facilities for expansion. Facility hottext reads the
content name; special names are Railyard, Capitol, Warehouse, Shipyard, Armory,
University and Trade School.

## Side rail

All positions and roles below are M. The existing manifest entries under `art/icon/commodity_*.png` supply 6001–6011 and 6013; 6012 is in `art/industry/`. Each picture is 39×35, except 6012 (39×80).
Figures are labels at picture-local (2,22), size 33×14; 6012 uses (2,65).
Figure placement and using the existing Presentation snapshots are I.

| Art | Position | shows |
|---|---|---|
| 6013 | 6,110 | IndustryPreview.LabourAvailable; unlimited marker where applicable |
| 6001 | 6,149 | CountryStatusView.Workforce untrained Total |
| 6002 | 6,189 | CountryStatusView.Workforce trained Total |
| 6003 | 6,229 | CountryStatusView.Workforce expert Total |
| 6007 | 6,269 | PowerPlantView.PowerMade, empty when not modelled |
| 6008 | 6,349 | CountryStatusView.TotalSickWorkers, summed in Presentation |
| 6009 | 6,389 | MISSING starved workers; empty |
| 6010 | 595,110 | MISSING grain needed; empty |
| 6011 | 595,149 | MISSING fruit needed; empty |
| 6012 | 595,189 | MISSING meat needed; empty |
| 6005 | 595,318 | MISSING clothing needed; empty |
| 6006 | 595,357 | MISSING furniture needed; empty |
| 6004 | 595,395 | MISSING hardware needed; empty |

Hover identifies the picture; workforce/power/sickness show the matching no-workers,
no-power or no-illness wording when the existing figure is zero. Missing figures
stay empty, rather than substituting warehouse stock or calculating demand here.

## Header and dialogs

| name | x | y | w | h | shows |
|---|---|---|---|---|---|
| date | 50 | 7 | 105 | 22 | CountryStatusView.CurrentDate via TransportText |
| treasury | 161 | 7 | 100 | 22 | CountryStatusView.Cash |
| hottext | 430 | 2 | 157 | 18 | facility name / static special name |
| hottext.detail | 420 | 18 | 167 | 16 | current-screen wording / expansion bill and warning |
| tabs | 270/298/326/355 | 8 | 14 | 18 | 9455/9454/9451/9449; Transport/Industry/Trade/Diplomacy |

Dialog art origins: Textile (98,166), Clothing (300,330), Steel and Metalworks
(300,271), Lumber (98,310), Furniture (245,271), Refinery and Power Plant (300,220),
Food Processing and Capitol (63,236), Railyard (98,166), Warehouse (71,61).
Textile, Furniture, Capitol and Warehouse positions are M screenshot measurements;
other panel positions are I. Each frame is panel-local (-3,-26), 3 px border with
26 px top bar, plus the original panel's dimensions. Top bar or Escape closes the
top dialog. Warehouse remains open beside production and refreshes after edits.

Panel-local controls: title (19,8,174,17); capacity (19,24,170,16); expansion
9244/9245 at (196,9), 61×24; single-recipe arrows 9240/9242 at (68,94)/(231,94), 9×9,
with 9241/9243 held art; figure (81,92,146,14). Both Metalworks and Lumber recipes
share the proper panel: arrows at y=102/132 and figures at y=100/130, below the
ingredients on the original bars. Recipe names remain available as arrow hottext. Chosen: alternative recipes in a
single-bar panel (Textile) split that bottom row horizontally, keeping every arrow
and figure below the ingredients; two alternatives use arrows x=68/145 and 154/231,
figures (81,92,60,14) and (167,92,60,14). Food Processing uses its lower
bar: arrows y=134, figure (81,132,146,14). Capitol recruitment arrows (74,89)/(237,89), figure (85,102,149,16).
Power Plant's fuel arrows retain (68,91)/(231,91), figure (79,103,149,16), and the existing BurnFuel setter.

Frame-bar previous/next arrows at panel-local (5,-19)/(252,-19) are retained for
absent facilities and the covered Power Plant. They do not change production priority.
Expansion remains the plain existing SetExpansion toggle (I identity). 9251 is traced
320×225 confirmation art; its name/capacity/cost/warning association is I, and its
OK/Cancel confirmation flow is deferred to keep this change bounded.

### Warehouse geometry (M positions, I food identities)

| name | x | y | w | h | art | shows |
|---|---|---|---|---|---|---|
| `warehouse` | 71 | 61 | 176 | 335 | pictuniv/9215.png | CountryStatusView.Warehouse / IndustryPreview.WarehouseBalance |
| `warehouse.frame` | 68 | 35 | 182 | 364 | — | static E9ECF3 / BFCEDC / 4D4D4D; top bar closes |
| `warehouse.title` | 123 | 68 | 120 | 18 | — | static Warehouse |
| `warehouse.stock.cotton` | 107 | 81 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.fabric` | 170 | 100 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.clothing` | 224 | 100 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.wool` | 106 | 114 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.paper` | 172 | 151 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.timber` | 106 | 167 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.furniture` | 221 | 167 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.lumber` | 169 | 180 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.iron` | 107 | 218 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.hardware` | 224 | 221 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.steel` | 170 | 238 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.coal` | 106 | 244 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.armaments` | 227 | 245 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.oil` | 110 | 277 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.fuel` | 175 | 279 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.grain` | 109 | 314 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **unresolved measurement** food identity |
| `warehouse.stock.fruit` | 158 | 314 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **unresolved measurement** food identity |
| `warehouse.stock.livestock` | 158 | 341 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **unresolved measurement** food identity |
| `warehouse.stock.canned-food` | 215 | 333 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.horses` | 137 | 379 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.labour` | 204 | 379 | 30 | 14 | — | IndustryPreview.LabourAvailable |
| `warehouse.stock.fish` | 109 | 341 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.power` | 223 | 282 | 24 | 14 | — | PowerPlantView.PowerMade (transient, not inventory) |
| `warehouse.balance` | 224 | 42 | 8 | 9 | pictuniv/9242.png | toggle opening stock / IndustryPreview.WarehouseBalance (chosen) |


## Open items

- MISSING: University panel art 9210; it supplies hover text and opens nothing.
- Trade School panel 9209 and SetTraining wiring remain a follow-up; its baked-in target supplies hover text.
- MISSING: starved and six needed-goods figures lack Presentation fields; empty on the side rail.
- MISSING: Capitol province-count line until https://github.com/PlueRyvius/Imperialism-1-Modern/pull/103 lands.
- Other food identities, frame right-side grey area and small background residual remain unresolved measurements.
- Priority arrows stay omitted; standing draft order is unchanged. Flagged for Andy.
- Windows 1920×1080 comparison beside the original and Tester gate remain pending.

Warehouse opens with stock; a chosen frame arrow switches to the detached
IndustryPreview.WarehouseBalance. It contains outputs and accepted construction,
Shipyard and recruitment deductions, not future feeding, transport or trade deliveries.
No game figure is calculated in the client.
