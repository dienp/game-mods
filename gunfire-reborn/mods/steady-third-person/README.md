# Steady Third Person

Press **F8** during normal play to switch between first person and an over-the-shoulder view. The plugin uses the game's third-person camera and blends between views. The optional level-horizon setting counters camera roll while keeping your aiming direction. The game still controls HUD animation, positional shake, and scripted camera changes.

## Build

Use the .NET 8 SDK. Run the Nx target from the monorepo root. The plugin targets .NET 6 for BepInEx and restores its build dependencies automatically.

```powershell
npx nx build gunfire-reborn-steady-third-person
```

The plugin DLL is `bin/Release/net6.0/SteadyThirdPerson.dll`.

## Run

Duoyi's Steam terms require written consent for game modifications. On an approved install with a working BepInEx 6 IL2CPP x64 setup, copy `SteadyThirdPerson.dll` into `BepInEx/plugins`. After launch, check `BepInEx/LogOutput.log` for the plugin's ready message. If it reports that camera controls are unavailable, the logged name identifies the missing game type or member.

Start a solo run, switch views with **F8**, and try walking, turning, firing, and dashing. Press **F8** again to return to first person. Check how it feels in a narrow corridor as well as an open room.

`BepInEx/config/gunfire.steadythirdperson.cfg` contains `ToggleKey`, `TransitionSeconds` (default `0.35`), and `LevelHorizon` (default `true`). Restart the game after editing the config. Set `LevelHorizon` to `false` to try the native third-person motion with the same transition speed.

The Steam install examined during development was build `26448669`. Runtime compatibility remains unverified on that build.
