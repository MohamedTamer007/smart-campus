import { test, expect } from "@playwright/test";

test("private staff and administration screens remain gated for guests", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  for (const route of ["/staff", "/admin", "/admin/accounts", "/admin/items"]) {
    await page.goto(route);
    await expect(page.getByRole("heading", { name: "Sign in required" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Sign in", exact: true }).last()).toBeVisible();
  }
});
