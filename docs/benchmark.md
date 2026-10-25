# Reproducible cold/warm comparison

From the repository root, using the .NET 10 SDK:

```sh
dotnet run --project Heic.Net.Benchmarks -c Release -- --input corpus/20260201_164721.heic --repetitions 5
```

The 200-MP file also opens with the default limits:

```sh
dotnet run --project Heic.Net.Benchmarks -c Release -- --input corpus/20260223_110514.heic --repetitions 5
```

Every repetition starts separate cold and warm child processes, sequentially.
Encoded file I/O happens before the timed region in both modes.

- **Cold:** first document copy, metadata parse and primary decode in that process,
  including first-use JIT and initialization costs.
- **Warm:** load a document and decode it once, collect the discarded warm-up image,
  then time a second decode of the same document. Picture and output buffers are
  reconstructed afresh; decoded pixels are not cached.

Every sample records wall time, all-thread process CPU time, whole-process peak
resident working set, and total managed bytes allocated during the measured region.
CPU time has the operating system's resolution; very short samples can report zero.
Peak working set is the OS lifetime high-water mark and includes process startup
and warm-up. It is not a resettable allocation counter. Managed allocation totals
include all managed threads; native runtime memory is reflected in resident memory.
`ProcessMilliseconds` separately measures the entire child lifetime, including file
I/O, warm-up where applicable, hashing and reporting.

The tool saves all repetitions, input/output SHA-256, runtime, OS, architecture, CPU
count and GC mode to a unique JSON file under `artifacts/benchmarks`. It updates the
file after each completed sample and rejects differing output hashes. `--output`
selects another report directory. Reports are local measurement artifacts.

Compare the distributions of all repetitions under the same runtime, GC and machine
conditions. First-decode and warm-decode scopes intentionally differ by document
loading; the report stores those definitions. The warm result is not guaranteed to
be faster for a large image, where decoding, allocation and memory bandwidth dominate.

## Cross-decoder results

The comparison dated 2026-10-25 uses Heic.NET 0.1.2 (`142088c`), Openize
commit `1640f6ed26dd4038b86ed9d548a7b34c2cdd7ba1`, and libheif 1.23.2 with
libde265 1.1.1 through LibHeifSharp 3.2.0.

The host is an Intel Core i5-12450H with 12 logical processors, Linux x64 and
.NET 10.0.13. Processes use `DOTNET_PROCESSOR_COUNT=4`; libheif is configured
for at most four decoding threads. Openize decodes sequentially.
The container has a 24-GiB memory limit and an 11-CPU time quota. Unrelated
workloads remained active. Results should not be interpreted as isolated-machine
throughput or guarantees for other hardware.

Each decoder runs three times per input in a fresh process. Runs are sequential,
with the decoder order rotated between files and repetitions. All decoders load
the document, decode the primary image, apply supported geometry and alpha, and
return an owned RGBA8 array. Native interleaved rows are copied to that array.
Input file I/O, output hashing and pixel comparisons are outside the timed region.
First-use JIT and initialization are included. Peak RAM is the whole-process
high-water mark captured before hashing, including runtime and native memory.
MB means 1,000,000 bytes. Every output hash stayed identical across repetitions.

The two photographs are available in [the repository corpus](../corpus/README.md):
`20260201_164721.heic` (4000×3000) and `20260223_110514.heic` (16320×12240).
Each cell below gives **minimum / median / maximum seconds**, followed by the
highest peak RAM observed across the three runs.

| Decoder | 12 MP, seconds | Peak RAM, MB | 200 MP, seconds | Peak RAM, MB |
| --- | ---: | ---: | ---: | ---: |
| libheif | 0.307 / 0.371 / 0.372 | 141.2 | 3.376 / 4.699 / 4.718 | 1682.5 |
| Openize | 6.063 / 6.225 / 7.626 | 229.4 | 78.573 / 93.756 / 106.033 | 2419.2 |
| Heic.NET | 0.967 / 1.143 / 1.166 | 118.0 | 4.446 / 5.429 / 6.075 | 910.9 |

### Corpus coverage

The complete comparison contains 84 positive HEVC samples (83 unique input hashes)
and three negative samples: two damaged HEIC files and a JPEG with a `.heic`
extension, giving 783 fresh-process runs in total.
It covers camera photographs, grids, 8/10-bit images, monochrome, 4:2:0, 4:2:2,
4:4:4, orientation, clean aperture, overlays and alpha. AVIF is excluded.
Image sequences are tested only through their primary image. No process reached
the 180-second or 8-GiB benchmark safety limit. Opening status and output hashes
were stable across all repetitions.

Heic.NET opened 84/84 with expected dimensions. libheif opened 82/84; its default
decode path rejected the clean-aperture and conformance-window padding samples
with coded-size security errors (256×256 versus a 16,384-pixel bound, and
160×64 versus a 4,225-pixel bound). Openize returned expected dimensions
for 50/84, returned different dimensions for nine, and threw on 25. All three
decoders rejected all three negative samples.

For the same 50 samples with matching output dimensions from every decoder, the
sums of per-file median times were 13.856 seconds for libheif, 297.184 seconds for
Openize and 34.742 seconds for Heic.NET. This aggregate is scoped to
matching dimensions; it does not imply identical pixels.

Complete RGBA buffers were compared after the timed runs, with each buffer's hash
checked against the corresponding timed output. Across all 82 images decoded by
both libheif and Heic.NET, dimensions and alpha match exactly. RGB differs by at
most one 8-bit level on 80 images and by at most three levels on two 10-bit images;
five complete images are byte-for-byte identical. Both benchmark photographs
have a maximum RGB difference of one and identical alpha.

All 50 equal-sized Openize outputs were also compared against Heic.NET. Maximum
RGB differences range up to 31 levels; 18 images contain differences above ten
levels, and three alpha images contain different alpha values. These are pairwise
output comparisons, not a standard conformance verdict. Timing results retain
each decoder's actual output behavior.
