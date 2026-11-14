import type { Account, Role } from "./contracts";
import { hasRole, ServiceError } from "./contracts";
import { api, departmentId, form, json, pages, withPhoto } from "./http";

export type Department = string;
export interface FoundInput {
  description: string;
  place: string;
  date: string;
  storage: string;
  photo?: File;
  destination?: Department;
  office?: string;
}
export interface Handover {
  department: Department;
  office: string;
  status: "pending" | "not-received" | "confirmed" | "cancelled";
}
export interface FoundItem extends Omit<FoundInput, "storage"> {
  storage: string | null;
  id: string;
  uploaderId: string;
  custodianId: string | null;
  custodianDepartment?: Department | null;
  custodianOffice?: string;
  state: "available" | "reserved" | "delivered";
  createdAt: string;
  handover?: Handover | null;
  version: number;
  canClaim?: boolean;
  mediaUrl?: string;
  canHandover?: boolean;
  canReceive?: boolean;
  canCancelHandover?: boolean;
}
export interface SearchResult {
  items: FoundItem[];
  calibrated: boolean;
}
export type ClaimStatus =
  "pending" | "approved" | "rejected" | "delivered" | "closed" | "cancelled";
export interface Claim {
  id: string;
  itemId: string;
  claimantId: string;
  claimantName: string;
  claimantRole: Role;
  claimantDepartment?: string;
  createdAt: string;
  message: string;
  status: ClaimStatus;
  phone?: string;
  place?: string;
  instructions?: string;
  claimantConfirmed: boolean;
  custodianConfirmed: boolean;
  claimantAt?: string;
  custodianAt?: string;
  canApprove?: boolean;
  canReject?: boolean;
  canCancel?: boolean;
  version?: number;
  closureReason?: string;
}
export interface CollectionDetails {
  version?: number;
  phone?: string;
  place?: string;
  instructions?: string;
}
export function currentCustodian(item: FoundItem, actor: Account) {
  return item.custodianDepartment
    ? (hasRole(actor, "department") || hasRole(actor, "secretariat")) &&
        (actor.departments?.some(scope => scope.code === item.custodianDepartment) ?? actor.department === item.custodianDepartment)
    : item.custodianId === actor.id;
}
export function receiving(item: FoundItem, actor: Account) {
  return (
    !!item.handover &&
    ["pending", "not-received"].includes(item.handover.status) &&
    (hasRole(actor, "department") || hasRole(actor, "secretariat")) &&
    (actor.departments?.some(scope => scope.code === item.handover?.department) ?? actor.department === item.handover.department)
  );
}
export function departmentStats(
  items: FoundItem[],
  claims: Claim[],
  actor: Account,
) {
  const held = items.filter(
    (item) => item.state !== "delivered" && currentCustodian(item, actor),
  );
  const heldIds = new Set(held.map((item) => item.id));
  return {
    held: held.length,
    incoming: items.filter(
      (item) => receiving(item, actor) && item.handover?.status === "pending",
    ).length,
    pendingClaims: claims.filter(
      (claim) => claim.status === "pending" && heldIds.has(claim.itemId),
    ).length,
  };
}
interface LostFoundService {
  register(actor: Account, input: FoundInput): Promise<FoundItem>;
  search(actor: Account, description: string, photo?: File): Promise<SearchResult>;
  detail(actor: Account, id: string): Promise<FoundItem & { canClaim: boolean }>;
  itemsFor(actor: Account): Promise<FoundItem[]>;
  requestHandover(
    actor: Account,
    request: {
      id: string;
      department: Department;
      office: string;
      expectedVersion: number;
    },
  ): Promise<FoundItem>;
  cancelHandover(actor: Account, id: string, expectedVersion: number): Promise<FoundItem>;
  confirmHandover(actor: Account, id: string, expectedVersion: number): Promise<FoundItem>;
  notReceived(actor: Account, id: string, expectedVersion: number): Promise<FoundItem>;
  claim(actor: Account, itemId: string, message: string): Promise<Claim>;
  claims(actor: Account): Promise<Claim[]>;
  claimDetail(actor: Account, id: string): Promise<{ claim: Claim & { canApprove: boolean; canReject: boolean }; item: FoundItem }>;
  approve(actor: Account, id: string, details: CollectionDetails): Promise<Claim>;
  reject(actor: Account, id: string, reason?: string, version?: number): Promise<Claim>;
  cancelApproval(actor: Account, id: string, reason: string, version?: number): Promise<Claim>;
  confirmDelivery(actor: Account, id: string, version?: number): Promise<Claim>;
}
export const lostFound: LostFoundService = {
  register: async (_actor, input) => withPhoto(await api<FoundItem>("/found", { method: "POST", body: form({ ...input,
    destination: input.destination ? await departmentId(input.destination) : undefined }) })),
  search: async (actor, description, photo) => {
    const response = await api<{ itemIds: string[]; calibrated?: boolean }>("/found/search", { method: "POST", body: form({ description, photo }) });
    return {
      items: await Promise.all(response.itemIds.map(id => lostFound.detail(actor, id))),
      calibrated: response.calibrated === true,
    };
  },
  detail: async (_actor, id) => withPhoto(await api<FoundItem & { canClaim: boolean }>(`/found/${id}`)),
  itemsFor: () => pages<FoundItem>("/found"),
  requestHandover: async (_actor, request) => withPhoto(await api<FoundItem>(`/found/${request.id}/handovers`, json("POST", {
    version: request.expectedVersion, department: await departmentId(request.department), office: request.office }))),
  cancelHandover: (_actor, id, version) => handover(id, version, "cancel"),
  confirmHandover: (_actor, id, version) => handover(id, version, "confirm"),
  notReceived: (_actor, id, version) => handover(id, version, "not-received"),
  claim: async (_actor, id, message) => remember(await api<Claim>(`/found/${id}/claims`, json("POST", { message }))),
  claims: async () => (await pages<Claim>("/claims")).map(remember),
  claimDetail: async (_actor, id) => {
    const response = await api<{ claim: Claim & { canApprove: boolean; canReject: boolean }; item: FoundItem }>(`/claims/${id}`);
    remember(response.claim);
    return { claim: response.claim, item: await withPhoto(response.item) };
  },
  approve: (_actor, id, details) => decision(id, "approve", details),
  reject: (_actor, id, reason, version) => decision(id, "reject", { reason, version }),
  cancelApproval: (_actor, id, reason, version) => decision(id, "cancel", { reason, version }),
  confirmDelivery: async (_actor, id, version) => remember(await api<Claim>(`/claims/${id}/confirm`, json("POST", { version: requireVersion(version) }))),
};
function remember(claim: Claim): Claim {
  if (claim.status === "closed" && claim.closureReason === "Recovered") claim.status = "delivered";
  return claim;
}
function requireVersion(current?: number) {
  if (current === undefined) throw new ServiceError("conflict");
  return current;
}
async function handover(id: string, expected: number, action: string) {
  return withPhoto(await api<FoundItem>(`/found/${id}/handovers/${action}`, json("POST", { version: expected })));
}
async function decision(id: string, action: string, details: CollectionDetails & { reason?: string }) {
  return remember(await api<Claim>(`/claims/${id}/${action}`, json("POST", { ...details, version: requireVersion(details.version) })));
}
