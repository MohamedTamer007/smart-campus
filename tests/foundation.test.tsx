import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { App } from "../src/app/App";

afterEach(() => {
  cleanup();
  localStorage.clear();
});
describe("bilingual guest shell", () => {
  it("starts Arabic, hides the demo disclaimer and gates service actions behind login", async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );
    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.queryByText(/بيانات توضيحية/)).toBeNull();
    expect(screen.queryByText("أدوات التجربة")).toBeNull();
    await userEvent.click(screen.getByRole("link", { name: "الإبلاغ عن عطل" }));
    expect(screen.getByRole("heading", { name: "تسجيل الدخول" })).toBeTruthy();
  });
  it("keeps sign-in inputs and reports unavailable without granting a session", async () => {
    render(<MemoryRouter><App /></MemoryRouter>);
    await userEvent.click(screen.getByRole("link", { name: "تسجيل الدخول" }));
    const loginPanel = within(document.querySelector(".auth-login")!);
    await userEvent.type(loginPanel.getByLabelText(/البريد الجامعي/), "student@o6u.edu.eg");
    await userEvent.type(loginPanel.getByLabelText(/^كلمة المرور/), "example-password");
    await userEvent.click(loginPanel.getByRole("button", { name: "تسجيل الدخول" }));
    expect((await screen.findByRole("alert")).textContent).toContain("غير متاحة");
    expect((loginPanel.getByLabelText(/البريد الجامعي/) as HTMLInputElement).value).toBe("student@o6u.edu.eg");
    expect(screen.getByRole("heading", { name: "تسجيل الدخول" })).toBeTruthy();
  });
  it("offers guests an account creation path", () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("link", { name: "إنشاء حساب" }).getAttribute("href"),
    ).toBe("/register");
  });
  it("keeps English when navigating and places maintenance before lost and found", async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: "English" }));
    expect(document.documentElement.lang).toBe("en");
    expect(
      screen.getAllByRole("heading", { level: 2 }).map((x) => x.textContent),
    ).toEqual(["Maintenance", "Lost and found"]);
    await userEvent.click(screen.getByRole("link", { name: "Report a fault" }));
    expect(screen.getByRole("heading", { name: "Sign in" })).toBeTruthy();
    expect(localStorage.getItem("smart-campus-language")).toBe("en");
  });
});
