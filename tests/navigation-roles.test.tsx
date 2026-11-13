import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { App } from "../src/app/App";
import { auth } from "../src/services/auth";
import type { Account } from "../src/services/contracts";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
});

function renderApp(account: Account) {
  vi.spyOn(auth, "me").mockResolvedValue(account);
  render(
    <MemoryRouter initialEntries={["/"]}>
      <App />
    </MemoryRouter>,
  );
}

it("shows work queue and admin navigation for additional authorized roles", async () => {
  renderApp({
    id: "multi-role",
    name: "Multi-role staff member",
    email: "staff@o6u.edu.eg",
    role: "user",
    roles: ["maintenance", "admin"],
  });

  expect(await screen.findByRole("link", { name: "لوحة العمل" })).toBeTruthy();
  expect(screen.getByRole("link", { name: "الإدارة" })).toBeTruthy();
});

it("does not show staff or admin navigation for an ordinary user", async () => {
  renderApp({
    id: "ordinary-user",
    name: "Ordinary user",
    email: "user@o6u.edu.eg",
    role: "user",
  });

  await screen.findByText("مرحباً، Ordinary user");
  expect(screen.queryByRole("link", { name: "لوحة العمل" })).toBeNull();
  expect(screen.queryByRole("link", { name: "الإدارة" })).toBeNull();
});

it("shows custody queue switching to a secretariat user with another staff role", async () => {
  renderApp({
    id: "maintenance-secretariat",
    name: "Dual-role staff member",
    email: "staff@o6u.edu.eg",
    role: "user",
    roles: ["maintenance", "secretariat"],
  });

  await userEvent.click(await screen.findByRole("link", { name: "لوحة العمل" }));
  expect(await screen.findByRole("button", { name: "العهدة والمعثورات" })).toBeTruthy();
});
