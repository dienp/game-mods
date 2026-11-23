# ANOMALITH Inventory Storage Preview

Hover or select an item in the Inventory grid to see the Storage icon and
quantity, such as `x 001`, on a separate line at the bottom of its tooltip.
Turn it ON or OFF under **Options → Other Settings → Inventory Storage Preview**.
It defaults to ON and remembers your choice.

## Install and build

Place `AnomalithStoragePreview.dll` for your game runtime in `BepInEx/plugins`.
See the [tested builds](../../docs/compatibility-26773190.md) and
[development setup](../../README.md#hot-reload-during-development).

Building requires a local game install, BepInEx, and the .NET 8 SDK.
See the [build setup](../../README.md#build-from-source). Run from the workspace root.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH.Build.26773190'
npx nx build anomalith-storage-preview
npx nx run anomalith-storage-preview:spec
```

See [verification](docs/verification.md) for gameplay checks.
