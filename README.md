# Heic.NET

A managed HEIC still image decoder targeting **.NET Standard 2.1**.

The basic implementation decodes single-layer HEVC IDR images, HEIF grids and
identity images to owned RGBA8 pixels. It supports 8/10-bit monochrome and 4:2:0,
rotation, mirroring, integral clean-aperture cropping and straight auxiliary alpha.

```csharp
using Heic.Net;

HeicDocument document = await HeicDocument.LoadAsync(stream);
RgbaImage image = document.DecodePrimary();
ReadOnlyMemory<byte> rgba = image.Pixels;
```

Documents copy their input once. Concurrent decode calls have separate workers and
return separate pixel arrays. RGB retains the signalled primaries, transfer function
and embedded ICC profile; it is not implicitly converted to sRGB.

Defaults allow 64 MiB of encoded input and 16 Mi pixels per coded or visible image.
For the included 200-megapixel photograph, explicitly increase the pixel limit:

```csharp
HeicDecodeOptions limits = new HeicDecodeOptions(maxPixels: 199756800);
HeicDocument document = HeicDocument.Load(encodedFile, limits);
```

Build and test with the .NET 10 SDK:

```sh
dotnet build Heic.Net.slnx -c Release
dotnet test Heic.Net.slnx -c Release --no-build
```

The complete test run includes the 200-megapixel case. To run the smaller cases:

```sh
dotnet test Heic.Net.slnx -c Release --filter "Category!=LargeImage"
```

One-command cold/warm comparison, with every repetition saved as JSON:

```sh
dotnet run --project Heic.Net.Benchmarks -c Release -- --input corpus/20260201_164721.heic --repetitions 5
```

See the [decoder map](docs/decoder.md), [support boundary](docs/support.md),
[measurement protocol](docs/benchmark.md) and [test images](corpus/README.md).
