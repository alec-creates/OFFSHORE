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
| F | Start / stop automatic fishing (anchored and stopped, over a fishing ground) |
| Tab | Show / hide the hold contents |
| F9 | **DEV ONLY** (editor and development builds): empty the hold |
| Mouse wheel, + / − | Zoom |
| Esc or P | Pause / resume |

## Editor tools

- **OFFSHORE > Build Sea Scene** regenerates `Sea.unity` from code (`Scripts/Editor/WorldBuilder.cs`). The
  previous scene is backed up to `SceneBackups/`. Materials and meshes are created once and reused, so
  material tweaks survive a rebuild. Delete a material to pick up new defaults.
  Batch: `-executeMethod Offshore.EditorTools.WorldBuilder.BuildFromCommandLine`
- **OFFSHORE > Capture Preview Images** renders the scene camera to `Previews/*.png` (git-ignored): the
  harbor, the fishing grounds, the boat with its rod rigged, and a HUD layout shot with sample text. These
  are edit-mode renders: waves are frozen, wakes are off and the HUD isn't live.
  Batch: `-executeMethod Offshore.EditorTools.PreviewCapture.CaptureFromCommandLine`
- **Fishing data** lives in `Assets/_Project/Data/`: `Species/`, `Grounds/` and `Fishing Settings`. The
  builder creates these once and never overwrites them, so tune them in the Inspector. A ground's position
  and radius are set in `WorldBuilder.GroundLayout`.
- **Tests:** Window > General > Test Runner > PlayMode (`Assets/_Project/Tests`).

## Milestone status

**M1 — boat, ocean and high-angle camera: implemented and play-tested by the developer.**

**M2 — automatic rod fishing: implemented, awaiting manual play test.**

What has been checked for M2:
- **Automated:** 12 PlayMode tests pass in batch mode (23 in total including M1):
  - Eligibility: open water, no anchor, still moving, full hold.
  - The automatic cycle passing through every state and storing only that ground's species.
  - Distinct catch mixes and size progression across the three grounds, with six species in total.
  - An explicit stop losing the hooked fish and keeping the anchor down.
  - Throttle and steering stopping fishing with the anchor kept down.
  - Raising the anchor stopping fishing.
  - A small hold filling up and stopping with a reason.
  - The dev empty-hold.
  - Pause freezing fishing timers and catches.
- **Rendered images:** the grounds' marker rings and water, the rod, line and bobber on the boat, and a HUD
  layout shot with sample text were inspected.

Still needs manual verification for M2: the F/Tab/F9 keys in play, the bobber and rod animating through a
real cycle, the landed-fish display, and live HUD text and readability.

What has been checked for M1:
- **Automated:** 11 PlayMode tests pass in batch mode. They cover the throttle lever and STOP detent,
  acceleration and top speed, reverse, speed-dependent steering, the anchor stopping and holding the boat,
  map bounds, camera fixed angle, follow and look-ahead, zoom limits, pause freezing the boat and waves,
  and wave height and harbor shallows.
- **Rendered images:** edit-mode preview captures of the boat, harbor and an island were inspected. Water
  colors, shore foam, the low-poly look and readability from the camera angle look right.

M1's manual items (handling, wakes, HUD, motion) were play-tested and approved by the developer.

Next: **M3 — port, money and upgrades** (not started).
