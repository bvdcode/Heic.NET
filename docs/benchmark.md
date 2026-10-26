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

The server comparison dated 2026-10-26 (UTC) uses Heic.NET `20b1f67`, Openize
commit `1640f6ed26dd4038b86ed9d548a7b34c2cdd7ba1`, and libheif 1.23.2 with
libde265 1.1.1 through LibHeifSharp 3.2.0.

The host is a Debian 13 x64 KVM server with four AMD EPYC virtual CPUs and
.NET 10.0.13. Processes use CPU affinity 0–3 and `DOTNET_PROCESSOR_COUNT=4`;
libheif is configured for at most four decoding threads. Openize decodes
sequentially. The server has about 8 GB of RAM, including about 2.4 GB reserved
as huge pages, and no swap. Virtual-host scheduling and runtime tiering can still
affect results; these measurements do not guarantee throughput on other hardware.

Each decoder runs three times per input in a fresh process. Runs are sequential,
with the decoder order rotated between files and repetitions. All decoders load
the document, decode the primary image, apply supported geometry and alpha, and
return an owned RGBA8 array. Native interleaved rows are copied to that array.
Input file I/O, output hashing and pixel comparisons are outside the timed region.
First-use JIT and initialization are included. Peak RAM is the whole-process
high-water mark captured before hashing, including runtime and native memory.
MB means 1,000,000 bytes. Every output hash stayed identical across repetitions.
Native context disposal occurs after the measurements; Heic.NET completes internal
picture cleanup before returning. The difference in cleanup scope, along
with overlapping timing ranges, means the close 200-MP times do not establish a
speed advantage over libheif.

The two photographs are available in [the repository corpus](../corpus/README.md):
`20260201_164721.heic` (4000×3000) and `20260223_110514.heic` (16320×12240).
Each cell below gives **minimum / median / maximum seconds**, followed by the
highest peak RAM observed across the three runs.

| Decoder | 12 MP, seconds | Peak RAM, MB | 200 MP, seconds | Peak RAM, MB |
| --- | ---: | ---: | ---: | ---: |
| libheif | 0.336 / 0.440 / 0.792 | 141.4 | 4.887 / 4.925 / 5.006 | 1682.7 |
| Openize | 9.157 / 9.954 / 10.640 | 226.0 | 128.094 / 137.460 / 138.898 | 2428.3 |
| Heic.NET | 0.803 / 0.827 / 1.334 | 112.5 | 4.469 / 4.851 / 4.998 | 905.9 |

The same series also measured the following inputs. Each successful cell gives
**median seconds / maximum peak RAM in MB**. Incorrect dimensions and decode
errors are excluded from speed comparisons.

| Input | libheif | Openize | Heic.NET |
| --- | ---: | ---: | ---: |
| Display P3, 10-bit | 0.132 / 52.2 | Decode error | 0.351 / 56.8 |
| iPhone 15 Pro | 0.245 / 137.1 | 8.607 / 273.9 | 0.850 / 105.3 |
| 10-bit 4:2:2 | 0.116 / 60.4 | Decode error | 0.480 / 58.6 |
| 8-bit 4:4:4 | 0.099 / 47.1 | Decode error | 0.299 / 52.0 |
| Monochrome | 0.064 / 54.0 | Decode error | 0.229 / 52.0 |
| 8.5 MP, parallel HEVC rows | 0.757 / 145.9 | Decode error | 1.046 / 115.9 |
| 14 MP, parallel HEVC rows | 0.926 / 209.9 | Wrong dimensions | 1.047 / 145.0 |

These inputs are respectively `display-p3-10bit.heic`, `iphone_15_pro.heic`,
`camera-style-10bit-422.hif`, `chroma-444.heic`, `greyscale-monochrome.heic`,
`1718_rotate_90_cw.HEIC` and `7765_horizontal_normal.HEIC` from the sample set
described in [the corpus documentation](../corpus/README.md).

### Repeated decoding

On the same server and runtime, Heic.NET `20b1f67` was also measured after at least
64 warm-up decodes and five elapsed seconds per process. Three independent
processes each performed 25 measured decodes of the same loaded document. Every
decode reconstructs its picture and returns a new owned RGBA8 array; decoded pixels
are not cached. All warm-up and measured output hashes matched the accepted
dimensions and complete RGBA output.

| Input | Median decode, seconds | Range of process medians, seconds |
| --- | ---: | ---: |
| 12 MP photograph | 0.374 | 0.355–0.453 |
| iPhone 15 Pro | 0.152 | 0.151–0.165 |
| 8.5 MP, parallel HEVC rows | 0.369 | 0.351–0.370 |
| 14 MP, parallel HEVC rows | 0.435 | 0.429–0.509 |

Each table value is the median of the three process medians. These times exclude
document loading and cannot be compared directly with another decoder's first
decode. The included CLI's `warm` mode performs one preceding decode; it uses a
different warm-up protocol. A long decoding batch's lifetime peak RSS is also a
different measurement from the single-image peaks in the first-decode table.

### Residual-group comparison

Heic.NET `dfa4fff` packs the base magnitudes and signs of each residual group into
scalar registers. It was compared with `20b1f67` on the same server and runtime
in three alternating process pairs per input. Each process performed at least
64 warm-up decodes and five elapsed seconds, followed by 25 measured decodes.
Every complete RGBA hash and dimension matched the accepted output.

| Input | `20b1f67`, seconds | `dfa4fff`, seconds | Faster pairs |
| --- | ---: | ---: | ---: |
| 12 MP photograph | 0.373052 | 0.351449 | 3 / 3 |
| Display P3, 10-bit | 0.057783 | 0.049014 | 3 / 3 |
| iPhone 15 Pro | 0.159143 | 0.147249 | 3 / 3 |
| 10-bit 4:2:2 | 0.046295 | 0.050713 | 1 / 3 |
| 8-bit 4:4:4 | 0.051113 | 0.052291 | 2 / 3 |
| Monochrome | 0.038774 | 0.038675 | 2 / 3 |
| 8.5 MP, parallel HEVC rows | 0.373810 | 0.347535 | 3 / 3 |
| 14 MP, parallel HEVC rows | 0.453914 | 0.445163 | 2 / 3 |

These are medians of three process medians from 48 processes, with 4,547 exact
warm-up and measured outputs. The 4:2:2 sample regresses; the 4:4:4 and monochrome
results have mixed pair directions. No universal speed improvement is implied.

The separate 200-MP comparison uses eight warm-up decodes and five elapsed seconds,
then ten measured decodes per process. Across three alternating pairs (six processes,
108 exact outputs), median time decreased from 3.356313 to 3.246490 seconds;
CPU time decreased from 12.904516 to 12.597344 seconds. All three wall-time pairs
were faster, by 2.8–4.9%.

First-decode results use five alternating fresh-process pairs per input and include
document loading. On the 12-MP photograph, median time increased from 0.876687 to
1.017730 seconds, with all five pairs slower. The 200-MP first-decode medians were
4.638511 and 4.297627 seconds, with three of five pairs faster. Peak RAM did not
materially change. These first-decode and sustained comparisons are separate from
the cross-decoder series above.

The `dfa4fff` source also passes all 360 tests, all 84 accepted full RGBA hashes and
dimensions, and the same three expected negative rejections.

### Corpus coverage

The coverage and pairwise pixel comparison below were measured on 2026-10-25
with Heic.NET `142088c` on an Intel Core i5-12450H Linux host. Heic.NET `20b1f67`
was subsequently checked against all 84 accepted full RGBA hashes and dimensions,
with the same three negative rejections. The aggregate timing totals below belong
to the earlier workstation series and must not be mixed with the server timings.

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
