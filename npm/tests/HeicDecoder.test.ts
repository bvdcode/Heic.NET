import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HeicDecoder } from "../src/HeicDecoder.js";
import type { HeicDecodeRequest } from "../src/HeicDecodeRequest.js";
import type { HeicDecodeResponse } from "../src/HeicDecodeResponse.js";

class DecoderWorker {
  static instances: DecoderWorker[] = [];
  readonly requests: HeicDecodeRequest[] = [];
  onmessage: ((event: MessageEvent<HeicDecodeResponse>) => void) | null = null;
  onerror: ((event: ErrorEvent) => void) | null = null;
  onmessageerror: (() => void) | null = null;
  terminate = vi.fn();
  postMessage = vi.fn((request: HeicDecodeRequest) => this.requests.push(request));

  constructor(readonly url: URL, readonly options: WorkerOptions) {
    DecoderWorker.instances.push(this);
  }

  respond(response: HeicDecodeResponse): void {
    this.onmessage?.(new MessageEvent("message", { data: response }));
  }
}

describe("HeicDecoder", () => {
  beforeEach(() => {
    DecoderWorker.instances = [];
    vi.stubGlobal("Worker", DecoderWorker);
    vi.stubGlobal("location", { href: "https://example.com/app/" });
  });

  afterEach(() => vi.unstubAllGlobals());

  it("loads lazily, respects a nested asset path and correlates concurrent results", async () => {
    const decoder = new HeicDecoder({ assetsUrl: "./heic/", maxPixels: 1234 });
    expect(DecoderWorker.instances).toHaveLength(0);
    const input = new Uint8Array([10, 20, 30, 40]);
    const first = decoder.decode(input.subarray(1, 3));
    const second = decoder.decode(new Uint8Array([1]));
    const worker = DecoderWorker.instances[0];
    expect(worker.url.href).toBe("https://example.com/app/heic/heic.worker.js");
    expect(worker.options.type).toBe("module");
    expect(DecoderWorker.instances).toHaveLength(1);
    expect(worker.requests[0].runtimeUrl).toBe("https://example.com/app/heic/runtime/_framework/dotnet.js");
    expect(worker.requests[0].maxPixels).toBe(1234);
    expect([...new Uint8Array(worker.requests[0].bytes)]).toEqual([20, 30]);
    expect([...input]).toEqual([10, 20, 30, 40]);

    worker.respond({ type: "decoded", requestId: 2, width: 1, height: 1, pixels: new Uint8Array([1, 2, 3, 255]).buffer });
    worker.respond({ type: "error", requestId: 1, message: "Invalid HEIC data" });
    await expect(second).resolves.toEqual({ width: 1, height: 1, pixels: new Uint8ClampedArray([1, 2, 3, 255]) });
    await expect(first).rejects.toThrow("Invalid HEIC data");
    expect(worker.terminate).not.toHaveBeenCalled();
  });

  it("accepts Blob and ArrayBuffer inputs without detaching the caller's buffer", async () => {
    const decoder = new HeicDecoder({ assetsUrl: "/heic/" });
    const bytes = new Uint8Array([1, 2, 3]).buffer;
    const first = decoder.decode(bytes);
    const second = decoder.decode(new Blob([bytes]));
    await vi.waitFor(() => expect(DecoderWorker.instances[0].requests).toHaveLength(2));
    const worker = DecoderWorker.instances[0];
    expect(worker.requests[0].bytes).not.toBe(bytes);
    expect(bytes.byteLength).toBe(3);
    for (const request of worker.requests) {
      worker.respond({ type: "decoded", requestId: request.requestId, width: 1, height: 1, pixels: new ArrayBuffer(4) });
    }
    await Promise.all([first, second]);
  });

  it("rejects pending and future requests on disposal", async () => {
    const decoder = new HeicDecoder({ assetsUrl: "/heic/" });
    const first = decoder.decode(new ArrayBuffer(4));
    const rejected = expect(first).rejects.toThrow("disposed");
    decoder.dispose();
    await rejected;
    await expect(decoder.decode(new ArrayBuffer(4))).rejects.toThrow("disposed");
    expect(DecoderWorker.instances[0].terminate).toHaveBeenCalledOnce();
  });

  it("rejects all requests after an unreadable worker response", async () => {
    const decoder = new HeicDecoder({ assetsUrl: "/heic/" });
    const first = decoder.decode(new ArrayBuffer(1));
    const second = decoder.decode(new ArrayBuffer(1));
    const rejected = Promise.all([
      expect(first).rejects.toThrow("could not be read"),
      expect(second).rejects.toThrow("could not be read"),
    ]);
    DecoderWorker.instances[0].onmessageerror?.();
    await rejected;
  });

  it("rejects a postMessage failure without affecting later requests", async () => {
    const decoder = new HeicDecoder({ assetsUrl: "/heic/" });
    const first = decoder.decode(new ArrayBuffer(1));
    const worker = DecoderWorker.instances[0];
    worker.respond({ type: "decoded", requestId: 1, width: 1, height: 1, pixels: new ArrayBuffer(4) });
    await first;
    worker.postMessage.mockImplementationOnce(() => { throw new Error("Transfer failed"); });
    await expect(decoder.decode(new ArrayBuffer(1))).rejects.toThrow("Transfer failed");
    expect(worker.terminate).not.toHaveBeenCalled();
  });

  it.each([0, -1, 1.5, Number.NaN, Number.POSITIVE_INFINITY, 536870912])(
    "rejects invalid maxPixels %s", (maxPixels) => {
      expect(() => new HeicDecoder({ assetsUrl: "/heic/", maxPixels })).toThrow(RangeError);
    },
  );

  it("requires a directory URL", () => {
    expect(() => new HeicDecoder({ assetsUrl: "/heic" })).toThrow(TypeError);
  });
});
