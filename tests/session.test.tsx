import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, MemoryRouter } from "react-router-dom";
import { AppProvider, useApp } from "../src/app/context";
import { auth } from "../src/services/auth";
import type { Account } from "../src/services/contracts";

afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); });
const current: Account = { id: "current", name: "Current account", email: "", role: "user" };
const previous: Account = { id: "previous", name: "Previous account", email: "", role: "user" };
function Session() {
  const { account, updateAccount, sessionExpired } = useApp();
  return <><p>{account.name}</p><p>{sessionExpired ? "Expired session" : "Active session"}</p>
    <Link to="/next">Next screen</Link><button onClick={() => updateAccount(current)}>Complete sign-in</button></>;
}
function show() { render(<MemoryRouter><AppProvider><Session /></AppProvider></MemoryRouter>); }

it("navigation preserves the restored account without repeated session requests", async () => {
  const restore = vi.spyOn(auth, "me").mockResolvedValue(current);
  show();
  await screen.findByText("Current account");
  await userEvent.click(screen.getByRole("link", { name: "Next screen" }));
  expect(screen.getByText("Current account")).toBeTruthy();
  expect(restore).toHaveBeenCalledTimes(1);
});

it("late session restoration cannot overwrite a completed sign-in", async () => {
  let complete!: (account: Account) => void;
  vi.spyOn(auth, "me").mockReturnValue(new Promise(resolve => { complete = resolve; }));
  show();
  await userEvent.click(screen.getByRole("button", { name: "Complete sign-in" }));
  await act(async () => { complete(previous); });
  expect(screen.getByText("Current account")).toBeTruthy();
  expect(screen.queryByText("Previous account")).toBeNull();
});

it("late session restoration cannot revive an expired session", async () => {
  let complete!: (account: Account) => void;
  vi.spyOn(auth, "me").mockReturnValue(new Promise(resolve => { complete = resolve; }));
  show();
  act(() => { window.dispatchEvent(new Event("smart-campus-session-expired")); });
  await act(async () => { complete(previous); });
  expect(screen.getByText("Expired session")).toBeTruthy();
  expect(screen.queryByText("Previous account")).toBeNull();
});
