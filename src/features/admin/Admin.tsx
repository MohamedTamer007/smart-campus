import { formatRecordDate } from "../../services/presentation";
import { CampusMapEditor } from "./CampusMapEditor";
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
  saveBuildingPosition,
} from "../../services/admin";
import {
  maintenance,
  maintenanceValueLabel,
  type MaintenanceStatus,
} from "../../services/maintenance";
import { lostFound, currentCustodian } from "../../services/lostFound";
import { hasRole, ServiceError, type Role } from "../../services/contracts";
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
function MetricDistribution({
  title,
  values,
  t,
}: {
  title: string;
  values: Record<string, number>;
  t: (arabic: string, english: string) => string;
}) {
  const entries = Object.entries(values).sort(([left], [right]) => left.localeCompare(right));
  const total = entries.reduce((sum, [, count]) => sum + count, 0);
  return (
    <div className="panel">
      <h2>{title}</h2>
      {entries.length ? (
        <div className="bar-chart">
          {entries.map(([value, count]) => (
            <div className="bar-row" key={value}>
              <span>{maintenanceValueLabel(value, t)}</span>
              <div className="bar-track" aria-hidden="true">
                <span style={{ width: `${total ? (count / total) * 100 : 0}%` }} />
              </div>
              <b>{count}</b>
            </div>
          ))}
        </div>
      ) : (
        <Feedback>{t("لا توجد قرارات موظفين ضمن الفترة المحددة.", "No staff decisions in the selected period.")}</Feedback>
      )}
    </div>
  );
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
  const { t, account, language } = useApp();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [scope, setScope] = useState<DashboardScope>("all");
  const [revision, setRevision] = useState(0);
  const [buildingId, setBuildingId] = useState("");
  const [mapDraft, setMapDraft] = useState<{ x: number; y: number } | null>(null);
  const [mapSaveFailed, setMapSaveFailed] = useState(false);
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
        <button className="button secondary no-print" onClick={() => window.print()}>{t("طباعة التقرير", "Print report")}</button>
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
      <AsyncView key={`${account.id}-${from}-${to}-${scope}-${revision}`} load={load}>
        {(stats) => {
          const mapBuildings = Array.from(new Map(
            [...(stats.unplacedBuildings ?? []), ...(stats.buildings ?? [])].map(building => [building.id, building]),
          ).values());
          const selectedBuilding = mapBuildings.find(building => building.id === buildingId);
          const selectedPosition = mapDraft ?? (selectedBuilding?.x != null && selectedBuilding.y != null
            ? { x: selectedBuilding.x, y: selectedBuilding.y } : null);
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
                  <span>{t("بلاغات مفتوحة", "Open reports")}</span>
                  <strong>{stats.openReports ?? stats.byStatus.submitted + stats.byStatus.review + stats.byStatus.repair}</strong>
                </div>
                <div className="stat">
                  <span>{t("نسبة الحل", "Resolution rate")}</span>
                  <strong>{stats.resolutionRate === undefined ? "—" : `${Math.round(stats.resolutionRate * 100)}%`}</strong>
                </div>
                <div className="stat">
                  <span>{t("متوسط زمن الحل", "Average resolution time")}</span>
                  <strong>{stats.averageResolutionHours == null ? "—" : `${stats.averageResolutionHours.toFixed(1)} ${t("ساعة", "h")}`}</strong>
                </div>
                <div className="stat">
                  <span>{t("عمر أقدم بلاغ مفتوح", "Oldest open report")}</span>
                  <strong>{stats.oldestOpenDays == null ? "—" : `${stats.oldestOpenDays.toFixed(1)} ${t("يوم", "days")}`}</strong>
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
                    {t("توزيع البلاغات حسب المبنى", "Reports by building")}
                  </h2>
                  {(stats.buildings ?? []).length ? (
                    stats.buildings!.map((building) => (
                      <p key={building.id}>
                        {t(building.ar, building.en)}: {building.count}
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
                        {maintenanceValueLabel(type, t)}: {count}
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
                  <h2>{t("الأعطال المتكررة التي سجلها الموظفون", "Recurring staff-recorded faults")}</h2>
                  {(stats.recurrence ?? []).length ? stats.recurrence!.map((fault) => <p key={`${fault.type}-${fault.buildingAr}-${fault.floorAr}`}>
                    {maintenanceValueLabel(fault.type, t)} · {t(fault.buildingAr, fault.buildingEn)} · {t(fault.floorAr, fault.floorEn)}: {fault.count}
                  </p>) : <p>{t("لا توجد أعطال متكررة معتمدة في هذه الفترة.", "No staff-confirmed recurring faults in this period.")}</p>}
                </div>
              </div>
              <p className="muted">
                {t("التوزيعات التالية من القرارات التشغيلية التي سجلها الموظفون؛ لا تشمل اقتراحات AI.", "The following distributions use staff-entered operational decisions; AI suggestions are excluded.")}
              </p>
              <div className="split">
                <MetricDistribution title={t("الأولوية المعتمدة", "Staff-confirmed priority")} values={stats.priorities ?? {}} t={t} />
                <MetricDistribution title={t("الخطورة المعتمدة", "Staff-confirmed risk")} values={stats.risks ?? {}} t={t} />
              </div>
              <div className="admin-dashboard-insights">
                <section className="panel">
                  <h2>{t("الاتجاه الأسبوعي للبلاغات", "Weekly report trend")}</h2>
                  {(stats.weeklyTrend ?? []).map(w => <p key={w.weekOf}><bdi>{formatRecordDate(w.weekOf, language)}</bdi> · {w.count}</p>)}
                </section>
                <OperationsStatus />
                <section className="panel campus-map-panel">
                  <h2>{t("مخطط مواقع البلاغات", "Report location map")}</h2>
                  <p>{t("صورة جوية للحرم؛ تظهر عليها المباني وأعداد البلاغات، ويمكن تحديد مواقع المباني بالنقر على الصورة.", "Aerial campus photo with building locations and report counts. Click the photo to place buildings.")}</p>
                  <div className="campus-map-stage">
                    <img className="campus-map-image" src="/campus-map.jpg" alt="" />
                    <svg className="campus-map-overlay" viewBox="0 0 100 52.58" role="img" aria-label={t("مواقع المباني وأعداد البلاغات على صورة الحرم", "Building locations and report counts on the campus photo")}>
                      {(stats.buildings ?? []).filter(b => b.x != null && b.y != null).map(b => <g key={b.id} transform={`translate(${b.x} ${b.y! * 52.58 / 56})`}>
                        <circle r="1.4" fill="#144caa" stroke="white" strokeWidth=".45" />
                        <text y="-2.2" textAnchor="middle" fontSize="1.5" fill="#fff" stroke="#142b54" strokeWidth=".2" paintOrder="stroke">{t(b.ar, b.en)}</text>
                        <text y="3.2" textAnchor="middle" fontSize="1.7" fontWeight="700" fill="#fff" stroke="#c44300" strokeWidth=".2" paintOrder="stroke">{b.count}</text>
                      </g>)}
                    </svg>
                  </div>
                  {hasRole(account, "admin") && <div className="map-editor no-print">
                    {mapSaveFailed && <Feedback kind="error">{t("تعذر حفظ الموضع؛ أعد تحميل اللوحة ثم حاول مجددًا.", "The position could not be saved. Reload the dashboard and try again.")}</Feedback>}
                    <div className="map-editor-controls">
                      <Field label={t("مبنى لتحديد موضعه", "Building to position")}>
                        <select value={buildingId} onChange={event => { setBuildingId(event.target.value); setMapDraft(null); }}>
                          <option value="">{t("اختر مبنى", "Choose a building")}</option>
                          {mapBuildings.map(b => <option key={b.id} value={b.id}>{t(b.ar, b.en)}</option>)}
                        </select>
                      </Field>
                      <p id="map-editor-help">{mapDraft ? `${t("الموضع", "Position")}: ${mapDraft.x.toFixed(1)}, ${mapDraft.y.toFixed(1)}` : t("اختر المبنى ثم انقر الصورة أو اسحب علامته. يمكنك التكبير واستخدام الأسهم؛ Shift يحرك الموضع خمس خطوات.", "Choose a building, then click the photo or drag its marker. You can zoom and use arrow keys; Shift moves five steps.")}</p>
                    </div>
                    <CampusMapEditor
                      enabled={!!buildingId}
                      position={selectedPosition}
                      buildings={stats.buildings ?? []}
                      onPosition={setMapDraft}
                    />
                    <div className="actions">
                      <button className="button" disabled={!buildingId || !mapDraft} onClick={async () => {
                        const target = (stats.unplacedBuildings ?? []).concat(stats.buildings ?? []).find(b => b.id === buildingId);
                        if (!target || !mapDraft) return;
                        setMapSaveFailed(false);
                        try {
                          const result = await saveBuildingPosition(buildingId, mapDraft.x, mapDraft.y, target.version);
                          if (result.version) { setBuildingId(""); setMapDraft(null); setRevision(value => value + 1); }
                        } catch { setMapSaveFailed(true); }
                      }}>{t("حفظ الموضع", "Save position")}</button>
                      {buildingId && (stats.buildings ?? []).some(b => b.id === buildingId) && <button className="button secondary" onClick={async () => {
                        const target = stats.buildings!.find(b => b.id === buildingId)!;
                        setMapSaveFailed(false);
                        try {
                          const result = await saveBuildingPosition(buildingId, null, null, target.version);
                          if (result.version) { setBuildingId(""); setMapDraft(null); setRevision(value => value + 1); }
                        } catch { setMapSaveFailed(true); }
                      }}>{t("إزالة من المخطط", "Remove from map")}</button>}
                    </div>
                  </div>}
                  <h3>{t("مواقع غير موضوعة على المخطط", "Locations not placed on the map")}</h3>
                  {(stats.unplacedBuildings ?? []).length ? (stats.unplacedBuildings ?? []).map(b => <p key={b.id}>{t(b.ar, b.en)} · {b.count}</p>) : <p>{t("جميع المباني ذات الصلة موضوعة.", "All relevant buildings are placed.")}</p>}
                </section>
              </div>
            </>
          );
        }}
      </AsyncView>
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
                    <Field label={t("النطاق", "Scope")} required>
                      <select name="scope" required defaultValue="">
                        <option value="">{t("اختر جهة الصيانة", "Choose maintenance department")}</option>
                        <DirectoryOptions kind="maintenance" />
                      </select>
                    </Field>
                  )}
                  {role === "department" && (
                    <Field label={t("الجهة", "Department")} required>
                      <select name="scope" required defaultValue="">
                        <option value="">{t("اختر الجهة", "Choose department")}</option>
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
