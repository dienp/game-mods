import { fileURLToPath } from "node:url";
import { buildMod } from "../../scripts/package-mod.mjs";

await buildMod(fileURLToPath(new URL(".", import.meta.url)), "PrimaryFireHoming", process.argv[2]);
