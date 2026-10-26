export type HeicDecodeResponse =
  | { type: "decoded"; requestId: number; width: number; height: number; pixels: ArrayBuffer }
  | { type: "error"; requestId: number; message: string };
