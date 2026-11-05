import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
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
    expect(screen.getByText("أدوات التجربة")).toBeTruthy();
    await userEvent.click(screen.getByRole("link", { name: "الإبلاغ عن عطل" }));
    expect(screen.getByRole("heading", { name: "تسجيل الدخول" })).toBeTruthy();
  });
  it("greets the simulated actor rather than a fixed example name", async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByText("أدوات التجربة"));
    await userEvent.selectOptions(
      screen.getByLabelText("محاكاة الحساب"),
      "maintenance",
    );
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain(
      "منى",
    );
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
