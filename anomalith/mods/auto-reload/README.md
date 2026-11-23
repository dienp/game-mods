# ANOMALITH Auto Reload

Auto Reload starts the normal reload when your active firearm runs empty and
the game allows it. Reloads use the game's animation and ammo rules.

## Install and configure

For the Windows x64 Unity Mono build with BepInEx 5.4.23.6, place
`AnomalithAutoReload.dll` in `BepInEx/plugins` and launch ANOMALITH. The mod is
enabled by default. Go to **Options → Other Settings → Auto Reload** to turn
it ON or OFF. The preference is saved in
`BepInEx/config/labs.anomalith.autoreload.cfg`.

## Build and verify

Requires the .NET 8 SDK and a local ANOMALITH installation with BepInEx. The
plugin targets `netstandard2.1` and uses Harmony from BepInEx. Run from the
workspace root.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH'
npx nx build anomalith-auto-reload
npx nx run anomalith-auto-reload:spec
```

See [verification](docs/verification.md) for the gameplay check.
