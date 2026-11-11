import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import { useApp } from "../../app/context";
import { DirectoryOptions } from "../../components/DirectoryOptions";
import { AsyncView } from "../../components/AsyncView";
import { ActionForm, Feedback, Field, Page } from "../../components/ui";
import {
  directory,
  statistics,
  type AssignableRole,
  type DashboardScope,
} from "../../services/admin";
import {
  maintenance,
  type MaintenanceStatus,
} from "../../services/maintenance";
import { lostFound, currentCustodian } from "../../services/lostFound";
import { ServiceError, type Role } from "../../services/contracts";
import { OperationsStatus } from "./OperationsStatus";
const labels: Record<MaintenanceStatus, [string, string]> = {
  submitted: ["تم الإرسال", "Submitted"],
  review: ["قيد المراجعة", "Under review"],
  repair: ["جارٍ الإصلاح", "Being repaired"],
  resolved: ["تم الحل", "Resolved"],
  rejected: ["مرفوض", "Rejected"],
  cancelled: ["ملغى", "Cancelled"],
};
function roleName(role: Role, t: (ar: string, en: string) => string) {
  const names: Record<Role, [string, string]> = {
    guest: ["زائر", "Guest"],
    unverified: ["غير موثق", "Unverified"],
    user: ["مستخدم", "User"],
    maintenance: ["موظف صيانة", "Maintenance staff"],
    department: ["موظف جهة", "Department staff"],
    secretariat: ["موظف جهة", "Department staff"],
    admin: ["مدير", "Administrator"],
  };
  return t(...names[role]);
}
function scopeName(
  scope: string | undefined,
  t: (ar: string, en: string) => string,
) {
  return scope === "security"
    ? t("الأمن", "Security")
    : scope === "secretariat"
      ? t("السكرتارية", "Secretariat")
      : scope === "maintenance"
        ? t("الصيانة", "Maintenance")
        : "—";
}
export function Requests() {
  const { t, account } = useApp();
  const load = useCallback(async () => {
    const [reports, items, claims] = await Promise.all([
      maintenance.list(account),
      lostFound.itemsFor(account),
      lostFound.claims(account),
    ]);
    return {
      reports: reports.filter((report) => report.ownerId === account.id),
      items: items.filter(
        (item) =>
          item.uploaderId === account.id ||
          (item.state !== "delivered" && currentCustodian(item, account)),
      ),
      claims,
    };
  }, [account]);
  return (
    <Page
      title={t("طلباتي", "My requests")}
      subtitle={t(
        "تابع بلاغاتك وأغراضك وطلبات الاسترداد.",
        "Track your reports, items and recovery requests.",
      )}
    >
      <AsyncView key={`${account.id}`} load={load}>
        {(data) => (
          <>
            <div className="stats">
              <div className="stat">
                <span>{t("بلاغات الصيانة", "Maintenance reports")}</span>
                <strong>{data.reports.length}</strong>
              </div>
              <div className="stat">
                <span>{t("طلبات الاسترداد", "Recovery requests")}</span>
                <strong>
                  {
                    data.claims.filter(
                      (claim) => claim.claimantId === account.id,
                    ).length
                  }
                </strong>
              </div>
              <div className="stat">
                <span>
                  {t("الأغراض المسجلة أو في عهدتي", "Registered or held items")}
                </span>
                <strong>{data.items.length}</strong>
              </div>
            </div>
            <div className="list">
              <Link className="record-card" to="/maintenance">
                <h2>{t("بلاغات الصيانة", "Maintenance reports")}</h2>
                <p>
                  {t("عرض البلاغات ومتابعة حالتها.", "View and track reports.")}
                </p>
              </Link>
              <Link className="record-card" to="/claims">
                <h2>{t("طلبات الاسترداد", "Recovery requests")}</h2>
                <p>
                  {t(
                    "طلباتك وطلبات الأغراض في عهدتك.",
                    "Your requests and requests for items you hold.",
                  )}
                </p>
              </Link>
              <Link className="record-card" to="/found">
                <h2>{t("المعثورات الخاصة بي", "My found items")}</h2>
                <p>
                  {t(
                    "الأغراض التي سجلتها أو في عهدتك.",
                    "Items you registered or currently hold.",
                  )}
                </p>
              </Link>
            </div>
          </>
        )}
      </AsyncView>
    </Page>
  );
}
export function AdminOverview() {
  const { t, account } = useApp();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [scope, setScope] = useState<DashboardScope>("all");
  const load = useCallback(() => statistics({ from, to, scope }), [from, to, scope]);
  return (
    <Page
      title={t("لوحة الإدارة", "Admin overview")}
      subtitle={t(
        "إحصاءات البلاغات والأغراض المسجلة.",
        "Statistics for recorded reports and items.",
      )}
    >
      <div className="panel">
        <div className="field-grid">
          <Field label={t("من تاريخ", "From date")}>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </Field>
          <Field label={t("إلى تاريخ", "To date")}>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </Field>
        </div>
        <Field label={t("الجهة", "Department scope")}>
          <select
            value={scope}
            onChange={(e) => setScope(e.target.value as DashboardScope)}
          >
            <option value="all">{t("كل الجهات", "All departments")}</option>
            <DirectoryOptions kind="all" />
          </select>
        </Field>
        <p className="muted">
          {t(
            "تُحسب البلاغات ضمن الصيانة، وتُحسب المعثورات والطلبات حسب جهة العهدة المؤكدة الحالية وتاريخ إنشاء كل سجل.",
            "Reports belong to Maintenance; items and claims use current confirmed custody. Dates filter each record’s creation date.",
          )}
        </p>
        {from && to && from > to && (
          <Feedback kind="error">
            {t(
              "تاريخ البداية بعد تاريخ النهاية.",
              "Start date is after end date.",
            )}
          </Feedback>
        )}
      </div>
      <div className="actions">
        <Link className="button secondary" to="/admin/accounts">
          {t("المستخدمون والصلاحيات", "Accounts and permissions")}
        </Link>
        <Link className="button secondary" to="/admin/directory">
          {t("دليل الجهات والمواقع", "Departments and locations")}
        </Link>
        <Link className="button" to="/staff">
          {t("بلاغات الصيانة", "Maintenance queue")}
        </Link>
        <Link className="button" to="/admin/items">
          {t("المعثورات", "Found items")}
        </Link>
      </div>
      <div className="spacer" />
      <AsyncView key={`${account.id}-${from}-${to}-${scope}`} load={load}>
        {(stats) => {
          return (
            <>
              <div className="stats admin-stats">
                <div className="stat">
                  <span>
                    {t("إجمالي بلاغات الصيانة", "Total maintenance reports")}
                  </span>
                  <strong>{stats.totalReports}</strong>
                </div>
                <div className="stat">
                  <span>
                    {t("بلاغات قيد المراجعة", "Reports under review")}
                  </span>
                  <strong>{stats.byStatus.review}</strong>
                </div>
                <div className="stat">
                  <span>{t("معثورات متاحة", "Available items")}</span>
                  <strong>{stats.availableItems}</strong>
                </div>
                <div className="stat">
                  <span>
                    {t("طلبات استرداد معلقة", "Pending recovery requests")}
                  </span>
                  <strong>{stats.pendingClaims}</strong>
                </div>
              </div>
              <div className="split">
                <div className="panel">
                  <h2>{t("البلاغات حسب الحالة", "Reports by status")}</h2>
                  <div className="bar-chart">
                    {(Object.keys(stats.byStatus) as MaintenanceStatus[]).map(
                      (status) => (
                        <div className="bar-row" key={status}>
                          <span>{t(...labels[status])}</span>
                          <div className="bar-track">
                            <span
                              style={{
                                width: `${stats.totalReports ? (stats.byStatus[status] / stats.totalReports) * 100 : 0}%`,
                              }}
                            />
                          </div>
                          <b>{stats.byStatus[status]}</b>
                        </div>
                      ),
                    )}
                  </div>
                </div>
                <div className="panel">
                  <h2>
                    {t("توزيع البلاغات حسب المرفق", "Reports by facility")}
                  </h2>
                  {Object.entries(stats.locations).length ? (
                    Object.entries(stats.locations).map(([location, count]) => (
                      <p key={location}>
                        {location}: {count}
                      </p>
                    ))
                  ) : (
                    <Feedback>
                      {t(
                        "لا توجد بلاغات في الفترة المحددة.",
                        "No reports in the selected period.",
                      )}
                    </Feedback>
                  )}
                  <h2>{t("أنواع الأعطال المسجلة", "Recorded fault types")}</h2>
                  {Object.entries(stats.faultTypes).length ? (
                    Object.entries(stats.faultTypes).map(([type, count]) => (
                      <p key={type}>
                        {type}: {count}
                      </p>
                    ))
                  ) : (
                    <p>
                      {t(
                        "لم يسجل الموظفون أنواع أعطال بعد.",
                        "Staff have not recorded fault types yet.",
                      )}
                    </p>
                  )}
                </div>
              </div>
            </>
          );
        }}
      </AsyncView>
      <OperationsStatus />
    </Page>
  );
}
export function AdminAccounts() {
  const { t, account } = useApp();
  const [revision, setRevision] = useState(0);
  const [key, setKey] = useState("");
  const [role, setRole] = useState<AssignableRole | "">("");
  const load = useCallback(
    () => directory.list(account),
    [account],
  );
  return (
    <Page
      title={t("المستخدمون والصلاحيات", "Accounts and permissions")}
      subtitle={t(
        "إدارة حسابات المستخدمين وصلاحياتهم.",
        "Manage user accounts and permissions.",
      )}
    >
      <AsyncView key={`${account.id}-${revision}`} load={load}>
        {(entries) => (
          <>
            <div className="split">
              <div className="panel table-wrap">
                <h2>{t("الحسابات", "Accounts")}</h2>
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>{t("الاسم", "Name")}</th>
                      <th>{t("الدور", "Role")}</th>
                      <th>{t("النطاق", "Scope")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entries.map((entry) => (
                      <tr key={entry.key}>
                        <td>
                          <bdi>{entry.account.name}</bdi>
                        </td>
                        <td>{(entry.account.roles?.length ? entry.account.roles : [entry.account.role]).map(grant =>
                          <div key={grant}>{roleName(grant as Role, t)}{entry.version && entry.key !== account.id && <ActionForm label={t("إزالة الدور", "Remove role")} onSubmit={async () => {
                            await directory.replaceGrants(entry.key, (entry.account.roles ?? []).filter(role => role !== grant), entry.account.departments?.map(scope => scope.id) ?? [], entry.version!);
                            setRevision(value => value + 1);
                          }}><span className="sr-only">{grant}</span></ActionForm>}</div>)}</td>
                        <td>{entry.account.departments?.map(scope => <div key={scope.id}>{scope.code}{entry.version && entry.key !== account.id &&
                          <ActionForm label={t("إزالة النطاق", "Remove scope")} onSubmit={async () => {
                            await directory.replaceGrants(entry.key, entry.account.roles ?? [], entry.account.departments?.filter(other => other.id !== scope.id).map(other => other.id) ?? [], entry.version!);
                            setRevision(value => value + 1);
                          }}><span className="sr-only">{scope.code}</span></ActionForm>}</div>) ?? scopeName(entry.account.department, t)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="panel">
                <h2>{t("تعديل صلاحيات حساب", "Edit account access")}</h2>
                <ActionForm
                  label={t("حفظ الصلاحيات", "Save permissions")}
                  onSubmit={async (data) => {
                    if (!role || !key) throw new ServiceError("invalid");
                    await directory.assign(
                      account,
                      {
                        key,
                        role,
                        scope: String(data.get("scope") ?? ""),
                        confirmAdmin: data.get("confirmAdmin") === "on",
                        ...(entries.find(entry => entry.key === key)?.version ? { version: entries.find(entry => entry.key === key)?.version } : {}),
                      },
                    );
                    setRevision((v) => v + 1);
                  }}
                >
                  <Field label={t("الحساب", "Account")} required>
                    <select
                      required
                      value={key}
                      onChange={(e) => {
                        setKey(e.target.value);
                        setRole("");
                      }}
                    >
                      <option value="">{t("اختر الحساب", "Choose account")}</option>
                      {entries
                        .filter(
                          (entry) =>
                            entry.account.id !== account.id &&
                            entry.account.role !== "unverified",
                        )
                        .map((entry) => (
                          <option key={entry.key} value={entry.key}>
                            {entry.account.name}
                          </option>
                        ))}
                    </select>
                  </Field>
                  <Field label={t("الدور", "Role")} required>
                    <select
                      required
                      value={role}
                      onChange={(e) =>
                        setRole(e.target.value as AssignableRole)
                      }
                    >
                      <option value="">{t("اختر الدور", "Choose role")}</option>
                      <option value="user">{t("مستخدم", "User")}</option>
                      <option value="maintenance">
                        {t("موظف صيانة", "Maintenance staff")}
                      </option>
                      <option value="department">
                        {t("موظف جهة", "Department staff")}
                      </option>
                      <option value="admin">
                        {t("مدير", "Administrator")}
                      </option>
                    </select>
                  </Field>
                  {role === "maintenance" && (
                    <Field label={t("النطاق", "Scope")}>
                      <select name="scope">
                        <DirectoryOptions kind="maintenance" />
                      </select>
                    </Field>
                  )}
                  {role === "department" && (
                    <Field label={t("الجهة", "Department")} required>
                      <select name="scope">
                        <DirectoryOptions />
                      </select>
                    </Field>
                  )}
                  {role === "admin" && (
                    <label className="check">
                      <input type="checkbox" name="confirmAdmin" required />
                      {t(
                        "أؤكد منح صلاحيات مدير.",
                        "I confirm granting administrator access.",
                      )}
                    </label>
                  )}
                </ActionForm>
              </div>
            </div>
          </>
        )}
      </AsyncView>
    </Page>
  );
}
