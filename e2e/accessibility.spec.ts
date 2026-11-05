import { test, expect } from "@playwright/test";
test("keyboard users can see focus and switch direction without a mouse", async ({
  page,
}) => {
  await page.goto("/");
  await page.keyboard.press("Tab");
  const skip = page.getByRole("link", { name: "انتقل للمحتوى" });
  await expect(skip).toBeFocused();
  const outline = await skip.evaluate((node) => ({
    width: getComputedStyle(node).outlineWidth,
    style: getComputedStyle(node).outlineStyle,
  }));
  expect(parseFloat(outline.width)).toBeGreaterThan(0);
  expect(outline.style).not.toBe("none");
  for (let i = 0; i < 12; i++) {
    if (
      await page
        .getByRole("button", { name: "English", exact: true })
        .evaluate((node) => document.activeElement === node)
    )
      break;
    await page.keyboard.press("Tab");
  }
  await expect(
    page.getByRole("button", { name: "English", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expect(
    page.getByRole("button", { name: "العربية", exact: true }),
  ).toBeFocused();
});
test("empty, failed read and retry are distinct in the user dashboard", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("user");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("link", { name: "My requests" }).click();
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Maintenance reports" })
      .locator("strong"),
  ).toHaveText("0");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate response", exact: true })
    .selectOption("error");
  await page.getByText("Demo controls", { exact: true }).click();
  await expect(
    page.getByText("Could not load data. Please retry."),
  ).toBeVisible();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate response", exact: true })
    .selectOption("normal");
  await page.getByText("Demo controls", { exact: true }).click();
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Maintenance reports" })
      .locator("strong"),
  ).toHaveText("0");
});
