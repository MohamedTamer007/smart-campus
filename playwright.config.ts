import { existsSync } from "node:fs";
import { defineConfig } from "@playwright/test";
const executablePath =
  process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ||
  (existsSync("/usr/bin/chromium") ? "/usr/bin/chromium" : undefined);
const integrated = process.env.SMART_CAMPUS_E2E === "1";
export default defineConfig({
  testDir: "./e2e",
  testMatch: integrated ? "**/integrated.spec.ts" : /^(?!.*integrated).*\.spec\.ts$/,
  fullyParallel: false,
  workers: 1,
  outputDir: integrated ? "test-results/browser-integrated" : "test-results/browser-static",
  reporter: [
    ["list"],
    ["html", { outputFolder: integrated ? "playwright-report/integrated" : "playwright-report/static", open: "never" }],
    ["junit", { outputFile: integrated ? "test-results/browser-integrated.xml" : "test-results/browser-static.xml" }],
  ],
  projects: [
    { name: "chromium", use: { browserName: "chromium" } },
    { name: "firefox", use: { browserName: "firefox" } },
    { name: "webkit", use: { browserName: "webkit" } },
  ],
  use: {
    baseURL: integrated ? "http://127.0.0.1:5080" : "http://127.0.0.1:4174",
    launchOptions: {
      executablePath,
      args: ["--no-sandbox"],
    },
    trace: "retain-on-failure",
  },
  webServer: integrated ? undefined : {
    command: "npm run build && npm run preview -- --port 4174 --strictPort",
    url: "http://127.0.0.1:4174",
    reuseExistingServer: false,
  },
});
