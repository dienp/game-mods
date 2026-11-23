# ANOMALITH HUD Numbers

A Windows x64 BepInEx plugin that shows current and maximum HP and MP to
the left of the game's HUD bars. It reads the player's native status values
and refreshes ten times per second. Numbers follow the bars' visibility
and HUD scaling. The HP number follows the health bar's current color.
The labels use the game's font and receive no input.

HP rounds up so a living character with less than 1 HP shows 1. MP rounds
down to show whole points currently available. Maximum values round up.

Use **Options → Other Settings → HP/MP Numbers** to turn the numbers ON or
OFF. The preference is saved in
`BepInEx/config/labs.anomalith.hudnumbers.cfg` as `General.Enabled`.

## Build and install

Requires the .NET 8 SDK and a local Windows x64 ANOMALITH installation with
BepInEx. See the [tested builds](../../docs/compatibility-26773190.md) and
[build setup](../../README.md#build-from-source). Run from the workspace root
and close the game before replacing the startup plugin.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH.Build.26773190'
npx nx build anomalith-hud-numbers
Copy-Item -LiteralPath 'anomalith/mods/hud-numbers/bin/Release/net6.0/AnomalithHudNumbers.dll' -Destination (Join-Path $env:AnomalithDir 'BepInEx/plugins')
npx nx run anomalith-hud-numbers:spec
```

See the [automated HUD smoke test](docs/verification.md#il2cpp-smoke-test).
