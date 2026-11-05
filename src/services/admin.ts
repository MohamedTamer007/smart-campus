import type { Account } from "./contracts";
import type { MaintenanceReport, MaintenanceStatus } from "./maintenance";
import type { FoundItem, Claim } from "./lostFound";
import { backendUnavailable } from "./unavailable";

export type AssignableRole = "user" | "maintenance" | "department" | "admin";
export interface DirectoryEntry {
  key: string;
  account: Account;
}
interface DirectoryService {
  list(actor: Account): Promise<DirectoryEntry[]>;
  assign(
    actor: Account,
    request: {
      key: string;
      role: AssignableRole;
      scope: string;
      confirmAdmin: boolean;
    },
  ): Promise<Account>;
}
export const directory: DirectoryService = {
  list: backendUnavailable,
  assign: backendUnavailable,
};

export interface Stats {
  totalReports: number;
  byStatus: Record<MaintenanceStatus, number>;
  availableItems: number;
  pendingClaims: number;
  locations: Record<string, number>;
  faultTypes: Record<string, number>;
}
export type DashboardScope = "all" | "maintenance" | "security" | "secretariat";
export function calculateStats(
  reports: MaintenanceReport[],
  items: FoundItem[],
  claims: Claim[],
  filter: { scope?: DashboardScope; from?: string; to?: string } = {},
): Stats {
  const inRange = (value: string) => {
    const date = new Intl.DateTimeFormat("en-CA", {
      timeZone: "Africa/Cairo",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date(value));
    return (
      (!filter.from || date >= filter.from) && (!filter.to || date <= filter.to)
    );
  };
  const scope = filter.scope ?? "all";
  const scopedItems =
    scope === "all"
      ? items
      : items.filter((item) => item.custodianDepartment === scope);
  const scopedItemIds = new Set(scopedItems.map((item) => item.id));
  reports = reports.filter(
    (report) =>
      (scope === "all" || scope === "maintenance") && inRange(report.createdAt),
  );
  claims = claims.filter(
    (claim) =>
      (scope === "all" || scopedItemIds.has(claim.itemId)) &&
      inRange(claim.createdAt),
  );
  items = scopedItems.filter((item) => inRange(item.createdAt));
  const byStatus: Stats["byStatus"] = {
    submitted: 0,
    review: 0,
    repair: 0,
    resolved: 0,
    rejected: 0,
    cancelled: 0,
  };
  const locations: Record<string, number> = {};
  const faultTypes: Record<string, number> = {};
  for (const report of reports) {
    byStatus[report.status]++;
    locations[report.faculty] = (locations[report.faculty] ?? 0) + 1;
    const type = report.decision?.type.trim();
    if (type) faultTypes[type] = (faultTypes[type] ?? 0) + 1;
  }
  return {
    totalReports: reports.length,
    byStatus,
    availableItems: items.filter((item) => item.state === "available").length,
    pendingClaims: claims.filter((claim) => claim.status === "pending").length,
    locations,
    faultTypes,
  };
}

export { departmentStats } from "./lostFound";
