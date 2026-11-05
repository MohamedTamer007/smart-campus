import { test, expect, type Page } from "@playwright/test";
async function actor(page: Page, role: string) {
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate account" })
    .selectOption(role);
  await page.getByText("Demo controls", { exact: true }).click();
}
test("report without a photo, review as staff, and track the new status", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "user");
  await page.getByRole("link", { name: "Report a fault" }).click();
  await page
    .getByRole("textbox", { name: /^Fault description/ })
    .fill("Tap leaking near lab");
  await page
    .getByRole("combobox", { name: /^Faculty or facility/ })
    .selectOption("demo-faculty");
  await page
    .getByRole("combobox", { name: /^Building/ })
    .selectOption("demo-building");
  await page.getByRole("combobox", { name: /^Floor/ }).selectOption("ground");
  await page
    .getByRole("textbox", { name: /^Precise location/ })
    .fill("Near the lab");
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(page.getByText("Report saved in the demo.")).toBeVisible();
  await expect(page.getByText("No photo attached.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit report" })).toBeVisible();
  await page.getByRole("button", { name: "Edit report" }).click();
  await page
    .getByRole("textbox", { name: /^Fault description/ })
    .fill("Draft across conflict");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("conflict");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(
    page.getByText(
      "Record changed. Reload and review its state before retrying.",
    ),
  ).toBeVisible();
  await page.getByRole("button", { name: "Refresh current report" }).click();
  await expect(
    page.getByRole("heading", { name: "Report details" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Edit report" }).click();
  await expect(
    page.getByRole("textbox", { name: /^Fault description/ }),
  ).toHaveValue("Draft across conflict");
  await page.getByRole("button", { name: "Cancel editing" }).click();
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("normal");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Edit report" }).click();
  await page
    .getByRole("textbox", { name: /^Fault description/ })
    .fill("Cancelled edit");
  await page.getByRole("button", { name: "Cancel editing" }).click();
  await page.getByRole("button", { name: "Edit report" }).click();
  await expect(
    page.getByRole("textbox", { name: /^Fault description/ }),
  ).toHaveValue("Tap leaking near lab");
  await page.getByRole("button", { name: "Cancel editing" }).click();
  await page.getByRole("button", { name: "Edit report" }).click();
  await page
    .getByRole("textbox", { name: /^Fault description/ })
    .fill("Updated leaking tap");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(
    page.getByRole("heading", { name: "Report details" }),
  ).toBeVisible();
  await expect(page.getByText("Updated leaking tap")).toBeVisible();
  await actor(page, "maintenance");
  await expect(
    page.getByRole("heading", { name: "Update report" }),
  ).toBeVisible();
  await expect(
    page.getByText("Demo facility · Demo building · Ground floor"),
  ).toBeVisible();
  await page.getByRole("button", { name: "Save update" }).click();
  await expect(page.getByText("Under review").first()).toBeVisible();
  await actor(page, "user");
  await expect(page.getByText("Under review").first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit report" })).toHaveCount(
    0,
  );
  await expect(
    page.getByRole("heading", { name: /Analysis suggestions/ }),
  ).toHaveCount(0);
});
test("failed submission preserves entered text and does not show success", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English", exact: true }).click();
  await actor(page, "user");
  await page.getByRole("link", { name: "Report a fault" }).click();
  await page
    .getByRole("textbox", { name: /^Fault description/ })
    .fill("Broken door");
  await page
    .getByRole("combobox", { name: /^Faculty or facility/ })
    .selectOption("demo-faculty");
  await page
    .getByRole("combobox", { name: /^Building/ })
    .selectOption("demo-building");
  await page.getByRole("combobox", { name: /^Floor/ }).selectOption("ground");
  await page.getByRole("textbox", { name: /^Precise location/ }).fill("Room 1");
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("error");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(
    page.getByText("Service unavailable. Your input is kept; please retry."),
  ).toBeVisible();
  await page.getByRole("button", { name: "العربية", exact: true }).click();
  await expect(
    page.getByText("الخدمة غير متاحة. احتُفظ بمدخلاتك؛ حاول مجدداً."),
  ).toBeVisible();
  await page.getByRole("button", { name: "English", exact: true }).click();
  await expect(
    page.getByRole("textbox", { name: /^Fault description/ }),
  ).toHaveValue("Broken door");
  await expect(page.getByText("Report saved in the demo.")).toHaveCount(0);
  await page.getByText("Demo controls", { exact: true }).click();
  await page
    .getByRole("combobox", { name: "Simulate action response" })
    .selectOption("normal");
  await page.getByText("Demo controls", { exact: true }).click();
  await page.getByRole("button", { name: "Submit report" }).click();
  await expect(page.getByText("Report saved in the demo.")).toBeVisible();
});
