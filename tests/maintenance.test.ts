import { describe, expect, it } from "vitest";
import { guestAccount } from "../src/services/contracts";
import { maintenance, maintenanceInput } from "../src/services/maintenance";

describe("maintenance frontend boundary", () => {
  it("keeps the user's draft values when preparing a report", () => {
    const draft = { description: "Leaking tap", faculty: "f", building: "b", floor: "0", details: "Room 1" };
    expect(maintenanceInput(draft)).toEqual(draft);
    expect(maintenanceInput(draft)).not.toBe(draft);
  });
  it("does not return locations or save a report without a backend", async () => {
    const draft = { description: "Leaking tap", faculty: "f", building: "b", floor: "0", details: "Room 1" };
    await expect(maintenance.locations(guestAccount)).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.create(guestAccount, draft)).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.list(guestAccount)).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.detail(guestAccount, "r")).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.edit(guestAccount, { id: "r", version: 1, input: draft })).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.cancel(guestAccount, { id: "r", version: 1 })).rejects.toMatchObject({ code: "unavailable" });
    await expect(maintenance.update(guestAccount, { id: "r", version: 1, status: "review", assignee: "", note: "" })).rejects.toMatchObject({ code: "unavailable" });
  });
});
