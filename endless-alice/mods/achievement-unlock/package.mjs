import { fileURLToPath } from "node:url";
import { buildMod } from "../../scripts/package-mod.mjs";

await buildMod(fileURLToPath(new URL(".", import.meta.url)), "AchievementUnlock", process.argv[2]);
