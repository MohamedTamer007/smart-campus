import { test, expect } from "@playwright/test";
test("production build does not expose demo controls in either language", async ({
  page,
}) => {
  await page.goto("http://127.0.0.1:4174/");
  await expect(page.getByText("أدوات التجربة", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("محاكاة الحساب")).toHaveCount(0);
  await page.getByRole("button", { name: "English", exact: true }).click();
  await expect(page.getByText("Demo controls", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("Simulate account")).toHaveCount(0);
  await expect(page.getByLabel("Simulate response", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("Simulate action response")).toHaveCount(0);
});

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
