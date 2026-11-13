import { describe, expect, it } from "vitest";
import { MAX_CAMPUS_IMAGE_BYTES, validateCampusImage } from "../src/services/imageUpload";

function imageFile(name: string, mime: string, bytes: number[], size = bytes.length) {
  const file = new File([new Uint8Array(size)], name, { type: mime });
  Object.defineProperty(file, "slice", {
    configurable: true,
    value: () => ({ arrayBuffer: async () => new Uint8Array(bytes).buffer }),
  });
  return file;
}

describe("campus image upload validation", () => {
  it.each([
    ["JPEG", "image/jpeg", [0xff, 0xd8, 0xff, 0x00]],
    ["PNG", "image/png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]],
    ["WebP", "image/webp", [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50]],
  ])("accepts a signature-verified %s within the 5 MiB limit", async (_name, mime, bytes) => {
    expect(await validateCampusImage(imageFile("sample", mime, bytes))).toBeNull();
  });

  it("rejects oversized images before reading their contents", async () => {
    const file = imageFile("large.png", "image/png", [0x89], MAX_CAMPUS_IMAGE_BYTES + 1);
    expect(await validateCampusImage(file)).toBe("too_large");
  });

  it("rejects empty, unsupported, and MIME-spoofed files", async () => {
    expect(await validateCampusImage(imageFile("empty", "image/png", [0x89], 0))).toBe("empty");
    expect(await validateCampusImage(imageFile("text", "text/plain", [0x41]))).toBe("invalid_image");
    expect(await validateCampusImage(imageFile("spoof.jpg", "image/jpeg", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))).toBe("type_mismatch");
  });

  it("accepts a blank browser MIME type only when the bytes identify an allowed image", async () => {
    expect(await validateCampusImage(imageFile("sample", "", [0xff, 0xd8, 0xff]))).toBeNull();
  });

  it("accepts the exact size limit and reads browser files through FileReader when needed", async () => {
    expect(await validateCampusImage(imageFile("limit.jpg", "image/jpeg", [0xff, 0xd8, 0xff], MAX_CAMPUS_IMAGE_BYTES))).toBeNull();
    const file = new File([new Uint8Array([0xff, 0xd8, 0xff])], "photo.jpg", { type: "image/jpeg" });
    expect(await validateCampusImage(file)).toBeNull();
  });

  it("rejects unreadable files without an unhandled exception", async () => {
    const file = imageFile("broken.jpg", "image/jpeg", [0xff, 0xd8, 0xff]);
    Object.defineProperty(file, "slice", { value: () => { throw new Error("read failed"); } });
    expect(await validateCampusImage(file)).toBe("invalid_image");
  });
});
