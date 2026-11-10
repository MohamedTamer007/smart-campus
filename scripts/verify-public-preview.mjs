/* global process, URL, document, console */
import { chromium } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { join } from "node:path";

const origin = "http://smartcampus.tryasp.net/preview/";
const output = join(process.cwd(), "preview-browser-evidence");
await mkdir(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
try {
  for (const device of [
    { label: "desktop", viewport: { width: 1440, height: 900 } },
    { label: "mobile", viewport: { width: 390, height: 844 } },
  ]) {
    const context = await browser.newContext({ viewport: device.viewport, locale: "ar-EG" });
    const page = await context.newPage();
    const pageErrors = [];
    const apiCalls = [];
    page.on("pageerror", error => pageErrors.push(error.name));
    page.on("request", request => {
      if (new URL(request.url()).pathname.startsWith("/api/")) apiCalls.push("API request attempted");
    });

    let loaded = false;
    for (let attempt = 1; attempt <= 4; attempt++) {
      try {
        const result = await page.goto(origin, { waitUntil: "domcontentloaded", timeout: 20000 });
        if (result?.ok()) { loaded = true; break; }
      } catch {
        await page.waitForTimeout(150);
      }
      await page.waitForTimeout(1200);
    }
    if (!loaded) throw new Error("Public preview failed to load on " + device.label);

    await page.getByRole("heading", { name: "أهلاً بك في Smart Campus" }).waitFor();
    await page.getByRole("heading", { name: "تجربة تفاعلية بدون تسجيل" }).waitFor();
    await page.getByRole("button", { name: "إضافة بلاغ تجريبي" }).click();
    await page.getByText(/#103/).waitFor();

    await page.getByRole("button", { name: "الموظف" }).click();
    await page.getByRole("button", { name: "تحديث الحالة" }).first().click();
    await page.getByRole("button", { name: "الإدارة" }).click();
    await page.getByRole("heading", { name: "ملخص الإدارة (وهمي)" }).waitFor();

    await page.screenshot({ path: join(output, device.label + "-ar.png"), fullPage: true });
    await page.getByRole("button", { name: "English" }).click();
    await page.getByRole("heading", { name: "Sample admin overview" }).waitFor();
    await page.getByRole("button", { name: "Reset demo" }).click();
    await page.getByRole("button", { name: "Simulate claim" }).first().click();
    await page.getByRole("button", { name: "Staff" }).click();
    await page.getByRole("button", { name: "Simulate return" }).click();
    await page.getByRole("button", { name: "Admin" }).click();
    await page.getByRole("heading", { name: "Sample admin overview" }).waitFor();
    await page.screenshot({ path: join(output, device.label + "-en.png"), fullPage: true });

    if (apiCalls.length) throw new Error("Static preview unexpectedly attempted an API call.");
    if (pageErrors.length) throw new Error("Browser rendering errors: " + pageErrors.join(", "));
    const width = await page.evaluate(() => document.documentElement.scrollWidth);
    if (width > device.viewport.width + 2) {
      throw new Error("Horizontal overflow in " + device.label + " preview: " + width);
    }
    console.log(device.label + ": public browser role flows, Arabic/English, no API, no page errors, responsive width PASS");
    await context.close();
  }
} finally {
  await browser.close();
}
