import { test, expect } from "@playwright/test";

test("maintenance actions require sign-in and expose no sample reports", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("link", { name: "Report a fault" }).click();
  await expect(page.getByRole("heading", { name: "Sign in", exact: true })).toBeVisible();
  await page.goto("/maintenance");
  await expect(page.getByRole("heading", { name: "Sign in required" })).toBeVisible();
  await expect(page.getByText(/demo facility|demo building/i)).toHaveCount(0);
});
