/* global process, console, document */
import { chromium } from '@playwright/test';
import { existsSync } from 'node:fs';
(async () => {
  const executablePath = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || (existsSync('/usr/bin/chromium') ? '/usr/bin/chromium' : undefined);
  const browser = await chromium.launch({ executablePath, args: ['--no-sandbox'] });
  const observations = [];
  for (const width of [390, 768, 1440]) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    async function capture(name) {
      await page.screenshot({ path: `docs/screenshots/${name}-${width}.png`, fullPage: true });
      observations.push({ name, width, scroll: await page.evaluate(() => document.documentElement.scrollWidth) });
    }
    async function signIn(email) {
      await page.goto('http://127.0.0.1:4175/login');
      await page.getByRole('textbox', { name: /^University email/ }).fill(email);
      await page.locator('input[name=password]').fill('demo');
      await page.getByRole('button', { name: 'Sign in', exact: true }).click();
      await page.waitForURL('http://127.0.0.1:4175/');
    }
    await page.goto('http://127.0.0.1:4175/');
    await capture('home-ar');
    await page.getByRole('button', { name: 'English', exact: true }).click();
    await capture('home-en');
    await page.getByRole('link', { name: 'Create account', exact: true }).click();
    await capture('register-en');
    await signIn('ahmed@o6u.edu.eg');
    await page.getByRole('link', { name: 'Report a fault' }).click();
    await page.getByRole('textbox', { name: /Fault description/ }).waitFor();
    await capture('maintenance-en');
    await page.getByRole('link', { name: 'Home', exact: true }).click();
    await page.getByRole('link', { name: 'Find a lost item' }).click();
    await capture('search-en');
    await signIn('admin@o6u.edu.eg');
    await page.getByRole('link', { name: 'Admin', exact: true }).click();
    await page.getByText('Total maintenance reports').waitFor();
    await capture('admin-en');
    await page.close();
  }
  console.log(JSON.stringify(observations));
  await browser.close();
  if (observations.some(({ width, scroll }) => scroll > width)) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
