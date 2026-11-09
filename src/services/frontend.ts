import type { Account, Language } from "./contracts";
import { api, json, pages } from "./http";

export interface Notification {
  id: string;
  accountId: string;
  ar: string;
  en: string;
  to: string;
  read: boolean;
}
interface AccountService {
  saveName(account: Account, name: string, language?: Language): Promise<Account>;
  changePassword(
    account: Account,
    currentPassword: string,
    newPassword: string,
  ): Promise<void>;
  notifications(account: Account): Promise<Notification[]>;
  markRead(account: Account, id: string): Promise<void>;
}
export const accountService: AccountService = {
  saveName: (account, name, language) => api<Account>("/auth/profile", json("PUT", { name, language: language ?? account.language ?? "ar" })),
  changePassword: (_account, currentPassword, newPassword) => api<void>("/auth/password", json("POST", { currentPassword, newPassword })),
  notifications: async () => (await pages<Notification & { kind: string }>("/notifications")).map(notification => ({ ...notification,
    ar: notificationText(notification.kind)[0], en: notificationText(notification.kind)[1] })),
  markRead: (_account, id) => api<void>(`/notifications/${id}/read`, json("POST", {})),
};
function notificationText(kind: string): [string, string] {
  const labels: Record<string, [string, string]> = {
    submitted: ["تم إرسال البلاغ", "Report submitted"], review: ["البلاغ قيد المراجعة", "Report under review"],
    repair: ["جارٍ إصلاح العطل", "Repair in progress"], resolved: ["تم حل البلاغ", "Report resolved"],
    rejected: ["تم رفض الطلب", "Request rejected"], cancelled: ["تم إلغاء الطلب", "Request cancelled"],
    requested: ["طلب استرداد جديد", "New recovery request"], approved: ["تحديث على الاسترداد المقبول", "Approved recovery updated"],
    closed: ["أُغلق طلب الاسترداد", "Recovery request closed"], confirmed: ["تم تأكيد استلام العهدة", "Custody receipt confirmed"],
    "not-received": ["لم يتم استلام العهدة", "Custody not received"], handover_requested: ["طلب تسليم عهدة جديد", "New custody handover"],
  };
  return labels[kind] ?? ["تحديث جديد على الطلب", "Request updated"];
}
