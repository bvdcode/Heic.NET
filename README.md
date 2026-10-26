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

First decode to an owned RGBA8 buffer on a four-vCPU server, measured on
2026-10-26:

| Decoder | 12 MP, seconds | Peak RAM, MB | 200 MP, seconds | Peak RAM, MB |
| --- | ---: | ---: | ---: | ---: |
| libheif 1.23.2 / libde265 1.1.1 | 0.440 | 141.4 | 4.925 | 1682.7 |
| Openize, `1640f6e` | 9.954 | 226.0 | 137.460 | 2428.3 |
| **Heic.NET**, `20b1f67` | **0.827** | **112.5** | **4.851** | **905.9** |

On this 200-MP photograph, Heic.NET used **46% less peak memory than libheif**
and decoded **28.3 times faster than Openize**. Its 200-MP time was close to
libheif; libheif remained faster on the 12-MP photograph.

Times are medians of three fresh-process runs; RAM is the highest process peak,
in decimal MB. AMD EPYC, four virtual CPUs, Debian 13 x64, .NET 10.0.13,
CPU affinity 0–3. These are results for these inputs and this virtual server.
See the [measurement conditions and ranges](https://github.com/bvdcode/Heic.NET/blob/main/docs/benchmark.md#cross-decoder-results).

For repeated decoding, the residual-group change in `dfa4fff` was compared
with `20b1f67` in three alternating process pairs:

| Image | `20b1f67`, seconds | `dfa4fff`, seconds |
| --- | ---: | ---: |
| 12 MP photograph | 0.373 | 0.351 |
| iPhone 15 Pro photograph | 0.159 | 0.147 |
| 8.5 MP photograph with parallel HEVC rows | 0.374 | 0.348 |
| 14 MP photograph with parallel HEVC rows | 0.454 | 0.445 |

Each result is the median of three process medians, with 25 measured decodes per
process after at least 64 warm-up decodes and five seconds. Document loading is
excluded; every decode reconstructs a new owned image. A separate 200-MP series,
using eight warm-up decodes and ten measured decodes per process, improved from
3.356 to 3.246 seconds, with all three pairs faster.

The change favors sustained processing: first-decode 12-MP time increased from
0.877 to 1.018 seconds in its paired series. The 10-bit 4:2:2 sample's warmed
decode increased from 0.046 to 0.051 seconds. Peak memory did not materially change.
See the [paired results and protocol](https://github.com/bvdcode/Heic.NET/blob/main/docs/benchmark.md#residual-group-comparison).

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
