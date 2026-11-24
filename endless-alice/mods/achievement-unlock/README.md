# Achievement Unlock

Adds **Unlock All** to the Gallery's **Achievements** page. The action completes each missing achievement through the game's local achievement system and shows the result on the button. Use the in-game Gallery count to check completion and Steam to check platform sync.

## Use

Install [UE4SS v4.0.0](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v4.0.0) beside `EndlessAlice-Win64-Shipping.exe`. Extract `AchievementUnlock.zip` into its `ue4ss` folder. In Endless Alice, open **Gallery → Achievements** and select **Unlock All**.

The installed `Scripts/settings.lua` has an `Enabled` toggle and `BatchSize` pacing setting. Every activation processes all remaining batches.

## Build

From the workspace root:

```powershell
npx nx build endless-alice-achievement-unlock
npx nx dev endless-alice-achievement-unlock
```

The Release ZIP is in `bin/Release` under this project. The Debug target watches source files and writes to `bin/Debug/Mods` by default. To develop against a test installation, point `ENDLESS_ALICE_DEV_MODS` at that loader's Mods directory and enable Lua auto-reload in its UE4SS settings.
