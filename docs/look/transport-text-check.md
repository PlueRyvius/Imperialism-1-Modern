# Transport text check: D13–D18

These checks compare the original's measurements in [transport-layout.md](https://github.com/PlueRyvius/Imperialism-1-Modern/blob/5c68ec69b974a5fd63cfb5d63f5089b73e887ffd/docs/look/transport-layout.md)
with the existing client fonts. They do **not** establish Windows screenshot acceptance.
No game values, Core dates, original assets or transport allocation rules change.

## Native font measurements

Measured on the server with Godot 4.6.3 Mono's headless CPU font rasterizer:
original TTF bytes, antialiasing off, subpixel positioning off, native 1x glyph
atlas alpha counted as binary ink. Text shaping supplies label advances; figure
ink boxes use two page pixels between digits and six beside the slash. This
measures glyphs, not the final GPU-rendered 1920x1080 frame or its shadows.

| Sample | Original target | Measured native font result |
| --- | --- | --- |
| `15 / 19` | 38×9; about 48 dark pixels | 37×9; 55 black pixels (previous hinted font: 61) |
| `119 / 120` | 48×10 | 49×9; 79 black pixels |
| Digits / slash | 7 px / 9 px tall | 7 px / 9 px tall; slash 5 px wide |
| `Spring, 1882` | 73×10 | 72×10 |
| `$10,000` | 43×9 | 44×9 |
| `Give Transport Orders` | 135 px wide | 134×10 |
| `(current screen)` | 95 px wide | 95×9 |
| `Transport` | 86×15; 244 core pixels | 87×15; 242 core pixels |
| `Ledger` | 59×15; 158 core pixels | 59×15; 162 core pixels |

The title face is the existing Belwe Light (`display.ttf`), at 17 px with
2 px glyph spacing; the left title uses the font's 0.4 embolden variation.
The header uses the existing Antiqua at 10 px with explicit glyph/word spacing.
Private font copies keep AA changes local to Transport. Header labels retain
the canvas's scale-dependent oversampling. Titles and figures now copy native
1x atlas ink into image textures, enlarged with nearest filtering.

Remaining differences: the available Antiqua rasterizer leaves the `5` and `9`
five pixels wide rather than the specified three-pixel cores. Disabling hinting
lightens the figure and preserves height, but removes the foot of `1`. The
earlier report that the unhinted atlas retained a three-pixel foot was wrong;
a fresh native-atlas check found a one-pixel stem at the baseline. It does not
reproduce every original stroke. The row is one pixel narrower,
capacity is one pixel wider and one pixel shorter, the date is one pixel narrower,
and the treasury is one pixel wider. Title ink counts are close, not exact. A native-width left title
would begin one pixel left of its target when its right edge matches.
These differences are recorded rather than replacing the original glyphs with
invented pixel art. Font-cache measurements also cannot prove final scaled
label placement; the ETHERCAT screenshot remains required.

## Functional verification

The Transport smoke checks idle text, hover replacement of both lines, restoration
on exit and the black (+1,+1) header shadow. It retains the existing checks for
2x centred geometry, row placement, one-unit arrow edits, drag endpoints,
unchanged opening orders and return to the Terrain Map.

The Presentation theory checks all four quarter names and that Core's
`TurnDate.ToString()` remains `year Qn`.

## Round 3: preserving the native raster at 2x

The Windows round-3 frame at
`1e31ae9be1368cd4aff7323289f56356f6149ccf` did not reproduce the
native atlas measurements: title counts were 276/120 and the figure had 59
dark pixels with no foot on `1`. The title font explicitly changed its
oversampling to the canvas scale; figures still called `DrawString` inside
the scaled canvas. Thus the measured atlas was not the final drawn input.
The precise renderer/cache choice behind the figure discrepancy was not
isolated on Windows.

`LedgerRasterText` now copies binary ink from the existing font's native atlas
into an RGBA image and uploads that image as an `ImageTexture`. Titles use the
same shaped glyph advances, existing font variation and label baseline; figures
use the same ink alignment, baseline and spacing. Both draw only the texture at
an integer origin with nearest filtering, at its native size. No font draw call
remains in either draw path, and title oversampling stays at 1 when resized.
The header labels are unchanged.

The Transport smoke checks the actual native images supplied to the texture
upload: title ink counts 242/162, figure `15 / 19` count 55, and reports whether the first `1` has a three-pixel foot. The foot is absent
in the unhinted source raster and is not claimed as passing. It also checks native texture dimensions, nearest
filtering, integer global origins and exact scale axes (2,0)/(0,2) in the
1920x1080 run. Those conditions give each source pixel a 2x2 destination block;
they are a CPU-data and transform check, not a GPU screenshot comparison.

Final frame acceptance remains a Windows re-shoot. The native font's 55 pixels
still differ from the original's approximately 48, and the native title counts
242/162 still differ from 244/158. This fix preserves the native raster rather
than inventing glyph strokes to remove those documented differences.

### Native foot discrepancy

A fresh CPU atlas check of both the imported font copy and a new FontFile made
from the existing Antiqua bytes gives the following at 10 px, AA/subpixel off,
oversampling 1. Counts are for `15 / 19`:

| Native hinting | Black pixels | Baseline of `1` |
| --- | --- | --- |
| None | 55 | one-pixel stem, no foot |
| Light | 55 | one-pixel stem, no foot |
| Normal | 61 | three-pixel foot |

Forcing the autohinter gives 58 pixels with no foot under Normal hinting.
The existing unhinted setting is retained to preserve the requested native
55-pixel raster. The requested combination of 55 pixels and a foot could not be
reproduced with these native settings. This is an unmatched criterion, rather
than evidence of an additional scaling error. No glyph strokes were added.

Round-3 verification on the server: the final Transport smoke reports
`TRANSPORT_RASTER_OK titles=242/162 figure=55 foot=False nativePixels=True scale=2`.
All five headless smoke runs (Transport, viewer, eight-screen navigation,
turn/newspaper/report, orders) exited 0 with no errors or warnings. The full
solution suite passed 1,261 tests, failed 0 and skipped 11, including all seven
art-manifest tests. Logs for this round are server-only in this worktree's
`fixtures/local_only/raster-r3/`; no GPU frame or original screenshot is committed.
