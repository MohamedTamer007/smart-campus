import { test, expect } from "@playwright/test";
test("register, verify, update account and logout in the explicit demo", async ({
  page,
}) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("textbox", { name: /^Name/ }).fill("Nour");
  await page
    .getByRole("textbox", { name: /^University email/ })
    .fill("Nour@O6U.edu.eg");
  await page.locator("input[name=password]").fill("example");
  await page.locator("input[name=confirmation]").fill("example");
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Verify university email" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Simulate verification", exact: true })
    .click();
  await page.getByRole("link", { name: "Go to home" }).click();
  await expect(
    page.getByRole("heading", { name: "Welcome, Nour" }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Account", exact: true }).click();
  await page.getByRole("textbox", { name: /^Display name/ }).fill("Nour Ali");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByText("Changes saved in this demo.")).toBeVisible();
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Sign in", exact: true }),
  ).toBeVisible();
});
test("recovery uses generic feedback and expired reset offers another link", async ({
  page,
}) => {
  await page.goto("/recover");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page
    .getByRole("textbox", { name: /^University email/ })
    .fill("unknown@o6u.edu.eg");
  await page.getByRole("button", { name: "Request recovery link" }).click();
  await expect(page.getByText(/If the email is registered/)).toBeVisible();
  await page.goto("/reset/expired");
  await expect(page.getByRole("alert")).toContainText("invalid or expired");
  await expect(
    page.getByRole("link", { name: "Request a new link" }),
  ).toHaveAttribute("href", "/recover");
});
test("expired reads invalidate simulated access and explain sign-in", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: /^Simulate account/ })
    .selectOption("user");
  await page
    .getByRole("combobox", { name: /^Simulate response/ })
    .selectOption("expired");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("link", { name: "Notifications", exact: true }).click();
  await expect(
    page.getByText("Session expired. Sign in again.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Sign in", exact: true }),
  ).toBeVisible();
});

test("invalid verification stays blocked until a new link is simulated", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("unverified");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.goto("/verify");
  // Reload resets the demo actor; choose it on the verification page itself.
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("unverified");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Show expired link" }).click();
  await page
    .getByRole("button", { name: "Simulate verification", exact: true })
    .click();
  await expect(
    page.getByText("Check required fields and the current record state.", {
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Verify university email" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Simulate resend link" }).click();
  await page
    .getByRole("button", { name: "Simulate verification", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Email verified in demo" }),
  ).toBeVisible();
});
test("switching demo accounts does not carry another accounts settings draft", async ({
  page,
}) => {
  await page.goto("/settings");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("user");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("textbox", { name: /^Display name/ })
    .fill("Unsaved student draft");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("maintenance");
  await expect(
    page.getByRole("textbox", { name: /^Display name/ }),
  ).toHaveValue("منى / Mona");
});
test("expiry on the already open login clears passwords but keeps the email", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page
    .getByRole("textbox", { name: /^University email/ })
    .fill("student@o6u.edu.eg");
  await page.locator("input[name=password]").fill("demo");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("expired");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(
    page.getByText("Session expired. Sign in again.", { exact: true }).first(),
  ).toBeVisible();
  await expect(page.locator("input[name=password]")).toHaveValue("");
  await expect(
    page.getByRole("textbox", { name: /^University email/ }),
  ).toHaveValue("student@o6u.edu.eg");
});
test("the built-in unverified actor can use services after verification", async ({
  page,
}) => {
  await page.goto("/verify");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption("unverified");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Simulate verification" }).click();
  await page.getByRole("link", { name: "Go to home" }).click();
  await page.getByRole("link", { name: "Report a fault" }).click();
  await page
    .getByRole("textbox", { name: /Fault description/ })
    .fill("Verified actor report");
  await page
    .getByRole("combobox", { name: /Faculty or facility/ })
    .selectOption("demo-faculty");
  await page
    .getByRole("combobox", { name: /Building/ })
    .selectOption("demo-building");
  await page.getByRole("combobox", { name: /Floor/ }).selectOption("ground");
  await page.getByRole("textbox", { name: /Precise location/ }).fill("Room");
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(page.getByText("Report saved in the demo.")).toBeVisible();
});
test("validation feedback follows the selected language after an error", async ({
  page,
}) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("textbox", { name: /^Name/ }).fill("Example");
  await page
    .getByRole("textbox", { name: /University email/ })
    .fill("invalid@gmail.com");
  await page.locator("input[name=password]").fill("x");
  await page.locator("input[name=confirmation]").fill("x");
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await expect(
    page.getByText("Use a valid email with the exact o6u.edu.eg domain."),
  ).toBeVisible();
  await page.getByRole("button", { name: "العربية", exact: true }).click();
  await expect(
    page.getByText("استخدم بريداً صحيحاً بالنطاق o6u.edu.eg فقط."),
  ).toBeVisible();
});
