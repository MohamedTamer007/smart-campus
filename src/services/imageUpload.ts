export const MAX_CAMPUS_IMAGE_BYTES = 5 * 1024 * 1024;
export const MAX_CAMPUS_IMAGE_PIXELS = 16_000_000;

const signatures: { mime: string; bytes: readonly number[]; webp?: readonly number[] }[] = [
  { mime: "image/jpeg", bytes: [0xff, 0xd8, 0xff] },
  { mime: "image/png", bytes: [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a] },
  { mime: "image/webp", bytes: [0x52, 0x49, 0x46, 0x46], webp: [0x57, 0x45, 0x42, 0x50] },
];

export type ImageUploadError = "empty" | "too_large" | "invalid_image" | "type_mismatch";

type ImageDimensions = { width: number; height: number };

async function decodedDimensions(file: File): Promise<ImageDimensions | null | undefined> {
  // Modern browsers expose createImageBitmap. If unavailable, leave final
  // decoding to the server rather than relying on an HTMLImageElement fallback
  // that is not consistently supported by embedded/test browser environments.
  if (typeof createImageBitmap !== "function") return undefined;
  const bitmap = await createImageBitmap(file);
  try {
    return { width: bitmap.width, height: bitmap.height };
  } finally {
    bitmap.close();
  }
}

export async function validateCampusImage(file: File): Promise<ImageUploadError | null> {
  if (file.size <= 0) return "empty";
  if (file.size > MAX_CAMPUS_IMAGE_BYTES) return "too_large";

  let header: Uint8Array;
  try {
    const blob = file.slice(0, 12);
    const contents = typeof blob.arrayBuffer === "function"
      ? await blob.arrayBuffer()
      : await new Promise<ArrayBuffer>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => reader.result instanceof ArrayBuffer
            ? resolve(reader.result) : reject(new Error("unreadable_image"));
          reader.onerror = () => reject(new Error("unreadable_image"));
          reader.onabort = () => reject(new Error("unreadable_image"));
          reader.readAsArrayBuffer(blob);
        });
    header = new Uint8Array(contents);
  } catch {
    return "invalid_image";
  }
  const signature = signatures.find(({ bytes, webp }) =>
    bytes.every((byte, index) => header[index] === byte) &&
    (!webp || webp.every((byte, index) => header[index + 8] === byte)),
  );
  if (!signature) return "invalid_image";
  if (file.type && file.type.toLowerCase() !== signature.mime) return "type_mismatch";

  try {
    const dimensions = await decodedDimensions(file);
    if (dimensions === null) return "invalid_image";
    if (dimensions && (
      dimensions.width <= 0 ||
      dimensions.height <= 0 ||
      dimensions.width * dimensions.height > MAX_CAMPUS_IMAGE_PIXELS
    )) return "invalid_image";
  } catch {
    return "invalid_image";
  }
  return null;
}
