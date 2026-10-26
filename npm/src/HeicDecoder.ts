import type { HeicDecodeRequest } from "./HeicDecodeRequest.js";
import type { HeicDecodeResponse } from "./HeicDecodeResponse.js";
import type { HeicDecoderOptions } from "./HeicDecoderOptions.js";
import type { HeicImage } from "./HeicImage.js";
import type { PendingDecode } from "./PendingDecode.js";

const MAX_PIXEL_COUNT = Math.floor(0x7fff_ffff / 4);

/** Lazily starts one module worker and reuses its WebAssembly runtime. */
export class HeicDecoder {
  private worker: Worker | undefined;
  private failure: Error | undefined;
  private requestId = 0;
  private readonly pending = new Map<number, PendingDecode>();
  private readonly assetsUrl: URL;
  private readonly maxPixels: number;

  constructor(options: HeicDecoderOptions) {
    this.assetsUrl = new URL(options.assetsUrl, globalThis.location?.href);
    if (!this.assetsUrl.pathname.endsWith("/")) {
      throw new TypeError("assetsUrl must name a directory ending in '/'.");
    }
    const maxPixels = options.maxPixels ?? 0;
    if (options.maxPixels !== undefined &&
        (!Number.isInteger(maxPixels) || maxPixels <= 0 || maxPixels > MAX_PIXEL_COUNT)) {
      throw new RangeError(`maxPixels must be a positive integer no greater than ${MAX_PIXEL_COUNT}.`);
    }
    this.maxPixels = maxPixels;
  }

  /** Copies the input before transfer; the caller's buffer remains usable. */
  async decode(input: Blob | ArrayBuffer | Uint8Array): Promise<HeicImage> {
    if (this.failure) {
      throw this.failure;
    }
    let bytes: ArrayBuffer;
    if (input instanceof Blob) {
      bytes = await input.arrayBuffer();
    } else if (input instanceof Uint8Array) {
      bytes = new Uint8Array(input).buffer;
    } else {
      bytes = input.slice(0);
    }
    if (this.failure) {
      throw this.failure;
    }
    const worker = this.getWorker();
    const requestId = ++this.requestId;
    const request: HeicDecodeRequest = {
      requestId,
      bytes,
      runtimeUrl: new URL("runtime/_framework/dotnet.js", this.assetsUrl).href,
      maxPixels: this.maxPixels,
    };
    return new Promise((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject });
      try {
        worker.postMessage(request, [bytes]);
      } catch (error) {
        this.pending.delete(requestId);
        reject(error);
      }
    });
  }

  /** Terminates the worker and rejects pending and future calls. */
  dispose(): void {
    this.fail(new Error("The HEIC decoder has been disposed."));
  }

  private getWorker(): Worker {
    if (!this.worker) {
      this.worker = new Worker(new URL("heic.worker.js", this.assetsUrl), { type: "module" });
      this.worker.onmessage = (event: MessageEvent<HeicDecodeResponse>) => {
        const response = event.data;
        const request = this.pending.get(response.requestId);
        if (!request) {
          return;
        }
        this.pending.delete(response.requestId);
        switch (response.type) {
          case "decoded":
            request.resolve({
              width: response.width,
              height: response.height,
              pixels: new Uint8ClampedArray(response.pixels),
            });
            return;
          case "error":
            request.reject(new Error(response.message));
            return;
          default: {
            const unexpected: never = response;
            const error = new Error(`Unexpected HEIC decoder response: ${String(unexpected)}`);
            request.reject(error);
            this.fail(error);
          }
        }
      };
      this.worker.onerror = (event) => {
        event.preventDefault();
        this.fail(new Error(event.message || "The HEIC decoder worker failed."));
      };
      this.worker.onmessageerror = () => this.fail(new Error("The HEIC decoder response could not be read."));
    }
    return this.worker;
  }

  private fail(error: Error): void {
    this.failure ??= error;
    this.worker?.terminate();
    for (const request of this.pending.values()) {
      request.reject(this.failure);
    }
    this.pending.clear();
  }
}
