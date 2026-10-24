# Ironfront

Operational WW2 wargame in Unity. Real battalions, generals on the map, NATO counters, frontline and battle-plan tools, real-time with pause.

First playable slice: **France, 10 May – 25 June 1940** (Fall Gelb / Fall Rot).

- Design: [docs/DESIGN.md](docs/DESIGN.md)
- Roadmap: [docs/ROADMAP.md](docs/ROADMAP.md)

## Open it
Unity **6000.6.5f1** (same as Medieval Town). Open the folder in Unity Hub, then press Play. The scene `Assets/Scenes/Main.unity` is created on first open and the game boots from code.

## Layout
- `Assets/Scripts/Sim`: simulation core, plain C# with no UnityEngine references
- `Assets/Scripts/Game`: Unity view layer (camera, map rendering, HUD)
- `Assets/StreamingAssets/Maps/France1940`: generated map data (`map.json`, `terrain.png`)
- `tools/build_map.py`: regenerates the map from Natural Earth data, run with `python tools/build_map.py <geojson-dir>` (needs shapely and pillow)

## Controls
Pan with WASD, the arrow keys, or right/middle-drag; hold Shift to pan faster. Zoom with the wheel or +/−. Space pauses, 1–5 sets speed. T and P switch between terrain and political modes. G toggles the grid, H returns home, F1 shows help.

## Map data
Coast, borders, lakes and major rivers come from [Natural Earth](https://www.naturalearthdata.com/) (public domain). These are hand-entered and approximate until the real-data pass: the Albert Canal, Dyle, Sambre, Oise, Lys, Semois, the forest and hill regions, and the city list.
