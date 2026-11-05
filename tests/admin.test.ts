import { describe, expect, it } from "vitest";
import { calculateStats, directory } from "../src/services/admin";
import { guestAccount } from "../src/services/contracts";
import type { MaintenanceReport } from "../src/services/maintenance";

describe("administration frontend boundary", () => {
  it("does not invent an account directory or grant permissions", async () => {
    await expect(directory.list(guestAccount)).rejects.toMatchObject({ code: "unavailable" });
    await expect(directory.assign(guestAccount, { key: "user", role: "admin", scope: "", confirmAdmin: true })).rejects.toMatchObject({ code: "unavailable" });
  });
  it("derives counts from the supplied records and applies the scope filter", () => {
    const report: MaintenanceReport = { id: "r", ownerId: "u", description: "Fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "review", version: 1, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [] };
    const all = calculateStats([report], [], []);
    expect(all.totalReports).toBe(1);
    expect(all.byStatus.review).toBe(1);
    expect(all.locations.f).toBe(1);
    expect(calculateStats([report], [], [], { scope: "security" }).totalReports).toBe(0);
  });
});
