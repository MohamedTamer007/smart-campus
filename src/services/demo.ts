import {
  actors,
  verified,
  ServiceError,
  type Account,
  type Scenario,
} from "./contracts";
export interface Notification {
  id: string;
  accountId: string;
  ar: string;
  en: string;
  to: string;
  read: boolean;
}
export function universityEmail(input: string) {
  const email = input.trim();
  const separator = email.lastIndexOf("@");
  if (
    separator < 0 ||
    email.slice(separator + 1).toLowerCase() !== "o6u.edu.eg"
  )
    return false;
  const local = email.slice(0, separator);
  if (!local || local.length > 64) return false;
  if (local.startsWith('"'))
    return /^"(?:[\x20-\x21\x23-\x5b\x5d-\x7e]|\\[\x20-\x7e])*"$/.test(local);
  return /^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*$/i.test(
    local,
  );
}
export async function boundary(
  scenario: Scenario = "normal",
  account?: Account,
) {
  await new Promise((resolve) => setTimeout(resolve, 180));
  if (scenario === "error") throw new ServiceError("unavailable");
  if (scenario === "expired") throw new ServiceError("expired");
  if (scenario === "conflict") throw new ServiceError("conflict");
  if (account && !verified(account)) throw new ServiceError("forbidden");
}
export class DemoCampus {
  private registrations = new Map<string, Account>();
  private notices: Notification[] = [
    {
      id: "N-1",
      accountId: "user",
      ar: "مرحباً بك في تجربة Smart Campus",
      en: "Welcome to the Smart Campus demo",
      to: "/",
      read: false,
    },
  ];
  async register(input: {
    name: string;
    email: string;
    password: string;
    confirmation: string;
  }) {
    await boundary();
    if (
      !input.name.trim() ||
      !universityEmail(input.email) ||
      !input.password ||
      input.password !== input.confirmation
    )
      throw new ServiceError("invalid");
    const email = input.email.trim().toLowerCase();
    if (
      this.registrations.has(email) ||
      Object.values(actors).some((actor) => actor.email.toLowerCase() === email)
    )
      throw new ServiceError("invalid");
    const account: Account = {
      id: `registered-${crypto.randomUUID()}`,
      name: input.name.trim(),
      email,
      role: "unverified",
    };
    this.registrations.set(email, account);
    return { ...account };
  }
  async login(email: string, password: string, scenario: Scenario) {
    await boundary(scenario);
    const normalized = email.trim().toLowerCase();
    const matched =
      this.registrations.get(normalized) ??
      Object.values(actors).find(
        (actor) =>
          actor.email.toLowerCase() === normalized && actor.role !== "guest",
      );
    if (!universityEmail(email) || password !== "demo" || !matched)
      throw new ServiceError("credentials");
    return { ...matched };
  }
  async verify(account: Account, scenario: Scenario = "normal") {
    await boundary(scenario);
    const stored = this.registrations.get(account.email);
    if (
      account.role !== "unverified" ||
      (stored
        ? stored.id !== account.id || stored.role !== "unverified"
        : account.id !== actors.unverified.id)
    )
      throw new ServiceError("invalid");
    const verified: Account = { ...account, role: "user" };
    this.registrations.set(account.email, verified);
    return verified;
  }
  registeredAccounts() {
    return Array.from(this.registrations.values(), (account) => ({
      ...account,
    }));
  }
  updateRegistration(account: Account) {
    const existing = this.registrations.get(account.email);
    if (!existing || existing.id !== account.id)
      throw new ServiceError("forbidden");
    this.registrations.set(account.email, { ...account });
  }
  async saveName(
    account: Account,
    name: string,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario, account);
    const trimmed = name.trim();
    if (!trimmed) throw new ServiceError("invalid");
    const registered = this.registrations.get(account.email);
    if (registered?.id === account.id) {
      const next = { ...registered, name: trimmed };
      this.updateRegistration(next);
      return { ...next };
    }
    const key = (Object.keys(actors) as (keyof typeof actors)[]).find(
      (key) =>
        actors[key].id === account.id && actors[key].email === account.email,
    );
    if (!key) throw new ServiceError("forbidden");
    actors[key] = { ...actors[key], name: trimmed };
    return { ...actors[key] };
  }
  async notifications(account: Account, scenario: Scenario = "normal") {
    await boundary(scenario, account);
    return scenario === "empty"
      ? []
      : structuredClone(this.notices.filter((n) => n.accountId === account.id));
  }
  async markRead(account: Account, id: string, scenario: Scenario) {
    await boundary(scenario, account);
    const notice = this.notices.find(
      (n) => n.id === id && n.accountId === account.id,
    );
    if (!notice) throw new ServiceError("forbidden");
    notice.read = true;
  }
  notify(accountId: string, ar: string, en: string, to: string) {
    this.notices.push({
      id: crypto.randomUUID(),
      accountId,
      ar,
      en,
      to,
      read: false,
    });
  }
}
export const campus = new DemoCampus();
