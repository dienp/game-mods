# Steady Third Person

Press **F8** during normal play to switch between first person and Close Shoulder. In **Settings → Mods → Steady Third Person**, use **Third-person camera** to turn the view on or off. **Shoulder offset** moves the camera sideways. **Close Shoulder distance** sets the camera arm length from 35% to 100% of the game's native third-person distance, with a default of 65%. **Close Shoulder FOV** starts at 85 degrees. You can also adjust **Transition time**, **Camera height**, **Level horizon**, and **Roll reduction**.

## Build

Use the .NET 8 SDK and the current game's generated BepInEx interop assemblies. Set `GUNFIRE_GAME_DIR` if your Steam install differs from the workspace default. Run the Nx target from the monorepo root.

```powershell
npx nx build gunfire-reborn-steady-third-person
```

The plugin DLL is `bin/Release/net6.0/SteadyThirdPerson.dll`.

## Run

Duoyi's Steam terms require written consent for game modifications. On an approved install with a working BepInEx 6 IL2CPP x64 setup, install [Mods Core](../core/README.md) and copy `SteadyThirdPerson.dll` into `BepInEx/plugins`. After launch, check `BepInEx/LogOutput.log` for the plugin's ready message.

Start a solo run and switch to Close Shoulder with **F8**. Try it in an open area and next to a wall. Adjust the shoulder offset and check that the center crosshair remains clear. Hold aim on one landmark while moving left, right, and backward. Try firing, reloading, and switching weapons.

Close Shoulder can look almost straight up and down. Aim toward the ceiling and floor, then switch back with **F8** to check that first person uses its normal range.

If the BepInEx console title starts with **Select** and the game stalls, focus the console and press **Esc** to clear its selection. To hide the console on future starts, set `Enabled = false` under `[Logging.Console]` in `BepInEx/config/BepInEx.cfg`. Leave `[Logging.Disk]` enabled for `LogOutput.log`.

On Steam build `26448669`, the player confirmed the third-person view rendered. The debug log recorded the signed animation inputs for strafing left and walking backward. Close Shoulder uses the native third-person weapon prefab when available and forwards firing and reload signals to animation parameters supported by the avatar or weapon. In-game weapon visibility and animation are part of the solo test above.

## Develop with hot reload

Install a copy of the Debug plugin and set `ReloadAssembly` in the `[Development]` section of its config to the absolute workspace path of `bin/Debug/net6.0/SteadyThirdPerson.dll`. Run `npx nx run gunfire-reborn-steady-third-person:dev` from the monorepo root. Changes to camera placement, field of view, and roll in `CameraFraming.cs` reload during third-person play. Camera hooks and settings registration load at startup. The Release build excludes the reload code.

During a solo run with the Debug build, press **F9** to start recording. Keep the mouse still and press **W**, **A**, **S**, and **D** one at a time in an open area to check camera movement. To check aim, stand still, pick a mark on a flat wall, and fire a few spaced single shots followed by a burst. Switch views with **F8**, let the camera settle, and repeat. Press **F9** again to save. Recording also stops after three minutes.

Find the paired `movement-*.csv` and `shots-*.csv` in `BepInEx/diagnostics/steady-third-person`. The movement file stores input and character and camera poses. The shot file stores native and applied spread angles and accepted surface and target impacts from the native hitscan, projectile, and beam paths. A matched impact includes pixel offsets from the crosshair at the time of aiming. Positive X is right and positive Y is up. The Release build excludes both recordings.

Press **F7** in the Debug build to start or save a peer ping trace from a room or run. The `peer-ping-*.csv` has UTC timestamps, anonymous peer numbers within each capture, and the ping values the local game measures through its gameplay and Steam room callbacks. Negative callback values appear as status codes. Set `PeerPingTraceKey` or `PeerPingTraceSeconds` under `[Development]` to change the key or the three-minute recording limit. Installing the callbacks requires a restart. The Release build excludes them.
