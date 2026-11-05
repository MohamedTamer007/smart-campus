/* global process, console, document */
import { chromium } from '@playwright/test';
import { existsSync } from 'node:fs';

const executablePath = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ||
  (existsSync('/usr/bin/chromium') ? '/usr/bin/chromium' : undefined);
const browser = await chromium.launch({ executablePath, args: ['--no-sandbox'] });
const observations = [];
try {
  for (const width of [390, 768, 1440]) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    async function capture(name) {
      await page.screenshot({ path: `docs/screenshots/${name}-${width}.png`, fullPage: true });
      observations.push({ name, width, scroll: await page.evaluate(() => document.documentElement.scrollWidth) });
    }
    await page.goto('http://127.0.0.1:4175/');
    await capture('home-ar');
    await page.getByRole('button', { name: 'English', exact: true }).click();
    await capture('home-en');
    await page.getByRole('link', { name: 'Sign in', exact: true }).click();
    await capture('login-en');
    await page.getByRole('link', { name: 'Create a new account' }).click();
    await capture('register-en');
    await page.getByRole('link', { name: 'Forgot password?' }).click();
    await capture('recovery-en');
    await page.close();
  }
  console.log(JSON.stringify(observations));
  if (observations.some(({ width, scroll }) => scroll > width)) process.exitCode = 1;
} finally {
  await browser.close();
}
