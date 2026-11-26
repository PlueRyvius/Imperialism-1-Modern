# The original art, all of it

Every bitmap in the 1997 game's seven picture archives, 2,241 files, extracted
**unaltered**: no crop, no transparency, every pixel the palette colour the
archive stores. The owner decided on 2026-11-26 to commit the full set, so that
any screen can be rebuilt from the original's own art and any piece can be
touched up from its source.

| Folder | Archive | Bitmaps | What it holds |
|---|---|---|---|
| `pictuniv/` | `pictuniv.gob` | 1,098 | Screen backgrounds, map tiles, buildings, units, chrome, icons |
| `pictpaid/` | `pictpaid.gob` | 259 | Portraits, dialog and event art |
| `pictenu/` | `pictenu.gob` | 32 | English art with text baked in (buttons, title screen) |
| `pictwv0/`–`pictwv3/` | `pictwv0.gob`–`pictwv3.gob` | 213 each | The four world-view tile sets |

Files are named by resource, so `pictuniv/1021.png` is `1021.BMP` in
`pictuniv.gob`. Three resources keep the shipped game's own odd names
(`COPYOF~1`, `COPYOF~2`, `COPY(2~1`).

Palette index 16, magenta `FF00FF`, is the original's transparency key
(`docs/asset-pipeline.md`). It is left visible here on purpose. The interface
pieces the client draws are produced from the same archives by
`assets/manifest/imperialism-art.json`, which crops them and applies the key.

## Rebuilding it

`manifest.json` here names every file. From a local install:

```
dotnet run --project tools/Imperialism.AssetExtractor -- --manifest assets/original/manifest.json --data-dir <install>/Data --output assets/original
```

The result is byte for byte what is committed. Each archive's SHA-256 is in the
manifest's `source` list.

## Touching up

Leave these files as they are: they are the record of what the original
shipped. A touched-up piece is a new file beside the client's art, with its
source named here, so the original can always be compared against it.

The archives themselves (`.gob`), the music, the videos, the maps and the
scenarios stay out of the tree.
