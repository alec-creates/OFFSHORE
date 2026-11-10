# OFFSHORE

A single-player fishing game: run your own small charter boat on the open sea, fish semi-idle, sell your
catch, upgrade, and read the weather before it turns into an adventure.

- Unity **6000.6.7f1** (URP, Input System only)
- Design and milestone plan: [DESIGN.md](DESIGN.md)

## Getting started

1. Open this folder in Unity Hub with editor 6000.6.7f1.
2. Open `Assets/_Project/Scenes/Sea.unity` and press Play.

Controls: W/S throttle lever · A/D steer · Space anchor · mouse wheel or +/- zoom · Esc pause.

## Editor tools

- **OFFSHORE > Build Sea Scene** regenerates the scene from code (`WorldBuilder.cs`). Materials and meshes
  are kept between rebuilds; the previous scene is backed up to `SceneBackups/`.
- **OFFSHORE > Capture Preview Images** renders the scene camera to `Previews/*.png`.
- PlayMode tests: Window > General > Test Runner > PlayMode.
