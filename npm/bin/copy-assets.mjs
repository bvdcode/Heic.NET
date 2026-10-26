#!/usr/bin/env node

import { cp, mkdir, rm } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const destination = process.argv[2];
if (!destination || process.argv.length !== 3) {
  throw new Error("Usage: heic-net-assets <destination-directory>");
}
const packageRoot = new URL("../", import.meta.url);
const target = resolve(destination);
if (target === fileURLToPath(packageRoot)) {
  throw new Error("The asset destination must differ from the installed package directory.");
}
await rm(resolve(target, "runtime"), { recursive: true, force: true });
await mkdir(target, { recursive: true });
await cp(fileURLToPath(new URL("runtime", packageRoot)), resolve(target, "runtime"), { recursive: true });
await cp(fileURLToPath(new URL("heic.worker.js", packageRoot)), resolve(target, "heic.worker.js"));
process.stdout.write("Copied heic-net browser assets.\n");
