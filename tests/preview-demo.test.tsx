import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PreviewDemo } from "../src/preview-demo";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("isolated public preview", () => {
  it("simulates a maintenance report and staff resolution without contacting the backend", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch");
    const user = userEvent.setup();
    render(<PreviewDemo language="ar" />);
    expect(screen.getByText(/داخل الصفحة فقط/)).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "إضافة بلاغ تجريبي" }));
    expect(screen.getByText(/اتضاف بلاغ وهمي/)).toBeTruthy();
    expect(screen.getByText(/#103/)).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "الموظف" }));
    const updateButtons = screen.getAllByRole("button", { name: "تحديث الحالة" });
    expect(updateButtons.length).toBe(3);
    await user.click(updateButtons[0]);
    expect(screen.getByText(/#103/)).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "الإدارة" }));
    expect(screen.getByTestId("preview-admin")).toBeTruthy();
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("walks through synthetic recovery and reset without transmitting entered search", async () => {
    const fetchSpy = vi.spyOn(globalThis, "fetch");
    const user = userEvent.setup();
    render(<PreviewDemo language="en" />);
    await user.type(screen.getByRole("searchbox", { name: "Search sample items" }), "keys");
    expect(screen.getAllByRole("button", { name: "Simulate claim" })).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Simulate claim" }));
    await user.click(screen.getByRole("button", { name: "Staff" }));
    await user.click(screen.getByRole("button", { name: "Simulate return" }));
    await user.click(screen.getByRole("button", { name: "Admin" }));
    expect(screen.getByText("Returned items")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Reset demo" }));
    expect(screen.getByRole("button", { name: "Student" }).getAttribute("aria-pressed")).toBe("true");
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
