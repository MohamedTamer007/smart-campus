import type { Account } from "./contracts";
import { api, json } from "./http";

export const auth = {
  me: () => api<Account>("/auth/me"),
  login: (email: string, password: string) => api<Account>("/auth/login", json("POST", { email, password })),
  register: (name: string, email: string, password: string, confirmation: string) => api("/auth/register", json("POST", { name, email, password, confirmation })),
  logout: () => api<void>("/auth/logout", json("POST", {})),
  verify: (userId: string, token: string) => api("/auth/verify", json("POST", { userId, token })),
  resend: (email: string) => api("/auth/resend", json("POST", { email })),
  recover: (email: string) => api("/auth/recover", json("POST", { email })),
  reset: (userId: string, token: string, password: string, confirmation: string) => api("/auth/reset", json("POST", { userId, token, password, confirmation })),
};
