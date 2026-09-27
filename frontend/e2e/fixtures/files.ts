import { randomUUID } from "node:crypto";

export type FileUpload = { name: string; mimeType: string; buffer: Buffer };

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const filler = Buffer.from(Array.from({ length: 251 }, (_, index) => index));

// The content depends only on the size and each name is unique unless one is given, so every upload of one size, from any
// spec and any run, deduplicates to one stored file.
export function png({ size = 256, name = `e2e-${randomUUID().slice(-12)}.png` } = {}): FileUpload {
  return {
    name,
    mimeType: "image/png",
    buffer: Buffer.concat([pngSignature, Buffer.alloc(size - pngSignature.length, filler)]),
  };
}
