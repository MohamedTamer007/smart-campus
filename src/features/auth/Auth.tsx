import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useApp } from "../../app/context";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  Password,
} from "../../components/ui";
import { campus, boundary, universityEmail } from "../../services/demo";
import { ServiceError } from "../../services/contracts";
export function Auth({ register = false }: { register?: boolean }) {
  const { t, actionScenario, updateAccount } = useApp();
  const navigate = useNavigate();
  const [emailError, setEmailError] = useState(false);
  const [confirmationError, setConfirmationError] = useState(false);
  async function submit(data: FormData) {
    const email = String(data.get("email"));
    setEmailError(false);
    setConfirmationError(false);
    if (!universityEmail(email)) {
      setEmailError(true);
      return;
    }
    const password = String(data.get("password"));
    if (register && password !== data.get("confirmation")) {
      setConfirmationError(true);
      return;
    }
    await boundary(actionScenario);
    const account = register
      ? await campus.register({
          name: String(data.get("name")),
          email,
          password,
          confirmation: String(data.get("confirmation")),
        })
      : await campus.login(email, password, actionScenario);
    updateAccount(account);
    navigate(register ? "/verify" : "/");
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
              <input name="name" autoComplete="name" required />
            </Field>
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
              aria-invalid={!!emailError}
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
          <Feedback>
            {register
              ? t(
                  "التسجيل تجريبي ويحفظ الحساب في الذاكرة فقط دون إرسال بريد. كلمة دخول التجربة هي demo.",
                  "Registration is simulated and kept only in memory; no email is sent. Demo sign-in uses the password demo.",
                )
              : t(
                  "للتجربة: استخدم ahmed@o6u.edu.eg أو حساباً تجريبياً مسجلاً، وكلمة المرور demo.",
                  "Demo: use ahmed@o6u.edu.eg or a registered demo email with password demo. This is not real authentication.",
                )}
          </Feedback>
        </ActionForm>
        <div className="auth-links">
          <Link to={register ? "/login" : "/register"}>
            {register
              ? t("لديك حساب؟ سجّل الدخول", "Have an account? Sign in")
              : t("إنشاء حساب جديد", "Create a new account")}
          </Link>
          <Link to="/recover">
            {t("نسيت كلمة المرور؟", "Forgot password?")}
          </Link>
        </div>
      </div>
    </Page>
  );
}
export function Verification() {
  const { t, account, updateAccount, actionScenario } = useApp();
  const [sent, setSent] = useState(false);
  const [done, setDone] = useState(false);
  const [invalid, setInvalid] = useState(false);
  return (
    <Page
      title={
        done
          ? t("تم تأكيد البريد تجريبياً", "Email verified in demo")
          : t("تأكيد البريد الجامعي", "Verify university email")
      }
    >
      <div className="panel narrow">
        <p>
          {t(
            "استخدم رابط التأكيد المرسل لبريدك في النظام الحقيقي. هذه محاكاة فقط.",
            "In the real system, use the verification email link. This is a simulation only.",
          )}
        </p>
        <bdi>{account.email}</bdi>
        {invalid && (
          <Feedback kind="error">
            {t(
              "الرابط غير صالح أو منتهي. اطلب رابطاً جديداً.",
              "Link invalid or expired. Request a new link.",
            )}
          </Feedback>
        )}
        {sent && (
          <Feedback kind="success">
            {t(
              "تمت محاكاة إعادة الإرسال؛ لم يُرسل بريد.",
              "Resend simulated; no email was sent.",
            )}
          </Feedback>
        )}
        <ActionForm
          label={t("محاكاة تأكيد البريد", "Simulate verification")}
          onSubmit={async () => {
            if (invalid || account.role !== "unverified")
              throw new ServiceError("invalid");
            updateAccount(await campus.verify(account, actionScenario));
            setDone(true);
          }}
        >
          <button
            type="button"
            className="button"
            onClick={() => setInvalid(true)}
          >
            {t("عرض رابط منتهي", "Show expired link")}
          </button>
          <button
            type="button"
            className="button"
            onClick={() => {
              setSent(true);
              setInvalid(false);
            }}
          >
            {t("محاكاة إعادة إرسال الرابط", "Simulate resend link")}
          </button>
        </ActionForm>
        {done && (
          <Link className="button secondary" to="/">
            {t("الانتقال للرئيسية", "Go to home")}
          </Link>
        )}
      </div>
    </Page>
  );
}
export function Recovery() {
  const { t, actionScenario } = useApp();
  const [sent, setSent] = useState(false);
  return (
    <Page title={t("نسيت كلمة المرور؟", "Forgot password?")}>
      <div className="panel narrow">
        {sent ? (
          <Feedback kind="success">
            {t(
              "إذا كان البريد مسجلاً، سيصلك رابط استعادة في النظام الحقيقي. هذه تجربة دون إرسال بريد.",
              "If the email is registered, the real system will send recovery instructions. This demo sends no email.",
            )}
          </Feedback>
        ) : (
          <ActionForm
            label={t("طلب رابط الاستعادة", "Request recovery link")}
            onSubmit={async () => {
              await boundary(actionScenario);
              setSent(true);
            }}
          >
            <Field label={t("البريد الجامعي", "University email")} required>
              <input required type="email" dir="ltr" name="email" />
            </Field>
          </ActionForm>
        )}
        <Link className="text-link" to="/reset/demo">
          {t("معاينة رابط استعادة تجريبي", "Preview demo reset link")}
        </Link>
      </div>
    </Page>
  );
}
export function ResetPassword() {
  const { token } = useParams();
  const { t, actionScenario } = useApp();
  const [done, setDone] = useState(false);
  return (
    <Page title={t("تعيين كلمة مرور جديدة", "Set a new password")}>
      <div className="panel narrow">
        {token !== "demo" ? (
          <>
            <Feedback kind="error">
              {t(
                "الرابط غير صالح أو منتهي.",
                "This link is invalid or expired.",
              )}
            </Feedback>
            <Link className="button" to="/recover">
              {t("اطلب رابطاً جديداً", "Request a new link")}
            </Link>
          </>
        ) : done ? (
          <Feedback kind="success">
            {t(
              "تمت محاكاة تغيير كلمة المرور. لم تتغير أي بيانات حقيقية.",
              "Password change simulated. No real credentials changed.",
            )}
          </Feedback>
        ) : (
          <ActionForm
            label={t("حفظ كلمة المرور", "Save password")}
            onSubmit={async (data) => {
              if (data.get("password") !== data.get("confirmation"))
                throw new ServiceError("invalid");
              await boundary(actionScenario);
              setDone(true);
            }}
          >
            <Password label={t("كلمة المرور الجديدة", "New password")} />
            <Password
              name="confirmation"
              label={t("تأكيد كلمة المرور", "Confirm password")}
            />
          </ActionForm>
        )}
        <Link className="text-link" to="/login">
          {t("تسجيل الدخول", "Sign in")}
        </Link>
      </div>
    </Page>
  );
}
