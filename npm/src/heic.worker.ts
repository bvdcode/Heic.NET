/// <reference lib="webworker" />

import type { HeicDecodeRequest } from "./HeicDecodeRequest.js";
import type { HeicDecodeResponse } from "./HeicDecodeResponse.js";
import type { HeicDecodedPixels, HeicRuntimeModule } from "./HeicRuntime.js";

declare const self: DedicatedWorkerGlobalScope;

type Decoder = (bytes: Uint8Array, maxPixels: number) => HeicDecodedPixels;
let decoderPromise: Promise<Decoder> | undefined;

async function createDecoder(runtimeUrl: string): Promise<Decoder> {
  const module: HeicRuntimeModule = await import(/* @vite-ignore */ runtimeUrl);
  const runtime = await module.dotnet.create();
  runtime.setModuleImports("heic-net", {
    createImage: (width, height, pixels) => ({ width, height, pixels: pixels.slice() }),
  });
  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  return exports.Heic.Net.Browser.HeicDecoder.Decode;
}

self.addEventListener("message", async (event: MessageEvent<HeicDecodeRequest>) => {
  const request = event.data;
  let response: HeicDecodeResponse;
  try {
    decoderPromise ??= createDecoder(request.runtimeUrl);
    const decode = await decoderPromise;
    const result = decode(new Uint8Array(request.bytes), request.maxPixels);
    response = {
      type: "decoded",
      requestId: request.requestId,
      width: result.width,
      height: result.height,
      pixels: result.pixels.buffer,
    };
    self.postMessage(response, [response.pixels]);
  } catch (error) {
    response = {
      type: "error",
      requestId: request.requestId,
      message: error instanceof Error ? error.message : String(error),
    };
    self.postMessage(response);
  }
});
