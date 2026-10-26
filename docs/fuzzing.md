# Fuzzing

`Heic.Net.Fuzzing` exercises the public API with SharpFuzz 2.3.0 and libFuzzer.
The decoder package does not depend on either tool.

- `parse` loads the container and image metadata.
- `decode` also reconstructs and composes the primary image, then checks its pixel-buffer dimensions.

Both targets accept `InvalidDataException` and `UnsupportedHeicFeatureException`
as expected input rejections. Other exceptions remain failures. Expected
rejections are available through .NET trace listeners.

## Run

On Ubuntu 24.04 with the .NET 10 SDK, curl and GNU coreutils:

```sh
scripts/fuzz.sh parse 60
scripts/fuzz.sh decode 60
```

The script publishes a separate executable, instruments only its copy of
`Heic.Net.dll`, verifies the pinned driver download with SHA-256, and starts the
selected target. Inputs from `corpus/*.heic` and `corpus/regressions/*.heic` up to
64 KiB seed the run. Generated inputs stay in `artifacts/fuzz/<target>/corpus`;
failures stay in `artifacts/fuzz/<target>/findings`.

The target admits at most 64 KiB of encoded input, 262,144 pixels per image,
64 items and eight levels of image derivation. The runner sets a 256 MiB managed
GC heap limit, two logical processors, a ten-second input timeout and an overall
process timeout. The native driver's 512 MiB RSS limit and reported RSS describe
the driver process; they are not a total-memory guarantee for the managed child.
These test settings do not change the decoder's public defaults.

Each run stops at its first failure. Preserve that input before rerunning.
Replay a saved input against an ordinary, uninstrumented build:

```sh
dotnet run --project Heic.Net.Fuzzing -c Release -- replay decode <input-file>
```

A successful replay means either successful decoding or an expected input
rejection. Unexpected exceptions terminate the replay. After fixing a failure,
add its minimized input to `corpus/regressions` with a regression test describing
the expected result.

## Continuous checks

The fuzz workflow runs both targets for 60 seconds on pull requests, relevant
pushes to `main`, and manual dispatch. It saves generated corpora, findings and
logs. Unit tests verify that valid seeds reach both targets and that truncated
inputs receive expected rejections.

Finite fuzz runs do not prove format completeness, bounded CPU cost for every
input or the absence of decoder defects. They complement the independent pixel
references and malformed-input tests.

Tool documentation: [SharpFuzz](https://github.com/Metalnem/sharpfuzz) and
[libFuzzer integration](https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md).
