import {
  actors,
  currentDemoAccount,
  type Account,
  type Role,
  type Scenario,
  ServiceError,
} from "./contracts";
import { boundary, campus, type DemoCampus } from "./demo";
import { type MaintenanceReport, type MaintenanceStatus } from "./maintenance";
import { lostFound, type FoundItem, type Claim } from "./lostFound";

export type AssignableRole = "user" | "maintenance" | "department" | "admin";
export interface DirectoryEntry {
  key: string;
  account: Account;
}
type Registrations = Pick<
  DemoCampus,
  "registeredAccounts" | "updateRegistration"
>;
export class AdminDirectory {
  constructor(
    private records: Record<Role, Account> = structuredClone(actors),
    private canAssign: (account: Account) => boolean = () => true,
    private registrations?: Registrations,
  ) {}
  private accounts() {
    return [
      ...Object.values(this.records),
      ...(this.registrations?.registeredAccounts() ?? []),
    ];
  }
  actor(key: Role) {
    return { ...this.records[key] };
  }
  async list(
    actor: Account,
    scenario: Scenario = "normal",
  ): Promise<DirectoryEntry[]> {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (actor.role !== "admin") throw new ServiceError("forbidden");
    const seeded = (Object.keys(this.records) as Role[])
      .filter((key) => !["guest", "unverified"].includes(key))
      .map((key) => ({ key, account: { ...this.records[key] } }));
    const registered = (this.registrations?.registeredAccounts() ?? []).map(
      (account) => ({ key: account.id, account }),
    );
    return [...seeded, ...registered];
  }
  async assign(
    actor: Account,
    key: string,
    role: AssignableRole,
    scope: string,
    confirmAdmin: boolean,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const seededKey = (Object.keys(this.records) as Role[]).find(
      (candidate) =>
        candidate === key && !["guest", "unverified"].includes(candidate),
    );
    const target = seededKey
      ? this.records[seededKey]
      : this.registrations
          ?.registeredAccounts()
          .find((account) => account.id === key);
    if (
      actor.role !== "admin" ||
      !target ||
      target.role === "unverified" ||
      target.id === actor.id
    )
      throw new ServiceError("forbidden");
    if (
      !["user", "maintenance", "department", "admin"].includes(role) ||
      (role === "maintenance" && scope !== "maintenance") ||
      (role === "department" && !["security", "secretariat"].includes(scope)) ||
      (["user", "admin"].includes(role) && scope !== "") ||
      (role === "admin" && !confirmAdmin)
    )
      throw new ServiceError("invalid");
    const proposed = { ...target, role, department: scope || undefined };
    if (!this.canAssign(proposed)) throw new ServiceError("conflict");
    if (seededKey) this.records[seededKey] = proposed;
    else this.registrations?.updateRegistration(proposed);
    return { ...proposed };
  }
}
export const directory = new AdminDirectory(
  actors,
  (account) => lostFound.canAssign(account),
  campus,
);
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
