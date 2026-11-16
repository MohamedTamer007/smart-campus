import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { App } from "../src/app/App";
import { auth } from "../src/services/auth";
import { accountService } from "../src/services/frontend";
import { guestAccount, ServiceError, type Account } from "../src/services/contracts";

const account: Account = { id: "student", name: "Student", email: "", role: "user" };
afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); });

function show(current = account) {
  vi.spyOn(auth, "me").mockResolvedValue(current);
  render(<MemoryRouter><App /></MemoryRouter>);
}
const unreadLabel = "الإشعارات، توجد إشعارات غير مقروءة";

it("shows unread indicators on both bells and clears them after a read refresh", async () => {
  const status = vi.spyOn(accountService, "hasUnreadNotifications").mockResolvedValue(true);
  show();
  expect(await screen.findAllByRole("link", { name: unreadLabel }, { timeout: 5000 })).toHaveLength(2);
  status.mockResolvedValue(false);
  act(() => { window.dispatchEvent(new Event("smart-campus-notifications-changed")); });
  await waitFor(() => expect(screen.queryAllByRole("link", { name: unreadLabel })).toHaveLength(0));
});

it("refreshes on focus and preserves the unread indicator during a temporary failure", async () => {
  const status = vi.spyOn(accountService, "hasUnreadNotifications").mockResolvedValue(false);
  show();
  await waitFor(() => expect(status).toHaveBeenCalledOnce());
  status.mockResolvedValue(true);
  act(() => { window.dispatchEvent(new Event("focus")); });
  await waitFor(() => expect(screen.getAllByRole("link", { name: unreadLabel })).toHaveLength(2));
  status.mockRejectedValue(new ServiceError("unavailable"));
  await act(async () => { window.dispatchEvent(new Event("focus")); });
  expect(screen.getAllByRole("link", { name: unreadLabel })).toHaveLength(2);
});

it("does not request private notification status for guests", async () => {
  const status = vi.spyOn(accountService, "hasUnreadNotifications");
  show(guestAccount);
  await screen.findAllByRole("link", { name: "تسجيل الدخول" });
  expect(status).not.toHaveBeenCalled();
  expect(screen.queryAllByRole("link", { name: unreadLabel })).toHaveLength(0);
});

it("ignores an unread response that arrives after the session expires", async () => {
  let complete!: (unread: boolean) => void;
  const status = vi.spyOn(accountService, "hasUnreadNotifications").mockReturnValue(new Promise(resolve => { complete = resolve; }));
  show();
  await waitFor(() => expect(status).toHaveBeenCalledOnce());
  act(() => { window.dispatchEvent(new Event("smart-campus-session-expired")); });
  await act(async () => { complete(true); });
  expect(screen.queryAllByRole("link", { name: unreadLabel })).toHaveLength(0);
  expect(await screen.findByRole("heading", { name: "تسجيل الدخول" })).toBeTruthy();
});
