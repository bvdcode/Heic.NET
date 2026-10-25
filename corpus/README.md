# Decoder corpus

The codec fixtures contain one 64×48 IDR picture each: 8-bit 4:2:0, 10-bit 4:2:0,
and 8-bit monochrome. Deblocking, SAO and wavefront coding are disabled.

The source pattern is original and covered by the repository license. For zero-based
coordinates, its 8-bit planes are:

- Y: `16 + (3*x + 5*y + 30*((x/16)%2)) % 220`, with integer division.
- Cb: `16 + (7*x + 3*y) % 225`, at half width and height.
- Cr: `16 + (2*x + 9*y) % 225`, at half width and height.

The 10-bit source multiplies these samples by four. The monochrome source uses Y.
Streams were encoded with x265 4.1+188 and decoded independently with FFmpeg.
`IntraPictureTests` checks SHA-256 of every reconstructed sample in planar Y/Cb/Cr
order (8-bit bytes or 10-bit little-endian words). These are codec tests; HEIF
composition and RGBA output remain to be covered.
