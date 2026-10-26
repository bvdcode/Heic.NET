/** Caller-owned, straight-alpha RGBA8 pixels in row-major order. */
export interface HeicImage {
  width: number;
  height: number;
  pixels: Uint8ClampedArray<ArrayBuffer>;
}
