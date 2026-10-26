# Basic-version support

| Area | Implemented boundary |
| --- | --- |
| Container | `heic`/`heix` brands; primary image; 16/32-bit item IDs; bounded file/idat extents; multiple extents; property associations and references |
| Image items | `hvc1`, `grid`, `iden`, `iovl`; ordered overlay layers with signed offsets and straight-alpha composition; right/bottom grid cropping; recursive derivation with depth and cycle checks |
| Geometry | All quarter-turn rotations; both mirror axes; exact integral `clap` rectangles, in association order |
| Alpha | Monochrome auxiliary alpha, including alpha grids and matching transformations; HEVC and MPEG alpha type identifiers; HEVC auxiliary SEI ranges, increment and clipping |
| HEVC pictures | Single layer; one IDR, CRA or BLA picture, NAL types 16–21, per coded image item; multiple independent I-slices; WPP row substreams |
| Samples | 8 or 10 bits, equal component depths; monochrome, 4:2:0, 4:2:2 or 4:4:4 |
| Reconstruction | CABAC, all 35 intra modes, grouped residual coding, sign hiding, QP deltas, scaling lists, DCT 4/8/16/32, intra luma DST4, transform skip and transquant bypass |
| Filtering | Intra deblocking and SAO band/edge offsets, including CTB merges |
| Colour | NCLX or HEVC VUI matrices 1, 5, 6, 9; BT.601 for unspecified matrix 2; full/limited range; chroma replication at the coded sampling ratio; RGBA8 |
| Colour metadata | Retained primaries, transfer code and ICC bytes; gamut conversion, ICC application and HDR tone mapping are absent |

Unsupported tools throw `UnsupportedHeicFeatureException` with a `Feature` value.
Examples include inter prediction, dependent slice segments, HEVC tiles,
multilayer coding, PCM, field sequences,
separate colour planes, other sample depths, active range-extension tools,
fractional clean apertures, external/protected items and premultiplied alpha.
HEIF grids are supported; HEVC tiles inside one coded picture are a separate tool.
Tracks, animation playback, EXIF/XMP interpretation and depth-map rendering are absent.
Pixel-aspect metadata does not rescale the returned sample grid.
Overlay inputs must share colour information. Coloured canvas fills require sRGB
or unspecified input colour information; conversion between profiles is absent.

Unknown essential image properties are rejected. Nonessential unknown properties
are skipped under the container rules. Descriptive `pixi` values do not override
the channel count or depth from the coded picture; essential `pixi` must agree. Unspecified YCbCr matrix code 2 uses BT.601 conversion, while retaining the
signalled full/limited range and colour metadata. Reserved matrix codes remain
unsupported. Single-layer Annex A decoding may discard VPS metadata;
the active SPS/PPS controls reconstruction. HEVC auxiliary
alpha SEI endpoints use the specified depth plus one bit. Legacy 8-bit endpoint
records with valid payload alignment are also accepted when the standard syntax
is invalid. The HEIF `prem` relationship determines premultiplication. Other SEI
messages are not applied to rendering.

The tests cover complete 8/10-bit and monochrome pictures, default scaling lists,
geometry, alpha and grid composition, plus two original camera files up to 200 MP.
WPP reconstruction has independent complete-plane references for 8/10-bit and
monochrome pictures, including partial CTU rows and mixed quantization groups.
Full-image 4:2:2 references include 8/10 bits, WPP, scaling lists, lossless bypass
and odd visible heights. Explicit custom scaling lists and transform skip have kernel coverage;
the complete-image corpus does not yet cover every combination of those tools.
CABAC slice termination is checked; the decoder is not a complete bitstream
conformance validator, and trailing zero-word/padding validation remains incomplete.

Full-image references use FFmpeg-decoded YUV and the documented RGBA conversion.
libheif 1.23.2 opens the generated containers. Complete RGBA comparisons also
verify visible dimensions and auxiliary alpha against libheif.

CRA and all three BLA NAL types have complete 8/10-bit image references decoded
with FFmpeg. Their POC and short/long-term reference syntax is consumed without
requiring other decoded pictures; only I-slice reconstruction is supported.

Two- and three-slice CRA/BLA pictures have complete 8/10-bit references with WPP.
Prediction and entropy contexts stop at independent slice boundaries; deblocking
and SAO follow each slice’s filter flags. Missing, duplicated and reordered slices
are rejected. Dependent slice segments remain unsupported.
