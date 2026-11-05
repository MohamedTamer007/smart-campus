import { describe, expect, it } from "vitest";
import { guestAccount, type Account } from "../src/services/contracts";
import { currentCustodian, departmentStats, lostFound, receiving, type FoundItem, type Claim } from "../src/services/lostFound";

describe("lost-and-found frontend boundary", () => {
  it("does not invent search results or claims without a backend", async () => {
    const operations = [
      () => lostFound.register(guestAccount, { description: "watch", place: "Hall", date: "2025-01-01", storage: "Desk" }),
      () => lostFound.search(guestAccount, "watch"),
      () => lostFound.detail(guestAccount, "item"),
      () => lostFound.itemsFor(guestAccount),
      () => lostFound.requestHandover(guestAccount, { id: "item", department: "security", office: "Desk", expectedVersion: 1 }),
      () => lostFound.cancelHandover(guestAccount, "item", 1),
      () => lostFound.confirmHandover(guestAccount, "item", 1),
      () => lostFound.notReceived(guestAccount, "item", 1),
      () => lostFound.claim(guestAccount, "item", ""),
      () => lostFound.claims(guestAccount),
      () => lostFound.claimDetail(guestAccount, "claim"),
      () => lostFound.approve(guestAccount, "claim", { phone: "01000000000" }),
      () => lostFound.reject(guestAccount, "claim"),
      () => lostFound.confirmDelivery(guestAccount, "claim"),
    ];
    for (const operation of operations)
      await expect(operation()).rejects.toMatchObject({ code: "unavailable" });
  });
  it("distinguishes incoming custody from held items in displayed statistics", () => {
    const actor: Account = { id: "staff", name: "", email: "", role: "department", department: "security" };
    const incoming: FoundItem = { id: "1", description: "Item", place: "", date: "2025-01-01", storage: "", uploaderId: "owner", custodianId: "owner", state: "available", createdAt: "2025-01-01", version: 1, handover: { department: "security", office: "Desk", status: "pending" } };
    const held: FoundItem = { ...incoming, id: "2", custodianDepartment: "security", handover: undefined };
    const claim: Claim = { id: "c", itemId: "2", claimantId: "other", claimantName: "", claimantRole: "user", createdAt: "2025-01-01", message: "", status: "pending", claimantConfirmed: false, custodianConfirmed: false };
    expect(receiving(incoming, actor)).toBe(true);
    expect(currentCustodian(incoming, actor)).toBe(false);
    expect(departmentStats([incoming, held], [claim], actor)).toEqual({ held: 1, incoming: 1, pendingClaims: 1 });
  });
});
