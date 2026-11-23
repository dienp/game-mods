# ANOMALITH Auto Reload

Auto Reload starts the normal reload when your active firearm runs empty and
the game allows it. Reloads use the game's animation and ammo rules.

## Install and configure

Place `AnomalithAutoReload.dll` for your game runtime in `BepInEx/plugins`
and launch ANOMALITH. See the [tested builds](../../docs/compatibility-26773190.md). The mod is
enabled by default. Go to **Options → Other Settings → Auto Reload** to turn
it ON or OFF. The preference is saved in
`BepInEx/config/labs.anomalith.autoreload.cfg`.

## Build and verify

Requires the .NET 8 SDK and a local ANOMALITH installation with BepInEx.
See the [build setup](../../README.md#build-from-source). Run from the workspace root.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH.Build.26773190'
npx nx build anomalith-auto-reload
npx nx run anomalith-auto-reload:spec
```

See [verification](docs/verification.md) for the gameplay check.
