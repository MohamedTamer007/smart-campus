import { useEffect, type ReactNode } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AppProvider, useApp } from "../src/app/context";
import { AdminOverview } from "../src/features/admin/Admin";
import * as adminService from "../src/services/admin";
import * as http from "../src/services/http";
import type { Account } from "../src/services/contracts";

const admin: Account = {
  id: "keyboard-admin",
  name: "Admin",
  email: "admin@o6u.edu.eg",
  role: "admin",
};

function Session({ children }: { children: ReactNode }) {
  const { account, updateAccount } = useApp();
  useEffect(() => {
    if (account.id !== admin.id) updateAccount(admin);
  }, [account.id, updateAccount]);
  return account.id === admin.id ? children : null;
}

beforeEach(() => {
  localStorage.setItem("smart-campus-language", "en");
  vi.spyOn(adminService, "statistics").mockResolvedValue({
    totalReports: 0,
    byStatus: { submitted: 0, review: 0, repair: 0, resolved: 0, rejected: 0, cancelled: 0 },
    availableItems: 0,
    pendingClaims: 0,
    locations: {},
    faultTypes: {},
    buildings: [],
    unplacedBuildings: [{ id: "building-a", ar: "مبنى أ", en: "Building A", count: 0, x: null, y: null, version: 7 }],
    recurrence: [],
    weeklyTrend: [],
    openReports: 0,
    resolutionRate: 0,
    averageResolutionHours: null,
    oldestOpenDays: null,
  });
  vi.spyOn(adminService, "operations").mockResolvedValue({
    sampledAt: "2026-11-13T00:00:00Z",
    deployment: "test",
    workingSetBytes: 0,
    jobs: [],
    quotas: [],
    worker: "hosted_blocked",
    calibrationApproved: false,
  });
  vi.spyOn(adminService, "saveBuildingPosition").mockResolvedValue({ version: 8 });
  vi.spyOn(http, "departments").mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.restoreAllMocks();
});

it("lets an administrator place a building with arrow keys and saves the selected coordinates", async () => {
  const user = userEvent.setup();
  render(
    <MemoryRouter>
      <AppProvider>
        <Session><AdminOverview /></Session>
      </AppProvider>
    </MemoryRouter>,
  );

  const mapControl = await screen.findByRole("button", { name: "Set building position on the schematic" });
  await user.selectOptions(screen.getByLabelText("Building to position"), "building-a");
  expect((mapControl as HTMLButtonElement).disabled).toBe(false);

  mapControl.focus();
  await user.keyboard("{Shift>}{ArrowRight}{ArrowUp}{/Shift}");
  expect(await screen.findByText(/Position: 55\.0, 23\.0/)).toBeTruthy();

  await user.click(screen.getByRole("button", { name: "Save position" }));
  await waitFor(() => expect(adminService.saveBuildingPosition).toHaveBeenCalledWith("building-a", 55, 23, 7));
});
