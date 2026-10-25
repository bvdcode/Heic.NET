<p align="center">
  <img src="https://raw.githubusercontent.com/bvdcode/Heic.NET/main/assets/icon.png" alt="Heic.NET" width="144" height="144" />
</p>

# Heic.NET

[![Build](https://github.com/bvdcode/Heic.NET/actions/workflows/publish-release.yml/badge.svg)](https://github.com/bvdcode/Heic.NET/actions/workflows/publish-release.yml)
[![NuGet](https://img.shields.io/nuget/v/Heic.NET.svg)](https://www.nuget.org/packages/Heic.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/bvdcode/Heic.NET/blob/main/LICENSE)

A managed HEIC still image decoder targeting **.NET Standard 2.1**.

The decoder supports single-layer HEVC IDR images, HEIF grids and
identity images with owned RGBA8 output. It supports 8/10-bit monochrome, 4:2:0 and 4:4:4,
rotation, mirroring, integral clean-aperture cropping and straight auxiliary alpha.

Install the package:

```sh
dotnet add package Heic.NET
```

```csharp
using Heic.Net;

HeicDocument document = await HeicDocument.LoadAsync(stream);
RgbaImage image = document.DecodePrimary();
ReadOnlyMemory<byte> rgba = image.Pixels;
```

Documents copy their input once. Concurrent decode calls have separate workers and
return separate pixel arrays. RGB retains the signalled primaries, transfer function
and embedded ICC profile; it is not implicitly converted to sRGB.

Defaults allow 64 MiB of encoded input and images up to the RGBA buffer size limit
(`int.MaxValue / 4` pixels). The included 200-megapixel photograph opens with the
default options. Applications can set a smaller pixel limit to bound resource use:

```csharp
HeicDecodeOptions limits = new HeicDecodeOptions(maxPixels: 16 * 1024 * 1024);
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

See the [decoder map](https://github.com/bvdcode/Heic.NET/blob/main/docs/decoder.md), [support boundary](https://github.com/bvdcode/Heic.NET/blob/main/docs/support.md),
[measurement protocol](https://github.com/bvdcode/Heic.NET/blob/main/docs/benchmark.md) and [test images](https://github.com/bvdcode/Heic.NET/blob/main/corpus/README.md).

Packages are versioned with GitVersion and published automatically from `main` after
Linux, Windows and macOS checks pass. See [publishing](https://github.com/bvdcode/Heic.NET/blob/main/docs/publishing.md).
