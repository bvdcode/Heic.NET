import { spawn } from "node:child_process";
import { cp, mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const root = resolve(packageRoot, "..");
const publishRoot = resolve(root, "artifacts/browser");
const output = resolve(root, "artifacts/npm/package");

async function run(command, args, cwd = root) {
  await new Promise((resolveRun, rejectRun) => {
    const child = spawn(command, args, { cwd, stdio: "inherit", shell: process.platform === "win32" });
    child.on("error", rejectRun);
    child.on("exit", (code) => {
      if (code === 0) {
        resolveRun();
      } else {
        rejectRun(new Error(`${command} exited with code ${code}.`));
      }
    });
  });
}

const version = process.env.PACKAGE_VERSION;
if (!version || !/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
  throw new Error("Set PACKAGE_VERSION to the GitVersion SemVer output.");
}
await rm(publishRoot, { recursive: true, force: true });
await run("dotnet", ["publish", "Heic.Net.Browser/Heic.Net.Browser.csproj", "-c", "Release",
  `-p:Version=${version}`, "--output", publishRoot]);
await run("npx", ["--no-install", "tsc"], packageRoot);
await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });
const framework = resolve(publishRoot, "wwwroot/_framework");
if (!(await readdir(framework)).includes("dotnet.js")) {
  throw new Error("The WebAssembly build did not produce dotnet.js.");
}
await cp(resolve(packageRoot, "dist"), output, { recursive: true });
await cp(framework, resolve(output, "runtime/_framework"), {
  recursive: true, filter: (path) => !/\.(br|gz|pdb)$/.test(path),
});
await cp(resolve(publishRoot, "wwwroot/Licenses"), resolve(output, "runtime/Licenses"), { recursive: true });
await cp(resolve(packageRoot, "bin"), resolve(output, "bin"), { recursive: true });
await cp(resolve(root, "LICENSE"), resolve(output, "LICENSE"));
await cp(resolve(packageRoot, "README.md"), resolve(output, "README.md"));
const manifest = JSON.parse(await readFile(resolve(packageRoot, "package.json"), "utf8"));
delete manifest.private;
delete manifest.scripts;
delete manifest.devDependencies;
manifest.version = version;
await writeFile(resolve(output, "package.json"), JSON.stringify(manifest, null, 2) + "\n");
process.stdout.write(`Built heic-net@${version}.\n`);
