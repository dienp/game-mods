# Game mods workspace

## Workspace layout

- Use one root Nx workspace and one npm lockfile.
- Put each mod under `<game>/mods/<mod>/` with its own `AGENTS.md` and Nx library definition. Name the project `<game>-<mod>` and tag it `game:<game>` and `type:mod`.
- Keep game-specific instructions in `<game>/AGENTS.md` and mod-specific instructions beside each mod.
- Provide a Release `build` target and a Debug `dev` target. Disable build caching when the build uses assemblies from a local game install.

## Mod development

- Support hot reload in development builds. Keep hot reload out of release builds.
- Give players an on/off toggle and intensity control for every feature. Give third-person cameras a field-of-view slider.
- Reuse game assets and native UI controls wherever practical.
- Present several concept sketches and get approval before implementing a new mod or visual change.
