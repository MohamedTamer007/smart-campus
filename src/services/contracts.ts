export type Language = "ar" | "en";
export type Role =
  | "guest"
  | "unverified"
  | "user"
  | "finder"
  | "other"
  | "maintenance"
  | "department"
  | "secretariat"
  | "admin";
export interface Account {
  id: string;
  name: string;
  email: string;
  role: Role;
  department?: string;
}
export type Scenario = "normal" | "empty" | "error" | "expired" | "conflict";
export class ServiceError extends Error {
  constructor(
    public code:
      | "unavailable"
      | "expired"
      | "conflict"
      | "forbidden"
      | "invalid"
      | "credentials",
  ) {
    super(code);
  }
}
export const actors: Record<Role, Account> = {
  guest: { id: "guest", name: "", email: "", role: "guest" },
  unverified: {
    id: "unverified",
    name: "Demo",
    email: "demo@o6u.edu.eg",
    role: "unverified",
  },
  user: {
    id: "user",
    name: "أحمد / Ahmed",
    email: "ahmed@o6u.edu.eg",
    role: "user",
  },
  other: {
    id: "other",
    name: "علي / Ali",
    email: "ali@o6u.edu.eg",
    role: "other",
  },
  finder: {
    id: "finder",
    name: "سارة / Sara",
    email: "sara@o6u.edu.eg",
    role: "finder",
  },
  secretariat: {
    id: "secretariat",
    name: "السكرتارية / Secretariat",
    email: "office@o6u.edu.eg",
    role: "secretariat",
    department: "secretariat",
  },
  maintenance: {
    id: "maintenance",
    name: "منى / Mona",
    email: "mona@o6u.edu.eg",
    role: "maintenance",
    department: "maintenance",
  },
  department: {
    id: "department",
    name: "أمن / Security",
    email: "security@o6u.edu.eg",
    role: "department",
    department: "security",
  },
  admin: {
    id: "admin",
    name: "إدارة / Admin",
    email: "admin@o6u.edu.eg",
    role: "admin",
  },
};
export const verified = (account: Account) =>
  !["guest", "unverified"].includes(account.role);

export function currentDemoAccount(
  actor: Account,
  accounts: Account[],
): Account {
  const current = accounts.filter((account) => account.id === actor.id).at(-1);
  if (!current || !verified(current)) throw new ServiceError("forbidden");
  return current;
}
