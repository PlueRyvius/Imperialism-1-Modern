# Phase 1 map viewer

`Imperialism.Client` is the Godot 4.7.1 presentation shell for modern
`.iworld` packages. It never reads `.map`, `.scn`, or `.inf` files and has no
reference to the legacy codecs or importer. Convert legacy inputs separately,
then open the resulting modern package.

## Architecture

The viewer is split at an intentionally narrow engine boundary:

- `Imperialism.Presentation` is a plain .NET 8 library. It projects the
  pointy-top odd-row grid into map space, performs deterministic hit-testing,
  and resolves a compiled world into a read-only viewer snapshot. Its geometry
  is xUnit-tested without Godot.
- `Imperialism.Client` owns the Godot nodes, rendering, camera input, and UI.
  It converts presentation points to `Vector2` only at this boundary.

Terrain and ownership each use one `MultiMeshInstance2D`, with one colored
instance per cell. A static custom `Node2D` draw surface renders derived
province/sea-zone borders, local river paths, rail links, settlements,
capitals, and resources. A second tiny surface redraws only hover and selection,
so mouse motion never rebuilds whole-map overlays. There is no scene node per
hex, so larger maps increase instance and snapshot data rather than scene-tree
size.

River paths remain local cell geometry. Rendering an endpoint at a shared edge
does not assert that another cell continues the river.

## Modes and controls

Normal mode keeps ownership shading subtle and shows the useful map features.
`Debug overlays` strengthens ownership colors, draws individual cell edges,
and adds resource markers. The same viewer is therefore suitable for normal
play presentation and for later economy, transport, AI, and editor inspection.

| Input | Action |
|---|---|
| Mouse wheel | Zoom |
| Middle/right drag | Pan |
| Arrow keys | Pan |
| Home | Fit the whole map |
| Left click | Pin a cell in the inspector |
| Hover | Temporarily inspect a cell |

The scenario picker switches among every scenario embedded in one `.iworld`
package without rebuilding or reimporting its shared map.

## Run and verify

Install the .NET build of Godot 4.7.1 and .NET SDK 8.0.404, then open
`src/Imperialism.Client/project.godot` in Godot. The repository includes a
small synthetic, freely authored demonstration package and no original game
data.

From a console executable, launch the demo with:

```powershell
godot --path src/Imperialism.Client
```

Open an explicit package by placing arguments after Godot's `--` separator:

```powershell
godot --path src/Imperialism.Client -- --world C:\path\to\scenario.iworld
```

The runtime smoke gate initializes the real Godot scene, creates both render
batches, loads every package scenario, and checks center-point picking:

```powershell
godot --headless --path src/Imperialism.Client -- --smoke-test
```

CI restores and compiles the Godot project and runs all engine-independent
presentation tests. The Godot runtime smoke is a local gate because the engine
distribution is intentionally not downloaded into every CI job.
