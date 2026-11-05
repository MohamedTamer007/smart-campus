import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { ActionForm, Feedback, Field, Page } from "../../components/ui";
import {
  directory,
  calculateStats,
  type AssignableRole,
  type DashboardScope,
} from "../../services/admin";
import {
  maintenance,
  type MaintenanceStatus,
} from "../../services/maintenance";
import { lostFound, currentCustodian } from "../../services/lostFound";
import { actors, type Role } from "../../services/contracts";
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
    finder: ["مستخدم", "User"],
    other: ["مستخدم", "User"],
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
  const { t, account, scenario } = useApp();
  const load = useCallback(async () => {
    const [reports, items, claims] = await Promise.all([
      maintenance.list(account, scenario),
      lostFound.itemsFor(account, scenario),
      lostFound.claims(account, scenario),
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
  }, [account, scenario]);
  return (
    <Page
      title={t("طلباتي", "My requests")}
      subtitle={t(
        "تابع بلاغاتك وأغراضك وطلبات الاسترداد.",
        "Track your reports, items and recovery requests.",
      )}
    >
      <AsyncView key={`${account.id}-${scenario}`} load={load}>
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
  const { t, account, scenario } = useApp();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [scope, setScope] = useState<DashboardScope>("all");
  const load = useCallback(async () => {
    const [reports, items, claims] = await Promise.all([
      maintenance.list(account, scenario),
      lostFound.itemsFor(account, scenario),
      lostFound.claims(account, scenario),
    ]);
    return { reports, items, claims };
  }, [account, scenario]);
  return (
    <Page
      title={t("لوحة الإدارة", "Admin overview")}
      subtitle={t(
        "إحصاءات توضيحية من سجلات التجربة فقط.",
        "Illustrative statistics derived from demo records only.",
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
            <option value="maintenance">{t("الصيانة", "Maintenance")}</option>
            <option value="security">{t("الأمن", "Security")}</option>
            <option value="secretariat">
              {t("السكرتارية", "Secretariat")}
            </option>
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
        <Link className="button" to="/staff">
          {t("بلاغات الصيانة", "Maintenance queue")}
        </Link>
        <Link className="button" to="/admin/items">
          {t("المعثورات", "Found items")}
        </Link>
      </div>
      <div className="spacer" />
      <AsyncView key={`${account.id}-${scenario}`} load={load}>
        {(data) => {
          const stats = calculateStats(data.reports, data.items, data.claims, {
            from,
            to,
            scope,
          });
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
                        {location === "demo-faculty"
                          ? t("مرفق تجريبي", "Demo facility")
                          : location}
                        : {count}
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
    </Page>
  );
}
export function AdminAccounts() {
  const { t, account, scenario, actionScenario } = useApp();
  const [revision, setRevision] = useState(0);
  const [key, setKey] = useState<string>(
    () =>
      (Object.keys(actors) as Role[]).find(
        (candidate) =>
          !["guest", "unverified"].includes(candidate) &&
          actors[candidate].id !== account.id,
      ) ?? "finder",
  );
  const [role, setRole] = useState<AssignableRole | "">("");
  const load = useCallback(
    () => directory.list(account, scenario),
    [account, scenario],
  );
  return (
    <Page
      title={t("المستخدمون والصلاحيات", "Accounts and permissions")}
      subtitle={t(
        "التغييرات هنا محاكاة فقط ولا تمنح صلاحيات حقيقية.",
        "Changes here are simulated and grant no real permissions.",
      )}
    >
      <AsyncView key={`${account.id}-${scenario}-${revision}`} load={load}>
        {(entries) => (
          <>
            <div className="split">
              <div className="panel table-wrap">
                <h2>{t("الحسابات التجريبية", "Demo accounts")}</h2>
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
                        <td>{roleName(entry.account.role, t)}</td>
                        <td>{scopeName(entry.account.department, t)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="panel">
                <h2>{t("تعديل صلاحيات حساب", "Edit account access")}</h2>
                <ActionForm
                  label={t("حفظ الصلاحيات التجريبية", "Save demo permissions")}
                  onSubmit={async (data) => {
                    if (!role) return;
                    await directory.assign(
                      account,
                      key,
                      role,
                      String(data.get("scope") ?? ""),
                      data.get("confirmAdmin") === "on",
                      actionScenario,
                    );
                    setRevision((v) => v + 1);
                  }}
                >
                  <Field label={t("الحساب", "Account")} required>
                    <select
                      value={key}
                      onChange={(e) => {
                        setKey(e.target.value);
                        setRole("");
                      }}
                    >
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
                        <option value="maintenance">
                          {t("الصيانة", "Maintenance")}
                        </option>
                      </select>
                    </Field>
                  )}
                  {role === "department" && (
                    <Field label={t("الجهة", "Department")} required>
                      <select name="scope">
                        <option value="security">
                          {t("الأمن", "Security")}
                        </option>
                        <option value="secretariat">
                          {t("السكرتارية", "Secretariat")}
                        </option>
                      </select>
                    </Field>
                  )}
                  {role === "admin" && (
                    <label className="check">
                      <input type="checkbox" name="confirmAdmin" required />
                      {t(
                        "أؤكد منح صلاحيات مدير تجريبية.",
                        "I confirm granting simulated administrator access.",
                      )}
                    </label>
                  )}
                </ActionForm>
              </div>
            </div>
            <div className="panel">
              <h2>
                {t("الجهات المسجلة في التجربة", "Registered demo departments")}
              </h2>
              <p>
                {t(
                  "الأمن والسكرتارية أمثلة فقط؛ الدليل الفعلي لم يُقدّم بعد.",
                  "Security and Secretariat are illustrative; the real directory has not been supplied.",
                )}
              </p>
            </div>
          </>
        )}
      </AsyncView>
    </Page>
  );
}
