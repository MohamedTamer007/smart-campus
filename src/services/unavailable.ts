import { ServiceError } from "./contracts";

export async function backendUnavailable<T>(): Promise<T> {
  throw new ServiceError("unavailable");
}
