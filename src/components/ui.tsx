import { useId, useRef, useState, type ReactNode, type FormEvent } from "react";
import { useApp } from "../app/context";
import { ServiceError } from "../services/contracts";
export function Page({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <section className="page">
      <div className="page-title">
        <h1>{title}</h1>
        {subtitle && <p>{subtitle}</p>}
      </div>
      {children}
    </section>
  );
}
export function Field({
  label,
  required,
  children,
  hint,
}: {
  label: string;
  required?: boolean;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="field">
      <span>
        {label}{" "}
        {required && (
          <span className="required">
            * <span className="sr-only">(required / مطلوب)</span>
          </span>
        )}
      </span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  );
}
export function Feedback({
  kind = "info",
  children,
}: {
  kind?: "info" | "error" | "success";
  children: ReactNode;
}) {
  return (
    <div
      className={`feedback ${kind}`}
      role={kind === "error" ? "alert" : "status"}
    >
      {children}
    </div>
  );
}
export function useAction() {
  const { t, expireSession } = useApp();
  const [pending, setPending] = useState(false);
  const inFlight = useRef(false);
  const [errorCode, setErrorCode] = useState<ServiceError["code"] | null>(null);
  const [errorDetail, setErrorDetail] = useState<string | null>(null);
  const messages = {
    unavailable: t(
      "الخدمة غير متاحة حالياً. احتُفظ بمدخلاتك.",
      "This service is temporarily unavailable. Your input is kept.",
    ),
    expired: t(
      "انتهت الجلسة. سجل الدخول مجدداً.",
      "Session expired. Sign in again.",
    ),
    conflict: t(
      "تغير السجل. أعد تحميله وراجع حالته قبل المحاولة.",
      "Record changed. Reload and review its state before retrying.",
    ),
    forbidden: t(
      "هذا الإجراء غير مسموح لحسابك.",
      "This action is not allowed for your account.",
    ),
    credentials: t(
      "البريد الإلكتروني أو كلمة المرور غير صحيحة.",
      "Incorrect email or password.",
    ),
    invalid: t(
      "تحقق من الحقول المطلوبة وحالة السجل.",
      "Check required fields and the current record state.",
    ),
    unverified: t("أكد بريدك الجامعي أولاً من رابط البريد.", "Verify your university email using the email link first."),
    release_restricted: t("النظام في مرحلة اختبار مُقيد بالحسابات المعتمدة فقط.", "This testing release is restricted to authorized accounts."),
    rate_limited: t("طلبات كثيرة. انتظر قليلاً ثم حاول مجدداً.", "Too many requests. Wait briefly and retry."),
    internal: t("تعذر إكمال الطلب. حاول مجدداً.", "The request failed. Please retry."),
  };

  const detailMessages: Record<string, string> = {
    invalid_upload: t("الصورة غير صالحة أو تتجاوز حدود المعالجة. استخدم JPEG أو PNG أو WebP حتى 5 ميجابايت.", "The image is invalid or exceeds processing limits. Use JPEG, PNG or WebP up to 5 MiB."),
    image_required: t("أرفق صورة صالحة للغرض قبل التسجيل.", "Attach a valid item photo before registering."),
    invalid_date: t("تاريخ العثور غير صالح أو يقع في المستقبل.", "The found date is invalid or is in the future."),
    invalid_input: t("راجع الحقول المطلوبة وتأكد من أن النصوص ضمن الحدود المسموح بها.", "Check required fields and ensure text is within the allowed limits."),
    storage_unavailable: t("تعذر حفظ الصورة في التخزين الخاص حالياً. احتُفظ بمدخلاتك؛ حاول لاحقاً.", "Private image storage is temporarily unavailable. Your form details are kept; retry later."),
    search_input_required: t("اكتب وصفاً للغرض أو أرفق صورته قبل البحث.", "Enter an item description or attach its photo before searching."),
    search_calibration_required: t("البحث الدلالي غير متاح قبل اعتماد معايير المطابقة.", "Semantic search is unavailable until retrieval calibration is approved."),
    assignee_required: t("عيّن موظفاً مسؤولاً قبل تحويل البلاغ إلى قيد الإصلاح.", "Assign a staff member before moving the report to repair."),
    invalid_destination: t("اختر جهة استلام مسجلة وصالحة.", "Choose a valid registered receiving department."),
    ai_not_configured: t("خدمة الذكاء الاصطناعي غير مفعّلة حالياً؛ يمكنك المتابعة يدوياً إذا كان ذلك متاحاً.", "AI is not enabled for this service; continue manually if the workflow allows it."),
  };

  async function run(action: () => Promise<void>) {
    if (inFlight.current) return;
    inFlight.current = true;
    setPending(true);
    setErrorCode(null);
    setErrorDetail(null);
    try {
      await action();
    } catch (error) {
      const code = error instanceof ServiceError ? error.code : "unavailable";
      setErrorCode(code);
      setErrorDetail(error instanceof ServiceError ? error.detail ?? null : null);
      if (code === "expired") expireSession();
    } finally {
      inFlight.current = false;
      setPending(false);
    }
  }
  return { pending, message: errorDetail && detailMessages[errorDetail] ? detailMessages[errorDetail] : errorCode ? messages[errorCode] : "", run };
}
export function ActionForm({
  children,
  onSubmit,
  label,
  danger = false,
}: {
  children: ReactNode;
  onSubmit: (data: FormData) => Promise<void>;
  label: string;
  danger?: boolean;
}) {
  const { pending, message, run } = useAction();
  const { t } = useApp();
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    void run(async () => {
      try {
        await onSubmit(data);
      } catch (error) {
        if (error instanceof ServiceError && error.code === "expired") {
          form
            .querySelectorAll<HTMLInputElement>("input[data-sensitive]")
            .forEach((input) => {
              input.value = "";
            });
        }
        throw error;
      }
    });
  }
  return (
    <form onSubmit={submit} className="form-stack">
      {children}
      {message && <Feedback kind="error">{message}</Feedback>}
      <button className={danger ? "button danger" : "button primary"} disabled={pending}>
        {pending ? t("جارٍ التنفيذ…", "Working…") : label}
      </button>
    </form>
  );
}
export function Password({
  label,
  name = "password",
  autoComplete,
}: {
  label: string;
  name?: string;
  autoComplete?: "current-password" | "new-password";
}) {
  const [visible, setVisible] = useState(false);
  const { t } = useApp();
  const id = useId();
  const completion = autoComplete ?? (name === "currentPassword" ? "current-password" : "new-password");
  const creatingPassword = completion === "new-password";
  const hintId = `${id}-hint`;
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} <span className="required">*</span>
      </label>
      <div className="password">
        <input
          id={id}
          data-sensitive="true"
          required
          name={name}
          type={visible ? "text" : "password"}
          autoComplete={completion}
          minLength={creatingPassword ? 12 : undefined}
          maxLength={creatingPassword ? 128 : undefined}
          aria-describedby={creatingPassword ? hintId : undefined}
        />
        <button
          type="button"
          onClick={() => setVisible(!visible)}
          aria-pressed={visible}
          aria-controls={id}
          aria-label={`${visible ? t("إخفاء", "Hide") : t("إظهار", "Show")} ${label}`}
        >
          {visible ? t("إخفاء", "Hide") : t("إظهار", "Show")}
        </button>
      </div>
      {creatingPassword && <small id={hintId}>{t("من 12 إلى 128 حرفًا.", "Use 12 to 128 characters.")}</small>}
    </div>
  );
}
