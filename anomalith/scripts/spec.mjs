import { spawnSync } from 'node:child_process';
import { realpathSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = realpathSync(resolve(dirname(fileURLToPath(import.meta.url)), '..'));
const [target, ...args] = process.argv.slice(2);

function fail(message) {
  console.error(message);
  process.exit(1);
}

if (!target || target === '--help' || target === '-h') {
  console.log('Usage: npm run spec -- mods/<name> <openspec arguments>');
  process.exit(target ? 0 : 1);
}

const mod = /^(?:mods\/|apps\/anomalith-)([a-z0-9]+(?:-[a-z0-9]+)*)$/.exec(target)?.[1];
if (!mod) {
  fail('Target must be mods/<name>, using lowercase kebab-case.');
}

const modPath = ['mods', mod].join(sep);
let cwd;
try {
  cwd = realpathSync(resolve(root, modPath));
} catch {
  fail(`Create ${modPath}/ before running OpenSpec there.`);
}
if (relative(root, cwd) !== modPath) {
  fail('Target must resolve to its own directory inside this repository.');
}

const cli = resolve(root, '../node_modules/@fission-ai/openspec/bin/openspec.js');
const result = spawnSync(process.execPath, [cli, ...args], {
  cwd,
  stdio: 'inherit',
});
if (result.error) fail(result.error.message);
if (result.signal) fail(`OpenSpec terminated by ${result.signal}.`);
process.exit(result.status ?? 1);
