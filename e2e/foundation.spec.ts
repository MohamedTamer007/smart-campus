import { test, expect } from "@playwright/test";
test("mobile menu and fixed navigation follow the small-screen layout", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  const topNav = page.locator("#primary-navigation");
  await expect(topNav).toBeHidden();
  const bottomNav = page.getByRole("navigation", { name: "تنقل الهاتف" });
  await expect(bottomNav).toBeVisible();
  await expect(bottomNav.getByRole("link", { name: "طلباتي" })).toBeVisible();
  const menu = page.getByRole("button", { name: "فتح القائمة" });
  await menu.click();
  await expect(menu).toHaveAttribute("aria-expanded", "true");
  await expect(topNav.getByRole("link", { name: "الرئيسية" })).toBeVisible();
  await topNav.getByRole("link", { name: "الرئيسية" }).click();
  await expect(menu).toHaveAttribute("aria-expanded", "false");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("guest home follows the reference order with its sign-in and university registration paths", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(page.getByText("وضع الزائر", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "أهلاً بك في Smart Campus" })).toBeVisible();
  await expect(page.getByRole("link", { name: "سجّل الدخول لبدء الاستخدام" })).toHaveAttribute("href", "/login");
  await expect(page.getByRole("link", { name: "إنشاء حساب جامعي" })).toHaveAttribute("href", "/register");
  const hero = await page.locator(".guest-home .hero").boundingBox();
  const campus = await page.getByRole("img", { name: "مبنى الحرم الجامعي" }).boundingBox();
  const services = await page.locator(".guest-home .services").boundingBox();
  expect(hero).not.toBeNull();
  expect(campus).not.toBeNull();
  expect(services).not.toBeNull();
  expect(hero!.y + hero!.height).toBeLessThanOrEqual(campus!.y + 1);
  expect(campus!.y + campus!.height).toBeLessThanOrEqual(services!.y + 1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("the full sign-in link fits on phones, tablets and desktops", async ({ page }) => {
  for (const width of [320, 360, 375, 390, 414, 430, 760, 761, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("http://127.0.0.1:4174/");
    const signIn = width <= 760
      ? page.getByRole("navigation", { name: "تنقل الهاتف" }).getByRole("link", { name: "حسابي", exact: true })
      : page.getByRole("link", { name: "تسجيل الدخول", exact: true });
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
