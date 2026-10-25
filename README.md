<p align="center">
  <img src="https://raw.githubusercontent.com/bvdcode/Heic.NET/main/assets/icon.png" alt="Heic.NET" width="144" height="144" />
</p>

# Heic.NET

[![Build](https://github.com/bvdcode/Heic.NET/actions/workflows/publish-release.yml/badge.svg)](https://github.com/bvdcode/Heic.NET/actions/workflows/publish-release.yml)
[![NuGet](https://img.shields.io/nuget/v/Heic.NET.svg)](https://www.nuget.org/packages/Heic.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/bvdcode/Heic.NET/blob/main/LICENSE)

A managed HEIC still image decoder targeting **.NET Standard 2.1**.

The decoder supports single-layer HEVC IDR images, HEIF grids and
identity images with owned RGBA8 output. It supports 8/10-bit monochrome, 4:2:0, 4:2:2 and 4:4:4,
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

## Performance

Cold decoding to an owned RGBA8 buffer, measured on 2026-10-25:

| Decoder | 12 MP, seconds | Peak RAM, MB | 200 MP, seconds | Peak RAM, MB |
| --- | ---: | ---: | ---: | ---: |
| libheif 1.23.2 / libde265 1.1.1 | 0.37 | 141 | 4.70 | 1683 |
| Openize, `1640f6e` | 6.23 | 229 | 93.76 | 2419 |
| **Heic.NET 0.1.2**, `142088c` | **1.14** | **118** | **5.43** | **911** |

On this 200-MP photograph, Heic.NET used **46% less peak memory than libheif**
and decoded **17.3 times faster than Openize**.

Times are medians of three fresh-process runs; RAM is the highest process peak,
in decimal MB. Intel Core i5-12450H, Linux x64, .NET 10.0.13,
`DOTNET_PROCESSOR_COUNT=4`. Other workloads remained active, so these are shared-machine
measurements. See the [measurement conditions and ranges](https://github.com/bvdcode/Heic.NET/blob/main/docs/benchmark.md#cross-decoder-results).

Across an 84-file HEVC sample set, the primary-image results were:

| Decoder | Decoded with expected dimensions | Wrong dimensions | Rejected |
| --- | ---: | ---: | ---: |
| libheif | 82 / 84 | 0 | 2 |
| Openize | 50 / 84 | 9 | 25 |
| **Heic.NET** | **84 / 84** | **0** | **0** |

All three decoders rejected the three negative samples (two damaged HEIC files
and a JPEG with a `.heic` extension). AVIF was
excluded; sequences were checked through their primary image. These are results
for this sample set. Pixel agreement is reported separately in the
[comparison details](https://github.com/bvdcode/Heic.NET/blob/main/docs/benchmark.md#corpus-coverage).

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
