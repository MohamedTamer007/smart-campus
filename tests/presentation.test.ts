import { expect, it } from "vitest";
import { formatRecordDate, recordReference } from "../src/services/presentation";

it("uses the stored maintenance reference and stable compact item references", () => {
  const id = "a1234567-89ab-4cde-8f01-23456789abcd";
  expect(recordReference(id, "SC", "SC-SERVER123456")).toBe("SC-SERVER123456");
  expect(recordReference(id, "LF")).toBe("LF-A123456789AB");
  expect(recordReference(id, "CR")).toBe("CR-A123456789AB");
});

it("localizes date-only values without changing the calendar day", () => {
  expect(formatRecordDate("2026-11-16", "en")).toBe(new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium", timeZone: "Africa/Cairo"
  }).format(new Date("2026-11-16T12:00:00Z")));
  expect(formatRecordDate("2026-11-16", "ar")).toContain("٢٠٢٦");
  expect(formatRecordDate("invalid", "ar")).toBe("—");
});
