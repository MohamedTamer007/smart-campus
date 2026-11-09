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
