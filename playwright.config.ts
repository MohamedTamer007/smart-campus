import { existsSync } from "node:fs";
import { defineConfig } from "@playwright/test";
const executablePath =
  process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ||
  (existsSync("/usr/bin/chromium") ? "/usr/bin/chromium" : undefined);
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: "http://127.0.0.1:5174",
    launchOptions: {
      executablePath,
      args: ["--no-sandbox"],
    },
    trace: "retain-on-failure",
  },
  webServer: [
    {
      command: "npm run dev -- --mode e2e --port 5174 --strictPort",
      url: "http://127.0.0.1:5174",
      reuseExistingServer: false,
    },
    {
      command: "npm run build && npm run preview -- --port 4174 --strictPort",
      url: "http://127.0.0.1:4174",
      reuseExistingServer: false,
    },
  ],
});
