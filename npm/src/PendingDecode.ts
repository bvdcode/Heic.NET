import type { HeicImage } from "./HeicImage.js";

export interface PendingDecode {
  resolve: (image: HeicImage) => void;
  reject: (error: Error) => void;
}
