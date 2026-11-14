import { useState } from "react";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { ImageField } from "../src/features/maintenance/Maintenance";
import { validateCampusImage, type ImageUploadError } from "../src/services/imageUpload";

vi.mock("../src/app/context", () => ({ useApp: () => ({ t: (_ar: string, en: string) => en }) }));
vi.mock("../src/services/imageUpload", () => ({ validateCampusImage: vi.fn() }));
afterEach(() => { cleanup(); vi.resetAllMocks(); });

function pendingValidation() {
  let resolve!: (error: ImageUploadError | null) => void;
  const promise = new Promise<ImageUploadError | null>((done) => { resolve = done; });
  return { promise, resolve };
}

it("does not reattach a photo when removal cancels its pending validation", async () => {
  const pending = pendingValidation();
  vi.mocked(validateCampusImage).mockReturnValue(pending.promise);
  const changed = vi.fn();
  function Form() {
    const [photo, setPhoto] = useState<File | undefined>(new File(["old"], "old.jpg"));
    return <ImageField photo={photo} onChange={(next) => { changed(next); setPhoto(next); }} />;
  }
  render(<Form />);
  const input = screen.getByLabelText(/^Fault photo/) as HTMLInputElement;
  fireEvent.change(input, { target: { files: [new File(["new"], "new.jpg")] } });
  expect(input.validity.customError).toBe(true);
  expect(screen.getByRole("status").textContent).toBe("Checking image…");
  fireEvent.click(screen.getByRole("button", { name: "Remove photo" }));
  expect(input.validity.customError).toBe(false);
  await act(async () => { pending.resolve(null); await pending.promise; });
  expect(changed).toHaveBeenCalledExactlyOnceWith(undefined);
  expect(screen.queryByRole("status")).toBeNull();
  expect(screen.queryByRole("button", { name: "Remove photo" })).toBeNull();
});

it("keeps the latest selected photo when earlier validation completes out of order", async () => {
  const first = pendingValidation();
  const second = pendingValidation();
  vi.mocked(validateCampusImage).mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
  const changed = vi.fn();
  render(<ImageField onChange={changed} />);
  const input = screen.getByLabelText(/^Fault photo/);
  const latest = new File(["latest"], "latest.jpg");
  fireEvent.change(input, { target: { files: [new File(["first"], "first.jpg")] } });
  fireEvent.change(input, { target: { files: [latest] } });
  await act(async () => { second.resolve(null); await second.promise; });
  await act(async () => { first.resolve("invalid_image"); await first.promise; });
  expect(changed).toHaveBeenCalledExactlyOnceWith(latest);
  expect(screen.queryByRole("alert")).toBeNull();
  expect(screen.queryByRole("status")).toBeNull();
});
