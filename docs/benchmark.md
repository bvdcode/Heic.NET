# Reproducible cold/warm comparison

From the repository root, using the .NET 10 SDK:

```sh
dotnet run --project Heic.Net.Benchmarks -c Release -- --input corpus/20260201_164721.heic --repetitions 5
```

For the 200-MP file, explicitly admit its output dimensions:

```sh
dotnet run --project Heic.Net.Benchmarks -c Release -- --input corpus/20260223_110514.heic --max-pixels 199756800 --repetitions 5
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
