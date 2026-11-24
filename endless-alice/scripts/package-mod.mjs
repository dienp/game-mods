import { spawnSync } from "node:child_process";
import { watch } from "node:fs";
import { copyFile, mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";

export async function buildMod(project, modName, mode) {
  const source = join(project, "src");
  if (mode !== "release" && mode !== "debug") {
    throw new Error("Expected release or debug.");
  }

  const debug = mode === "debug";
  const output = join(project, "bin", debug ? "Debug" : "Release");
  const debugMods = debug && process.env.ENDLESS_ALICE_DEV_MODS;
  const mods = debugMods ? resolve(debugMods) : join(output, "Mods");

  async function writeIfChanged(path, content) {
    try {
      const previous = await readFile(path);
      if (previous.equals(Buffer.from(content))) return;
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
    await writeFile(path, content);
  }

  async function packageMod() {
    const mod = join(mods, modName);
    const scripts = join(mod, "Scripts");
    await mkdir(scripts, { recursive: true });

    if (debug) {
      for (const file of await readdir(source)) {
        await writeIfChanged(join(scripts, file), await readFile(join(source, file)));
      }
    } else {
      for (const file of await readdir(source)) {
        await copyFile(join(source, file), join(scripts, file));
      }
    }

    await writeIfChanged(join(scripts, "build_options.lua"), `return { Debug = ${debug} }\n`);
    await writeIfChanged(join(mod, "enabled.txt"), "");
    console.log(`Packaged ${mod}`);
  }

  await packageMod();
  if (debug) {
    let pending;
    let build = Promise.resolve();
    watch(source, () => {
      clearTimeout(pending);
      pending = setTimeout(() => {
        build = build.then(packageMod).catch(error => console.error(error));
      }, 150);
    });
    console.log("Watching Lua files for changes.");
  } else {
    const archive = join(output, `${modName}.zip`);
    const result = spawnSync("tar.exe", ["-a", "-cf", archive, "-C", output, "Mods"], { stdio: "inherit" });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`tar exited with status ${result.status}`);
    console.log(archive);
  }
}
