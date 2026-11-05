import {
  actors,
  currentDemoAccount,
  type Account,
  type Role,
  type Scenario,
  ServiceError,
} from "./contracts";
import { boundary, campus } from "./demo";

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
function validDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(date.valueOf()) || date.toISOString().slice(0, 10) !== value)
    return false;
  const today = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Africa/Cairo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
  return value <= today;
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
function cloneItem(item: FoundItem): FoundItem {
  return {
    ...item,
    handover: item.handover ? { ...item.handover } : undefined,
  };
}
function claimantIsCustodian(
  item: FoundItem,
  claim: Claim,
  accounts: Account[],
) {
  const current = accounts
    .filter((account) => account.id === claim.claimantId)
    .at(-1);
  const role = current?.role ?? claim.claimantRole;
  const department =
    current?.department ?? (current ? undefined : claim.claimantDepartment);
  return item.custodianDepartment
    ? ["department", "secretariat"].includes(role) &&
        department === item.custodianDepartment
    : item.custodianId === claim.claimantId;
}
function custodianRecipients(item: FoundItem) {
  return item.custodianDepartment
    ? [...Object.values(actors), ...campus.registeredAccounts()]
        .filter(
          (actor) =>
            ["department", "secretariat"].includes(actor.role) &&
            actor.department === item.custodianDepartment,
        )
        .map((actor) => actor.id)
    : [item.custodianId];
}
function notifyCustodian(
  item: FoundItem,
  ar: string,
  en: string,
  path: string,
) {
  for (const id of new Set(custodianRecipients(item).filter(Boolean)))
    campus.notify(id, ar, en, path);
}
function requireVersion(item: FoundItem, expectedVersion: number) {
  if (item.version !== expectedVersion) throw new ServiceError("conflict");
}

function notifyDepartment(
  department: Department,
  ar: string,
  en: string,
  path: string,
) {
  const recipients = [
    ...Object.values(actors),
    ...campus.registeredAccounts(),
  ].filter(
    (actor) =>
      ["department", "secretariat"].includes(actor.role) &&
      actor.department === department,
  );
  for (const id of new Set(recipients.map((actor) => actor.id)))
    campus.notify(id, ar, en, path);
}
function copyClaim(claim: Claim, item: FoundItem, actor: Account): Claim {
  const copy = { ...claim };
  if (!(
    currentCustodian(item, actor) ||
    actor.role === "admin" ||
    (claim.claimantId === actor.id &&
      (claim.status === "approved" || claim.status === "delivered"))
  )) {
    delete copy.phone;
    delete copy.place;
    delete copy.instructions;
  }
  return copy;
}
export class LostFoundDemo {
  constructor(
    private readonly accounts: () => Account[] = () => [
      ...Object.values(actors),
      ...campus.registeredAccounts(),
    ],
  ) {}
  canAssign(candidate: Account) {
    const accounts = this.accounts().map((account) =>
      account.id === candidate.id ? candidate : account,
    );
    for (const claim of this.requests.filter(
      (request) => request.status === "approved",
    )) {
      const item = this.item(claim.itemId);
      if (claimantIsCustodian(item, claim, accounts)) return false;
      if (
        item.custodianDepartment &&
        !accounts.some(
          (account) =>
            ["department", "secretariat"].includes(account.role) &&
            account.department === item.custodianDepartment,
        )
      )
        return false;
    }
    return true;
  }
  private items: FoundItem[] = [];
  private requests: Claim[] = [];
  private searched = new Set<string>();
  private itemCounter = 0;
  private claimCounter = 0;
  private item(id: string) {
    const item = this.items.find((value) => value.id === id);
    if (!item) throw new ServiceError("forbidden");
    return item;
  }
  private request(id: string) {
    const claim = this.requests.find((value) => value.id === id);
    if (!claim) throw new ServiceError("forbidden");
    return claim;
  }
  private mayView(item: FoundItem, actor: Account) {
    return (
      item.uploaderId === actor.id ||
      currentCustodian(item, actor) ||
      receiving(item, actor) ||
      actor.role === "admin" ||
      this.searched.has(`${actor.id}:${item.id}`) ||
      this.requests.some(
        (claim) => claim.itemId === item.id && claim.claimantId === actor.id,
      )
    );
  }
  async register(
    actor: Account,
    input: FoundInput,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (
      !input.description.trim() ||
      !input.place.trim() ||
      !input.storage.trim() ||
      !validDate(input.date) ||
      !input.photo?.type.startsWith("image/") ||
      (input.destination &&
        (!["security", "secretariat"].includes(input.destination) ||
          !input.office?.trim()))
    )
      throw new ServiceError("invalid");
    const { description, place, date, storage, photo, destination, office } =
      input;
    const item: FoundItem = {
      description: description.trim(),
      place: place.trim(),
      date,
      storage: storage.trim(),
      photo,
      destination,
      office,
      id: `FND-${++this.itemCounter}`,
      uploaderId: actor.id,
      custodianId: actor.id,
      state: "available",
      createdAt: new Date().toISOString(),
      version: 1,
    };
    if (destination && office)
      item.handover = {
        department: destination,
        office: office.trim(),
        status: "pending",
      };
    this.items.push(item);
    if (destination)
      notifyDepartment(
        destination,
        "لديك طلب تسليم غرض.",
        "You have an incoming item handover.",
        `/found/${item.id}`,
      );
    return cloneItem(item);
  }
  async search(
    actor: Account,
    description: string,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const query = description.trim().toLocaleLowerCase();
    if (!query) throw new ServiceError("invalid");
    if (scenario === "empty") return [];
    const found = this.items.filter(
      (item) =>
        item.state === "available" &&
        item.description.toLocaleLowerCase().includes(query),
    );
    found.forEach((item) => this.searched.add(`${actor.id}:${item.id}`));
    return found.map(cloneItem);
  }
  async detail(actor: Account, id: string, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(id);
    if (!this.mayView(item, actor)) throw new ServiceError("forbidden");
    const activeClaim = this.requests.some(
      (claim) =>
        claim.itemId === item.id &&
        claim.claimantId === actor.id &&
        ["pending", "approved"].includes(claim.status),
    );
    return {
      ...cloneItem(item),
      canClaim:
        item.state === "available" &&
        this.searched.has(`${actor.id}:${item.id}`) &&
        !currentCustodian(item, actor) &&
        !activeClaim,
    };
  }
  async itemsFor(actor: Account, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (scenario === "empty") return [];
    return this.items
      .filter(
        (item) =>
          item.uploaderId === actor.id ||
          currentCustodian(item, actor) ||
          receiving(item, actor) ||
          actor.role === "admin",
      )
      .map(cloneItem);
  }
  async requestHandover(
    actor: Account,
    id: string,
    department: Department,
    office: string,
    expectedVersion: number,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(id);
    if (!currentCustodian(item, actor)) throw new ServiceError("forbidden");
    requireVersion(item, expectedVersion);
    if (item.state !== "available" || item.handover?.status === "pending")
      throw new ServiceError("conflict");
    if (!["security", "secretariat"].includes(department) || !office.trim())
      throw new ServiceError("invalid");
    item.handover = { department, office: office.trim(), status: "pending" };
    item.version++;
    notifyDepartment(
      department,
      "لديك طلب تسليم غرض.",
      "You have an incoming item handover.",
      `/found/${item.id}`,
    );
    return cloneItem(item);
  }
  async cancelHandover(
    actor: Account,
    id: string,
    expectedVersion: number,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(id);
    if (!currentCustodian(item, actor)) throw new ServiceError("forbidden");
    requireVersion(item, expectedVersion);
    if (
      !item.handover ||
      !["pending", "not-received"].includes(item.handover.status)
    )
      throw new ServiceError("conflict");
    item.handover.status = "cancelled";
    item.version++;
    return cloneItem(item);
  }
  async confirmHandover(
    actor: Account,
    id: string,
    expectedVersion: number,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(id);
    if (!item.handover || !receiving(item, actor))
      throw new ServiceError("forbidden");
    requireVersion(item, expectedVersion);
    if (item.handover.status !== "pending" || item.state !== "available")
      throw new ServiceError("conflict");
    const recipients = new Set(
      [...custodianRecipients(item), item.uploaderId].filter(Boolean),
    );
    for (const id of recipients)
      campus.notify(
        id,
        "تم تأكيد استلام الغرض.",
        "The item handover was confirmed.",
        `/found/${item.id}`,
      );
    item.custodianDepartment = item.handover.department;
    item.custodianOffice = item.handover.office;
    item.custodianId = "";
    item.handover.status = "confirmed";
    item.version++;
    return cloneItem(item);
  }
  async notReceived(
    actor: Account,
    id: string,
    expectedVersion: number,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(id);
    if (!item.handover || !receiving(item, actor))
      throw new ServiceError("forbidden");
    requireVersion(item, expectedVersion);
    if (item.handover.status !== "pending") throw new ServiceError("conflict");
    item.handover.status = "not-received";
    item.version++;
    return cloneItem(item);
  }
  async claim(
    actor: Account,
    itemId: string,
    message: string,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const item = this.item(itemId);
    if (
      !this.searched.has(`${actor.id}:${item.id}`) ||
      currentCustodian(item, actor)
    )
      throw new ServiceError("forbidden");
    if (
      item.state !== "available" ||
      this.requests.some(
        (c) =>
          c.itemId === item.id &&
          c.claimantId === actor.id &&
          ["pending", "approved"].includes(c.status),
      )
    )
      throw new ServiceError("conflict");
    const claim: Claim = {
      id: `CLM-${++this.claimCounter}`,
      itemId,
      claimantId: actor.id,
      claimantName: actor.name,
      claimantRole: actor.role,
      claimantDepartment: actor.department,
      createdAt: new Date().toISOString(),
      message: message.trim(),
      status: "pending",
      claimantConfirmed: false,
      custodianConfirmed: false,
    };
    this.requests.push(claim);
    notifyCustodian(
      item,
      "لديك طلب استرداد جديد.",
      "You have a new recovery request.",
      `/claims/${claim.id}`,
    );
    return copyClaim(claim, item, actor);
  }
  async claims(actor: Account, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (scenario === "empty") return [];
    return this.requests
      .filter((claim) => {
        const item = this.item(claim.itemId);
        return (
          claim.claimantId === actor.id ||
          currentCustodian(item, actor) ||
          actor.role === "admin"
        );
      })
      .map((claim) => copyClaim(claim, this.item(claim.itemId), actor));
  }
  async claimDetail(actor: Account, id: string, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const claim = this.request(id);
    const item = this.item(claim.itemId);
    if (
      claim.claimantId !== actor.id &&
      !currentCustodian(item, actor) &&
      actor.role !== "admin"
    )
      throw new ServiceError("forbidden");
    const canReject =
      currentCustodian(item, actor) &&
      claim.status === "pending" &&
      item.state === "available";
    const canApprove =
      canReject && !claimantIsCustodian(item, claim, this.accounts());
    return {
      claim: { ...copyClaim(claim, item, actor), canApprove, canReject },
      item: cloneItem(item),
    };
  }
  async approve(
    actor: Account,
    id: string,
    details: CollectionDetails,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const claim = this.request(id);
    const item = this.item(claim.itemId);
    if (
      !currentCustodian(item, actor) ||
      claimantIsCustodian(item, claim, this.accounts())
    )
      throw new ServiceError("forbidden");
    if (claim.status !== "pending" || item.state !== "available")
      throw new ServiceError("conflict");
    if (
      item.custodianDepartment
        ? !details.place?.trim() || !details.instructions?.trim()
        : !details.phone?.trim()
    )
      throw new ServiceError("invalid");
    claim.status = "approved";
    claim.phone = details.phone?.trim();
    claim.place = details.place?.trim();
    claim.instructions = details.instructions?.trim();
    item.state = "reserved";
    item.version++;
    campus.notify(
      claim.claimantId,
      "تم قبول طلب الاسترداد.",
      "Your recovery request was approved.",
      `/claims/${claim.id}`,
    );
    return copyClaim(claim, item, actor);
  }
  async reject(actor: Account, id: string, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const claim = this.request(id);
    const item = this.item(claim.itemId);
    if (!currentCustodian(item, actor)) throw new ServiceError("forbidden");
    if (claim.status !== "pending" || item.state !== "available")
      throw new ServiceError("conflict");
    claim.status = "rejected";
    campus.notify(
      claim.claimantId,
      "تم رفض طلب الاسترداد.",
      "Your recovery request was rejected.",
      `/claims/${claim.id}`,
    );
    return copyClaim(claim, item, actor);
  }
  async confirmDelivery(
    actor: Account,
    id: string,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const claim = this.request(id);
    const item = this.item(claim.itemId);
    const custodian = currentCustodian(item, actor);
    const claimant = actor.id === claim.claimantId;
    if (
      (!custodian && !claimant) ||
      claimantIsCustodian(item, claim, this.accounts())
    )
      throw new ServiceError("forbidden");
    if (claim.status !== "approved" && claim.status !== "delivered")
      throw new ServiceError("conflict");
    if (claimant && !claim.claimantConfirmed) {
      claim.claimantConfirmed = true;
      claim.claimantAt = new Date().toISOString();
      notifyCustodian(
        item,
        "أكد المستلم استلام الغرض.",
        "The requester confirmed receipt.",
        `/claims/${claim.id}`,
      );
    }
    if (custodian && !claim.custodianConfirmed) {
      claim.custodianConfirmed = true;
      claim.custodianAt = new Date().toISOString();
      campus.notify(
        claim.claimantId,
        "أكد الحائز تسليم الغرض.",
        "The custodian confirmed delivery.",
        `/claims/${claim.id}`,
      );
    }
    if (
      claim.claimantConfirmed &&
      claim.custodianConfirmed &&
      claim.status !== "delivered"
    ) {
      claim.status = "delivered";
      item.state = "delivered";
      item.version++;
      for (const other of this.requests.filter(
        (c) => c.itemId === item.id && c.status === "pending",
      )) {
        other.status = "closed";
        campus.notify(
          other.claimantId,
          "أُغلق طلب الاسترداد بعد تسليم الغرض.",
          "Recovery request closed because the item was delivered.",
          `/claims/${other.id}`,
        );
      }
    }
    return copyClaim(claim, item, actor);
  }
}
export const lostFound = new LostFoundDemo();
