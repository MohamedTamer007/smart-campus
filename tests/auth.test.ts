import { describe, expect, it } from "vitest";
import { universityEmail, DemoCampus } from "../src/services/demo";
import { actors } from "../src/services/contracts";

describe("identity demo boundary", () => {
  it.each([
    ["  NAME@O6U.EDU.EG  ", true],
    ["a@o6u.edu.eg.evil.com", false],
    ["a@sub.o6u.edu.eg", false],
    ["a@@o6u.edu.eg", false],
    ["a b@o6u.edu.eg", false],
    ["@o6u.edu.eg", false],
    ["a@gmail.com", false],
    ['"a b"@o6u.edu.eg', true],
    ['"a@b"@o6u.edu.eg', true],
    ['"bad"quote@o6u.edu.eg', false],
    ["a".repeat(64) + "@o6u.edu.eg", true],
    ["a".repeat(65) + "@o6u.edu.eg", false],
    [".a@o6u.edu.eg", false],
    ["a..b@o6u.edu.eg", false],
    ["a.@o6u.edu.eg", false],
  ])("checks complete syntax and the exact domain: %s", (email, valid) =>
    expect(universityEmail(email)).toBe(valid),
  );
  it("wrong login credentials return a distinct generic credentials error", async () => {
    await expect(
      new DemoCampus().login("student@o6u.edu.eg", "wrong", "normal"),
    ).rejects.toMatchObject({ code: "credentials" });
  });
  it("does not let unrelated emails inherit a promoted demo actor role", async () => {
    const saved = actors.user.role;
    try {
      actors.user.role = "admin";
      await expect(
        new DemoCampus().login("unrelated@o6u.edu.eg", "demo", "normal"),
      ).rejects.toMatchObject({ code: "credentials" });
      expect(
        (await new DemoCampus().login(actors.user.email, "demo", "normal"))
          .role,
      ).toBe("admin");
    } finally {
      actors.user.role = saved;
    }
  });
  it("registration never grants staff and mismatched confirmation is rejected", async () => {
    const api = new DemoCampus();
    await expect(
      api.register({
        name: "Student",
        email: "student@o6u.edu.eg",
        password: "x",
        confirmation: "y",
      }),
    ).rejects.toMatchObject({ code: "invalid" });
    expect(
      (
        await api.register({
          name: "Student",
          email: "student@o6u.edu.eg",
          password: "x",
          confirmation: "x",
        })
      ).role,
    ).toBe("unverified");
  });
  it("a simulated failed request rejects rather than returning successful fixture data", async () => {
    const api = new DemoCampus();
    await expect(api.notifications(actors.user, "error")).rejects.toMatchObject(
      { code: "unavailable" },
    );
    await expect(
      api.notifications(actors.user, "expired"),
    ).rejects.toMatchObject({ code: "expired" });
  });
  it("notifications are scoped and unverified accounts cannot fetch private records", async () => {
    const api = new DemoCampus();
    expect(
      (await api.notifications(actors.user)).every(
        (n) => n.accountId === actors.user.id,
      ),
    ).toBe(true);
    await expect(api.notifications(actors.unverified)).rejects.toMatchObject({
      code: "forbidden",
    });
  });
});
it("verified demo registrations get separate identities and cannot read another user report", async () => {
  const { MaintenanceDemo } = await import("../src/services/maintenance");
  const api = new DemoCampus();
  const reports = new MaintenanceDemo();
  const existing = await reports.create(actors.user, {
    description: "Private",
    faculty: "demo-faculty",
    building: "demo-building",
    floor: "ground",
    details: "Room",
  });
  const registered = await api.register({
    name: "New user",
    email: "new@o6u.edu.eg",
    password: "secret",
    confirmation: "secret",
  });
  const verified = await api.verify(registered, "normal");
  expect(verified.id).not.toBe(actors.user.id);
  await expect(reports.detail(verified, existing.id)).rejects.toMatchObject({
    code: "forbidden",
  });
  expect((await api.login("new@o6u.edu.eg", "demo", "normal")).id).toBe(
    verified.id,
  );
});

it("a saved registered display name survives sign-out and same-session demo sign-in", async () => {
  const api = new DemoCampus();
  const registered = await api.register({
    name: "Before",
    email: "name-change@o6u.edu.eg",
    password: "x",
    confirmation: "x",
  });
  const verified = await api.verify(registered, "normal");
  await api.saveName(verified, "After", "normal");
  expect(
    (await api.login("name-change@o6u.edu.eg", "demo", "normal")).name,
  ).toBe("After");
});
it("the built-in unverified demo actor can use services after simulated verification", async () => {
  const { MaintenanceDemo } = await import("../src/services/maintenance");
  const api = new DemoCampus();
  const verifiedAccount = await api.verify(actors.unverified, "normal");
  await expect(api.verify(actors.unverified, "normal")).rejects.toMatchObject({
    code: "invalid",
  });
  const reports = new MaintenanceDemo(() => [
    ...Object.values(actors),
    ...api.registeredAccounts(),
  ]);
  expect(await reports.list(verifiedAccount)).toEqual([]);
});
