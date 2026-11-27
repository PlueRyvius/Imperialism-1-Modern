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
Private font copies keep AA changes local to Transport. Header/title fonts
retain the canvas's scale-dependent oversampling. Figures keep native 1x
rasterization and nearest filtering.

Remaining differences: the available Antiqua rasterizer leaves the `5` and `9`
five pixels wide rather than the specified three-pixel cores. Disabling hinting
lightens the figure while preserving height and the three-pixel foot of `1`;
it does not reproduce every original stroke. The row is one pixel narrower,
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
