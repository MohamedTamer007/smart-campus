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
}
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
export const guestAccount: Account = { id: "", name: "", email: "", role: "guest" };
export const verified = (account: Account) =>
  !["guest", "unverified"].includes(account.role);
