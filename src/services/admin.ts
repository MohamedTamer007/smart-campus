import type { Account } from "./contracts";
import type { MaintenanceReport, MaintenanceStatus } from "./maintenance";
import type { FoundItem, Claim } from "./lostFound";
import { api, departmentId, json, pages } from "./http";

export type AssignableRole = "user" | "maintenance" | "department" | "admin";
export interface DirectoryEntry {
  key: string;
  account: Account;
  version?: string;
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
      version?: string;
    },
  ): Promise<Account>;
  replaceGrants(key: string, roles: string[], departmentIds: string[], version: string): Promise<void>;
}
export const directory: DirectoryService = {
  replaceGrants: (key, roles, departmentIds, version) => api<void>(`/admin/accounts/${key}/grants`, json("PUT", { roles, departmentIds, version })),
  list: () => pages<DirectoryEntry>("/admin/accounts"),
  assign: async (_actor, request) => {
    const entries = await pages<DirectoryEntry>("/admin/accounts");
    const entry = entries.find(candidate => candidate.key === request.key);
    if (!entry) throw new Error("Account no longer available");
    await api(`/admin/accounts/${request.key}/grants`, json("PUT", {
      roles: request.role === "user" ? ["user"] : Array.from(new Set([...(entry.account.roles ?? []), request.role])),
      departmentIds: request.role === "user" ? [] : Array.from(new Set([...(entry.account.departments?.map(scope => scope.id) ?? []),
        ...(request.scope ? [await departmentId(request.scope)] : [])])), version: request.version ?? entry.version,
    }));
    return (await pages<DirectoryEntry>("/admin/accounts")).find(candidate => candidate.key === request.key)!.account;
  },
};
export const statistics = async (query: { scope?: string; from?: string; to?: string } = {}) => {
  const parameters = new URLSearchParams();
  if (query.from) parameters.set("from", query.from);
  if (query.to) parameters.set("to", query.to);
  if (query.scope && query.scope !== "all") parameters.set("departmentId", await departmentId(query.scope));
  return api<Stats>(`/statistics?${parameters}`);
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
export interface OperationsSnapshot {
  sampledAt: string;
  deployment: string;
  workingSetBytes: number;
  jobs: { kind: string; state: string; count: number; maximumAttempts: number }[];
  quotas: { provider: string; used: number; dailyLimit: number }[];
  worker: "available" | "unavailable" | "hosted_configured" | "hosted_blocked";
  calibrationApproved: boolean;
  // Optional while an older API build is still deployed.
  ai?: {
    provider: string;
    textConfigured: boolean;
    imagesConfigured: boolean;
    taxonomyConfigured: boolean;
    textTransferEnabled: boolean;
    imageTransferEnabled: boolean;
    nvidiaKeyPresent: boolean;
    openRouterKeyPresent: boolean;
    nvidiaBudgetConfigured: boolean;
    openRouterBudgetConfigured: boolean;
    syntheticSmokeTestsEnabled: boolean;
    adminSyntheticDiagnosticEnabled?: boolean;
  };
}
export const operations = () => api<OperationsSnapshot>("/admin/operations");
export interface AiSmokeResult {
  status: "validated";
  provider: string;
  model: string;
}
export const smokeAi = () => api<AiSmokeResult>("/admin/operations/ai-smoke", json("POST", {}));
export interface AiDemoResult extends AiSmokeResult {
  type: "plumbing" | "electrical" | "other";
  priority: "low" | "medium" | "high";
  risk: "low" | "medium" | "high";
  explanation: string;
}
export const demoAi = () => api<AiDemoResult>("/admin/operations/ai-demo", json("POST", {}));
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
    const type = report.decision?.type?.trim();
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
