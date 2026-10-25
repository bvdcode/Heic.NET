# Decoder map

## Path from file to pixels

| Stage | Main components | Verification |
| --- | --- | --- |
| Copy input and freeze HEIF metadata | `HeicDocument`, `HeifContainerReader`, `HeifItemBuilder`, `HeifContainer`, `HeifItem` | `ContainerReaderTests`: parent bounds, truncation, graph indices, extent concatenation, immutable snapshots |
| Select coded or derived images | `ImageProperties`, `ImageItemDecoder`, `GridImageDecoder` | `HeicDocumentTests`: cycles, limits, essential properties; `CorpusTests`: complete images |
| Unpack decoder configuration and NAL units | `DecoderConfiguration`, `NalPayload`, `ByteReader`, `BitReader` | `DecoderConfigurationTests`, `BitReaderTests`: lengths, headers, emulation prevention, signed bit patterns |
| Read reconstruction parameters | `SequenceParameters`, `PictureParameters`, `IntraSliceHeader`, `VideoInformation` | `ParameterSetTests`: native parameter sets, truncation, slice alignment, explicit unsupported tools |
| Decode CABAC bins | `CabacDecoder`, `CabacContext`, `IntraCabacReader` | `CabacDecoderTests`: decision vectors and an independent exact-interval arithmetic writer |
| Recover coefficient positions, magnitudes and signs | `CoefficientScan`, `CoefficientContexts`, `ResidualDecoder`, `ScalingLists` | `ResidualDecoderTests`, `ScalingListsTests`: grouped scans, Rice levels, sign hiding, explicit lists |
| Predict and reconstruct blocks | `IntraPictureDecoder`, `IntraModeDecoder`, `BlockReconstructor`, `IntraPredictor`, `IntegerTransform`, `QuantizationGroup` | `IntraPredictorTests`: independent coordinate oracle; `IntegerTransformTests`: dense matrix oracle; `IntraPictureTests`: complete native YUV hashes |
| Filter reconstructed planes | `DeblockingFilter`, `DeblockingKernel`, `SaoFilter`, `SaoKernel` | `DeblockingKernelTests`, `SaoKernelTests`: independent kernels and whole-picture hashes |
| Convert, compose and orient | `PictureRenderer`, `ColorConversion`, `GridImageDecoder`, `ImageTransforms` | `ColorConversionTests`: floating-point reference; `ImageTransformsTests`: labelled pixels; `CorpusTests`: full RGBA hashes |

HEVC coding units and transform units are traversed in recursive Z order; coding
tree blocks are visited in raster order. Each block publishes reconstructed samples
before a later block can use them as intra references. Deblocking runs vertically,
then horizontally. SAO sees the original deblocked neighbours, including when rows
are updated in place. Derived grids decode independent tiles with at most `Environment.ProcessorCount`
workers and crop their right and bottom edges to the declared canvas. Nested grids
inside a worker remain sequential; worker sets do not multiply with nesting.
The first tile establishes geometry before the remaining tiles start. Failures
wait for all active workers before returning the original exception type.

## Memory ownership

| Owner and lifetime | Storage | Reuse contract |
| --- | --- | --- |
| Document | One encoded-file copy; frozen item, property and NAL metadata | Shared read-only between calls; independent of the original input buffer or stream |
| One decode call | Immutable ancestor paths; concatenated item data when an item has several extents | Ancestors shared read-only by tile branches; item buffers remain call-local |
| One HEVC worker | CABAC contexts, block modes/depths/QPs, coefficient/prediction/residual scratch | Reused between blocks of this picture only |
| One reconstructed picture | Y/Cb/Cr `SamplePlane` arrays and availability maps | Owned arrays; never rented and never exposed as RGBA storage |
| Temporary worker rentals | `TransformWorkspace` intermediate array; SAO two-row buffer | Returned through `Dispose` or `finally`; no returned image retains them |
| Returned `RgbaImage` | Tightly packed RGBA8 and copied ICC profile | Caller-owned lifetime; subsequent calls do not overwrite it |

Loading a stream borrows a bounded copy buffer and leaves the stream open. Grid
composition keeps the canvas and at most one tile per active worker; it does not
cache every decoded tile. Parallel calls multiply working memory. A 16320×12240 RGBA8 result alone
contains 799,027,200 bytes; resource limits are admission limits, not a peak-RAM cap.

## Mathematical contracts

- Arrays use `index = y * stride + x`: x is the column increasing rightwards, y is
  the row increasing downwards. RGBA adds `x * 4 + channel`, ordered R/G/B/A.
- SPS `Width`/`Height` are coded luma dimensions, without an extra CTB border.
  `VisibleWidth`/`VisibleHeight` exclude the conformance window. Chroma dimensions
  are half the coded luma dimensions for 4:2:0. The final RGBA stride is `Width * 4`.
- Coefficients are row-major signed 16-bit values stored in `int`. Dequantization
  uses a 64-bit product, exact signed shifts and signed-16 clipping. The inverse
  transform performs vertical then horizontal passes; its first pass adds 64,
  shifts by 7 and clips to signed 16-bit. The second shifts by `20 - bitDepth`.
- Negative angular coordinates use floor-style arithmetic shifts. Reference
  substitution, filtering and prediction use the normative integer rounding.
  No unchecked `int` overflow is required for reconstruction; explicitly unchecked
  casts only reinterpret bit fields or signed file integers.
- Colour conversion uses Q20 coefficients, constructed once with nearest rounding.
  Each channel adds `2^19`, shifts by 20 and clips to 0..255. Limited range is
  Y=16..235 and C=16..240, scaled for 10 bits. Full range is 0..`2^depth-1`.
  Chroma uses nearest block replication, with one chroma sample per 2×2 luma block.
- Transform properties run in item-association order. Rotation is counterclockwise.
  HEIF 2022 mirror axis 0 reverses rows; axis 1 reverses columns. Clean-aperture
  dimensions and offsets must produce exact integer coordinates; all divisions
  are checked with integer remainders. No fractional resampling is approximated.
- Auxiliary alpha is normalized over its full sample range, independently of video
  colour-range flags. Returned pixels use straight, unpremultiplied alpha.

The corresponding algorithms carry their local contracts in XML remarks. The
normative codec reference is [ITU-T H.265](https://www.itu.int/rec/T-REC-H.265).
Container processing follows ISO/IEC 23008-12; the
[HEIF technical overview](https://nokiatech.github.io/heif/technical.html) describes
the relationships between coded items, derived items and image properties.
