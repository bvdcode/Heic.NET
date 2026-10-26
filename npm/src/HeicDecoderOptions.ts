export interface HeicDecoderOptions {
  /** Directory populated by the heic-net-assets command. */
  assetsUrl: string | URL;
  /** Maximum coded or visible pixels. The .NET decoder defaults apply when omitted. */
  maxPixels?: number;
}
