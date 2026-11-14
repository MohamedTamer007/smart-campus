import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { ActionForm, Feedback, Field, Page } from "../../components/ui";
import { ServiceError } from "../../services/contracts";
import { campusDirectory, type LocationDraft } from "../../services/campusDirectory";

export function AdminCampusDirectory() {
  const { t } = useApp();
  const [revision, setRevision] = useState(0);
  const [kind, setKind] = useState<LocationDraft["kind"]>("faculty");
  const [parentId, setParentId] = useState("");
  const [saved, setSaved] = useState<"department" | "location" | null>(null);
  const load = useCallback(async () => {
    const [departments, locations] = await Promise.all([
      campusDirectory.departments(), campusDirectory.locations(),
    ]);
    return { departments, locations };
  }, []);

  return <Page
    title={t("دليل الجهات والمواقع", "Departments and locations")}
    subtitle={t(
      "أدخل بيانات الجامعة المعتمدة فقط. لا تُنشأ بيانات تجريبية أو صلاحيات موظفين تلقائياً.",
      "Enter only institution-approved data. No demo locations or staff permissions are created automatically.",
    )}
  >
    <div className="actions">
      <Link className="button secondary" to="/admin">{t("العودة إلى الإدارة", "Back to administration")}</Link>
      <Link className="button secondary" to="/admin/accounts">{t("إدارة صلاحيات الموظفين", "Manage staff access")}</Link>
    </div>
    {saved && <Feedback kind="success">{saved === "department"
      ? t("تم تسجيل الجهة. عيّن لها موظفين موثّقين من شاشة الصلاحيات.", "Department saved. Assign verified staff in the access screen.")
      : t("تم تسجيل الموقع.", "Location saved.")}</Feedback>}
    <AsyncView key={revision} load={load}>
      {({ departments, locations }) => {
        const maintenance = departments.filter(d => d.maintenance);
        const parentChoices = kind === "building"
          ? locations.map(f => ({ id: f.id, label: t(f.ar, f.en) }))
          : kind === "floor"
            ? locations.flatMap(f => f.buildings.map(b => ({
                id: b.id, label: `${t(f.ar, f.en)} / ${t(b.ar, b.en)}`,
              })))
            : [];

        return <div className="split">
          <div className="panel">
            <h2>{t("الجهات المسجلة", "Registered departments")}</h2>
            {departments.length ? departments.map(d =>
              <p key={d.id}>
                <strong>{t(d.ar, d.en)}</strong> <bdi>({d.code})</bdi>
                {" — "}{d.canReceive
                  ? t("تستلم عهدة", "Receives custody")
                  : t("لا تستلم عهدة", "No custody intake")}
                {d.maintenance && ` · ${t("صيانة", "Maintenance")}`}
              </p>,
            ) : <Feedback>{t("لا توجد جهات مسجلة بعد.", "No departments have been configured yet.")}</Feedback>}
            <h2>{t("إنشاء جهة معتمدة", "Add approved department")}</h2>
            <ActionForm label={t("إضافة الجهة", "Add department")} onSubmit={async data => {
              const code = String(data.get("code") ?? "").trim();
              const ar = String(data.get("ar") ?? "").trim();
              const en = String(data.get("en") ?? "").trim();
              if (!code || !ar || !en) throw new ServiceError("invalid");
              await campusDirectory.createDepartment({
                code, ar, en,
                canReceive: data.get("canReceive") === "on",
                maintenance: data.get("maintenance") === "on",
              });
              setSaved("department");
              setRevision(previous => previous + 1);
            }}>
              <Field label={t("رمز الجهة", "Department code")} required>
                <input name="code" maxLength={50} required />
              </Field>
              <Field label={t("الاسم بالعربية", "Arabic name")} required>
                <input name="ar" maxLength={150} required />
              </Field>
              <Field label={t("الاسم بالإنجليزية", "English name")} required>
                <input name="en" maxLength={150} required />
              </Field>
              <label className="check">
                <input name="canReceive" type="checkbox" />
                {t("مسموح لها باستلام المفقودات", "Authorized to receive found items")}
              </label>
              <label className="check">
                <input name="maintenance" type="checkbox" />
                {t("جهة مسؤولة عن الصيانة", "Maintenance department")}
              </label>
            </ActionForm>
          </div>
          <div className="panel">
            <h2>{t("مواقع الجامعة", "Campus locations")}</h2>
            {locations.length ? locations.map(f =>
              <div key={f.id}>
                <strong>{t(f.ar, f.en)}</strong>
                {f.buildings.map(b => <p key={b.id}>
                  {t(b.ar, b.en)} — {b.floors.length
                    ? b.floors.map(floor => t(floor.ar, floor.en)).join("، ")
                    : t("لا توجد أدوار", "No floors")}
                </p>)}
              </div>,
            ) : <Feedback>{t("لا توجد كليات أو مرافق مسجلة.", "No faculties or facilities configured.")}</Feedback>}
            <h2>{t("إضافة موقع معتمد", "Add approved location")}</h2>
            <ActionForm label={t("إضافة الموقع", "Add location")} onSubmit={async data => {
              const ar = String(data.get("ar") ?? "").trim();
              const en = String(data.get("en") ?? "").trim();
              const maintenanceDepartmentId = String(data.get("maintenanceDepartmentId") ?? "") || null;
              if (!ar || !en || (kind !== "faculty" && !parentId) ||
                  (kind === "faculty" && !maintenanceDepartmentId))
                throw new ServiceError("invalid");
              await campusDirectory.createLocation({
                kind, parentId: kind === "faculty" ? null : parentId,
                ar, en, maintenanceDepartmentId,
              });
              setSaved("location");
              setParentId("");
              setRevision(previous => previous + 1);
            }}>
              <Field label={t("نوع الموقع", "Location kind")} required>
                <select value={kind} onChange={event => {
                  setKind(event.target.value as LocationDraft["kind"]);
                  setParentId("");
                }}>
                  <option value="faculty">{t("كلية أو مرفق", "Faculty or facility")}</option>
                  <option value="building">{t("مبنى", "Building")}</option>
                  <option value="floor">{t("دور", "Floor")}</option>
                </select>
              </Field>
              {kind !== "faculty" && <Field label={kind === "building"
                ? t("الكلية أو المرفق", "Faculty or facility")
                : t("المبنى", "Building")} required>
                <select required value={parentId} onChange={event => setParentId(event.target.value)}>
                  <option value="">{t("اختر الموقع الأب", "Choose parent location")}</option>
                  {parentChoices.map(parent => <option key={parent.id} value={parent.id}>{parent.label}</option>)}
                </select>
              </Field>}
              <Field label={t("الاسم بالعربية", "Arabic name")} required>
                <input name="ar" maxLength={150} required />
              </Field>
              <Field label={t("الاسم بالإنجليزية", "English name")} required>
                <input name="en" maxLength={150} required />
              </Field>
              <Field label={t("جهة الصيانة المسؤولة", "Responsible maintenance department")}
                required={kind === "faculty"}
                hint={t(
                  "يلزم ربط الكلية بجهة صيانة. ويمكن للمبنى أو الدور وراثة الجهة من المستوى الأعلى.",
                  "Faculties require a maintenance department. Buildings and floors may inherit it from a parent.",
                )}>
                <select name="maintenanceDepartmentId" required={kind === "faculty"} defaultValue="">
                  <option value="">{t("استخدام جهة المستوى الأعلى / اختر", "Use parent routing / choose")}</option>
                  {maintenance.map(d => <option key={d.id} value={d.id}>{t(d.ar, d.en)}</option>)}
                </select>
              </Field>
            </ActionForm>
          </div>
        </div>;
      }}
    </AsyncView>
    <Feedback>{t(
      "إنشاء جهة مستلمة لا يمنح أي موظف حق الاستلام تلقائياً؛ يتطلب التسليم حساب موظف موثّق ودور جهة ونطاقاً مسجلاً.",
      "Creating a receiving department does not grant staff access. Actual handover requires a verified department staff account and matching scope.",
    )}</Feedback>
  </Page>;
}
