import { describe, expect, it } from "vitest";
import { universityEmail } from "../src/services/validation";
import { guestAccount } from "../src/services/contracts";
import { accountService } from "../src/services/frontend";

describe("university email validation", () => {
  it.each([
    ["  NAME@O6U.EDU.EG  ", true],
    ["a@o6u.edu.eg.evil.com", false],
    ["a@sub.o6u.edu.eg", false],
    ["a@@o6u.edu.eg", false],
    ["a b@o6u.edu.eg", false],
    ["@o6u.edu.eg", false],
    ["a@gmail.com", false],
    ['"a b"@o6u.edu.eg', true],
    ['"a@b"@o6u.edu.eg', true],
    ['"bad"quote@o6u.edu.eg', false],
    ["a".repeat(64) + "@o6u.edu.eg", true],
    ["a".repeat(65) + "@o6u.edu.eg", false],
    [".a@o6u.edu.eg", false],
    ["a..b@o6u.edu.eg", false],
    ["a.@o6u.edu.eg", false],
  ])("checks complete syntax and the exact domain: %s", (email, valid) =>
    expect(universityEmail(email)).toBe(valid),
  );
});

it("does not save account data or generate notifications without a backend", async () => {
  await expect(accountService.saveName(guestAccount, "Student")).rejects.toMatchObject({ code: "unavailable" });
  await expect(accountService.notifications(guestAccount)).rejects.toMatchObject({ code: "unavailable" });
  await expect(accountService.markRead(guestAccount, "notice")).rejects.toMatchObject({ code: "unavailable" });
  await expect(accountService.changePassword(guestAccount, "old", "new")).rejects.toMatchObject({ code: "unavailable" });
});
