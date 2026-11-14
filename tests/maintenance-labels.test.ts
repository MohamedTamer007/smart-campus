import { describe, expect, it } from "vitest";
import { maintenanceValueLabel } from "../src/services/maintenance";

describe("maintenance values follow the selected interface language", () => {
  it("translates known fault types and severity levels", () => {
    const arabic = (ar: string) => ar;
    const english = (_ar: string, en: string) => en;

    expect(maintenanceValueLabel(" plumbing ", arabic)).toBe("سباكة");
    expect(maintenanceValueLabel("HIGH", arabic)).toBe("مرتفعة");
    expect(maintenanceValueLabel("water", english)).toBe("Water leak");
    expect(maintenanceValueLabel("medium", english)).toBe("Medium");
  });

  it("preserves unrecognized staff-entered labels and renders missing values clearly", () => {
    const arabic = (ar: string) => ar;

    expect(maintenanceValueLabel("Elevator", arabic)).toBe("Elevator");
    expect(maintenanceValueLabel(null, arabic)).toBe("—");
    expect(maintenanceValueLabel("   ", arabic)).toBe("—");
  });
});
