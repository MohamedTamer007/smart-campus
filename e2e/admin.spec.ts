import { test, expect, type Page } from "@playwright/test";
async function actor(page: Page, role: string) {
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption(role);
  await page.getByText("Demo controls", { exact: true }).click();
}
test("admin sees record-derived counts and changes a simulated scope", async ({
  page,
}) => {
  await page.goto("/admin");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "user");
  await expect(
    page.getByRole("heading", { name: "Access denied" }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Home", exact: true }).click();
  await page.getByRole("link", { name: "Report a fault" }).click();
  await page
    .getByRole("textbox", { name: /Fault description/ })
    .fill("Dashboard test fault");
  await page
    .getByRole("combobox", { name: /Faculty or facility/ })
    .selectOption("demo-faculty");
  await page
    .getByRole("combobox", { name: /Building/ })
    .selectOption("demo-building");
  await page.getByRole("combobox", { name: /Floor/ }).selectOption("ground");
  await page
    .getByRole("textbox", { name: /Precise location/ })
    .fill("Test room");
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(page.getByText("Report saved in the demo.")).toBeVisible();
  await actor(page, "admin");
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Total maintenance reports" })
      .locator("strong"),
  ).toHaveText("1");
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Reports under review" })
      .locator("strong"),
  ).toHaveText("0");
  await page
    .getByRole("combobox", { name: "Department scope" })
    .selectOption("security");
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Total maintenance reports" })
      .locator("strong"),
  ).toHaveText("0");
  await page
    .getByRole("combobox", { name: "Department scope" })
    .selectOption("maintenance");
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Total maintenance reports" })
      .locator("strong"),
  ).toHaveText("1");
  await page.getByRole("link", { name: "Accounts and permissions" }).click();
  await page.getByRole("combobox", { name: /Account/ }).selectOption("finder");
  await page.getByRole("combobox", { name: /Role/ }).selectOption("department");
  await page
    .getByRole("combobox", { name: /Department/ })
    .selectOption("security");
  await page.getByRole("button", { name: "Save demo permissions" }).click();
  await expect(page.getByRole("row").filter({ hasText: "Sara" })).toContainText(
    "Department staff",
  );
  await actor(page, "finder");
  await expect(page.getByRole("link", { name: "Work queue" })).toBeVisible();
});
test("a promoted demo admin edits the account actually shown in the selector", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "admin");
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByRole("link", { name: "Accounts and permissions" }).click();
  await page.getByRole("combobox", { name: /Account/ }).selectOption("user");
  await page.getByRole("combobox", { name: /Role/ }).selectOption("admin");
  await page
    .getByRole("checkbox", {
      name: "I confirm granting simulated administrator access.",
    })
    .check();
  await page.getByRole("button", { name: "Save demo permissions" }).click();
  await expect(
    page.getByRole("row").filter({ hasText: "Ahmed" }),
  ).toContainText("Administrator");
  await actor(page, "user");
  const accountSelector = page.getByRole("combobox", { name: /Account/ });
  await expect(accountSelector).toBeVisible();
  const selectedAccountName = (
    await accountSelector.locator("option:checked").textContent()
  )?.trim();
  expect(selectedAccountName).toBeTruthy();
  const selectedAccountRow = page
    .getByRole("row")
    .filter({ hasText: selectedAccountName! });
  await expect(selectedAccountRow).not.toContainText("Maintenance staff");
  await page
    .getByRole("combobox", { name: /Role/ })
    .selectOption("maintenance");
  await page.getByRole("button", { name: "Save demo permissions" }).click();
  await expect(selectedAccountRow).toContainText("Maintenance staff");
});
test("personal counts exclude other users items for an administrator", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "user");
  await page.getByRole("link", { name: /Register a found item/ }).click();
  await page
    .locator("input[type=file]")
    .setInputFiles("e2e/fixtures/watch.png");
  await page
    .getByRole("textbox", { name: /Item description/ })
    .fill("Blue watch");
  await page.getByRole("textbox", { name: /Place found/ }).fill("Outside");
  await page.locator("input[name=date]").fill("2025-01-01");
  await page
    .getByRole("textbox", { name: /Current storage/ })
    .fill("With user");
  await page.getByRole("button", { name: "Register item" }).click();
  await expect(
    page.getByRole("heading", { name: "Item details" }),
  ).toBeVisible();
  await actor(page, "admin");
  await page.getByRole("link", { name: "My requests" }).click();
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Registered or held items" })
      .locator("strong"),
  ).toHaveText("0");
  await page.getByRole("link", { name: "My found items" }).click();
  await expect(
    page.getByText("There are no items in this list."),
  ).toBeVisible();
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByRole("link", { name: "Found items", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Blue watch" })).toBeVisible();
});
test("a newly verified simulated account cannot see another demo users records", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "user");
  await page.getByRole("link", { name: "Report a fault" }).click();
  await page
    .getByRole("textbox", { name: /Fault description/ })
    .fill("Other user private fault");
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
  await page.getByRole("link", { name: "Account", exact: true }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await page.getByRole("link", { name: "Create a new account" }).click();
  await page.getByRole("textbox", { name: /^Name/ }).fill("New person");
  await page
    .getByRole("textbox", { name: /University email/ })
    .fill("new@o6u.edu.eg");
  await page.locator("input[name=password]").fill("example");
  await page.locator("input[name=confirmation]").fill("example");
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await page.getByRole("button", { name: "Simulate verification" }).click();
  await page.getByRole("link", { name: "Go to home" }).click();
  await page.getByRole("link", { name: "My requests" }).click();
  await expect(
    page
      .locator(".stat")
      .filter({ hasText: "Maintenance reports" })
      .locator("strong"),
  ).toHaveText("0");
});
test("account access editor requires an explicit role for each selected account", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "admin");
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByRole("link", { name: "Accounts and permissions" }).click();
  await page
    .getByRole("combobox", { name: /Account/ })
    .selectOption("maintenance");
  await expect(page.getByRole("combobox", { name: /Role/ })).toHaveValue("");
  await page.getByRole("button", { name: "Save demo permissions" }).click();
  await expect(page.getByRole("row").filter({ hasText: "Mona" })).toContainText(
    "Maintenance staff",
  );
});
test("administrator grants a newly registered account departmental scope", async ({
  page,
}) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await page.getByRole("textbox", { name: /^Name/ }).fill("New Staff");
  await page
    .getByRole("textbox", { name: /University email/ })
    .fill("newstaff@o6u.edu.eg");
  await page.locator("input[name=password]").fill("registration");
  await page.locator("input[name=confirmation]").fill("registration");
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await page.getByRole("button", { name: "Simulate verification" }).click();
  await page.getByRole("link", { name: "Go to home" }).click();
  await actor(page, "admin");
  await page.getByRole("link", { name: "Admin", exact: true }).click();
  await page.getByRole("link", { name: "Accounts and permissions" }).click();
  await page
    .getByRole("combobox", { name: /Account/ })
    .selectOption({ label: "New Staff" });
  await page.getByRole("combobox", { name: /Role/ }).selectOption("department");
  await page
    .getByRole("combobox", { name: /Department/ })
    .selectOption("security");
  await page.getByRole("button", { name: "Save demo permissions" }).click();
  await expect(
    page.getByRole("row").filter({ hasText: "New Staff" }),
  ).toContainText("Department staff");
  await page.getByRole("link", { name: "Account", exact: true }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await page
    .getByRole("textbox", { name: /University email/ })
    .fill("newstaff@o6u.edu.eg");
  await page.locator("input[name=password]").fill("demo");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("link", { name: "Work queue" })).toBeVisible();
});
