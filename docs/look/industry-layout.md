# Industry layout

Measured sources (ETHERCAT only; file names, not repository files): O31-industry,
O32-industry-furniture-factory, O33-industry-warehouse, O34-industry-capitol,
C1a-01-textile and C1a-tmp-city. Draft 3 measurements, 28 November 2026.

Coordinates are original 640×480 pixels, top-left origin. Background:
`pictuniv/6000.png`. At 1920×1080 the frame is 2×, at (320,60), on #0E0905.
Text uses Lora and Fraunces Bold, oversampled at the final scale.

| name | x | y | w | h | art | shows |
|---|---|---|---|---|---|---|
| `canvas` | 0 | 0 | 640 | 480 | — | static |
| `city` | 0 | 0 | 640 | 480 | pictuniv/6000.png | static |
| `bldg.7026` | 50 | 35 | 250 | 165 | pictuniv/7026.png | FacilityPreview.Capacity / static railyard; **pending trace** role |
| `bldg.7026.hit` | 67 | 87 | 191 | 100 | — | opens railyard; opaque bounding rectangle, not alpha |
| `production.railyard` | 98 | 166 | 270 | 120 | pictuniv/9214.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7039` | 240 | 35 | 250 | 165 | pictpaid/7039.png | FacilityPreview.Capacity / static warehouse; **pending trace** role |
| `bldg.7039.hit` | 248 | 47 | 216 | 141 | — | opens warehouse; opaque bounding rectangle, not alpha |
| `bldg.7030` | 340 | 44 | 250 | 165 | pictuniv/7030.png | FacilityPreview.Capacity / static facility.oil-refinery; **pending trace** role |
| `bldg.7030.hit` | 451 | 69 | 138 | 140 | — | opens facility.oil-refinery; opaque bounding rectangle, not alpha |
| `production.facility.oil-refinery` | 300 | 110 | 270 | 120 | pictuniv/9211.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7031` | 150 | 95 | 250 | 165 | pictuniv/7031.png | FacilityPreview.Capacity / static capitol; **pending trace** role |
| `bldg.7031.hit` | 204 | 123 | 148 | 102 | — | opens capitol; opaque bounding rectangle, not alpha |
| `production.capitol` | 63 | 236 | 270 | 120 | pictuniv/9216.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association |
| `bldg.7050` | 281 | 184 | 250 | 165 | pictuniv/7050.png | FacilityPreview.Capacity / static facility.textile-mill; **pending trace** role |
| `bldg.7050.hit` | 306 | 208 | 205 | 131 | — | opens facility.textile-mill; opaque bounding rectangle, not alpha |
| `production.facility.textile-mill` | 98 | 166 | 270 | 120 | pictuniv/9200.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association |
| `bldg.7024` | 50 | 220 | 250 | 165 | pictuniv/7024.png | FacilityPreview.Capacity / static facility.food-processing; **pending trace** role |
| `bldg.7024.hit` | 50 | 258 | 107 | 75 | — | opens facility.food-processing; opaque bounding rectangle, not alpha |
| `production.facility.food-processing` | 63 | 236 | 270 | 120 | pictuniv/9214.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7048` | 200 | 235 | 250 | 165 | pictuniv/7048.png | FacilityPreview.Capacity / static facility.furniture-factory; **pending trace** role |
| `bldg.7048.hit` | 208 | 259 | 210 | 135 | — | opens facility.furniture-factory; opaque bounding rectangle, not alpha |
| `production.facility.furniture-factory` | 245 | 271 | 270 | 120 | pictuniv/9205.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association |
| `bldg.7051` | 340 | 266 | 250 | 165 | pictuniv/7051.png | FacilityPreview.Capacity / static facility.steel-mill; **pending trace** role |
| `bldg.7051.hit` | 462 | 270 | 128 | 139 | — | opens facility.steel-mill; opaque bounding rectangle, not alpha |
| `production.facility.steel-mill` | 300 | 271 | 270 | 120 | pictuniv/9202.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7052` | 87 | 286 | 250 | 165 | pictuniv/7052.png | FacilityPreview.Capacity / static facility.lumber-mill; **pending trace** role |
| `bldg.7052.hit` | 107 | 294 | 209 | 147 | — | opens facility.lumber-mill; opaque bounding rectangle, not alpha |
| `production.facility.lumber-mill` | 98 | 310 | 270 | 120 | pictuniv/9206.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7049` | 340 | 300 | 250 | 165 | pictuniv/7049.png | FacilityPreview.Capacity / static facility.metal-works; **pending trace** role |
| `bldg.7049.hit` | 378 | 343 | 170 | 118 | — | opens facility.metal-works; opaque bounding rectangle, not alpha |
| `production.facility.metal-works` | 300 | 330 | 270 | 120 | pictuniv/9211.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `bldg.7053` | 230 | 310 | 250 | 165 | pictuniv/7053.png | FacilityPreview.Capacity / static facility.clothing-factory; **pending trace** role |
| `bldg.7053.hit` | 272 | 378 | 177 | 97 | — | opens facility.clothing-factory; opaque bounding rectangle, not alpha |
| `production.facility.clothing-factory` | 245 | 330 | 270 | 120 | pictuniv/9201.png | FacilityPreview / RecruitmentPreview / RailyardPreview; **pending trace** association and candidate art |
| `tab.Transport` | 270 | 8 | 14 | 18 | pictuniv/9455.png | static navigation |
| `tab.Industry` | 298 | 8 | 14 | 18 | pictuniv/9454.png | static navigation |
| `tab.BidAndOffers` | 326 | 8 | 14 | 18 | pictuniv/9451.png | static navigation |
| `tab.Diplomacy` | 355 | 8 | 14 | 18 | pictuniv/9449.png | static navigation |
| `date` | 50 | 7 | 105 | 22 | — | CountryStatusView.CurrentDate via TransportText |
| `treasury` | 161 | 7 | 100 | 22 | — | CountryStatusView.Cash |
| `hottext` | 430 | 2 | 157 | 18 | — | FacilityPreview.FacilityName / static hover name |
| `hottext.detail` | 420 | 18 | 167 | 16 | — | RecipePreview.RequestedCycles and FacilityPreview.Capacity / expansion cost and warning |
| `side.6013` | 6 | 110 | 39 | 35 | pictuniv/6013.png | static |
| `side.6001` | 6 | 149 | 39 | 35 | pictuniv/6001.png | static |
| `side.6002` | 6 | 189 | 39 | 35 | pictuniv/6002.png | static |
| `side.6003` | 6 | 229 | 39 | 35 | pictuniv/6003.png | static |
| `side.6010` | 595 | 110 | 39 | 35 | pictuniv/6010.png | static |
| `side.6011` | 595 | 149 | 39 | 35 | pictuniv/6011.png | static |
| `side.6012` | 595 | 189 | 39 | 80 | pictuniv/6012.png | static |
| `side.6013.figure` | 10 | 132 | 25 | 14 | — | IndustryPreview.LabourAvailable (inferred) |
| `side.6001.figure` | 23 | 175 | 6 | 8 | — | **pending trace**, empty |
| `side.6002.figure` | 19 | 215 | 11 | 8 | — | **pending trace**, empty |
| `side.6003.figure` | 18 | 255 | 12 | 8 | — | **pending trace**, empty |
| `side.6010.figure` | 599 | 132 | 25 | 14 | — | CountryStatusView.Warehouse canned-food (inferred) |
| `side.6011.figure` | 608 | 171 | 11 | 12 | — | **pending trace**, empty |
| `side.6012.figure` | 608 | 260 | 11 | 8 | — | **pending trace**, empty |
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
| `warehouse.stock.grain` | 109 | 314 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **pending trace** food identity |
| `warehouse.stock.fruit` | 158 | 314 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **pending trace** food identity |
| `warehouse.stock.livestock` | 158 | 341 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity; **pending trace** food identity |
| `warehouse.stock.canned-food` | 215 | 333 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.stock.horses` | 137 | 379 | 24 | 14 | — | CountryStatusView.Warehouse.Available / IndustryPreview.WarehouseBalance.Quantity |
| `warehouse.labour` | 204 | 379 | 30 | 14 | — | IndustryPreview.LabourAvailable |
| `warehouse.stock.fish` | — | — | — | — | — | **MISSING** fish position pending trace; omitted |
| `warehouse.power` | 223 | 282 | 12 | 8 | — | **MISSING** warehouse power (transient, not inventory); omitted |
| `warehouse.balance` | 224 | 42 | 8 | 9 | pictuniv/9242.png | toggle opening stock / IndustryPreview.WarehouseBalance (chosen) |

The following rows are panel-local (add the relevant `production.*` origin).

| name | x | y | w | h | art | shows |
|---|---|---|---|---|---|---|
| `production.frame` | -3 | -26 | 276 | 149 | — | static code-drawn frame; top bar closes |
| `title` | 19 | 8 | 174 | 17 | — | FacilityPreview.FacilityName / static Capitol, Railyard |
| `capacity` | 19 | 24 | 170 | 16 | — | FacilityPreview.Capacity / RecruitmentPreview.Cap |
| `expansion` | 196 | 9 | 61 | 24 | pictuniv/9244.png; 9245 alternate | FacilityPreview.ExpansionOrdered; toggles SetExpansion (chosen) |
| `recipe.left` | 68 | 91 | 9 | 9 | pictuniv/9240.png; 9241 held | RecipePreview.RequestedCycles decrease |
| `recipe.right` | 231 | 91 | 9 | 9 | pictuniv/9242.png; 9243 held | RecipePreview.RequestedCycles increase |
| `recipe.figure` | 79 | 103 | 149 | 16 | — | RecipePreview.RequestedCycles |
| `recruitment.left` | 74 | 89 | 9 | 9 | pictuniv/9240.png | RecruitmentPreview.Requested decrease |
| `recruitment.right` | 237 | 89 | 9 | 9 | pictuniv/9242.png | RecruitmentPreview.Requested increase |
| `recruitment.figure` | 85 | 102 | 149 | 16 | — | RecruitmentPreview.Requested |
| `capitol.provinces` | 136 | 24 | 100 | 16 | — | **MISSING** DiplomacyPreview.ProvincesOwned on base 5169add; omitted |

For multiple recipes, all appear in this panel, in draft order: earlier rows are
40 px above the last row; a recipe name appears 13 px above its arrows.
Railyard uses the same arrows and figure as a single recipe, from
`RailyardPreview.RequestedPoints`; there is no expansion control on Capitol or Railyard.

The frame adds previous/next facility arrows at panel-local (5,-19) and (252,-19)
(Page Up / Page Down too), chosen to keep expansions of hidden or unmapped
facilities reachable. Power Plant uses candidate 9214; its tile is **pending trace**.
Custom facility keys also use 9214 through the selector. No priority setter is called.

## Chosen rules and unresolved measurements

- Draw a facility tile only when its country has it (positive capacity, or unlimited). Warehouse, Capitol and Railyard always draw.
- Expansion button 9244/9245 calls the existing SetExpansion setter; identity is inferred.
- Production priority arrows are omitted; draft order remains unchanged. Andy’s ruling remains requested.
- Rectangular building hit boxes are the measured opaque bounds. 7039 uses the inferred (8,12,216,141) bound.
- Draw order is tile bottom, then x, exactly the table order.
- Panel coordinates for textile, furniture and Capitol are measured; other positions and all tile roles are pending trace.
- Panel candidates 9201, 9202, 9206, 9211 and 9214, including Railyard and Power Plant, are pending trace.
- Five side figures (6001/6002/6003/6011/6012) remain empty pending trace.
- Food positions are inferred grain/fruit/livestock, pending trace; fish placement and power are MISSING and omitted.
- Frame-bar wording, textile extra box, and the small ground residual near (467,199) remain pending trace; no invented figure is drawn.
- Warehouse frame uses the actual panel width; the original’s grey area to its right is pending trace and omitted.
- Text boxes are enlarged slightly for the modern pair. Figures are crisp labels, never raster text enlarged from 1×.
- Warehouse figures open as stock; a frame arrow toggles after-industry balance and hover names both. This presentation access is chosen.

Warehouse balance includes production outputs and accepted construction, Shipyard and
recruitment deductions; it excludes feeding and future transport/trade deliveries.
It is a detached read-only Presentation field, not arithmetic in the client.

Escape or clicking a dialog frame closes the top dialog. Warehouse stays open while
a building panel is used. Hold repeat: 400 ms, then every 60 ms.
ETHERCAT 1920×1080 comparison remains a later acceptance step.
