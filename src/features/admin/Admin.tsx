import { useCallback, useEffect, useState } from "react";
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
  demoPreparationStatus,
  prepareDemoEnvironment,
  type DemoPreparationResult,
} from "../../services/admin";
import {
  maintenance,
  type MaintenanceStatus,
} from "../../services/maintenance";
import { lostFound, currentCustodian } from "../../services/lostFound";
import { ServiceError, type Role } from "../../services/contracts";
import { OperationsStatus } from "./OperationsStatus";
function DemoEnvironmentSetup() {
  const { t } = useApp();
  const [enabled, setEnabled] = useState(false);
  const [prepared, setPrepared] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<DemoPreparationResult | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    void demoPreparationStatus().then(status => {
      setEnabled(status.enabled);
      setPrepared(status.alreadyPrepared);
    }).catch(() => setEnabled(false));
  }, []);
  if (!enabled) return null;
  async function prepare() {
    setBusy(true);
    setFailed(false);
    try {
      const preparedResult = await prepareDemoEnvironment();
      setResult(preparedResult);
      setPrepared(true);
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }
  return <section className="panel demo-setup" aria-labelledby="demo-setup-title">
    <h2 id="demo-setup-title">{t("تجهيز حسابات العرض", "Prepare demo accounts")}</h2>
    <p>{t("ينشئ خمس هويات وبيانات خيالية فقط. كلمة المرور تظهر مرة واحدة بعد الإنشاء؛ احفظها في مكان آمن. بعد التجهيز عطّل Demo:PreparationEnabled من إعدادات الاستضافة.", "Creates five fictional identities and records only. Passwords appear once after creation; save them securely. After setup, disable Demo:PreparationEnabled in hosting settings.")}</p>
    {failed && <Feedback kind="error">{t("تعذر تجهيز العرض. تحقق من إعداد التخزين وسجل التشغيل.", "Demo preparation failed. Check storage configuration and the application log.")}</Feedback>}
    {prepared && !result && <Feedback>{t("البيانات مجهزة بالفعل. لن تُعرض كلمات المرور مرة أخرى.", "Demo data is already prepared. Passwords are not shown again.")}</Feedback>}
    {!prepared && <button className="button" type="button" disabled={busy} onClick={() => { void prepare(); }}>
      {busy ? t("جارٍ التجهيز…", "Preparing…") : t("جهّز حسابات وبيانات العرض", "Prepare demo accounts and data")}
    </button>}
    {result && <div role="status">
      <Feedback>{t("اكتمل تجهيز البيانات. احفظ كلمات المرور الآن ثم عطّل الإعداد المؤقت.", "Demo data is ready. Save the passwords now, then disable the temporary setting.")}</Feedback>
      {result.credentials.length > 0 ? <div className="table-wrap"><table className="data-table demo-credentials">
        <thead><tr><th>{t("الدور", "Role")}</th><th>Email</th><th>{t("كلمة المرور التي تظهر مرة واحدة", "One-time password")}</th></tr></thead>
        <tbody>{result.credentials.map(credential => <tr key={credential.role}>
          <td>{credential.role}</td><td><bdi>{credential.email}</bdi></td><td><code>{credential.password ?? "—"}</code></td>
        </tr>)}</tbody>
      </table></div> : <p>{t("التهيئة موجودة من قبل، ولا يمكن استعادة كلمات المرور من النظام.", "The setup already existed; passwords cannot be recovered from the system.")}</p>}
    </div>}
  </section>;
}
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
      <DemoEnvironmentSetup />
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
        <Link className="button" to="/admin/ai-evaluation">
          {t("نتائج تقييم الذكاء الاصطناعي", "AI Evaluation")}
        </Link>
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
                  <h2>{t("الأعطال المتكررة التي سجلها الموظفون", "Recurring staff-recorded faults")}</h2>
                  {(stats.recurrence ?? []).length ? stats.recurrence!.map((fault) => <p key={`${fault.type}-${fault.buildingAr}-${fault.floorAr}`}>
                    {fault.type} · {t(fault.buildingAr, fault.buildingEn)} · {t(fault.floorAr, fault.floorEn)}: {fault.count}
                  </p>) : <p>{t("لا توجد أعطال متكررة معتمدة في هذه الفترة.", "No staff-confirmed recurring faults in this period.")}</p>}
                </div>
              </div>
              <div className="split">
                <section className="panel campus-map-panel">
                  <h2>{t("مخطط الحرم التجريبي", "Demo campus map")}</h2>
                  <p>{t("مخطط توضيحي غير جغرافي؛ الأسماء والأعداد من بيانات النظام، وتُحفظ مواضع المباني يدويًا.", "Schematic, not geographic. Names and counts come from records; building positions are set manually.")}</p>
                  <svg className="campus-map" viewBox="0 0 100 56" role="img" aria-label={t("مخطط مباني الحرم وعدد البلاغات", "Campus building schematic and report counts")}>
                    <path d="M2 27 Q20 19 34 29 T66 27 T98 30 M5 46 Q22 36 40 44 T75 43 T98 46 M25 4 Q33 15 28 27 T31 52 M70 4 Q63 17 71 29 T68 52" fill="none" stroke="#dce5ef" strokeWidth="2" />
                    {(stats.buildings ?? []).filter(b => b.x != null && b.y != null).map(b => <g key={b.id} transform={`translate(${b.x} ${b.y})`}>
                      <circle r="2.5" fill="#144caa" stroke="white" strokeWidth=".8" />
                      <text y="-3.5" textAnchor="middle" fontSize="2.5" fill="#142b54">{t(b.ar, b.en)}</text>
                      <text y="5.5" textAnchor="middle" fontSize="2.7" fontWeight="700" fill="#c44300">{b.count}</text>
                    </g>)}
                  </svg>
                  {account.role === "admin" && <div className="map-editor no-print">
                    {mapSaveFailed && <Feedback kind="error">{t("تعذر حفظ الموضع؛ أعد تحميل اللوحة ثم حاول مجددًا.", "The position could not be saved. Reload the dashboard and try again.")}</Feedback>}
                    <Field label={t("مبنى لتحديد موضعه", "Building to position")}>
                      <select value={buildingId} onChange={event => { setBuildingId(event.target.value); setMapDraft(null); }}>
                        <option value="">{t("اختر مبنى", "Choose a building")}</option>
                        {[...(stats.unplacedBuildings ?? []), ...(stats.buildings ?? [])].map(b => <option key={b.id} value={b.id}>{t(b.ar, b.en)}</option>)}
                      </select>
                    </Field>
                    <p>{mapDraft ? `${t("الموضع", "Position")}: ${mapDraft.x.toFixed(1)}, ${mapDraft.y.toFixed(1)}` : t("اختر المبنى ثم انقر المخطط لتحديد موضعه.", "Choose a building, then click the schematic to set its position.")}</p>
                    <svg className="campus-map map-editor-canvas" viewBox="0 0 100 56" preserveAspectRatio="none" onClick={event => {
                      if (!buildingId) return;
                      const rect = event.currentTarget.getBoundingClientRect();
                      setMapDraft({ x: Math.max(0, Math.min(100, (event.clientX - rect.left) / rect.width * 100)), y: Math.max(0, Math.min(56, (event.clientY - rect.top) / rect.height * 56)) });
                    }} aria-label={t("انقر لاختيار موضع المبنى", "Click to choose a building position")}>
                      <path d="M2 27 Q20 19 34 29 T66 27 T98 30 M5 46 Q22 36 40 44 T75 43 T98 46 M25 4 Q33 15 28 27 T31 52 M70 4 Q63 17 71 29 T68 52" fill="none" stroke="#dce5ef" strokeWidth="2" />
                      {(stats.buildings ?? []).filter(b => b.x != null && b.y != null).map(b => <circle key={b.id} cx={b.x!} cy={b.y!} r="2" fill="#144caa" />)}
                      {mapDraft && <circle cx={mapDraft.x} cy={mapDraft.y} r="2.8" fill="#c44300" />}
                    </svg>
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
                <section className="panel">
                  <h2>{t("الاتجاه الأسبوعي للبلاغات", "Weekly report trend")}</h2>
                  {(stats.weeklyTrend ?? []).map(w => <p key={w.weekOf}><bdi>{w.weekOf}</bdi> · {w.count}</p>)}
                </section>
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
