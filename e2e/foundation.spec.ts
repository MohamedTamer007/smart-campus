import { test, expect } from "@playwright/test";
test("the full sign-in link fits on phones, tablets and desktops", async ({ page }) => {
  for (const width of [320, 360, 375, 390, 414, 430, 760, 761, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("http://127.0.0.1:4174/");
    const signIn = page.getByRole("link", { name: "تسجيل الدخول", exact: true });
    await expect(signIn).toBeVisible();
    const bounds = await signIn.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.x).toBeGreaterThanOrEqual(0);
    expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
    expect(await signIn.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
    await signIn.click();
    await expect(page.getByRole("heading", { name: "تسجيل الدخول", exact: true })).toBeVisible();
  }
});

for (const width of [390, 768, 1440]) {
  test(`home remains usable at ${width}px in both languages`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/");
    await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
    await expect(
      page.getByRole("heading", { name: "الصيانة", exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `work/screenshots/home-ar-${width}.png`,
      fullPage: true,
    });
    await page.getByRole("button", { name: "English", exact: true }).click();
    await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
    await page.getByRole("link", { name: "Report a fault" }).click();
    await expect(
      page.getByRole("heading", { name: "Sign in", exact: true }),
    ).toBeVisible();
  });
}
