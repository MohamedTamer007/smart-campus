import { test, expect } from "@playwright/test";

test("sign-in cannot create a session without a backend and keeps the entered email", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByText(/للتجربة|حساباً تجريبياً|كلمة المرور demo/)).toHaveCount(0);
  await page.getByRole("button", { name: "English", exact: true }).click();
  const email = page.getByRole("textbox", { name: /^University email/ });
  await email.fill("student@o6u.edu.eg");
  await page.locator('input[name="password"]').fill("example-password");
  const writes: string[] = [];
  page.on("request", (request) => { if (["POST", "PUT", "PATCH", "DELETE"].includes(request.method())) writes.push(request.url()); });
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("unavailable until the backend is connected");
  await expect(email).toHaveValue("student@o6u.edu.eg");
  await expect(page).toHaveURL(/\/login$/);
  expect(writes).toEqual([]);
  await expect(page.getByText(/demo password|demo sign-in|ahmed@o6u/i)).toHaveCount(0);
});

test("registration validates password confirmation before reporting the unavailable service", async ({ page }) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("textbox", { name: /^Name/ }).fill("   ");
  await page.getByRole("textbox", { name: /^University email/ }).fill("student@o6u.edu.eg");
  await page.locator('input[name="password"]').fill("example-password");
  await page.locator('input[name="confirmation"]').fill("different-password");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Enter your name");
  await page.getByRole("textbox", { name: /^Name/ }).fill("Student");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Passwords do not match");
  await page.locator('input[name="confirmation"]').fill("example-password");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("unavailable until the backend is connected");
  await expect(page).toHaveURL(/\/register$/);
});

test("recovery and password reset never report a false success", async ({ page }) => {
  await page.goto("/recover");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("textbox", { name: /^University email/ }).fill("student@o6u.edu.eg");
  await page.getByRole("button", { name: "Request recovery link" }).click();
  await expect(page.getByRole("alert")).toContainText("unavailable until the backend is connected");
  await expect(page.getByText(/preview demo reset/i)).toHaveCount(0);
  await page.goto("/reset/example-token");
  await page.locator('input[name="password"]').fill("example-password");
  await page.locator('input[name="confirmation"]').fill("example-password");
  await page.getByRole("button", { name: "Save password" }).click();
  await expect(page.getByRole("alert")).toContainText("unavailable until the backend is connected");
  await expect(page.getByRole("link", { name: "Request a new link" })).toHaveAttribute("href", "/recover");
  await page.goto("/verify");
  await page.getByRole("textbox", { name: /^University email/ }).fill("student@o6u.edu.eg");
  await page.getByRole("button", { name: "Resend verification link" }).click();
  await expect(page.getByRole("alert")).toContainText("unavailable until the backend is connected");
});
