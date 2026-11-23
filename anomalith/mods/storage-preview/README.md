# ANOMALITH Inventory Storage Preview

Hover or select an item in the Inventory grid to see the Storage icon and
quantity, such as `x 001`, on a separate line at the bottom of its tooltip.
Turn it ON or OFF under **Options → Other Settings → Inventory Storage Preview**.
It defaults to ON and remembers your choice.

## Install and build

For the Windows x64 Unity Mono build with BepInEx 5.4.23.6, place
`AnomalithStoragePreview.dll` in `BepInEx/plugins`. See the
[ScriptEngine development setup](../../README.md#hot-reload-during-development)
for hot reload.

Building requires a local game install, BepInEx, and the .NET 8 SDK.
The plugin targets `netstandard2.1`. Run from the workspace root.

```powershell
$env:AnomalithDir = 'E:/Games/ANOMALITH'
npx nx build anomalith-storage-preview
npx nx run anomalith-storage-preview:spec
```

See [verification](docs/verification.md) for gameplay checks.
