import type { Account } from "./contracts";
import { backendUnavailable } from "./unavailable";

export interface Notification {
  id: string;
  accountId: string;
  ar: string;
  en: string;
  to: string;
  read: boolean;
}
interface AccountService {
  saveName(account: Account, name: string): Promise<Account>;
  changePassword(
    account: Account,
    currentPassword: string,
    newPassword: string,
  ): Promise<void>;
  notifications(account: Account): Promise<Notification[]>;
  markRead(account: Account, id: string): Promise<void>;
}
export const accountService: AccountService = {
  saveName: backendUnavailable,
  changePassword: backendUnavailable,
  notifications: backendUnavailable,
  markRead: backendUnavailable,
};
