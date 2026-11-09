import { ServiceError } from "./contracts";

let csrf: string | undefined;
export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  try {
    const method = init.method ?? "GET";
    const headers = new Headers(init.headers);
    if (!["GET", "HEAD"].includes(method)) {
      csrf ??= (await api<{ token: string }>("/auth/csrf")).token;
      headers.set("X-CSRF-TOKEN", csrf);
    }
    if (init.body && !(init.body instanceof FormData)) headers.set("Content-Type", "application/json");
    const response = await fetch(`/api/v1${path}`, { ...init, headers, credentials: "same-origin" });
    if (!response.ok) {
      const error = await response.json().catch(() => ({})) as { code?: string };
      if (response.status === 401 && path !== "/auth/login" && path !== "/auth/me")
        window.dispatchEvent(new Event("smart-campus-session-expired"));
      const code = error.code === "unverified" ? "unverified" : response.status === 429 ? "rate_limited" : response.status === 500 ? "internal" :
        response.status === 401 ? (path === "/auth/login" ? "credentials" : "expired") :
        response.status === 403 ? "forbidden" : response.status === 409 ? "conflict" :
        response.status === 400 ? "invalid" : "unavailable";
      throw new ServiceError(code, error.code);
    }
    if (path.startsWith("/auth/") && method !== "GET") csrf = undefined;
    return response.status === 204 ? undefined as T : await response.json() as T;
  } catch (error) {
    if (error instanceof ServiceError) throw error;
    if (error instanceof TypeError) throw new ServiceError("unavailable");
    throw error;
  }
}

export function json(method: string, body: unknown): RequestInit { return { method, body: JSON.stringify(body) }; }
export function form(input: object): FormData {
  const body = new FormData();
  for (const [key, value] of Object.entries(input)) if (value !== undefined && value !== null && value !== "")
    body.append(key, value instanceof Blob ? value : String(value));
  return body;
}
export async function pages<T>(path: string): Promise<T[]> {
  const records: T[] = [];
  for (let page = 1; ; page++) {
    const batch = await api<{ items: T[] }>(`${path}${path.includes("?") ? "&" : "?"}page=${page}&pageSize=100`);
    records.push(...batch.items);
    if (batch.items.length < 100) return records;
  }
}
export interface RegisteredDepartment { id: string; code: string; ar: string; en: string; canReceive: boolean; maintenance: boolean }
export const departments = () => api<RegisteredDepartment[]>("/directory/departments");
export async function departmentId(code: string) {
  const department = (await departments()).find(entry => entry.code === code || entry.id === code);
  if (!department) throw new ServiceError("invalid");
  return department.id;
}

export async function withPhoto<T extends { mediaUrl?: string | null; photo?: File }>(record: T): Promise<T> {
  if (!record.mediaUrl) return record;
  const response = await fetch(record.mediaUrl, { credentials: "same-origin" });
  if (!response.ok) throw new ServiceError("unavailable");
  return { ...record, photo: new File([await response.blob()], "attachment.jpg", { type: "image/jpeg" }) };
}
