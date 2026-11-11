# OFFSHORE — design and milestone plan

Single-player, Unity 6000.6.7f1 (URP, Input System only), placeholder low-poly art.

## The game in one paragraph

You own a small charter boat in a bounded stretch of open sea with one home port. Each trip you pick a
destination and target species, load bait and tackle, drive out, anchor or hold position over productive
water, and let the rod fish on its own while you watch the sea. The hold fills, conditions change, and you
decide when to head home to sell, repair and upgrade. Remote grounds hold better fish and bigger risks:
storms first, later sea creatures and pirates. All of them are announced well ahead and can be avoided by
reading the signs and moving in time.

*A peaceful day fishing that could turn into an adventure.*

## Main player decisions

| Decision | Trade-off |
|---|---|
| **Where to fish** | Near port: safe and modest. Remote: rarer species, longer trips, higher danger level |
| **What to target** | Species vary by ground, depth, bottom type and weather, so the bait and method choice follows from this |
| **Method, bait and tackle** | Wrong bait catches mostly bycatch. Light tackle can't hold big fish, and heavy tackle scares small ones |
| **Stay or move** | Grounds deplete gently and conditions drift. Is a better spot worth the travel time? |
| **Hold space** | Keep fishing for a trophy, or fill up on reliable fish and leave? |
| **When to leave** | Leaving early on the first storm warning is always safe; staying longer is a gamble |
| **What to buy** | Every upgrade unlocks a capability (more range, deeper water, storm tolerance, bigger fish), not just a percentage bonus |

## Relaxed fishing vs. avoidable danger

The split is by place. **Safe waters allow unattended fishing; dangerous waters require you to respond to
telegraphed warnings.** The game never escapes for you and never makes an idle boat immune. Leaving the
game running unattended in dangerous water is a risk you choose, and you are warned before you choose it.

1. **Danger is a property of the place, shown before you go.** Each ground has a published danger level
   (map, buoys, water color). Water near port is level 0 and never spawns major threats, so a player who
   wants pure relaxation can fish there unattended indefinitely at a modest income.
2. **A danger director, not random rolls.** One director starts a threat only when the current ground
   allows it and the recovery cooldown since the last threat has passed. There is at most one major
   threat at a time.
3. **Long, layered telegraphs.** Every threat moves through *Forecast → Visible → Imminent → Active →
   Passing*. Forecast and Visible together last at least about 60–90 s of game time, and each stage has a
   visual and an audio cue plus a HUD warning line.
4. **Responding is always possible and always quick, but it is the player's job.** Any helm input
   (throttle, steering, raising anchor) stops fishing instantly: the fish on the line is lost, the bait is
   kept. At *Imminent* the warning escalates (louder audio, flashing HUD line). Auto-fishing may stop
   casting new lines at that point, but that only stops the catch. It does not move the boat or reduce
   damage: an anchored, unattended boat in a storm's path still takes the damage.
5. **Pause freezes everything.** Fishing, the danger director and weather all run on game time; there is no
   offline progress.
6. **Failure is a setback, not a reset.** Hull at 0 → rescue tow to port: you lose part of the catch in
   the hold and pay a towing fee (never below zero money), and some equipment may be damaged. Skills,
   unlocks, the collection log and upgrades are always kept.

## Biggest scope risks and what to defer

| Risk | Mitigation |
|---|---|
| Water and visuals become a rabbit hole | One cheap custom shader (GPU sine swells, faceted shading, vertex-color shallows, crest foam) plus trail wakes. No buoyancy, no FFT water |
| UI volume (shop, hold, log, map, skills) | One screen per concern, built from code with plain uGUI. Map and log stay plain lists until the loop is fun |
| Content explosion (species, gear, grounds) | ScriptableObjects from the start, but only about 6 species, 3 grounds and about 6 upgrades in the first loop |
| Danger AI (pirate pursuit, creature behavior) | Storm first: a moving zone with a forecast path. Creatures and pirates only after the storm proves the telegraph rules |
| Economy balance | One currency, a few prices, tuned through play tests. Deferred: fuel, spoilage, hunger, maintenance |
| Save/load retrofitted late | Keep state in a few plain data classes from milestone 2 on. Implement save/load after the loop works |
| Exploits (circle-driving XP, idle farming) | XP comes from outcomes (fish landed, grounds discovered, storms escaped), never from raw distance or time |

**Deferred until the first loop is fun:** trolling, bottom fishing, traps, sea creatures, pirates, charter
customers, day/night cycle beyond a simple tint, fuel, spoilage, crew, more than one port, save slots.

## Skills (proposal)

- **Angling**, the first working skill. It grows from fish landed, weighted by difficulty, with
  diminishing returns for the same species at the same ground. It unlocks capabilities: estimating fish
  size on the bite, handling heavier line classes, and a short "assist" window that helps land an
  exceptional catch. It also gives a small time-per-fish improvement.
- **Seamanship** grows from completing voyages (port → ground → port legs) and from escaping storms
  without major damage, never from distance driven. It unlocks safer handling in rough water, field
  repairs at sea and better wave timing.
- **Navigation** grows from discovering grounds and reading forecasts that turn out to be right. It
  unlocks ground details on the map (species hints, depth), more precise storm paths and earlier warnings.
- **Method specializations** (Trolling, Bottom fishing, Traps) are added only when those methods are
  playable.

## Milestones

Each milestone ends with a playable scene, PlayMode tests and a manual play-test checklist.

### M1 — Boat, sea and camera *(implemented in this session)*
A playable scene with a controllable boat, stylized ocean, port, islands, buoys and a high-angle camera.
- W/S moves a persistent throttle lever (ahead/stop/astern, with a stop detent). A/D steers; steering
  needs speed, but some low-speed authority keeps it forgiving. Momentum and sideways drift come from
  separate forward and lateral damping.
- Space drops or raises the anchor: the boat stops and holds position, and throttle is locked while anchored.
- Orthographic camera at a fixed 45° yaw and 50° pitch. It follows smoothly with look-ahead in the
  direction of travel, zooms with the mouse wheel or +/− within limits, and never rotates with the boat.
- Water: GPU sine swells with faceted low-poly shading, turquoise shallows and shore foam around land,
  foam on crests and wake trails behind the boat. The boat and buoys bob visually from the same wave function.
- Bounded 600 × 600 m map, a harbor with breakwaters, a pier and a lighthouse, a few islands, a reef patch
  and channel buoys.
- HUD: speed in knots, throttle lever, heading, anchor state. Esc pauses (time frozen).

**Acceptance:** drive out of the harbor without fighting the controls; turning feels weighty but forgiving;
anchoring stops the boat within a few boat lengths; the camera never rotates and shows enough water ahead;
zoom stays within limits; pausing freezes the boat and waves; PlayMode tests pass.

### M2 — Automatic rod fishing *(implemented)*
- Three `FishingGround` assets, each placed on the map by a `FishingGroundZone` and ringed with marker buoys
  in its color:
  - Harbor Flats: safe, sandy shallows.
  - Gull Reef: moderate, turquoise reef.
  - Deep Drop: high, open deep water.

  Each has its own catch table (chance and size band per species) and bite delay.
- Six `FishSpecies` assets (Mackerel, Flounder, Sea Bass, Red Snapper, Mahi-mahi, Yellowfin Tuna) with
  weight range, length and reel time. `FishingSettings` holds the shared timings. All of these are
  Inspector-tunable and are created once under `Data/`.
- Rod fishing needs the boat **anchored and stopped over a fishing ground**. Press F to start. It then
  repeats cast → wait → bite → reel → landed on its own, with no reflex input.
- Fishing stops at once on F, on any helm input (W/S/A/D) or when the anchor is raised. Stopping never
  changes the anchor, and a fish on the line is lost.
- Every refusal or stop is explained on screen (no anchor, still moving, no ground, hold full, no room for
  this fish).
- Hold: 80 kg by weight. A fish that doesn't fit is released and fishing stops. Tab shows the contents;
  in development builds F9 empties the hold (DEV ONLY, replaced by selling in M3).
- Deferred from the original plan: the `RodSetup` (bait and tackle choice) moves to M3 with the shop, and
  skill effects on timing move to M4. The danger rating is shown per ground but has no effect until M5.

**Acceptance:** each ground produces a visibly different catch mix; any helm input stops fishing at once;
when the hold is full, fishing stops with a clear message.

### M3 — Port, money and upgrades
- Dock zone in the harbor: sell the hold, repair the hull, buy bait.
- Money, a hull condition (collisions damage it), and 4–6 upgrades: Better rod (heavier fish),
  Bigger hold, Engine (top speed), Reinforced hull, Fish finder (shows ground info).

**Acceptance:** a full short trip works: buy bait, fish, return, sell, buy one upgrade, and feel the difference.

### M4 — Angling skill and collection log
- Angling XP and levels with the unlocks above, shown on the HUD.
- Collection log: each species with its largest catch and where and how it was first caught.

**Acceptance:** XP comes only from landed fish, with diminishing returns visible; the log fills across a trip.

### M5 — Storm event
- Danger director with recovery cooldown and ground danger levels.
- Storm: radio forecast with a projected path drawn on the sea → darkening light, wind, distant thunder and
  lightning → a moving storm zone (rain, rough water, slower steering, hull damage over time, no fishing
  inside) → it passes → calm.
- Rescue tow at hull 0.

**Acceptance:** a storm is always announced well ahead; leaving its path avoids all damage; being caught
costs hull and catch but never progress; no second storm during the cooldown.

### M6 — Life and persistence
- Birds over feeding fish, surface splashes, floating debris, audio pass (engine, waves, gulls, warnings).
- Save/load for money, upgrades, skills, the log and the boat in port.

### Later
Trolling (speed band), bottom fishing (depth and stationary), traps (deploy and return), sea creatures,
pirates, charter customers.

## Architecture notes

- `Boat/` movement and visuals, `Sea/` water and floating props, `CameraRig/`, `Input/`, `UI/`, `Core/`.
  Future folders: `Fishing/`, `Inventory/`, `Progression/`, `Danger/`, `Port/`.
- Boat physics runs in `FixedUpdate` on a constrained Rigidbody; lever, camera and visuals use `deltaTime`
  damping, so movement doesn't depend on frame rate.
- Scenes are generated by `OFFSHORE > Build Sea Scene` (also batch `-executeMethod
  Offshore.EditorTools.WorldBuilder.BuildFromCommandLine`). Materials and meshes are kept between rebuilds.
- All tuning values are serialized fields with tooltips. Game data (fish, gear, grounds, upgrades) will be
  ScriptableObjects.
