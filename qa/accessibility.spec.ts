import { test, expect } from "@playwright/test";

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
