import { describe, expect, it } from "vitest";
import { LostFoundDemo, type FoundInput } from "../src/services/lostFound";
import { campus } from "../src/services/demo";
import { actors } from "../src/services/contracts";
const finder = { ...actors.user, id: "finder", name: "Finder" };
const other = { ...actors.user, id: "other", name: "Other" };
const input: FoundInput = {
  description: "blue watch",
  place: "Outside library",
  date: "2025-01-01",
  storage: "With me",
  photo: new File(["image"], "watch.png", { type: "image/png" }),
};
describe("private found-item and recovery workflow", () => {
  it("requires photo and a valid date; search requires description and never returns an inventory before a query", async () => {
    const api = new LostFoundDemo();
    await expect(
      api.register(finder, { ...input, photo: undefined }),
    ).rejects.toMatchObject({ code: "invalid" });
    await expect(
      api.register(finder, { ...input, date: "2025-02-30" }),
    ).rejects.toMatchObject({ code: "invalid" });
    const item = await api.register(finder, input);
    await expect(api.search(actors.user, "")).rejects.toMatchObject({
      code: "invalid",
    });
    await expect(api.detail(actors.user, item.id)).rejects.toMatchObject({
      code: "forbidden",
    });
    expect(await api.search(actors.user, "watch")).toHaveLength(1);
    expect(await api.search(actors.user, "umbrella")).toHaveLength(0);
    await expect(
      api.search(actors.user, "watch", "error"),
    ).rejects.toMatchObject({ code: "unavailable" });
  });
  it("transferring custody remains pending until the receiving department confirms", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, {
      ...input,
      destination: "security",
      office: "Office A",
    });
    expect(item.custodianId).toBe("finder");
    expect(item.handover?.status).toBe("pending");
    await expect(
      api.confirmHandover(actors.maintenance, item.id, item.version),
    ).rejects.toMatchObject({ code: "forbidden" });
    const accepted = await api.confirmHandover(
      actors.department,
      item.id,
      item.version,
    );
    expect(accepted.custodianDepartment).toBe("security");
    await api.search(actors.user, "watch");
    const claim = await api.claim(actors.user, item.id, "");
    await expect(
      api.approve(finder, claim.id, { phone: "123" }),
    ).rejects.toMatchObject({ code: "forbidden" });
    await expect(
      api.approve(actors.department, claim.id, { place: "", instructions: "" }),
    ).rejects.toMatchObject({ code: "invalid" });
  });
  it("removes a cancelled handover from the proposed department queue and access", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, {
      ...input,
      destination: "security",
      office: "Desk",
    });
    expect(await api.itemsFor(actors.department)).toHaveLength(1);
    await api.cancelHandover(finder, item.id, item.version);
    expect(await api.itemsFor(actors.department)).toHaveLength(0);
    await expect(api.detail(actors.department, item.id)).rejects.toMatchObject({
      code: "forbidden",
    });
    expect((await api.detail(finder, item.id)).id).toBe(item.id);
  });
  it("rejects a stale receipt confirmation after a handover is cancelled and reissued", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, {
      ...input,
      destination: "security",
      office: "Office A",
    });
    const cancelled = await api.cancelHandover(finder, item.id, item.version);
    await api.requestHandover(
      finder,
      item.id,
      "security",
      "Office B",
      cancelled.version,
    );
    await expect(
      api.confirmHandover(actors.department, item.id, item.version),
    ).rejects.toMatchObject({ code: "conflict" });
    const latest = await api.detail(finder, item.id);
    expect(latest.handover?.office).toBe("Office B");
    expect(latest.handover?.status).toBe("pending");
    expect(latest.custodianId).toBe(finder.id);
  });
  it("notifies the outgoing department when another department confirms a handover", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, {
      ...input,
      destination: "security",
      office: "Desk",
    });
    const firstReceipt = await api.confirmHandover(
      actors.department,
      item.id,
      item.version,
    );
    const nextTransfer = await api.requestHandover(
      actors.department,
      item.id,
      "secretariat",
      "Office",
      firstReceipt.version,
    );
    expect((await api.detail(actors.department, item.id)).custodianOffice).toBe(
      "Desk",
    );
    const before = (await campus.notifications(actors.department)).length;
    await api.confirmHandover(
      actors.secretariat,
      item.id,
      nextTransfer.version,
    );
    expect((await campus.notifications(actors.department)).length).toBe(
      before + 1,
    );
  });
  it("sends one receipt notification when the uploader is also the outgoing department", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(actors.department, {
      ...input,
      destination: "secretariat",
      office: "Office",
    });
    const before = (await campus.notifications(actors.department)).length;
    await api.confirmHandover(actors.secretariat, item.id, item.version);
    expect((await campus.notifications(actors.department)).length).toBe(
      before + 1,
    );
  });
  it("blocks a claimant who becomes the custodian through transfer from approving their own claim", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, input);
    await api.search(actors.department, "watch");
    const claim = await api.claim(actors.department, item.id, "");
    const transfer = await api.requestHandover(
      finder,
      item.id,
      "security",
      "Office A",
      item.version,
    );
    await api.confirmHandover(actors.department, item.id, transfer.version);
    await expect(
      api.approve(actors.department, claim.id, {
        place: "Office A",
        instructions: "Collect",
      }),
    ).rejects.toMatchObject({ code: "forbidden" });
    await expect(
      api.confirmDelivery(actors.department, claim.id),
    ).rejects.toMatchObject({ code: "forbidden" });
  });
  it("only current student custodian may approve; phone is required and a second approval is blocked", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, input);
    await api.search(actors.user, "watch");
    await api.search(other, "watch");
    const first = await api.claim(actors.user, item.id, "");
    const second = await api.claim(other, item.id, "");
    await expect(api.approve(finder, first.id, {})).rejects.toMatchObject({
      code: "invalid",
    });
    await api.approve(finder, first.id, { phone: "01000000000" });
    await expect(
      api.approve(finder, second.id, { phone: "01000000000" }),
    ).rejects.toMatchObject({ code: "conflict" });
    const otherClaims = await api.claims(other);
    expect(otherClaims[0].phone).toBeUndefined();
    expect(otherClaims[0].status).toBe("pending");
  });
  it("two independent confirmations close item only after both, making repeated actions harmless", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, input);
    await api.search(actors.user, "watch");
    const claim = await api.claim(actors.user, item.id, "");
    await api.approve(finder, claim.id, { phone: "01000000000" });
    await expect(api.confirmDelivery(other, claim.id)).rejects.toMatchObject({
      code: "forbidden",
    });
    const first = await api.confirmDelivery(actors.user, claim.id);
    expect(first.claimantConfirmed).toBe(true);
    expect(first.custodianConfirmed).toBe(false);
    expect(first.status).toBe("approved");
    expect((await api.confirmDelivery(actors.user, claim.id)).status).toBe(
      "approved",
    );
    const closed = await api.confirmDelivery(finder, claim.id);
    expect(closed.status).toBe("delivered");
    expect((await api.confirmDelivery(finder, claim.id)).status).toBe(
      "delivered",
    );
    expect(await api.search(other, "watch")).toHaveLength(0);
  });
  it("notifies the receiving department and both delivery parties without repeated events", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, {
      ...input,
      destination: "security",
      office: "Desk",
    });
    await api.confirmHandover(actors.department, item.id, item.version);
    await api.search(actors.user, "watch");
    const before = (await campus.notifications(actors.department)).length;
    const claim = await api.claim(actors.user, item.id, "");
    expect((await campus.notifications(actors.department)).length).toBe(
      before + 1,
    );
    await api.approve(actors.department, claim.id, {
      place: "Desk",
      instructions: "Collect",
    });
    await api.confirmDelivery(actors.user, claim.id);
    const after = (await campus.notifications(actors.department)).length;
    expect(after).toBe(before + 2);
    await api.confirmDelivery(actors.user, claim.id);
    expect((await campus.notifications(actors.department)).length).toBe(after);
    const claimantBefore = (await campus.notifications(actors.user)).length;
    await api.confirmDelivery(actors.department, claim.id);
    expect((await campus.notifications(actors.user)).length).toBe(
      claimantBefore + 1,
    );
  });
  it("prevents self and duplicate active claims and blocks a transfer during a reservation", async () => {
    const api = new LostFoundDemo();
    const item = await api.register(finder, input);
    await api.search(finder, "watch");
    await expect(api.claim(finder, item.id, "")).rejects.toMatchObject({
      code: "forbidden",
    });
    await api.search(actors.user, "watch");
    const claim = await api.claim(actors.user, item.id, "");
    await expect(api.claim(actors.user, item.id, "")).rejects.toMatchObject({
      code: "conflict",
    });
    await api.approve(finder, claim.id, { phone: "01000000000" });
    await expect(
      api.requestHandover(
        finder,
        item.id,
        "security",
        "Office A",
        item.version,
      ),
    ).rejects.toMatchObject({ code: "conflict" });
  });
});
it("offers claim actions only for eligible searched items without an active request", async () => {
  const api = new LostFoundDemo();
  const item = await api.register(actors.finder, {
    ...input,
    destination: "security",
    office: "Desk",
  });
  expect((await api.detail(actors.department, item.id)).canClaim).toBe(false);
  await api.search(actors.user, "watch");
  expect((await api.detail(actors.user, item.id)).canClaim).toBe(true);
  await api.claim(actors.user, item.id, "");
  expect((await api.detail(actors.user, item.id)).canClaim).toBe(false);
});
it("simultaneous approvals produce one reservation and leave the other request pending", async () => {
  const api = new LostFoundDemo();
  const item = await api.register(actors.finder, input);
  await api.search(actors.user, "watch");
  await api.search(actors.other, "watch");
  const first = await api.claim(actors.user, item.id, "");
  const second = await api.claim(actors.other, item.id, "");
  const results = await Promise.allSettled([
    api.approve(actors.finder, first.id, { phone: "01000000000" }),
    api.approve(actors.finder, second.id, { phone: "01000000000" }),
  ]);
  expect(
    results.filter((result) => result.status === "fulfilled"),
  ).toHaveLength(1);
  expect(results.filter((result) => result.status === "rejected")).toHaveLength(
    1,
  );
  expect(
    (await api.claims(actors.finder)).filter(
      (claim) => claim.status === "pending",
    ),
  ).toHaveLength(1);
});
it("custodian-first confirmation still waits for the claimant before closure", async () => {
  const api = new LostFoundDemo();
  const item = await api.register(actors.finder, input);
  await api.search(actors.user, "watch");
  const claim = await api.claim(actors.user, item.id, "");
  await api.approve(actors.finder, claim.id, { phone: "01000000000" });
  const waiting = await api.confirmDelivery(actors.finder, claim.id);
  expect(waiting.custodianConfirmed).toBe(true);
  expect(waiting.claimantConfirmed).toBe(false);
  expect(waiting.status).toBe("approved");
  expect((await api.confirmDelivery(actors.user, claim.id)).status).toBe(
    "delivered",
  );
});
