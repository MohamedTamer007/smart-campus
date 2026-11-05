import type { Account, Role } from "./contracts";
import { backendUnavailable } from "./unavailable";

export type Department = "security" | "secretariat";
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
export interface FoundItem extends FoundInput {
  id: string;
  uploaderId: string;
  custodianId: string;
  custodianDepartment?: Department;
  custodianOffice?: string;
  state: "available" | "reserved" | "delivered";
  createdAt: string;
  handover?: Handover;
  version: number;
  canClaim?: boolean;
}
export type ClaimStatus =
  "pending" | "approved" | "rejected" | "delivered" | "closed";
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
}
export interface CollectionDetails {
  phone?: string;
  place?: string;
  instructions?: string;
}
export function currentCustodian(item: FoundItem, actor: Account) {
  return item.custodianDepartment
    ? ["department", "secretariat"].includes(actor.role) &&
        actor.department === item.custodianDepartment
    : item.custodianId === actor.id;
}
export function receiving(item: FoundItem, actor: Account) {
  return (
    !!item.handover &&
    ["pending", "not-received"].includes(item.handover.status) &&
    ["department", "secretariat"].includes(actor.role) &&
    actor.department === item.handover.department
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
  search(actor: Account, description: string, photo?: File): Promise<FoundItem[]>;
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
  reject(actor: Account, id: string): Promise<Claim>;
  confirmDelivery(actor: Account, id: string): Promise<Claim>;
}
export const lostFound: LostFoundService = {
  register: backendUnavailable, search: backendUnavailable, detail: backendUnavailable,
  itemsFor: backendUnavailable, requestHandover: backendUnavailable,
  cancelHandover: backendUnavailable, confirmHandover: backendUnavailable,
  notReceived: backendUnavailable, claim: backendUnavailable, claims: backendUnavailable,
  claimDetail: backendUnavailable, approve: backendUnavailable, reject: backendUnavailable,
  confirmDelivery: backendUnavailable,
};
