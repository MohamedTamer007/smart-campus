import { DemoCampus } from "../src/services/demo";
import { describe, expect, it } from "vitest";
import { AdminDirectory, calculateStats } from "../src/services/admin";
import { actors } from "../src/services/contracts";
import { MaintenanceDemo } from "../src/services/maintenance";
import { LostFoundDemo } from "../src/services/lostFound";
describe("demo administration", () => {
  it("role assignment requires admin, registered scope, and explicit administrator confirmation", async () => {
    const directory = new AdminDirectory();
    await expect(
      directory.assign(
        actors.user,
        "finder",
        "maintenance",
        "maintenance",
        false,
      ),
    ).rejects.toMatchObject({ code: "forbidden" });
    await expect(
      directory.assign(actors.admin, "finder", "department", "unknown", false),
    ).rejects.toMatchObject({ code: "invalid" });
    await expect(
      directory.assign(actors.admin, "finder", "admin", "", false),
    ).rejects.toMatchObject({ code: "invalid" });
    await directory.assign(
      actors.admin,
      "finder",
      "department",
      "security",
      false,
    );
    expect(directory.actor("finder")).toMatchObject({
      id: "finder",
      role: "department",
      department: "security",
    });
    await directory.assign(actors.admin, "finder", "user", "", false);
    expect(directory.actor("finder").department).toBeUndefined();
  });
  it("dashboard counts distinguish status, availability and pending recovery from totals", async () => {
    const maintenance = new MaintenanceDemo();
    const found = new LostFoundDemo();
    const input = {
      description: "Fault",
      faculty: "demo-faculty",
      building: "demo-building",
      floor: "ground",
      details: "Room",
    };
    const report = await maintenance.create(actors.user, input);
    await maintenance.update(actors.maintenance, {
      id: report.id,
      version: report.version,
      status: "review",
      assignee: "",
      note: "",
    });
    const item = await found.register(actors.finder, {
      description: "blue watch",
      place: "Outside",
      date: "2025-01-01",
      storage: "With me",
      photo: new File(["x"], "x.png", { type: "image/png" }),
    });
    await found.search(actors.user, "watch");
    await found.claim(actors.user, item.id, "");
    const stats = calculateStats(
      await maintenance.list(actors.admin),
      await found.itemsFor(actors.admin),
      await found.claims(actors.admin),
    );
    expect(stats.totalReports).toBe(1);
    expect(stats.byStatus.review).toBe(1);
    expect(stats.byStatus.submitted).toBe(0);
    expect(stats.availableItems).toBe(1);
    expect(stats.pendingClaims).toBe(1);
  });
});
it("role changes are checked against current claimant scope before approval", async () => {
  const people = structuredClone(actors);
  const directory = new AdminDirectory(people);
  const api = new LostFoundDemo(() => Object.values(people));
  const item = await api.register(people.finder, {
    description: "watch",
    place: "Outside",
    date: "2025-01-01",
    storage: "With me",
    photo: new File(["x"], "x.png", { type: "image/png" }),
  });
  await api.search(people.user, "watch");
  const claim = await api.claim(people.user, item.id, "");
  const pending = await api.requestHandover(
    people.finder,
    item.id,
    "security",
    "Desk",
    item.version,
  );
  await api.confirmHandover(people.department, item.id, pending.version);
  await directory.assign(people.admin, "user", "department", "security", false);
  const actions = (await api.claimDetail(people.department, claim.id)).claim;
  expect(actions.canApprove).toBe(false);
  expect(actions.canReject).toBe(true);
  await expect(
    api.approve(people.department, claim.id, {
      place: "Desk",
      instructions: "Collect",
    }),
  ).rejects.toMatchObject({ code: "forbidden" });
  expect((await api.reject(people.department, claim.id)).status).toBe(
    "rejected",
  );
});

it("maintenance assignment follows current demo grants after demotion and promotion", async () => {
  const people = structuredClone(actors);
  const directory = new AdminDirectory(people);
  const api = new MaintenanceDemo(() => Object.values(people));
  const report = await api.create(people.user, {
    description: "Fault",
    faculty: "demo-faculty",
    building: "demo-building",
    floor: "ground",
    details: "Room",
  });
  await directory.assign(people.admin, "maintenance", "user", "", false);
  await directory.assign(
    people.admin,
    "finder",
    "maintenance",
    "maintenance",
    false,
  );
  await expect(
    api.update(people.admin, {
      id: report.id,
      version: report.version,
      status: "review",
      assignee: "maintenance",
      note: "",
    }),
  ).rejects.toMatchObject({ code: "invalid" });
  const saved = await api.update(people.admin, {
    id: report.id,
    version: report.version,
    status: "review",
    assignee: "finder",
    note: "",
  });
  expect(saved.assignee).toBe("finder");
});

it("does not strand an approved recovery when changing the claimant scope", async () => {
  const people = structuredClone(actors);
  const api = new LostFoundDemo(() => Object.values(people));
  const directory = new AdminDirectory(people, (candidate) =>
    api.canAssign(candidate),
  );
  const item = await api.register(people.finder, {
    description: "watch",
    place: "Outside",
    date: "2025-01-01",
    storage: "With me",
    photo: new File(["x"], "x.png", { type: "image/png" }),
  });
  await api.search(people.user, "watch");
  const claim = await api.claim(people.user, item.id, "");
  const pending = await api.requestHandover(
    people.finder,
    item.id,
    "security",
    "Desk",
    item.version,
  );
  await api.confirmHandover(people.department, item.id, pending.version);
  await api.approve(people.department, claim.id, {
    place: "Desk",
    instructions: "Collect",
  });
  await expect(
    directory.assign(people.admin, "user", "department", "security", false),
  ).rejects.toMatchObject({ code: "conflict" });
  expect(people.user.role).toBe("user");
  await expect(
    directory.assign(people.admin, "department", "user", "", false),
  ).rejects.toMatchObject({ code: "conflict" });
  expect(people.department.role).toBe("department");
});

it("admin can assign a verified registered demo account and the grant applies at next login", async () => {
  const people = structuredClone(actors);
  const identity = new DemoCampus();
  const registered = await identity.register({
    name: "New Person",
    email: "new.person@o6u.edu.eg",
    password: "x",
    confirmation: "x",
  });
  const verified = await identity.verify(registered, "normal");
  const directory = new AdminDirectory(people, () => true, identity);
  expect(
    (await directory.list(people.admin)).some(
      (entry) => entry.account.id === verified.id,
    ),
  ).toBe(true);
  await directory.assign(
    people.admin,
    verified.id,
    "department",
    "security",
    false,
  );
  expect(
    await identity.login("new.person@o6u.edu.eg", "demo", "normal"),
  ).toMatchObject({
    id: verified.id,
    role: "department",
    department: "security",
  });
});
it("revoked staff snapshots cannot read or update reports, items, or grants", async () => {
  const people = structuredClone(actors);
  const directory = new AdminDirectory(people);
  const reports = new MaintenanceDemo(() => Object.values(people));
  const found = new LostFoundDemo(() => Object.values(people));
  const report = await reports.create(people.user, {
    description: "Private",
    faculty: "demo-faculty",
    building: "demo-building",
    floor: "ground",
    details: "Room",
  });
  const item = await found.register(people.finder, {
    description: "watch",
    place: "Outside",
    date: "2025-01-01",
    storage: "With me",
    photo: new File(["x"], "x.png", { type: "image/png" }),
    destination: "security",
    office: "Desk",
  });
  const oldMaintenance = { ...people.maintenance };
  const oldDepartment = { ...people.department };
  await directory.assign(people.admin, "maintenance", "user", "", false);
  await directory.assign(people.admin, "department", "user", "", false);
  await expect(reports.detail(oldMaintenance, report.id)).rejects.toMatchObject(
    { code: "forbidden" },
  );
  await expect(
    reports.update(oldMaintenance, {
      id: report.id,
      version: report.version,
      status: "review",
      assignee: "",
      note: "",
    }),
  ).rejects.toMatchObject({ code: "forbidden" });
  await expect(found.detail(oldDepartment, item.id)).rejects.toMatchObject({
    code: "forbidden",
  });
  await expect(
    found.confirmHandover(oldDepartment, item.id, item.version),
  ).rejects.toMatchObject({ code: "forbidden" });
  await directory.assign(people.admin, "user", "admin", "", true);
  const oldAdmin = { ...people.user };
  await directory.assign(people.admin, "user", "user", "", false);
  await expect(directory.list(oldAdmin)).rejects.toMatchObject({
    code: "forbidden",
  });
  await expect(
    directory.assign(oldAdmin, "finder", "user", "", false),
  ).rejects.toMatchObject({ code: "forbidden" });
});
it("department dashboard separates pending receipt, confirmed custody and pending claims", async () => {
  const { departmentStats } = await import("../src/services/admin");
  const api = new LostFoundDemo();
  const item = await api.register(actors.finder, {
    description: "watch",
    place: "Outside",
    date: "2025-01-01",
    storage: "With me",
    photo: new File(["x"], "x.png", { type: "image/png" }),
    destination: "security",
    office: "Desk",
  });
  expect(
    departmentStats(
      await api.itemsFor(actors.department),
      await api.claims(actors.department),
      actors.department,
    ),
  ).toEqual({ held: 0, incoming: 1, pendingClaims: 0 });
  await api.confirmHandover(actors.department, item.id, item.version);
  await api.search(actors.user, "watch");
  await api.claim(actors.user, item.id, "");
  expect(
    departmentStats(
      await api.itemsFor(actors.department),
      await api.claims(actors.department),
      actors.department,
    ),
  ).toEqual({ held: 1, incoming: 0, pendingClaims: 1 });
  const claim = (await api.claims(actors.department))[0];
  await api.approve(actors.department, claim.id, {
    place: "Desk",
    instructions: "Collect",
  });
  await api.confirmDelivery(actors.user, claim.id);
  await api.confirmDelivery(actors.department, claim.id);
  expect(
    departmentStats(
      await api.itemsFor(actors.department),
      await api.claims(actors.department),
      actors.department,
    ),
  ).toEqual({ held: 0, incoming: 0, pendingClaims: 0 });
});
it("admin scope filter counts only records belonging to the selected demo department", async () => {
  const m = new MaintenanceDemo();
  const f = new LostFoundDemo();
  await m.create(actors.user, {
    description: "Fault",
    faculty: "demo-faculty",
    building: "demo-building",
    floor: "ground",
    details: "Room",
  });
  const item = await f.register(actors.finder, {
    description: "watch",
    place: "Outside",
    date: "2025-01-01",
    storage: "With me",
    photo: new File(["x"], "x.png", { type: "image/png" }),
    destination: "security",
    office: "Desk",
  });
  await f.confirmHandover(actors.department, item.id, item.version);
  await f.search(actors.user, "watch");
  await f.claim(actors.user, item.id, "");
  const reports = await m.list(actors.admin),
    items = await f.itemsFor(actors.admin),
    claims = await f.claims(actors.admin);
  const security = calculateStats(reports, items, claims, {
    scope: "security",
  });
  expect(security.totalReports).toBe(0);
  expect(security.availableItems).toBe(1);
  expect(security.pendingClaims).toBe(1);
  const maintenance = calculateStats(reports, items, claims, {
    scope: "maintenance",
  });
  expect(maintenance.totalReports).toBe(1);
  expect(maintenance.availableItems).toBe(0);
  expect(maintenance.pendingClaims).toBe(0);
});
