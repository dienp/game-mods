# game-mods

An Nx monorepo for game mods.

- [ANOMALITH mods and build setup](anomalith/README.md)
- [Gunfire Reborn mods and build setup](gunfire-reborn/README.md)
- [Endless Alice mods and build setup](endless-alice/README.md)

Install Node.js 24. For .NET mods, install the .NET 8 SDK and follow the game's setup for local assemblies. Run Nx from the repository root.

```powershell
npm ci
npm run projects
npx nx build anomalith-minimap
```

Run `npm run build` to build every project with a build target. See [AGENTS.md](AGENTS.md) for workspace conventions.
