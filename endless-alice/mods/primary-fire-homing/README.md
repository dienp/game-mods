# Primary Fire Homing

Left-click shots from Alice's Radish Machine Gun, Lea's Archery, and Mia's Burst Shot acquire a visible enemy near the direction you're aiming and home in flight. Shibako's lock-on marker shows which enemy the next shot will track. Locked shots fly at Wand Point Shot's speed of 3,000 Unreal units per second (about 30 m/s). The mod retains each attack's projectiles, damage, firing cadence, and effects. It applies to the local player in solo play and to all players in a modded co-op lobby. Firing continues normally when no target qualifies.

## Install

1. Close Endless Alice and install [UE4SS v4.0.0](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v4.0.0). Extract the regular `UE4SS_v4.0.0.zip` next to `EndlessAlice-Win64-Shipping.exe` under `EndlessAlice\Binaries\Win64`. The `dwmapi.dll` and `ue4ss` folder should both be beside the game executable.
2. Extract `PrimaryFireHoming.zip` into that `ue4ss` folder. Confirm that `ue4ss\Mods\PrimaryFireHoming\Scripts\main.lua` exists.
3. Launch the game and start a solo run or join a co-op lobby with the same mod installed on the host and guests. The UE4SS log will contain `[PrimaryFireHoming] Loaded` when the mod starts.

Edit `ue4ss\Mods\PrimaryFireHoming\Scripts\settings.lua` before launching to set `Enabled` or change `Strength` from 0 to 100 for each character. Set `EnableCoop = false` to restrict homing to solo play. Keep character settings in sync with the host for matching lock-on and projectile behavior. The host's settings control authoritative shots.

## Playtest

- Select Alice in Radish Machine Gun mode. With a visible enemy a little to the side of the crosshair, look for Shibako's lock-on marker before firing. Hold left click and watch the shots bend toward the marked enemy. Repeat with Lea's Archery and Mia's unscoped Burst Shot.
- With Lea's Powerful Penetration, line up two enemies and lock onto the closer one. Check that the arrow homes into the first enemy and continues through toward the second.
- Fire into an empty area, then toward an enemy behind cover. Shots should follow their normal trajectory when there is no eligible target.
- In co-op, host a lobby and invite a guest. Check that both players see a local lock-on marker and their eligible shots track targets.
- Try Alice's cannon and Mia's scoped shot to check their original behavior. Compare an eligible attack at strength 100 and 25, then with its toggle disabled.

## Build

Run these commands from the repository root.

```powershell
npx nx build endless-alice-primary-fire-homing
npx nx dev endless-alice-primary-fire-homing
```

The Release ZIP is in `bin/Release` under this project. The Debug target watches Lua source changes and writes to `bin/Debug/Mods` by default. To hot-reload in a development game, set `ENDLESS_ALICE_DEV_MODS` to the test loader's Mods directory before running the Debug target and set `EnableAutoReloadingLuaMods = 1` in that development loader's `UE4SS-settings.ini`.
