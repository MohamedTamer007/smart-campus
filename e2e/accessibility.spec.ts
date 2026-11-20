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
  const skip = page.getByRole("link", { name: "انتقل للمحتوى" });
  await skip.focus();
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


for (const route of ["/login", "/register"] as const) {
  test(`guest ${route} exposes labeled form fields, language metadata and a primary landmark`, async ({ page }) => {
    await page.goto(route);
    await expect(page.locator("html")).toHaveAttribute("lang", "ar");
    await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
    await expect(page.getByRole("main")).toHaveCount(1);
    await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toHaveCount(1);
    await expect(page.getByLabel(/البريد الجامعي/)).toBeVisible();
    await expect(page.getByLabel(/^كلمة المرور/)).toBeVisible();
    if (route === "/register") {
      await expect(page.getByLabel(/^تأكيد كلمة المرور\s*\*/)).toBeVisible();
      await expect(page.getByLabel(/^الاسم/)).toBeVisible();
    }
    await page.getByRole("button", { name: "English", exact: true }).click();
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
    await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
    await expect(page.getByLabel(/^University email/)).toBeVisible();
    await expect(page.getByLabel(/^Password/)).toBeVisible();
    if (route === "/register") {
      await expect(page.getByLabel(/^Confirm password/)).toBeVisible();
      await expect(page.getByLabel(/^Name/)).toBeVisible();
    }
    const unlabeledImages = await page.locator("main img").evaluateAll(
      (nodes) => nodes.filter((node) => !node.hasAttribute("alt"))
        .map((node) => node.getAttribute("src")),
    );
    expect(unlabeledImages).toEqual([]);
  });
}

test("guest primary content is reachable by skip link without a pointer", async ({ page }) => {
  await page.goto("/");
  const skip = page.getByRole("link", { name: "انتقل للمحتوى", exact: true });
  await skip.focus();
  await expect(skip).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/#main$/);
  await expect(page.locator("main#main")).toBeVisible();
});
