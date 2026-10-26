export interface HeicDecodedPixels {
  width: number;
  height: number;
  pixels: Uint8Array<ArrayBuffer>;
}

interface PixelMemoryView {
  slice: () => Uint8Array<ArrayBuffer>;
}

interface HeicAssemblyExports {
  Heic: {
    Net: {
      Browser: {
        HeicDecoder: {
          Decode: (bytes: Uint8Array, maxPixels: number) => HeicDecodedPixels;
        };
      };
    };
  };
}

interface HeicRuntime {
  setModuleImports: (
    name: string,
    imports: {
      createImage: (width: number, height: number, pixels: PixelMemoryView) => HeicDecodedPixels;
    },
  ) => void;
  getConfig: () => { mainAssemblyName: string };
  getAssemblyExports: (name: string) => Promise<HeicAssemblyExports>;
}

export interface HeicRuntimeModule {
  dotnet: { create: () => Promise<HeicRuntime> };
}
