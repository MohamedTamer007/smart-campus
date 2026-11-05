import { describe, expect, it } from "vitest";
import {
  MaintenanceDemo,
  type MaintenanceInput,
} from "../src/services/maintenance";
import { actors } from "../src/services/contracts";
const input: MaintenanceInput = {
  description: "Leaking tap",
  faculty: "demo-faculty",
  building: "demo-building",
  floor: "ground",
  details: "Beside the lab",
};
describe("maintenance workflow", () => {
  it("accepts no image and ground floor but rejects missing location or unknown hierarchy", async () => {
    const api = new MaintenanceDemo();
    const result = await api.create(actors.user, input);
    expect(result.status).toBe("submitted");
    expect(result.photo).toBeUndefined();
    await expect(
      api.create(actors.user, { ...input, details: " " }),
    ).rejects.toMatchObject({ code: "invalid" });
    await expect(
      api.create(actors.user, { ...input, floor: "invented" }),
    ).rejects.toMatchObject({ code: "invalid" });
  });
  it("does not mutate on failure and rejects unverified submissions", async () => {
    const api = new MaintenanceDemo();
    const before = await api.list(actors.user);
    await expect(api.create(actors.user, input, "error")).rejects.toMatchObject(
      { code: "unavailable" },
    );
    expect(await api.list(actors.user)).toHaveLength(before.length);
    await expect(api.create(actors.unverified, input)).rejects.toMatchObject({
      code: "forbidden",
    });
  });
  it("scopes reports and removes staff assessment from a reporter response", async () => {
    const people = structuredClone(actors);
    const api = new MaintenanceDemo(() => Object.values(people));
    const report = await api.create(actors.user, input);
    expect(await api.detail(actors.user, report.id)).not.toHaveProperty(
      "assessment",
    );
    expect(await api.detail(actors.maintenance, report.id)).toHaveProperty(
      "assessment",
    );
    await expect(
      api.detail({ ...actors.user, id: "another" }, report.id),
    ).rejects.toMatchObject({ code: "forbidden" });
    people.maintenance.department = "unrelated";
    await expect(
      api.detail(actors.maintenance, report.id),
    ).rejects.toMatchObject({ code: "forbidden" });
  });
  it("rejects stale edits after handling starts and requires a written rejection reason", async () => {
    const api = new MaintenanceDemo();
    const report = await api.create(actors.user, input);
    await api.update(actors.maintenance, {
      id: report.id,
      version: report.version,
      status: "review",
      assignee: "maintenance",
      note: "",
      decision: { type: "Plumbing", priority: "High", risk: "Medium" },
    });
    await expect(
      api.edit(actors.user, { id: report.id, version: report.version, input }),
    ).rejects.toMatchObject({ code: "conflict" });
    const latest = await api.detail(actors.maintenance, report.id);
    expect(latest.history.at(-1)?.assignee).toBe("maintenance");
    expect(latest.decision).toEqual({
      type: "Plumbing",
      priority: "High",
      risk: "Medium",
    });
    expect(await api.detail(actors.user, report.id)).not.toHaveProperty(
      "decision",
    );
    await expect(
      api.update(actors.maintenance, {
        id: report.id,
        version: latest.version,
        status: "rejected",
        assignee: "maintenance",
        note: "",
      }),
    ).rejects.toMatchObject({ code: "invalid" });
    await expect(
      api.cancel(actors.user, { id: report.id, version: latest.version }),
    ).rejects.toMatchObject({ code: "forbidden" });
  });
  it("saving a refreshed stale draft cannot overwrite authoritative history or version", async () => {
    const api = new MaintenanceDemo();
    const original = await api.create(actors.user, input);
    const oldDraft = { ...original, description: "Retained draft" };
    await api.edit(actors.user, {
      id: original.id,
      version: original.version,
      input: { ...input, description: "Concurrent edit" },
    });
    const latest = await api.detail(actors.user, original.id);
    const saved = await api.edit(actors.user, {
      id: original.id,
      version: latest.version,
      input: oldDraft,
    });
    expect(saved.version).toBe(3);
    expect(saved.history).toHaveLength(3);
    expect(saved.description).toBe("Retained draft");
  });
  it("cancellation is distinct from resolution and repeated stale actions cannot change terminal state", async () => {
    const api = new MaintenanceDemo();
    const report = await api.create(actors.user, input);
    await api.cancel(actors.user, { id: report.id, version: report.version });
    const cancelled = await api.detail(actors.user, report.id);
    expect(cancelled.status).toBe("cancelled");
    await expect(
      api.edit(actors.user, {
        id: report.id,
        version: cancelled.version,
        input,
      }),
    ).rejects.toMatchObject({ code: "forbidden" });
  });
});
