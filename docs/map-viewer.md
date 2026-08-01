# Phase 1 map viewer

`Imperialism.Client` is the Godot 4.7.1 presentation shell for modern
`.iworld` packages. It never reads `.map`, `.scn`, or `.inf` files and has no
reference to the legacy codecs or importer. Convert legacy inputs separately,
then open the resulting modern package.

## Architecture

The viewer is split at an intentionally narrow engine boundary:

- `Imperialism.Presentation` is a plain .NET 8 library. It projects the
  pointy-top odd-row grid into map space, performs deterministic hit-testing,
  resolves immutable geography into `MapViewDefinition`, and captures current
  `WorldState` in `WorldViewState`. Its geometry and state mapping are
  xUnit-tested without Godot.
- `Imperialism.Client` owns the Godot nodes, rendering, camera input, and UI.
  It converts presentation points to `Vector2` only at this boundary.

Terrain and ownership each use one `MultiMeshInstance2D`, with one colored
instance per cell. A static custom `Node2D` draw surface renders derived
province/sea-zone borders, local river paths, settlements, and resources. A
dynamic surface owns current rails and capitals, while a tiny interaction
surface redraws only hover and selection. Ownership changes recolor the
existing ownership batch. State refreshes therefore do not rebuild terrain,
reset the camera, or disturb the selected cell. There is no scene node per hex,
so larger maps increase instance and snapshot data rather than scene-tree size.

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
| Probe state (debug mode) | Cycle sample ownership and rail state |

The scenario picker switches among every scenario embedded in one `.iworld`
package without rebuilding or reimporting its shared map. The debug state probe
is a development harness, not a gameplay command; it proves that mutable Core
state reaches the ownership and feature layers before the turn engine exists.

## Asset policy

The procedural palette and vector markers are the guaranteed fallback. Original
art may be extracted from a local installation into a gitignored cache and
shown with integer nearest-neighbor scaling, but missing assets must never stop
content loading, simulation, tests, or debug rendering. Hex geometry and input
remain independent of source sprite dimensions. Asset cleanup and replacement
art are intentionally deferred until gameplay systems need polish.

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
batches, applies and mutates `WorldState`, and checks center-point picking.
Build Debug first because that is the assembly configuration Godot launches by
default; a Release-only build can leave an older Debug DLL in the project cache:

```powershell
dotnet build src/Imperialism.Client/Imperialism.Client.csproj
godot --headless --path src/Imperialism.Client -- --smoke-test
```

CI restores and compiles the Godot project and runs all engine-independent
presentation tests. The Godot runtime smoke is a local gate because the engine
distribution is intentionally not downloaded into every CI job.

## Worthwhile follow-ups

- Define evidence-backed capital displacement or relocation behavior when a
  province changes owner. Core currently permits ownership and capital state to
  become temporarily inconsistent so a future turn phase can resolve it; the
  viewer reports current state without inventing a rule.
- Add change sets or events only if full `WorldViewState` reconstruction becomes
  measurable. The current linear pass is small at original and moderately
  expanded map sizes and keeps mutation APIs simple.
- Introduce the local asset extractor/cache when original graphics become useful
  for readability. The procedural fallback remains required afterward.
