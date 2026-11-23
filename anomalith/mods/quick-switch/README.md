# ANOMALITH Quick Weapon Switch

Quick Weapon Switch speeds up the normal firearm swap animation. In
**Options → Other Settings**, use **Quick Weapon Switch** to turn it ON or
OFF and **Switch Speed** to select 1.0×–3.0× in 0.1× steps. It starts
enabled at 1.5× and remembers both settings. Use Reset to Default to restore 1.5×.

While enabled, one mouse-wheel notch cycles your assigned weapons immediately.
Scrolling in either direction skips empty shortcuts and wraps around.

## Install

Place `AnomalithQuickSwitch.dll` for your game runtime in `BepInEx/plugins`.
See the [tested builds](../../docs/compatibility-26773190.md) and
[development setup](../../README.md#hot-reload-during-development).

## Build and verify

Requires a local ANOMALITH installation with BepInEx and the .NET 8 SDK.
See the [build setup](../../README.md#build-from-source). Run from the workspace root.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH.Build.26773190'
npx nx build anomalith-quick-switch
npx nx run anomalith-quick-switch:spec
```

See [verification](docs/verification.md) for the live test.
