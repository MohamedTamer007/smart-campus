import { describe, expect, it, vi } from "vitest";
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
      () => lostFound.approve(guestAccount, "claim", { phone: "01000000000", version: 1 }),
      () => lostFound.reject(guestAccount, "claim", "Reason", 1),
      () => lostFound.confirmDelivery(guestAccount, "claim", 1),
    ];
    for (const operation of operations)
      await expect(operation()).rejects.toMatchObject({ code: "unavailable" });
  });
  it("loads the authorized found-item page without prefetching every private image", async () => {
    const item: FoundItem = { id: "item-1", description: "Blue bottle", place: "Hall", date: "2026-11-13", storage: "Desk",
      uploaderId: "owner", custodianId: "owner", state: "available", createdAt: "2026-11-13T00:00:00Z", version: 1,
      mediaUrl: "/api/v1/media/photo-1" };
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(JSON.stringify({ items: [item] }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetchMock);
    try {
      const results = await lostFound.itemsFor(guestAccount);
      expect(results).toEqual([item]);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock.mock.calls[0][0]).toContain("/api/v1/found?page=1&pageSize=100");
    } finally {
      vi.unstubAllGlobals();
    }
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

  it("keeps backend similarity scores attached to item IDs and does not derive them from rank", async () => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async request => {
      const path = String(request);
      const body = path.endsWith("/csrf") ? { token: "synthetic-csrf" }
        : path.endsWith("/found/search") ? { itemIds: ["watch", "phone"], calibrated: false, candidates: [
          { itemId: "phone", score: 0.7455, textScore: 0.711, imageScore: 0.78 },
          { itemId: "watch", score: 0.821, textScore: 0.782, imageScore: 0.86 },
        ] }
          : { id: path.split("/").at(-1), description: "Synthetic item", mediaUrl: null };
      return new Response(JSON.stringify(body), { status: 200 });
    }));
    try {
      const result = await lostFound.search(guestAccount, "  Silver watch with red dial  ");
      expect(result.items.map(item => item.id)).toEqual(["watch", "phone"]);
      expect(result.similarities?.watch).toMatchObject({ score: 0.821, textScore: 0.782, imageScore: 0.86 });
      expect(result.similarities?.phone.score).toBe(0.7455);
      expect(result.calibrated).toBe(false);
      expect(result.queryDescription).toBe("Silver watch with red dial");
    } finally { vi.unstubAllGlobals(); }
  });

  it("passes through whether the server used keyword matching instead of the AI service", async () => {
    const respond = (mode?: string) => vi.fn<typeof fetch>(async request => new Response(JSON.stringify(
      String(request).endsWith("/csrf") ? { token: "synthetic-csrf" }
        : String(request).endsWith("/found/search") ? { itemIds: [], calibrated: false, mode, candidates: [] } : {},
    ), { status: 200 }));
    try {
      vi.stubGlobal("fetch", respond("keyword"));
      expect((await lostFound.search(guestAccount, "Silver watch")).mode).toBe("keyword");
      vi.stubGlobal("fetch", respond());
      expect((await lostFound.search(guestAccount, "Silver watch")).mode).toBe("semantic");
    } finally { vi.unstubAllGlobals(); }
  });

  it.each([null, 1.01, -1.01])("rejects malformed or out-of-range server similarity %s", async score => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async request => new Response(JSON.stringify(
      String(request).endsWith("/csrf") ? { token: "synthetic-csrf" }
        : { itemIds: ["watch"], candidates: [{ itemId: "watch", score, textScore: 0.8, imageScore: null }] },
    ), { status: 200 })));
    try { await expect(lostFound.search(guestAccount, "Silver watch")).rejects.toMatchObject({ code: "unavailable" }); }
    finally { vi.unstubAllGlobals(); }
  });
  it("counts custody for an account with a granted secretariat role and a primary user role", () => {
    const actor: Account = { id: "staff", name: "", email: "", role: "user", roles: ["secretariat"],
      departments: [{ id: "scope-1", code: "secretariat" }] };
    const incoming: FoundItem = { id: "1", description: "Item", place: "", date: "2025-01-01", storage: "", uploaderId: "owner",
      custodianId: "owner", state: "available", createdAt: "2025-01-01", version: 1,
      handover: { department: "secretariat", office: "Desk", status: "pending" } };
    const held: FoundItem = { ...incoming, id: "2", custodianDepartment: "secretariat", handover: undefined };

    expect(receiving(incoming, actor)).toBe(true);
    expect(currentCustodian(held, actor)).toBe(true);
    expect(departmentStats([incoming, held], [], actor)).toEqual({ held: 1, incoming: 1, pendingClaims: 0 });
  });
});
