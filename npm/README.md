# heic-net

An independent **MIT-licensed HEIC decoder** for browsers. The decoder is written
in C# and compiled ahead of time to WebAssembly. It does not use libheif, libde265,
or an LGPL-licensed HEVC decoder.

## Install

```sh
npm install heic-net
npx heic-net-assets public/heic
```

Run the asset-copy command before your application's development and production
builds. It copies prebuilt files; consumers do not need the .NET SDK.

```ts
import { HeicDecoder } from "heic-net";

const decoder = new HeicDecoder({ assetsUrl: "/heic/" });
const image = await decoder.decode(file);

const canvas = document.createElement("canvas");
canvas.width = image.width;
canvas.height = image.height;
const context = canvas.getContext("2d");
if (!context) {
  throw new Error("A 2D canvas is unavailable.");
}
context.putImageData(new ImageData(image.pixels, image.width, image.height), 0, 0);

// When the decoder is no longer needed:
decoder.dispose();
```

Use your deployment's base path in `assetsUrl`. Serve the copied files from the
same origin, with `application/wasm` for `.wasm` files and JavaScript MIME types
for `.js` files. Keep the entire copied directory together.

## API

`new HeicDecoder({ assetsUrl, maxPixels? })` creates a decoder without loading
WebAssembly. The first `decode` starts one module Web Worker and loads the runtime.
Later calls reuse that worker. Decoding runs outside the browser's main thread;
concurrent requests are processed by the same decoder worker.

`decode(input)` accepts a `Blob`, `ArrayBuffer`, or `Uint8Array` and returns
`Promise<HeicImage>`: `width`, `height`, and caller-owned RGBA8 `pixels`
(`Uint8ClampedArray`). Input buffers remain usable after decoding. Returned pixels
remain valid after later decodes or disposal. HEIC errors reject the individual
request; a worker failure rejects all pending calls.

`dispose()` terminates the worker and rejects pending and future calls.

## Support and color

The package uses the same decoder as [Heic.NET](https://github.com/bvdcode/Heic.NET).
It supports HEIF grids, primary images, transformations and auxiliary alpha, with
8/10-bit monochrome, 4:2:0, 4:2:2 and 4:4:4 HEVC intra images. See the current
[support boundary](https://github.com/bvdcode/Heic.NET/blob/main/docs/support.md).

RGBA values retain the source primaries and transfer function. ICC conversion and
HDR tone mapping are not performed; displaying those bytes in an sRGB canvas can
change their appearance. AVIF decoding and Node.js are not supported by this
browser API.

## License

The decoder and JavaScript API are MIT licensed. The package also includes the
.NET WebAssembly runtime and its license and third-party notices under
`runtime/Licenses/dotnet`.
