# Original art

The interface is drawn with the 1997 game's own artwork, extracted once from a
local installation and committed. This document records what the archives are,
how the extractor reads them, what was found rather than assumed, and what was
decided rather than found.

## The archives are resource-only modules

`Data/*.gob` are Win32 portable executables that contain nothing but resources.
Every one begins `MZ`, and in `pictuniv.gob` the resource data directory covers
`RVA 0x8000, size 0x1DF0850` — essentially the whole 31 MB file. So reading them
needs a resource-tree walk and a bitmap decode and no Windows API at all, which
matters because CI builds this solution on Linux.

| Archive | Bitmaps | What it holds |
|---|---|---|
| `pictuniv.gob` | 1,098 | Map tiles, buildings, civilians, chrome, screen frames |
| `pictpaid.gob` | 259 | Portraits, dialog art |
| `pictwv0-3.gob` | 213 each | World-view tile sets, four of them |
| `pictenu.gob` | 32 | English-localised art, text baked in |
| `tabsenu.gob` | — | 43 `TABLE` resources, `DATA/001.TAB` onwards, still undecoded |
| `STR#ENU.GOB` | — | 314 `RT_STRING` blocks; see `docs/disasm/` |

The reader lives in `src/Imperialism.Formats/Resources/` rather than in a new
project, because a `.gob` is a 1997 Imperialism format and that is where those
live. Two implementation notes are worth keeping, because both produce
plausible-looking output rather than errors when wrong:

- Offsets **inside** the resource directory are relative to the directory start;
  `IMAGE_RESOURCE_DATA_ENTRY.OffsetToData` is a **relative virtual address** and
  has to go through the section table. Mixing them silently reads the wrong bytes.
- Image rows are padded to four bytes. A one-pixel-wide eight-bit image still
  occupies four bytes per row.

## Two things the survey had wrong

**Thirty-four images are compressed.** `pictuniv.gob` has 16 and `pictpaid.gob`
has 18 encoded `BI_RLE8`. This was found by throwing on any unexpected
compression and reading the failures, which is the only reason the number is
known rather than assumed. The decoder handles run-length now, including the
end-of-line and delta escapes the encoder uses to skip regions outright.

**Every image shares one palette.** All 2,241 bitmaps across all seven archives
carry a byte-identical 256-entry table — its first twenty entries are the
standard Windows system palette. This matters more than it sounds: the concern
going in was that a per-image palette would make an index key mean different
colours in different images, so a mis-applied rule would punch holes in
unrelated art. It does not arise. **An index key and a colour key are the same
statement here**, and that caveat is retracted.

## The transparency rule

**Palette index 16 is magenta `FF00FF`, and it is the key.** The evidence:

- It is the only magenta entry in the shared palette.
- It is the commonest uniform one-pixel border ring: 113 of the 174 images in
  `pictuniv.gob` that have one, and the top value in `pictpaid.gob` too.
- 365 of 1,602 images contain it at all — sprites, overlays and figures. Rails
  and rivers are drawn on flat magenta fields, which is visible at a glance on a
  contact sheet.
- **Not one of the sixty-four full-screen 640x480 backgrounds contains a single
  magenta pixel.**

That last point is what makes the rule simple. The plan expected to have to
decide per image class and record an exception for the backgrounds; there is no
exception, because applying the key to an image that contains no magenta is
provably a no-op. `index:16` is therefore the default for everything.

Keyed pixels are cleared to fully transparent black rather than left magenta
with zero alpha, so filtering and mipmapping cannot bleed the key colour back
into an edge.

## Running the extractor

```bash
dotnet run --project tools/Imperialism.AssetExtractor -- --report --archive "E:/Imperialism/Data/pictuniv.gob" --output assets/staging/report.json
```

```bash
dotnet run --project tools/Imperialism.AssetExtractor -- --contact-sheet --archive "E:/Imperialism/Data/pictuniv.gob" --output assets/staging/sheets
```

```bash
dotnet run --project tools/Imperialism.AssetExtractor -- --probe 2303.BMP --archive "E:/Imperialism/Data/pictuniv.gob"
```

```bash
dotnet run --project tools/Imperialism.AssetExtractor -- --manifest assets/manifest/imperialism-art.json --data-dir "E:/Imperialism/Data" --output src/Imperialism.Client/art
```

`--report` describes every image without extracting one: dimensions, palette,
the commonest indices, whether the outer ring is uniform, and how many key
pixels there are. `--contact-sheet` montages an archive with resource names
burned in — the browsing aid for cataloguing. `--probe` prints the runs of
colour along an image's edges and the bounding box of a flat interior field,
which is how nine-patch margins get **measured**: `2303.BMP` puts its cream
field at x 5..77 and y 5..18 of an 83x24 image, so its frame is five pixels on
every side and the theme says `5` because the artwork does.

`--manifest` extracts only what the manifest names. It writes nothing when a
file is already byte-identical, so re-running leaves timestamps alone and the
working tree clean. That is a requirement rather than a nicety: the PNG writer
pins its compression level and filters every scanline with None precisely so
that two runs cannot differ.

## The manifest

`assets/manifest/imperialism-art.json` is the record of what became what. Crops
and nine-patch margins live there rather than in a Godot inspector, so the theme
is generated from data and recutting a border after looking at it on a wide
screen is a one-line diff.

Each entry carries `evidence` — why we believe it is what we say — and
`confidence`, either `confirmed` (matched to a manual figure or measured) or
`inferred`. Nothing may be `absent`.

**The catalogue is human work and it is deliberately incomplete.** The archives
name images with bare numbers and nothing anywhere says what `10000.BMP`
depicts. Thirty-three pieces are named; 1,569 are not. Unnamed is unextracted is
uncommitted, and the way to name more is a contact sheet on one screen and
`E:/Imperialism/Imperialism - manual.pdf` on the other.

**One thing was looked for and not found.** The manual describes the workforce
icons on the Industry screen's left border as encoding grade by the colour of
the worker's coverall. The figures at 415-426 are map civilians standing on
terrain, not that set, so they are named `civilian_*` and the three coverall
icons remain unidentified. The status border shows grades as text until they turn
up.

## What is committed, and why that is a change

The repository's rule was that no extracted byte belongs in the tree. That rule
is now **narrowed, not abandoned**.

Still uncommitted: `.map`, `.scn` and `.inf` files, the `.gob` archives
themselves, the disassembly listing, every full-screen background, every map
tile, and every image the manifest does not name.

Committed: the thirty-three interface pieces and four typefaces the manifest
names, 282 KB in total, under `src/Imperialism.Client/art/` with their Godot
`.import` sidecars.

The reason for the split is that the two kinds of art fail differently. A map
drawn with procedural terrain colours is legible and always has been; that
fallback stays required. A window with no chrome is not a degraded interface but
an absent one, and a shell that only works for people who own the original and
have run a tool is a shell nobody sees.

Three things keep this honest rather than aspirational:

- `ArtManifestTests` walks `art/` and fails on any file the manifest does not
  name, so art cannot arrive without a recorded source.
- CI enforces a 10 MB ceiling on the directory. Raising it is a decision, and
  the workflow file is where that decision gets made.
- The extractor still ships without content: anyone with their own installation
  can regenerate every committed byte from the manifest.

**The fonts are a weaker position than the art and are flagged separately.**
`Antqua.ttf`, `Antquab.ttf`, `WeBeLt__.ttf` and `WeBeBd__.ttf` are third-party
typefaces the original bundled, not artwork its authors drew, so "a
non-commercial reimplementation reuses the original's own art" does not cover
them in the same way. If that becomes a problem the fallback is a metric-similar
open face — an old-style serif for the Antiqua and a condensed display face for
the WeBe — and it is a one-line change in the theme, because nothing else refers
to a font by name.
