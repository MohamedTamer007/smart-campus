import { test, expect } from "@playwright/test";

test("lost-and-found records and claims remain private while no backend is connected", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  for (const route of ["/found", "/found/new", "/found/example", "/claims", "/claims/example"]) {
    await page.goto(route);
    await expect(page.getByRole("heading", { name: "Sign in required" })).toBeVisible();
    await expect(page.locator(".found-card")).toHaveCount(0);
  }
});
