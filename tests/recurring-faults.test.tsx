// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { RecurringFaults } from "../src/features/admin/Admin";
import type { RecurringFault } from "../src/services/admin";

const t = (_ar: string, en: string) => en;
const base: RecurringFault = { type: "doors", buildingAr: "مبنى", buildingEn: "Building 4", floorAr: "الدور الثاني", floorEn: "Floor 2", count: 4 };

afterEach(cleanup);

describe("recurring faults table", () => {
  it("explains the empty state", () => {
    render(<RecurringFaults faults={[]} t={t} />);
    expect(screen.getByText(/No staff-confirmed recurring faults/)).toBeTruthy();
  });

  it("shows the count, the place and an estimate with its basis", () => {
    render(<RecurringFaults t={t} faults={[{ ...base, location: "lab3", incidents: 3, lastReported: "2026-11-01T08:00:00Z",
      medianDays: 25, minDays: 20, maxDays: 30, nextEstimate: "2026-11-26T08:00:00Z", overdue: true }]} />);
    expect(screen.getByText("lab3")).toBeTruthy();
    expect(screen.getByText("4")).toBeTruthy();
    expect(screen.getByText("2026-11-26")).toBeTruthy();
    expect(screen.getByText(/Overdue/)).toBeTruthy();
    expect(screen.getByText(/Based on 3 incidents: median 25 days apart \(20–30\)/)).toBeTruthy();
  });

  it("says when there is not enough history for an estimate", () => {
    render(<RecurringFaults t={t} faults={[{ ...base, incidents: 2, lastReported: "2026-11-01T08:00:00Z", nextEstimate: null }]} />);
    expect(screen.getByText(/Not enough history/)).toBeTruthy();
  });

  it("shows only counts when the server sends no forecast fields (non-admin)", () => {
    render(<RecurringFaults t={t} faults={[base]} />);
    expect(screen.queryByText(/Next \(estimate\)/)).toBeNull();
    expect(screen.getByText("4")).toBeTruthy();
  });
});
