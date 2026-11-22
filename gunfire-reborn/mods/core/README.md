# Mods Core

Mods Core adds a shared **Settings → Mods** tab and gives Gunfire Reborn plugins a small API for registering settings. It integrates with the game's tab strip and reuses native On/Off rows, sliders, and the help pane. Values live in each mod's BepInEx config file.

## Check the baseline

Use the current public Steam build for live verification. Record the game and loader build numbers with the result.

On November 22, 2026, Steam build `26448669` passed the live check with stock BepInEx `6.0.0-be.791`, matching prebuilt `global-metadata.dat`, and Mods Core as the only installed plugin. The Mods tab, native tab arrows, both On/Off options, slider and help pane worked with mouse input. The slider moved from 50 to 75 and kept its value across a game restart. Steam launched the game normally with mod detection in place.

The unmodified Steam install lacked the metadata file expected by the interop generators. Stock BepInEx `6.0.0-be.791` and `6.0.0-be.793`, and MelonLoader `0.7.3`, all reached that missing path when tested on the unmodified build. Recheck loader compatibility and use metadata matched to the installed build after a Steam update.

Run the game with BepInEx once so it generates the assemblies in `BepInEx/interop`. Set `GUNFIRE_GAME_DIR` to your Steam install if it differs from the workspace default. Then build from the monorepo root.

```powershell
npx nx build gunfire-reborn-core
```

The DLL is `gunfire-reborn/mods/core/bin/Release/net6.0/GunfireReborn.Mods.Core.dll`. Place it in `BepInEx/plugins`.

1. Launch the game and check the BepInEx log for `Loader check passed: Mods Core started.` This confirms the loader ran our plugin.
2. Open **Settings → Mods → Mods Core**. Look for **Settings probe** and **Probe intensity**. Focus each row to check the help pane.
3. Turn **Settings probe** on, then set **Probe intensity** to 75. The log should show `Settings probe on; intensity 75%.` Turn the probe off and confirm the off message.
4. Restart the game and confirm the chosen values persist. The probe writes to the log and leaves gameplay state alone.

If a Steam update changes the settings panel, check the BepInEx log for a Mods Core compatibility message. The probe settings remain available in `BepInEx/config/gunfire.mods.core.cfg`.

## Register settings from another mod

Add a project reference to `GunfireReborn.Mods.Core.csproj` and declare `[BepInDependency(GunfireReborn.Mods.Core.Plugin.Id)]` on the consumer plugin. In the consumer's `Load` method, register its settings once and read their `Value` properties in the feature code.

```csharp
var settings = new GunfireReborn.Mods.Core.ModsSettings(Config, "Steady Third Person");
var enabled = settings.Toggle("Enabled", "Third-person camera", "Switch to the chase camera.");
var fieldOfView = settings.Slider("FieldOfView", "Field of view", "Width of the chase view.", 95, 60, 130);
```

## Develop the probe with hot reload

Set `GUNFIRE_MODS_CORE_DEBUG_DLL` in the game process environment to the absolute path of the workspace DLL under `bin/Debug/net6.0`. Run `npx nx run gunfire-reborn-core:dev` from the monorepo root and install a copy of its Debug DLL as the loader plugin. Keep the installed copy and workspace DLL in separate locations.

Edit `ProbeReporter.cs` while the watcher is running. The next probe interaction loads the new callback from the rebuilt workspace DLL. Restart the game for changes to the stable loader, shared API, and tab registration. The Release DLL excludes the reload code.
