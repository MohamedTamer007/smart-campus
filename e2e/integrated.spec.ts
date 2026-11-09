import { test, expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";

type DemoAccounts = { password: string; accounts: Record<string, { id: string; email: string }>; facultyId: string; buildingId: string; floorId: string };
const fixture = () => JSON.parse(readFileSync("backend/SmartCampus.Api/local-data/demo-accounts.json", "utf8").replace(/^\uFEFF/, "")) as DemoAccounts;

async function login(page: Page, role: string) {
  const accounts = fixture();
  await page.goto("/login");
  const english = page.getByRole("button", { name: "English", exact: true });
  if (await english.isVisible()) await english.click();
  await page.getByLabel(/^University email/).fill(accounts.accounts[role].email);
  await page.getByLabel(/^Password \*/).fill(accounts.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "My requests", exact: true })).toBeVisible();
}

test("browser maintenance submission, staff assignment and resolution persist", async ({ page }) => {
  const accounts = fixture();
  await login(page, "student");
  await page.goto("/maintenance/new");
  await page.getByLabel(/^Fault description/).fill("DEMO ONLY browser regression leak");
  await page.getByLabel(/^Faculty or facility/).selectOption(accounts.facultyId);
  await page.getByLabel(/^Building/).selectOption(accounts.buildingId);
  await page.getByLabel(/^Floor/).selectOption(accounts.floorId);
  await page.getByLabel(/^Precise location/).fill("DEMO ONLY sink");
  await page.getByRole("button", { name: "Submit report", exact: true }).click();
  await expect(page.getByText("Report saved.", { exact: true })).toBeVisible();
  const reportPath = new URL(page.url()).pathname;
  await page.goto("/settings");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await login(page, "staff");
  await page.goto(reportPath);
  await page.getByLabel(/^Status/).selectOption("review");
  await page.getByLabel(/^Assign to/).selectOption(accounts.accounts.staff.id);
  await page.getByRole("button", { name: "Save update", exact: true }).click();
  await expect(page.getByLabel(/^Status/)).toHaveValue("review");
  await page.getByLabel(/^Status/).selectOption("repair");
  await page.getByRole("button", { name: "Save update", exact: true }).click();
  await expect(page.getByLabel(/^Status/)).toHaveValue("repair");
  await page.getByLabel(/^Status/).selectOption("resolved");
  await page.getByRole("button", { name: "Save update", exact: true }).click();
  await expect(page.getByRole("button", { name: "Save update", exact: true })).toHaveCount(0);
  await page.reload();
  await expect(page.getByText("Resolved", { exact: true })).toHaveCount(2);
});

test("browser logout revokes access and uncalibrated search stays unavailable", async ({ page }) => {
  await login(page, "student");
  await page.goto("/search");
  await page.getByLabel(/^Lost item description/).fill("DEMO ONLY distinctive blue bottle");
  await page.getByRole("button", { name: "Search for matches", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("temporarily unavailable");
  await page.goto("/settings");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await page.goto("/maintenance");
  await expect(page.getByRole("heading", { name: "Sign in required", exact: true })).toBeVisible();
});

test("browser found-item upload and department receipt preserve custody across sessions", async ({ page }) => {
  await login(page, "student");
  await page.goto("/found/new");
  // Committed synthetic UI screenshot; no personal image or external inference input.
  await page.getByLabel(/^Item photo/).setInputFiles("docs/screenshots/admin-operations.png");
  await page.getByLabel(/^Item description/).fill("DEMO ONLY browser custody fixture");
  await page.getByLabel(/^Place found/).fill("DEMO ONLY courtyard");
  await page.getByLabel(/^Date found/).fill("2026-01-01");
  await page.getByLabel(/^Current storage/).fill("DEMO ONLY finder desk");
  await page.getByLabel(/^Proposed receiving department/).selectOption({ label: "DEMO ONLY department" });
  await page.getByLabel(/^Receiving office or location/).fill("DEMO ONLY receiving desk");
  await page.getByRole("button", { name: "Register item", exact: true }).click();
  await expect(page.getByText(/Handover status: Pending receipt/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Cancel handover", exact: true })).toBeVisible();
  const itemPath = new URL(page.url()).pathname;
  await page.goto("/settings");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await login(page, "staff");
  await page.goto(itemPath);
  await page.getByRole("button", { name: "Item not received", exact: true }).click();
  await expect(page.getByText(/Handover status: Not received/)).toBeVisible();
  await page.getByRole("button", { name: "Confirm physical receipt", exact: true }).click();
  await expect(page.getByText(/Handover status: Receipt confirmed/)).toBeVisible();
  await page.reload();
  await expect(page.getByText(/Current department: demo-maintenance-/)).toBeVisible();
  await page.getByRole("button", { name: "العربية", exact: true }).click();
  await expect(page.getByRole("heading", { name: "عهدة الغرض", exact: true })).toBeVisible();
  await page.goto("/settings");
  await page.getByRole("button", { name: "تسجيل الخروج", exact: true }).click();
  await login(page, "student");
  await page.goto(itemPath);
  await expect(page.getByRole("button", { name: "Request departmental handover", exact: true })).toHaveCount(0);
  await expect(page.getByRole("img", { name: "Photo of the registered item", exact: true })).toBeVisible();
});
