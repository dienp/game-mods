# ANOMALITH compatibility check for Steam build 26773190

Checked November 23, 2026 with the v1.0.7 source at `8776915` and the
IL2CPP ports prepared for ANOMALITH Mods v1.0.8. The user confirmed this
installation is Steam build `26773190`.
[SteamDB lists `26773190` on the public branch](https://steamdb.info/app/4017880/depots/#branches).
The supplied copy has no Steam app manifest for an independent local check.

## Mod results

The supplied copy is Windows x64 Unity 6000.3.18f1 IL2CPP with metadata
version 39. It has `GameAssembly.dll` and
`Anomalith_Data/il2cpp_data/Metadata/global-metadata.dat`. The managed
`Anomalith_Data/Managed/Assembly-CSharp.dll` referenced by the published Mono
builds is absent. The IL2CPP ports use generated interop assemblies and the
BepInEx 6 `BasePlugin` entry point.

| Mod | v1.0.8 plugin version | IL2CPP Release build | Pre-release startup with only that plugin |
| --- | --- | --- | --- |
| Auto Reload | 1.1.1 | Pass | Pass |
| HUD Numbers | 1.0.3 | Pass | Pass |
| Minimap | 1.4.1 | Pass | Pass |
| Quick Weapon Switch | 1.0.2 | Pass | Pass |
| Inventory Storage Preview | 1.0.1 | Pass | Pass |
| Upgrade Tracker | 1.8.2 | Pass | Pass |

| Mod | Gameplay check on the supplied IL2CPP copy |
| --- | --- |
| Auto Reload | Native patch installed. Empty-magazine reload still needs a shooting-range check. |
| HUD Numbers | Other Settings toggle appeared. After the placement fix, the automated check passed with `HP 272 / 272` and `MP 205 / 205`. |
| Minimap | Native maps rendered in the Base and at Front Entrance. Floor changes and the size slider still need a gameplay check. |
| Quick Weapon Switch | The v1.0.8 wheel appeared on the first notch. The [v1.0.9 fix](../mods/quick-switch/docs/verification.md#il2cpp-build-26773190) passed first-notch switching in both directions and restored the stock wheel with the toggle OFF. Swap speed still needs a shooting-range check. |
| Inventory Storage Preview | Sedative (Medium) showed `x 007` in its inventory tooltip. |
| Upgrade Tracker | The user confirmed that recipe pinning and the compatible weapons list worked after the fix. The pinned checklist logged its ready-to-build status in gameplay. |

The published Mono DLLs remain incompatible with IL2CPP. The ported DLLs
each initialized with one plugin loaded and a responsive game window.
The initial Minimap shutdown threw on an empty addressable texture handle.
The rebuilt DLL passed an individual startup and clean shutdown. All six
tested DLLs also initialized together. On the separate Mono installation
with BepInEx 5.4.23.6, all six Release builds and all seven OpenSpec checks passed.

## Loader and release status

A player reported a possible launch failure after installing the mods and
suspected BepInEx. The supplied copy reached the interactive title menu
with BepInEx 6.0.0-be.793, zero plugins, and zero patchers. BepInEx generated
the interop assemblies and reported chainloader startup complete. Il2CppInterop
warned that it used a substitute for `Class::Init`. The player-reported
failure remains unconfirmed.

The official [BepInEx 6.0.0-be.793 IL2CPP Windows x64 package](https://builds.bepinex.dev/projects/bepinex_be)
was staged separately under `anomalith/bin/compatibility/steam-26773190/`.
Its bundled Cpp2IL initialized the supplied metadata and native game
assembly offline. Il2CppInterop generated 144 assemblies under `interop/`
using the matching Unity base libraries. A .NET 6 test project compiled
against BepInEx 6 and the generated `OptionItemSelect` reference. The
generator reported partial restoration of stripped members. Live startup
used the loader-generated references from the same game installation.

The individual runs covered initialization and process exit. In the
combined run, Minimap loaded the native Base floor map `map_000_kyoten`
after a save was loaded. The [Inventory Storage Preview screenshot](screenshots/inventory-storage-count.png)
from that playtest shows the Storage icon and `x 007` on the Sedative (Medium)
tooltip. Comparing the count with Base Storage, toggling the preview, and
checking tooltip placement near screen edges still need verification on this build.
Full combat, map transitions, and Workshop Deploy hints still need verification.
The [Front Entrance screenshot](screenshots/gameplay-il2cpp-26773190.png)
shows HP/MP Numbers, the native minimap, and the pinned Workshop recipe.
The packaged v1.0.8 Release DLLs were checked for versions and archive
integrity. Gameplay and startup evidence came from the pre-release builds.

## Workshop repair and development reload

The initial combined run hit an invalid IL2CPP transform cast while building
the recipe pin and attachment panel. The unfinished panel left a white square
in Workshop. After the fix, live playtesting confirmed that Pin recipe and
the compatible weapons list worked. The log also confirmed that the
Semi-Automatic Magnum pin appeared in the gameplay checklist.

All six IL2CPP Debug builds compiled with the development loader. Upgrade
Tracker and Auto Reload each initialized, rebuilt, and reloaded in the same
running game process. A timestamp-triggered reload also passed for all six
plugins in one running game. See the [development setup](../README.md#hot-reload-during-development).

Windows logged a `UnityPlayer.dll` access violation (`0xc0000005`) in an
earlier combined run and again on a requested shutdown. The cause is
unresolved. The combined run reached the Base before that shutdown, and
the next launch initialized all six Debug plugins.

## v1.0.9 follow-up

The suite updates Quick Weapon Switch from 1.0.2 to 1.0.3. The other five
plugin versions are unchanged. First-notch switching and the OFF behavior
were verified in the running Debug session. The final Release package was
checked for versions and file integrity. A fresh launch from that package
was not part of this check.
