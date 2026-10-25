# Test images

`manifest.json` lists 20 complete HEIC files with expected visible dimensions,
SHA-256 of the original input, SHA-256 of every returned RGBA byte, the explicit
pixel limit and covered features. `CorpusTests` reads the manifest directly.

The 18 small generated files cover 8/10 bits, monochrome, deblocking, SAO, scaling
lists, all rotations, both mirror axes, combined orientation, clean aperture,
identity images, grids, alpha, rotated alpha and alpha grids. Their containers also
open with libheif 1.23.2. The two original camera files are preserved byte-for-byte:

| File | Visible dimensions | Grid | Reference |
| --- | --- | --- | --- |
| `20260201_164721.heic` | 4000×3000 | 48 tiles | Complete FFmpeg plane reconstruction and Q20 RGBA hash |
| `20260223_110514.heic` | 16320×12240 | 768 tiles | Complete FFmpeg plane reconstruction and Q20 RGBA hash |

The camera cases are 8-bit, full-range 4:2:0 with matrix 5, primaries 5 and transfer
6. Their RGBA references assemble the native 512×512 tile planes in grid order,
crop to the declared canvas, then apply the documented Q20 conversion.

## Elementary-stream sources

The codec fixtures contain one 64×48 IDR picture each: 8-bit 4:2:0, 10-bit 4:2:0,
and 8-bit monochrome. `pattern-*` disables deblocking; `deblock-*` enables it.
`sao-*` enables both deblocking and SAO. Wavefront coding is disabled in all fixtures.

The source pattern is original and covered by the repository license. For zero-based
coordinates, its 8-bit planes are:

- Y: `16 + (3*x + 5*y + 30*((x/16)%2)) % 220`, with integer division.
- Cb: `16 + (7*x + 3*y) % 225`, at half width and height.
- Cr: `16 + (2*x + 9*y) % 225`, at half width and height.

The 10-bit source multiplies these samples by four. The monochrome source uses Y.
Streams were encoded with x265 4.1+188 and decoded independently with FFmpeg.
`scaling-8bit.hevc` enables the standard default scaling lists.
`IntraPictureTests` checks SHA-256 of every reconstructed sample in planar Y/Cb/Cr
order (8-bit bytes or 10-bit little-endian words).

## Regeneration

Normal .NET builds and tests use the committed binary files and manifest; they do
not invoke Python or native codecs. To regenerate the small HEIF wrappers and their
independent references, use Python 3.12, NumPy and an explicit FFmpeg executable:

```sh
python corpus/create_fixtures.py --ffmpeg <ffmpeg executable>
```

The generator decodes the committed original elementary streams with FFmpeg, builds
the HEIF structures, and calculates RGB through separate Decimal/NumPy arithmetic.
It preserves the camera files and records their verified reference hashes. Camera
reference generation uses native per-tile decoding; it is not rerun by this command.
