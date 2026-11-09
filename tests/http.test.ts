import { afterEach, expect, it, vi } from "vitest";

afterEach(() => { vi.unstubAllGlobals(); vi.resetModules(); });

it("refreshes CSRF after login before changing account data", async () => {
  const writes: Headers[] = [];
  let issued = 0;
  vi.stubGlobal("fetch", vi.fn(async (path: string, init: RequestInit) => {
    if (path.endsWith("/csrf")) return new Response(JSON.stringify({ token: `token-${++issued}` }), { status: 200 });
    writes.push(new Headers(init.headers));
    return new Response(JSON.stringify({ name: "Saved" }), { status: 200 });
  }));
  const { api, json } = await import("../src/services/http");
  await api("/auth/login", json("POST", { email: "test@o6u.edu.eg", password: "synthetic-test-password" }));
  const saved = await api<{ name: string }>("/auth/profile", json("PUT", { name: "Saved", language: "ar" }));
  expect(saved.name).toBe("Saved");
  expect(writes.map(headers => headers.get("X-CSRF-TOKEN"))).toEqual(["token-1", "token-2"]);
});

it.each([
  [401, "expired"], [403, "forbidden"], [409, "conflict"], [429, "rate_limited"], [500, "internal"], [503, "unavailable"],
] as const)("preserves the distinct error outcome for HTTP %s", async (status, expected) => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("{}", { status })));
  const { api } = await import("../src/services/http");
  await expect(api("/maintenance")).rejects.toMatchObject({ code: expected });
});

it("network failure cannot become an empty successful result", async () => {
  vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
  const { api } = await import("../src/services/http");
  await expect(api("/found/search")).rejects.toMatchObject({ code: "unavailable" });
});
