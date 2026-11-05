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
  const messages = {
    unavailable: t(
      "الخدمة غير متاحة. احتُفظ بمدخلاتك؛ حاول مجدداً.",
      "Service unavailable. Your input is kept; please retry.",
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
  };

  async function run(action: () => Promise<void>) {
    if (inFlight.current) return;
    inFlight.current = true;
    setPending(true);
    setErrorCode(null);
    try {
      await action();
    } catch (error) {
      if (!(error instanceof ServiceError)) throw error;
      setErrorCode(error.code);
      if (error.code === "expired") expireSession();
    } finally {
      inFlight.current = false;
      setPending(false);
    }
  }
  return { pending, message: errorCode ? messages[errorCode] : "", run };
}
export function ActionForm({
  children,
  onSubmit,
  label,
}: {
  children: ReactNode;
  onSubmit: (data: FormData) => Promise<void>;
  label: string;
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
      <button className="button primary" disabled={pending}>
        {pending ? t("جارٍ التنفيذ…", "Working…") : label}
      </button>
    </form>
  );
}
export function Password({
  label,
  name = "password",
}: {
  label: string;
  name?: string;
}) {
  const [visible, setVisible] = useState(false);
  const { t } = useApp();
  const id = useId();
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
          autoComplete={
            name === "currentPassword" ? "current-password" : "new-password"
          }
        />
        <button
          type="button"
          onClick={() => setVisible(!visible)}
          aria-pressed={visible}
        >
          {visible ? t("إخفاء", "Hide") : t("إظهار", "Show")}
        </button>
      </div>
    </div>
  );
}
