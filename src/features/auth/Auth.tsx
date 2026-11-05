import { useState } from "react";
import { Link } from "react-router-dom";
import { useApp } from "../../app/context";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  Password,
} from "../../components/ui";
import { ServiceError } from "../../services/contracts";
import { backendUnavailable } from "../../services/unavailable";
import { universityEmail } from "../../services/validation";

export function Auth({ register = false }: { register?: boolean }) {
  const { t } = useApp();
  const [nameError, setNameError] = useState(false);
  const [emailError, setEmailError] = useState(false);
  const [confirmationError, setConfirmationError] = useState(false);

  async function submit(data: FormData) {
    setEmailError(false);
    setConfirmationError(false);
    setNameError(false);
    if (register && !String(data.get("name") ?? "").trim()) {
      setNameError(true);
      return;
    }
    if (!universityEmail(String(data.get("email")))) {
      setEmailError(true);
      return;
    }
    if (register && data.get("password") !== data.get("confirmation")) {
      setConfirmationError(true);
      return;
    }
    await backendUnavailable();
  }

  return (
    <Page
      title={
        register
          ? t("إنشاء حساب", "Create account")
          : t("تسجيل الدخول", "Sign in")
      }
      subtitle={t(
        "بريدك الجامعي، وكلمة مرور خاصة بالموقع.",
        "Your university email and a password for this site.",
      )}
    >
      <div className="panel narrow">
        <ActionForm
          onSubmit={submit}
          label={
            register
              ? t("إنشاء الحساب", "Create account")
              : t("تسجيل الدخول", "Sign in")
          }
        >
          {register && (
            <Field label={t("الاسم", "Name")} required>
              <input
                name="name"
                autoComplete="name"
                required
                aria-invalid={nameError}
                onChange={() => setNameError(false)}
              />
            </Field>
          )}
          {nameError && (
            <Feedback kind="error">{t("أدخل الاسم.", "Enter your name.")}</Feedback>
          )}
          <Field
            label={t("البريد الجامعي", "University email")}
            required
            hint={t("النطاق المسموح: o6u.edu.eg", "Allowed domain: o6u.edu.eg")}
          >
            <input
              name="email"
              type="text"
              inputMode="email"
              dir="ltr"
              autoComplete="email"
              required
              aria-invalid={emailError}
              onChange={() => setEmailError(false)}
            />
          </Field>
          {emailError && (
            <Feedback kind="error">
              {t(
                "استخدم بريداً صحيحاً بالنطاق o6u.edu.eg فقط.",
                "Use a valid email with the exact o6u.edu.eg domain.",
              )}
            </Feedback>
          )}
          <Password label={t("كلمة المرور", "Password")} />
          {register && (
            <Password
              label={t("تأكيد كلمة المرور", "Confirm password")}
              name="confirmation"
            />
          )}
          {confirmationError && (
            <Feedback kind="error">
              {t("كلمتا المرور غير متطابقتين.", "Passwords do not match.")}
            </Feedback>
          )}
        </ActionForm>
        <div className="auth-links">
          <Link to={register ? "/login" : "/register"}>
            {register
              ? t("لديك حساب؟ سجّل الدخول", "Have an account? Sign in")
              : t("إنشاء حساب جديد", "Create a new account")}
          </Link>
          <Link to="/recover">{t("نسيت كلمة المرور؟", "Forgot password?")}</Link>
        </div>
      </div>
    </Page>
  );
}

export function Verification() {
  const { t } = useApp();
  return (
    <Page title={t("تأكيد البريد الجامعي", "Verify university email")}>
      <div className="panel narrow">
        <Feedback>
          {t(
            "تأكيد البريد غير متاح حتى ربط الخدمة.",
            "Email verification is unavailable until the service is connected.",
          )}
        </Feedback>
        <ActionForm
          label={t("إعادة إرسال رابط التأكيد", "Resend verification link")}
          onSubmit={backendUnavailable}
        >
          <Field label={t("البريد الجامعي", "University email")} required>
            <input required type="email" dir="ltr" name="email" />
          </Field>
        </ActionForm>
        <Link className="text-link" to="/login">
          {t("تسجيل الدخول", "Sign in")}
        </Link>
      </div>
    </Page>
  );
}

export function Recovery() {
  const { t } = useApp();
  return (
    <Page title={t("نسيت كلمة المرور؟", "Forgot password?")}>
      <div className="panel narrow">
        <ActionForm
          label={t("طلب رابط الاستعادة", "Request recovery link")}
          onSubmit={backendUnavailable}
        >
          <Field label={t("البريد الجامعي", "University email")} required>
            <input required type="email" dir="ltr" name="email" />
          </Field>
        </ActionForm>
      </div>
    </Page>
  );
}

export function ResetPassword() {
  const { t } = useApp();
  return (
    <Page title={t("تعيين كلمة مرور جديدة", "Set a new password")}>
      <div className="panel narrow">
        <ActionForm
          label={t("حفظ كلمة المرور", "Save password")}
          onSubmit={async (data) => {
            if (data.get("password") !== data.get("confirmation"))
              throw new ServiceError("invalid");
            await backendUnavailable();
          }}
        >
          <Password label={t("كلمة المرور الجديدة", "New password")} />
          <Password
            name="confirmation"
            label={t("تأكيد كلمة المرور", "Confirm password")}
          />
        </ActionForm>
        <Link className="text-link" to="/recover">
          {t("اطلب رابطاً جديداً", "Request a new link")}
        </Link>
        <Link className="text-link" to="/login">
          {t("تسجيل الدخول", "Sign in")}
        </Link>
      </div>
    </Page>
  );
}
