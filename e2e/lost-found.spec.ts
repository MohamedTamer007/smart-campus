import { test, expect, type Page } from "@playwright/test";
async function actor(page: Page, role: string) {
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption(role);
  await page.getByText("Demo controls", { exact: true }).click();
}
test("department accepts custody, reviews a private claim and both parties confirm delivery", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "finder");
  await page.getByRole("link", { name: /Register a found item/ }).click();
  await page
    .locator("input[type=file]")
    .setInputFiles("e2e/fixtures/watch.png");
  await page
    .getByRole("textbox", { name: /Item description/ })
    .fill("Blue watch");
  await page
    .getByRole("textbox", { name: /Place found/ })
    .fill("Outside library");
  await page.locator("input[name=date]").fill("2025-01-01");
  await page
    .getByRole("textbox", { name: /Current storage/ })
    .fill("With finder");
  await page
    .getByRole("combobox", { name: /Proposed receiving department/ })
    .selectOption("security");
  await page
    .getByRole("textbox", { name: /Receiving office/ })
    .fill("Security desk");
  await page.getByRole("button", { name: "Register item" }).click();
  await expect(page.getByText(/Pending receipt/)).toBeVisible();
  await actor(page, "department");
  await page.getByRole("button", { name: "Confirm physical receipt" }).click();
  await expect(page.getByText(/Receipt confirmed/)).toBeVisible();
  await actor(page, "user");
  await page.getByRole("link", { name: "Home", exact: true }).click();
  await page.getByRole("link", { name: "Find a lost item" }).click();
  await expect(page.locator(".found-card")).toHaveCount(0);
  await page
    .getByRole("textbox", { name: /Lost item description/ })
    .fill("watch");
  await page.getByRole("button", { name: "Search for matches" }).click();
  await expect(page.locator(".found-card")).toHaveCount(1);
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate response", exact: true })
    .selectOption("error");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("textbox", { name: /Lost item description/ })
    .fill("umbrella");
  await page.getByRole("button", { name: "Search for matches" }).click();
  await expect(
    page.getByText("Service unavailable. Your input is kept; please retry."),
  ).toBeVisible();
  await expect(page.locator(".found-card")).toHaveCount(0);
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate response", exact: true })
    .selectOption("normal");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("textbox", { name: /Lost item description/ })
    .fill("watch");
  await page.getByRole("button", { name: "Search for matches" }).click();
  await page.locator(".found-card").first().click();
  await expect(page.getByText("Current custodian: Security")).toBeVisible();
  await expect(page.getByText("Security desk", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Send recovery request" }).click();
  await expect(page.getByText("Pending", { exact: true })).toBeVisible();
  await actor(page, "department");
  await page
    .getByRole("textbox", { name: /Collection place/ })
    .fill("Security desk");
  await page
    .getByRole("textbox", { name: /Collection instructions/ })
    .fill("Bring identification");
  await page.getByRole("button", { name: "Approve request" }).click();
  await expect(page.getByText("Approved — awaiting delivery")).toBeVisible();
  await actor(page, "user");
  await expect(page.getByText("Bring identification")).toBeVisible();
  await page.getByRole("button", { name: "I received the item" }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await expect(
    page.getByText("Waiting for custodian confirmation."),
  ).toBeVisible();
  await actor(page, "department");
  await page.getByRole("button", { name: "I delivered the item" }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await expect(page.getByText("Delivered", { exact: true })).toBeVisible();
});
test("found item text survives expiry and unknown department is honestly unavailable", async ({
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
    .fill("Preserve this description");
  await page.getByRole("textbox", { name: /Place found/ }).fill("Outside");
  await page.locator("input[name=date]").fill("2025-01-01");
  await page.getByRole("textbox", { name: /Current storage/ }).fill("In bag");
  await page
    .getByRole("combobox", { name: /Proposed receiving department/ })
    .selectOption("other");
  await expect(
    page.getByText(
      "Other registered departments are unavailable until the directory is supplied.",
    ),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: /Proposed receiving department/ })
    .selectOption("");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("expired");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Register item" }).click();
  await expect(
    page.getByRole("heading", { name: "Sign in", exact: true }),
  ).toBeVisible();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("normal");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("textbox", { name: /University email/ })
    .fill("ahmed@o6u.edu.eg");
  await page.locator("input[name=password]").fill("demo");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.getByRole("link", { name: /Register a found item/ }).click();
  await expect(
    page.getByRole("textbox", { name: /Item description/ }),
  ).toHaveValue("Preserve this description");
  await expect(page.getByRole("textbox", { name: /Place found/ })).toHaveValue(
    "Outside",
  );
});
test("a delivered student-held item describes its custodian in the past tense", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "finder");
  await page.getByRole("link", { name: /Register a found item/ }).click();
  await page
    .locator("input[type=file]")
    .setInputFiles("e2e/fixtures/watch.png");
  await page
    .getByRole("textbox", { name: /Item description/ })
    .fill("Student watch");
  await page.getByRole("textbox", { name: /Place found/ }).fill("Outside");
  await page.locator("input[name=date]").fill("2025-01-01");
  await page
    .getByRole("textbox", { name: /Current storage/ })
    .fill("With finder");
  await page.getByRole("button", { name: "Register item" }).click();
  await expect(
    page.getByRole("heading", { name: "Item details" }),
  ).toBeVisible();
  await actor(page, "user");
  await page.getByRole("link", { name: "Home", exact: true }).click();
  await page.getByRole("link", { name: "Find a lost item" }).click();
  await page
    .getByRole("textbox", { name: /Lost item description/ })
    .fill("watch");
  await page.getByRole("button", { name: "Search for matches" }).click();
  await page.locator(".found-card").first().click();
  await page.getByRole("button", { name: "Send recovery request" }).click();
  await actor(page, "finder");
  await page
    .getByRole("textbox", { name: /Contact phone/ })
    .fill("01000000000");
  await page.getByRole("button", { name: "Approve request" }).click();
  await page.getByRole("button", { name: "I delivered the item" }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await actor(page, "user");
  await page.getByRole("button", { name: "I received the item" }).click();
  await page.getByRole("button", { name: "Confirm", exact: true }).click();
  await actor(page, "finder");
  await page.getByRole("link", { name: "My requests" }).click();
  await page.getByRole("link", { name: "My found items" }).click();
  await page.locator(".found-card").first().click();
  await expect(
    page.getByText("The item was held by its previous custodian."),
  ).toBeVisible();
});
