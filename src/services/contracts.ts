export type Language = "ar" | "en";
export type Role =
  | "guest"
  | "unverified"
  | "user"
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
  roles?: string[];
  departments?: { id: string; code: string }[];
  language?: "ar" | "en";
  isDemo?: boolean;
}
export class ServiceError extends Error {
  constructor(
    public code:
      | "unavailable"
      | "expired"
      | "conflict"
      | "forbidden"
      | "invalid"
      | "credentials"
      | "unverified"
      | "release_restricted"
      | "rate_limited"
      | "internal",
    public detail?: string,
  ) {
    super(code);
  }
}
export const guestAccount: Account = { id: "", name: "", email: "", role: "guest" };
export function hasRole(account: Account, role: Role) {
  return account.role === role || account.roles?.includes(role) === true;
}
export const verified = (account: Account) =>
  !["guest", "unverified"].includes(account.role);
