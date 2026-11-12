import { useCallback, useEffect, useState } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { ActionForm, Feedback, Field, Page } from "../../components/ui";
import { ServiceError } from "../../services/contracts";
import {
  maintenance,
  maintenanceInput,
  type LocationOption,
  type MaintenanceInput,
  type MaintenanceReport,
  type MaintenanceStatus,
} from "../../services/maintenance";

const labels: Record<MaintenanceStatus, [string, string]> = {
  submitted: ["تم الإرسال", "Submitted"],
  review: ["قيد المراجعة", "Under review"],
  repair: ["جارٍ الإصلاح", "Being repaired"],
  resolved: ["تم الحل", "Resolved"],
  rejected: ["مرفوض", "Rejected"],
  cancelled: ["ملغى", "Cancelled"],
};
const drafts = new Map<string, MaintenanceInput>();
const emptyInput = (): MaintenanceInput => ({
  description: "",
  faculty: "",
  building: "",
  floor: "",
  details: "",
});
function Status({ value }: { value: MaintenanceStatus }) {
  const { t } = useApp();
  return <span className="tag">{t(...labels[value])}</span>;
}
function displayDate(date: string, language: string) {
  return new Intl.DateTimeFormat(language === "ar" ? "ar-EG" : "en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Africa/Cairo",
  }).format(new Date(date));
}
function PhotoPreview({ photo }: { photo?: File }) {
  const [preview, setPreview] = useState<{ file: File; url: string }>();
  const { t } = useApp();
  useEffect(() => {
    if (!photo) return;
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string")
        setPreview({ file: photo, url: reader.result });
    };
    reader.readAsDataURL(photo);
    return () => {
      reader.onload = null;
      if (reader.readyState === FileReader.LOADING) reader.abort();
    };
  }, [photo]);
  return photo && preview?.file === photo ? (
    <img
      className="preview"
      src={preview.url}
      alt={t("معاينة الصورة المرفقة", "Attached image preview")}
    />
  ) : null;
}
export function ImageField({
  photo,
  onChange,
  required = false,
  label,
}: {
  photo?: File;
  onChange: (photo?: File) => void;
  required?: boolean;
  label?: string;
}) {
  const { t } = useApp();
  const [error, setError] = useState("");
  return (
    <div className="upload">
      <Field
        label={
          label ??
          (required
            ? t("صورة الغرض", "Item photo")
            : t("صورة العطل — اختيارية", "Fault photo — optional"))
        }
        required={required}
        hint={t(
          "تظهر المعاينة على جهازك قبل الإرسال.",
          "The image is previewed on your device before submission.",
        )}
      >
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp"
          required={required && !photo}
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file && (!["image/jpeg", "image/png", "image/webp"].includes(file.type) || file.size > 5 * 1024 * 1024)) {
              setError(
                t("اختر JPEG أو PNG أو WebP حتى 5 ميجابايت.", "Choose JPEG, PNG or WebP up to 5 MiB."),
              );
              onChange();
            } else {
              setError("");
              onChange(file);
            }
            event.currentTarget.value = "";
          }}
        />
      </Field>
      {error && <Feedback kind="error">{error}</Feedback>}
      <PhotoPreview photo={photo} />
      {photo && (
        <button type="button" className="button" onClick={() => onChange()}>
          {t("إزالة الصورة", "Remove photo")}
        </button>
      )}
    </div>
  );
}
function ReportForm({
  initial,
  locations,
  report,
  onSaved,
}: {
  initial: MaintenanceInput;
  locations: LocationOption[];
  report?: MaintenanceReport;
  onSaved?: () => void;
}) {
  const { t, account } = useApp();
  const navigate = useNavigate();
  const key = `${account.id}:${report?.id ?? "new"}`;
  const [values, setValues] = useState<MaintenanceInput>(
    () => drafts.get(key) ?? maintenanceInput(initial),
  );
  function change(next: Partial<MaintenanceInput>) {
    setValues((previous) => {
      const value = { ...previous, ...next };
      drafts.set(key, value);
      return value;
    });
  }
  const faculty = locations.find((item) => item.id === values.faculty);
  const building = faculty?.buildings.find(
    (item) => item.id === values.building,
  );
  async function submit() {
    if (!values.description.trim() || !values.details.trim())
      throw new ServiceError("invalid");
    const result = report
      ? await maintenance.edit(
          account,
          { id: report.id, version: report.version, input: values },
        )
      : await maintenance.create(account, values);
    drafts.delete(key);
    if (report && onSaved) onSaved();
    else navigate(`/maintenance/${result.id}?saved=1`);
  }
  return (
    <ActionForm
      label={
        report
          ? t("حفظ التغييرات", "Save changes")
          : t("إرسال البلاغ", "Submit report")
      }
      onSubmit={submit}
    >
      <Field label={t("وصف العطل", "Fault description")} required>
        <textarea
          value={values.description}
          required
          onChange={(e) => change({ description: e.target.value })}
        />
      </Field>
      <div className="field-grid">
        <Field label={t("الكلية أو المرفق", "Faculty or facility")} required>
          <select
            required
            value={values.faculty}
            onChange={(e) =>
              change({ faculty: e.target.value, building: "", floor: "" })
            }
          >
            <option value="">{t("اختر المرفق", "Choose facility")}</option>
            {locations.map((f) => (
              <option value={f.id} key={f.id}>
                {t(f.ar, f.en)}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t("المبنى", "Building")} required>
          <select
            required
            value={values.building}
            onChange={(e) => change({ building: e.target.value, floor: "" })}
          >
            <option value="">{t("اختر المبنى", "Choose building")}</option>
            {faculty?.buildings.map((b) => (
              <option value={b.id} key={b.id}>
                {t(b.ar, b.en)}
              </option>
            ))}
          </select>
        </Field>
      </div>
      <Field label={t("الدور", "Floor")} required>
        <select
          required
          value={values.floor}
          onChange={(e) => change({ floor: e.target.value })}
        >
          <option value="">{t("اختر الدور", "Choose floor")}</option>
          {building?.floors.map((f) => (
            <option value={f.id} key={f.id}>
              {t(f.ar, f.en)}
            </option>
          ))}
        </select>
      </Field>
      <Field label={t("تفاصيل الموقع", "Precise location")} required>
        <input
          required
          value={values.details}
          onChange={(e) => change({ details: e.target.value })}
        />
      </Field>
      <ImageField
        photo={values.photo}
        onChange={(photo) => change({ photo })}
      />
    </ActionForm>
  );
}
export function NewMaintenance() {
  const { t, account } = useApp();
  const load = useCallback(() => maintenance.locations(account), [account]);
  return (
    <Page
      title={t("أبلغ عن عطل", "Report a fault")}
      subtitle={t(
        "أخبرنا عن المشكلة وموقعها بالتفصيل.",
        "Tell us about the issue and its precise location.",
      )}
    >
      <div className="panel narrow">
        <AsyncView key={`${account.id}`} load={load}>
          {(locations) =>
            !locations.length ? (
              <Feedback>
                {t(
                  "بيانات المواقع غير متاحة حالياً.",
                  "Location data is currently unavailable.",
                )}
              </Feedback>
            ) : (
              <ReportForm initial={emptyInput()} locations={locations} />
            )
          }
        </AsyncView>
      </div>
    </Page>
  );
}
// The connectivity check never uses any report content, account details,
// uploaded photos or user-entered prompts. Hidden unless the server opts in.
function SyntheticAiCheck() {
  const { t } = useApp();
  const [enabled, setEnabled] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    let active = true;
    maintenance.syntheticAiStatus().then(result => {
      if (active) setEnabled(result.enabled);
    }).catch(() => {
      if (active) setEnabled(false);
    });
    return () => { active = false; };
  }, []);

  if (!enabled) return null;
  async function test() {
    if (busy) return;
    setBusy(true);
    setMessage(null);
    try {
      const result = await maintenance.syntheticAiTest();
      setMessage({
        ok: true,
        text: t("المودل رد بنجاح على وصف وهمي ثابت", "Model successfully responded to a fixed fictional example") +
          ` (${result.provider}: ${result.model})`,
      });
    } catch (error) {
      setMessage({
        ok: false,
        text: error instanceof ServiceError && error.code === "rate_limited"
          ? t("وصلت لحد الاختبار اليومي. الاختبار مرة واحدة لكل حساب.", "Daily test limit reached. One test per account.")
          : t("المودل لم يجتز الاختبار، راجع إعدادات السيرفر أو حصته.", "Model test failed; check server configuration and quota."),
      });
    } finally {
      setBusy(false);
    }
  }

  return <section className="panel">
    <h2>{t("اختبار اتصال الذكاء الاصطناعي", "AI connection test")}</h2>
    <p>{t("اختبار بنص وهمي ثابت، بدون إرسال بلاغك أو صورتك. يستخدم طلبًا واحدًا من رصيد المزود، ومتاح مرة واحدة لكل حساب يوميًا على بيئة الاختبار.", "Uses a fixed fictional text without sending your report or photo. Consumes one provider request, once per account each UTC day in the test environment.")}</p>
    <button type="button" className="button" disabled={busy} onClick={() => { void test(); }}>
      {busy ? t("جارٍ الاختبار…", "Testing…") : t("اختبر مودل الصيانة", "Test maintenance AI")}
    </button>
    {message && <Feedback kind={message.ok ? "success" : "error"}>{message.text}</Feedback>}
  </section>;
}

export function MaintenanceList({ staff = false }: { staff?: boolean }) {
  const { t, account } = useApp();
  const load = useCallback(
    () => maintenance.list(account),
    [account],
  );
  return (
    <Page
      title={
        staff
          ? t("بلاغات الصيانة", "Maintenance queue")
          : t("بلاغات الصيانة الخاصة بي", "My maintenance reports")
      }
    >
      <div className="actions">
        <Link className="button primary" to="/maintenance/new">
          {t("أبلغ عن عطل", "Report a fault")}
        </Link>
        <Link className="button" to="/requests">
          {t("كل طلباتي", "All my requests")}
        </Link>
      </div>
      {!staff && <SyntheticAiCheck key={account.id} />}
      <div className="spacer" />
      <AsyncView key={`${account.id}-${staff}`} load={load}>
        {(records) =>
          (staff
            ? records
            : records.filter((record) => record.ownerId === account.id)
          ).length ? (
            <div className="list">
              {(staff
                ? records
                : records.filter((record) => record.ownerId === account.id)
              ).map((record) => (
                <Link
                  className="record-card"
                  to={`/maintenance/${record.id}`}
                  key={record.id}
                >
                  <div className="record-top">
                    <bdi>{record.id}</bdi>
                    <Status value={record.status} />
                  </div>
                  <h3>{record.description}</h3>
                  <p>
                    {record.details} · {displayDate(record.createdAt, "en")}
                  </p>
                </Link>
              ))}
            </div>
          ) : (
            <Feedback>
              {t(
                "لا توجد بلاغات في هذه القائمة.",
                "There are no reports in this list.",
              )}
            </Feedback>
          )
        }
      </AsyncView>
    </Page>
  );
}
function StaffActions({
  report,
  onChanged,
}: {
  report: MaintenanceReport;
  onChanged: () => void;
}) {
  const { t, account } = useApp();
  const options: Partial<Record<MaintenanceStatus, MaintenanceStatus[]>> = {
    submitted: ["review"],
    review: ["review", "repair", "rejected"],
    repair: ["repair", "resolved"],
  };
  const allowed = options[report.status] ?? [];
  const [status, setStatus] = useState<MaintenanceStatus>(
    allowed[0] ?? report.status,
  );
  if (!allowed.length) return null;
  return (
    <div className="panel">
      <h2>{t("تحديث البلاغ", "Update report")}</h2>
      <ActionForm
        label={t("حفظ التحديث", "Save update")}
        onSubmit={async (data) => {
          const note = String(data.get("note") ?? "");
          if (status === "rejected" && !note.trim())
            throw new ServiceError("invalid");
          const type = String(data.get("type") ?? "").trim();
          const priority = String(data.get("priority") ?? "").trim();
          const risk = String(data.get("risk") ?? "").trim();
          if ((type || priority || risk) && !(type && priority && risk)) throw new ServiceError("invalid");
          await maintenance.update(
            account,
            {
              id: report.id,
              version: report.version,
              status,
              assignee: String(data.get("assignee")),
              note,
              decision: type && priority && risk ? { type, priority, risk } : undefined,
            },
          );
          onChanged();
        }}
      >
        <Field label={t("الحالة", "Status")} required>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as MaintenanceStatus)}
          >
            {allowed.map((value) => (
              <option key={value} value={value}>
                {t(...labels[value])}
              </option>
            ))}
          </select>
        </Field>
        <Field label={t("إسناد إلى", "Assign to")}>
          <select name="assignee" defaultValue={report.assignee}>
            <option value="">{t("غير مسند", "Unassigned")}</option>
            {report.allowedAssignees?.map(person => <option key={person.id} value={person.id}>{person.name}</option>)}
          </select>
        </Field>
        <p>
          {t(
            "القرارات التشغيلية للموظف — مستقلة عن اقتراحات التحليل.",
            "Staff operational decisions — separate from analysis suggestions.",
          )}
        </p>
        <Field label={t("نوع العطل المعتمد", "Operational fault type")}>
          <input name="type" defaultValue={report.decision?.type ?? ""} />
        </Field>
        <div className="field-grid">
          <Field label={t("الأولوية المعتمدة", "Operational priority")}>
            <input
              name="priority"
              defaultValue={report.decision?.priority ?? ""}
            />
          </Field>
          <Field label={t("الخطورة المعتمدة", "Operational risk")}>
            <input name="risk" defaultValue={report.decision?.risk ?? ""} />
          </Field>
        </div>
        <Field
          label={
            status === "rejected"
              ? t("سبب الرفض", "Rejection reason")
              : t("ملاحظات التحديث", "Update notes")
          }
          required={status === "rejected"}
        >
          <textarea name="note" required={status === "rejected"} />
        </Field>
      </ActionForm>
    </div>
  );
}
function Detail({
  report,
  onChanged,
}: {
  report: MaintenanceReport;
  onChanged: () => void;
}) {
  const { t, account, language } = useApp();
  const [editing, setEditing] = useState(false);
  const [reviewedNonSensitive, setReviewedNonSensitive] = useState(false);
  const [includeAiImage, setIncludeAiImage] = useState(false);
  const [params] = useSearchParams();
  const loadLocations = useCallback(() => maintenance.locations(account), [account]);
  const owner = report.ownerId === account.id;
  const staff = !!report.canViewAssessment || !!report.canUpdate;
  if (editing)
    return (
      <div className="panel">
        <h2>{t("تعديل البلاغ", "Edit report")}</h2>
        <AsyncView load={loadLocations}>
          {(locations) => <ReportForm initial={report} locations={locations} report={report} onSaved={() => { setEditing(false); onChanged(); }} />}
        </AsyncView>
        <button
          className="button"
          onClick={() => {
            drafts.delete(`${account.id}:${report.id}`);
            setEditing(false);
          }}
        >
          {t("إلغاء التعديل", "Cancel editing")}
        </button>
      </div>
    );
  return (
    <>
      {params.has("saved") && (
        <Feedback kind="success">
          {t("تم حفظ البلاغ.", "Report saved.")}
        </Feedback>
      )}
      <div className="panel">
        <div className="record-top">
          <bdi>{report.reference ?? report.id}</bdi>
          <Status value={report.status} />
        </div>
        <h2>{t("تفاصيل البلاغ", "Report details")}</h2>
        <p>{report.description}</p>
        <dl className="details-grid">
          <div>
            <dt>{t("الموقع", "Location")}</dt>
            <dd>
              {report.locationNames ? `${t(report.locationNames.faculty.ar, report.locationNames.faculty.en)} · ${t(report.locationNames.building.ar, report.locationNames.building.en)} · ${t(report.locationNames.floor.ar, report.locationNames.floor.en)}` : `${report.faculty} · ${report.building} · ${report.floor}`}
            </dd>
            <dd>{report.details}</dd>
          </div>
          <div>
            <dt>{t("تاريخ الإرسال", "Submitted")}</dt>
            <dd>{displayDate(report.createdAt, language)}</dd>
          </div>
        </dl>
        <PhotoPreview photo={report.photo} />
        {!report.photo && (
          <p>{t("لا توجد صورة مرفقة.", "No photo attached.")}</p>
        )}
        {owner && report.canEdit && (
          <div className="actions">
            <button className="button" onClick={() => setEditing(true)}>
              {t("تعديل البلاغ", "Edit report")}
            </button>
            <ActionForm
              label={t("إلغاء البلاغ", "Cancel report")}
              onSubmit={async () => {
                await maintenance.cancel(
                  account,
                  { id: report.id, version: report.version },
                );
                onChanged();
              }}
            >
              <p>
                {t(
                  "إلغاء البلاغ لا يعني أنه تم إصلاح العطل.",
                  "Cancelling does not mean the fault was repaired.",
                )}
              </p>
            </ActionForm>
          </div>
        )}
      </div>
      <div className="panel">
        <h2>{t("تحديثات البلاغ", "Report history")}</h2>
        <ol className="timeline">
          {report.history.map((event, index) => (
            <li key={index}>
              <Status value={event.status} />
              <small>
                <bdi>{event.actor}</bdi> · {displayDate(event.at, language)}
                {event.assignee &&
                  ` · ${t("مسند إلى", "Assigned to")} ${event.assigneeName || event.assignee}`}{" "}
              </small>
              {event.note && <p>{event.note}</p>}
            </li>
          ))}
        </ol>
      </div>
      {staff && (
        <>
          <div className="panel">
            <h2>
              {t(
                "اقتراحات التحليل — للموظفين فقط",
                "Analysis suggestions — staff only",
              )}
            </h2>
            {report.assessment?.state === "completed" ? <dl className="details-grid">
              <div><dt>{t("نوع العطل المقترح", "Suggested fault type")}</dt><dd>{report.assessment.type}</dd></div>
              <div><dt>{t("الأولوية المقترحة", "Suggested priority")}</dt><dd>{report.assessment.priority}</dd></div>
              <div><dt>{t("المخاطر المقترحة", "Suggested risk")}</dt><dd>{report.assessment.risk}</dd></div>
            </dl> : <Feedback>
              {t(
                report.assessment?.state === "pending" ? "التحليل قيد الانتظار." : "التحليل غير متاح حالياً.",
                report.assessment?.state === "pending" ? "Analysis is pending." : "Analysis is currently unavailable.",
              )}
            </Feedback>}
            {report.canReviewAiInput && <ActionForm
              label={t("إرسال المحتوى المُراجع للتحليل", "Send reviewed content for analysis")}
              onSubmit={async () => {
                await maintenance.reviewAi(report.id, report.version, reviewedNonSensitive, includeAiImage);
                onChanged();
              }}
            >
              <p>{t("سيُرسل الوصف إلى مزود التحليل الخارجي. راجع المحتوى أولاً وتأكد من خلوه من البيانات الشخصية أو الحساسة.", "The description will be sent to the external analysis provider. Review it first and ensure it contains no personal or sensitive information.")}</p>
              <label><input type="checkbox" required checked={reviewedNonSensitive} onChange={event => setReviewedNonSensitive(event.target.checked)} /> {t("راجعت الوصف وأؤكد أنه غير حساس ومسموح بمشاركته.", "I reviewed the description and confirm it is non-sensitive and permitted to share.")}</label>
              {report.canReviewAiImage && <label><input type="checkbox" checked={includeAiImage} onChange={event => setIncludeAiImage(event.target.checked)} /> {t("راجعت الصورة أيضاً وأوافق على إرسالها لأنها غير حساسة.", "I also reviewed the image and approve sending it as non-sensitive content.")}</label>}
            </ActionForm>}
          </div>
          {report.canUpdate && <StaffActions
            key={`${report.id}-${report.version}`}
            report={report}
            onChanged={onChanged}
          />}
        </>
      )}
    </>
  );
}
export function MaintenanceDetail() {
  const { id = "" } = useParams();
  const { t, account } = useApp();
  const [revision, setRevision] = useState(0);
  const load = useCallback(
    () => maintenance.detail(account, id),
    [account, id],
  );
  return (
    <Page title={t("متابعة البلاغ", "Track report")}>
      <AsyncView
        key={`${id}-${account.id}-${revision}`}
        load={load}
      >
        {(report) => (
          <Detail report={report} onChanged={() => setRevision((v) => v + 1)} />
        )}
      </AsyncView>
      <button
        type="button"
        className="button"
        onClick={() => setRevision((v) => v + 1)}
      >
        {t("تحديث بيانات البلاغ", "Refresh current report")}
      </button>
      <Link className="text-link" to="/maintenance">
        {t("العودة إلى البلاغات", "Back to reports")}
      </Link>
    </Page>
  );
}
