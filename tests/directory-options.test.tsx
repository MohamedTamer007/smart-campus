import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppProvider } from "../src/app/context";
import { DirectoryOptions } from "../src/components/DirectoryOptions";
import * as http from "../src/services/http";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
});

function renderDirectory(kind: "receiving" | "maintenance" | "all" = "receiving") {
  localStorage.setItem("smart-campus-language", "en");
  return render(
    <MemoryRouter>
      <AppProvider>
        <select aria-label="Department">
          <option value="">Choose department</option>
          <DirectoryOptions kind={kind} />
        </select>
      </AppProvider>
    </MemoryRouter>,
  );
}

describe("hosted campus directory states", () => {
  it("explains why the handover department dropdown has no options", async () => {
    vi.spyOn(http, "departments").mockResolvedValue([]);
    renderDirectory();
    expect(await screen.findByRole("option", {
      name: "No receiving departments are configured yet",
    })).toBeTruthy();
  });

  it("distinguishes an empty maintenance directory from receiving departments", async () => {
    vi.spyOn(http, "departments").mockResolvedValue([
      { id: "security", code: "security", ar: "الأمن", en: "Security", canReceive: true, maintenance: false },
    ]);
    renderDirectory("maintenance");
    expect(await screen.findByRole("option", {
      name: "No maintenance departments are configured yet",
    })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "Security" })).toBeNull();
  });

  it("shows a directory load failure rather than an empty list", async () => {
    vi.spyOn(http, "departments").mockRejectedValue(new Error("network unavailable"));
    renderDirectory();
    expect(await screen.findByRole("option", {
      name: "Department directory unavailable",
    })).toBeTruthy();
  });

  it("shows only authorized receiving department choices", async () => {
    vi.spyOn(http, "departments").mockResolvedValue([
      { id: "s", code: "security", ar: "الأمن", en: "Security", canReceive: true, maintenance: false },
      { id: "m", code: "maintenance", ar: "الصيانة", en: "Maintenance", canReceive: false, maintenance: true },
    ]);
    renderDirectory();
    expect(await screen.findByRole("option", { name: "Security" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "Maintenance" })).toBeNull();
  });
});
