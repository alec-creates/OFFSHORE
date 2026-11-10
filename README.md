# OFFSHORE

A single-player fishing game: run your own small charter boat on the open sea, fish semi-idle, sell your
catch, upgrade, and read the weather before it turns into an adventure. Design and milestone plan:
[DESIGN.md](DESIGN.md).

## Requirements

- Unity **6000.6.7f1** (URP, Input System only). No paid packages.

## Open and play

1. In Unity Hub: **Add project from disk** → this folder → open with 6000.6.7f1. The first open imports
   for a few minutes because `Library/` is not in git.
2. Open `Assets/_Project/Scenes/Sea.unity` and press **Play**. Click the Game view so it has keyboard focus.

### Controls

| Input | Action |
|---|---|
| W / S (or ↑ / ↓) | Move the throttle lever ahead / astern. It stays where you leave it, and pauses briefly at STOP when you sweep through it |
| A / D (or ← / →) | Steer (much stronger with speed) |
| Space | Drop / raise anchor (throttle is locked while anchored) |
| Mouse wheel, + / − | Zoom |
| Esc or P | Pause / resume |

## Editor tools

- **OFFSHORE > Build Sea Scene** regenerates `Sea.unity` from code (`Scripts/Editor/WorldBuilder.cs`). The
  previous scene is backed up to `SceneBackups/`. Materials and meshes are created once and reused, so
  material tweaks survive a rebuild. Delete a material to pick up new defaults.
  Batch: `-executeMethod Offshore.EditorTools.WorldBuilder.BuildFromCommandLine`
- **OFFSHORE > Capture Preview Images** renders the scene camera to `Previews/*.png` (git-ignored). These are
  edit-mode renders: waves are frozen and wakes are off.
  Batch: `-executeMethod Offshore.EditorTools.PreviewCapture.CaptureFromCommandLine`
- **Tests:** Window > General > Test Runner > PlayMode (`Assets/_Project/Tests`).

## Milestone status

**M1 — boat, ocean and high-angle camera: implemented, awaiting manual play test.**

What has been checked:
- **Automated:** 11 PlayMode tests pass in batch mode. They cover the throttle lever and STOP detent,
  acceleration and top speed, reverse, speed-dependent steering, the anchor stopping and holding the boat,
  map bounds, camera fixed angle, follow and look-ahead, zoom limits, pause freezing the boat and waves,
  and wave height and harbor shallows.
- **Rendered images:** edit-mode preview captures of the boat, harbor and an island were inspected. Water
  colors, shore foam, the low-poly look and readability from the camera angle look right.

Still needs manual verification in the editor:
- Boat handling feel: acceleration, turning, drift, how responsive the lever is.
- Moving wakes: they are only visible in Play mode and were not captured.
- HUD layout and readability at real screen sizes.
- Waves animating, and the boat and buoys bobbing, in motion.

Next: **M2 — automatic rod fishing** (not started).
