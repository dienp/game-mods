# game-mods

An Nx monorepo with one root folder per game and one library per mod.

- [Gunfire Reborn](gunfire-reborn/README.md)
  - [Steady Third Person](gunfire-reborn/mods/steady-third-person/README.md)

Install Node.js 24 and the .NET 8 SDK, then run these commands from the repository root.

```powershell
npm ci
npm run projects
npm run build
```

Build a single library with `npx nx build gunfire-reborn-steady-third-person`.

To add a mod, put its source and Nx `project.json` under `<game>/mods/<mod>/`. Follow the [workspace rules](AGENTS.md) before implementing mod changes.
