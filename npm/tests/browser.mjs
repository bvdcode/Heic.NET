import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";

const root = fileURLToPath(new URL("../../", import.meta.url));
const packageRoot = process.env.HEIC_PACKAGE_ROOT ?? resolve(root, "artifacts/npm/package");
const corpusRoot = resolve(root, "corpus");
const manifest = JSON.parse(await readFile(resolve(corpusRoot, "manifest.json"), "utf8"));
const fixtures = manifest.filter((entry) => process.env.HEIC_LARGE_IMAGE === "true" || entry.width * entry.height <= 20_000_000);
const mimeTypes = new Map([[".js", "text/javascript"], [".json", "application/json"], [".wasm", "application/wasm"]]);
const server = createServer(async (request, response) => {
  const url = new URL(request.url, "http://localhost");
  if (url.pathname === "/") {
    response.setHeader("Content-Type", "text/html");
    response.end("<!doctype html><title>HEIC browser tests</title>");
    return;
  }
  const prefix = url.pathname.startsWith("/package/") ? "/package/" : "/corpus/";
  const directory = prefix === "/package/" ? packageRoot : corpusRoot;
  const path = resolve(directory, decodeURIComponent(url.pathname.slice(prefix.length)));
  if (!path.startsWith(directory + sep)) {
    response.writeHead(403).end();
    return;
  }
  try {
    const bytes = await readFile(path);
    response.setHeader("Content-Type", mimeTypes.get(extname(path)) ?? "application/octet-stream");
    response.end(bytes);
  } catch {
    response.writeHead(404).end();
  }
});
await new Promise((resolveListen) => server.listen(0, "127.0.0.1", resolveListen));
const address = server.address();
assert(address && typeof address !== "string");
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE,
  args: ["--no-sandbox"],
});
try {
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(`http://127.0.0.1:${address.port}/`);
  const result = await page.evaluate(async (entries) => {
    const { HeicDecoder } = await import("/package/index.js");
    const decoder = new HeicDecoder({ assetsUrl: "/package/" });
    const results = [];
    let firstPixels;
    let firstHash;
    async function hash(pixels) {
      const digest = await crypto.subtle.digest("SHA-256", pixels);
      return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("").toUpperCase();
    }
    try {
      for (const entry of entries) {
        const input = new Uint8Array(await (await fetch(`/corpus/${entry.file}`)).arrayBuffer());
        const inputLength = input.byteLength;
        const started = performance.now();
        const image = await decoder.decode(input);
        const rgbaSha256 = await hash(image.pixels);
        results.push({
          file: entry.file, width: image.width, height: image.height, rgbaSha256,
          milliseconds: Math.round(performance.now() - started), inputIntact: input.byteLength === inputLength,
        });
        if (!firstPixels) {
          firstPixels = image.pixels;
          firstHash = rgbaSha256;
        }
      }
      let invalidRejected = false;
      try {
        await decoder.decode(new Uint8Array([1, 2, 3, 4]));
      } catch {
        invalidRejected = true;
      }
      const recovery = await decoder.decode(await (await fetch(`/corpus/${entries[0].file}`)).blob());
      const limited = new HeicDecoder({ assetsUrl: "/package/", maxPixels: 1 });
      let limitRejected = false;
      try {
        await limited.decode(await (await fetch(`/corpus/${entries[0].file}`)).arrayBuffer());
      } catch {
        limitRejected = true;
      } finally {
        limited.dispose();
      }
      decoder.dispose();
      let disposedRejected = false;
      try {
        await decoder.decode(new ArrayBuffer(0));
      } catch {
        disposedRejected = true;
      }
      return {
        results, invalidRejected, limitRejected, disposedRejected,
        pixelsRetained: await hash(firstPixels) === firstHash,
        recoveryHash: await hash(recovery.pixels),
      };
    } finally {
      decoder.dispose();
    }
  }, fixtures);
  assert.equal(result.results.length, fixtures.length);
  for (let index = 0; index < fixtures.length; index++) {
    const expected = fixtures[index];
    const actual = result.results[index];
    assert.equal(actual.width, expected.width, expected.file);
    assert.equal(actual.height, expected.height, expected.file);
    assert.equal(actual.rgbaSha256, expected.rgbaSha256, expected.file);
    assert(actual.inputIntact, expected.file);
  }
  assert(result.invalidRejected);
  assert(result.limitRejected);
  assert(result.disposedRejected);
  assert(result.pixelsRetained);
  assert.equal(result.recoveryHash, fixtures[0].rgbaSha256);
  assert.deepEqual(errors, []);
  process.stdout.write(JSON.stringify(result, null, 2) + "\n");
} finally {
  await browser.close();
  await new Promise((resolveClose) => server.close(resolveClose));
}
