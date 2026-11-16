import { useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useApp } from "../../app/context";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  Password,
} from "../../components/ui";
import { ServiceError } from "../../services/contracts";
import { auth } from "../../services/auth";
import { universityEmail } from "../../services/validation";

function AuthPanel({ registration, onSubmit, showDeleted = false }: {
  registration: boolean;
  onSubmit: (data: FormData, registration: boolean) => Promise<void>;
  showDeleted?: boolean;
}) {
  const { t } = useApp();
  const [nameError, setNameError] = useState(false);
  const [emailError, setEmailError] = useState(false);
  const [confirmationError, setConfirmationError] = useState(false);

  async function submit(data: FormData) {
    setEmailError(false);
    setConfirmationError(false);
    setNameError(false);
    if (registration && !String(data.get("name") ?? "").trim()) {
      setNameError(true);
      return;
    }
    if (!universityEmail(String(data.get("email")))) {
      setEmailError(true);
      return;
    }
    if (registration && data.get("password") !== data.get("confirmation")) {
      setConfirmationError(true);
      return;
    }
    await onSubmit(data, registration);
  }

  return <section className={`panel auth-card ${registration ? "auth-register" : "auth-login"}`}>
        <h2>{registration ? t("ابدأ حسابك", "Create your account") : t("مرحباً بعودتك", "Welcome back")}</h2>
        <p>{registration ? t("أنشئ حسابك للوصول إلى خدمات الطالب في Smart Campus.", "Create an account to access Smart Campus student services.") : t("ادخل إلى حسابك لمتابعة خدمات الحرم.", "Sign in to continue to your campus services.")}</p>
        {showDeleted && <Feedback kind="success">
          {t("تم حذف بيانات حسابك وتعطيله. تقدر تعمل حساب جديد بنفس الإيميل.", "Your account details were removed and the account disabled. You can register again with the same email.")}
        </Feedback>}
        <ActionForm
          onSubmit={submit}
          label={registration ? t("إنشاء الحساب", "Create account") : t("تسجيل الدخول", "Sign in")}
        >
          {registration && (
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
          <Password label={t("كلمة المرور", "Password")} autoComplete={registration ? "new-password" : "current-password"} />
          {registration && (
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
        {!registration && <div className="auth-links">
          <Link to="/recover">{t("نسيت كلمة المرور؟", "Forgot password?")}</Link>
          <Link to="/verify">{t("تأكيد البريد أو إعادة الإرسال", "Verify email or resend link")}</Link>
        </div>}
  </section>;
}

export function Auth({ register = false }: { register?: boolean }) {
  const { t, updateAccount } = useApp();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  async function submit(data: FormData, registration: boolean) {
    if (registration) {
      await auth.register(String(data.get("name")), String(data.get("email")), String(data.get("password")), String(data.get("confirmation")));
      navigate("/verify");
      return;
    }
    updateAccount(await auth.login(String(data.get("email")), String(data.get("password"))));
    navigate("/requests");
  }
  return <Page title={register ? t("إنشاء حساب", "Create account") : t("تسجيل الدخول", "Sign in")}
    subtitle={t("بريدك الجامعي، وكلمة مرور خاصة بالموقع.", "Your university email and a password for this site.")}>
    <div className="auth-single narrow">
      <AuthPanel registration={register} onSubmit={submit} showDeleted={!register && params.get("deleted") === "1"} />
      <p className="auth-switch">
        {register
          ? <>{t("لديك حساب؟", "Already have an account?")} <Link to="/login">{t("سجّل الدخول", "Sign in")}</Link></>
          : <>{t("ليس لديك حساب؟", "Don't have an account?")} <Link to="/register">{t("أنشئ حساباً", "Sign up")}</Link></>}
      </p>
    </div>
  </Page>;
}

export function Verification() {
  const { t, account, updateAccount } = useApp();
  const alreadySignedIn = account.role !== "guest" && account.role !== "unverified";
  const [params] = useSearchParams();
  const token = params.get("token");
  const [verified, setVerified] = useState(false);
  const [alreadyVerified, setAlreadyVerified] = useState(false);
  const [resent, setResent] = useState(false);
  const [showResend, setShowResend] = useState(false);

  return (
    <Page title={t("تأكيد البريد الجامعي", "Verify university email")}>
      <div className="panel narrow">
        {alreadySignedIn || verified ? (
          <Feedback kind="success">
            {alreadySignedIn || alreadyVerified
              ? t("حسابك مؤكّد بالفعل ✅ مش محتاج تأكيد البريد مرة تانية.", "Your account is already verified ✅ No further confirmation is needed.")
              : t("تم تأكيد البريد. يمكنك تسجيل الدخول.", "Email verified. You can now sign in.")}
          </Feedback>
        ) : (
          <>
            {token ? (
              <>
                <Feedback>
                  {t("اضغط تأكيد البريد لإتمام التأكيد أو لمعرفة إذا كان بريدك مؤكّداً بالفعل.", "Confirm your email below, or check whether this link has already verified your account.")}
                </Feedback>
                <ActionForm
                  label={t("تأكيد البريد", "Confirm email")}
                  onSubmit={async () => {
                    const result = await auth.verify(params.get("userId") ?? "", token);
                    setAlreadyVerified(Boolean(result.alreadyVerified));
                    setVerified(true);
                  }}
                >
                  <p>{t("تأكيد عنوان البريد الجامعي.", "Verify your university email address.")}</p>
                </ActionForm>
              </>
            ) : (
              <>
                <Feedback>
                  {t("لو سجلت حساباً جديداً، افتح رابط التأكيد من بريدك الجامعي. ولو أكدت بريدك قبل كده، سجل دخولك هنا لمعرفة حالته.", "If you just registered, open the verification link in your university inbox. If you have already verified your email, sign in here to check its status.")}
                </Feedback>
                <ActionForm
                  label={t("التحقق من حالة الحساب", "Check verification status")}
                  onSubmit={async data => {
                    updateAccount(await auth.login(String(data.get("email")), String(data.get("password"))));
                  }}
                >
                  <Field label={t("البريد الجامعي", "University email")} required>
                    <input required type="email" dir="ltr" name="email" autoComplete="email" />
                  </Field>
                  <Password label={t("كلمة المرور", "Password")} autoComplete="current-password" />
                </ActionForm>
              </>
            )}
            {!resent && (
              <button
                type="button"
                className="text-link"
                aria-expanded={showResend}
                onClick={() => setShowResend(value => !value)}
              >
                {t("لم تصلك الرسالة؟ إعادة إرسال رابط التأكيد", "Didn't get the email? Resend verification link")}
              </button>
            )}
            {showResend && !resent && (
              <ActionForm
                label={t("إعادة إرسال رابط التأكيد", "Resend verification link")}
                onSubmit={async data => {
                  await auth.resend(String(data.get("email")));
                  setResent(true);
                }}
              >
                <Field label={t("البريد الجامعي", "University email")} required>
                  <input required type="email" dir="ltr" name="email" autoComplete="email" />
                </Field>
              </ActionForm>
            )}
            {resent && (
              <Feedback>
                {t("تحقق من بريدك إذا كان العنوان مؤهلاً. إذا لم تجد الرسالة، افحص مجلد الرسائل غير المرغوب فيها (Junk/Spam).", "Check your inbox if the address is eligible. If you do not see the message, check your Junk or Spam folder.")}
              </Feedback>
            )}
          </>
        )}
        {alreadySignedIn && <Link className="text-link" to="/settings">{t("الذهاب لحسابي", "Go to my account")}</Link>}
        {!alreadySignedIn && <Link className="text-link" to="/login">{t("تسجيل الدخول", "Sign in")}</Link>}
      </div>
    </Page>
  );
}

export function Recovery() {
  const { t } = useApp();
  const [requested, setRequested] = useState(false);
  return (
    <Page title={t("نسيت كلمة المرور؟", "Forgot password?")}>
      <div className="panel narrow">
        {requested && <Feedback>{t("تحقق من بريدك إذا كان العنوان مؤهلاً. إذا لم تجد الرسالة، افحص مجلد الرسائل غير المرغوب فيها (Junk/Spam).", "Check your inbox if the address is eligible. If you do not see the message, check your Junk or Spam folder.")}</Feedback>}
        <ActionForm
          label={t("طلب رابط الاستعادة", "Request recovery link")}
          onSubmit={async data => { await auth.recover(String(data.get("email"))); setRequested(true); }}
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
  const { token: pathToken = "" } = useParams();
  const [params] = useSearchParams();
  const token = params.get("token") ?? pathToken;
  const [completed, setCompleted] = useState(false);
  return (
    <Page title={t("تعيين كلمة مرور جديدة", "Set a new password")}>
      <div className="panel narrow">
        {completed && <Feedback kind="success">{t("تغيرت كلمة المرور. سجل الدخول مجدداً.", "Password changed. Sign in again.")}</Feedback>}
        {!completed && <ActionForm
          label={t("حفظ كلمة المرور", "Save password")}
          onSubmit={async (data) => {
            if (data.get("password") !== data.get("confirmation"))
              throw new ServiceError("invalid");
            await auth.reset(params.get("userId") ?? "", token, String(data.get("password")), String(data.get("confirmation")));
            setCompleted(true);
          }}
        >
          <Password label={t("كلمة المرور الجديدة", "New password")} />
          <Password
            name="confirmation"
            label={t("تأكيد كلمة المرور", "Confirm password")}
          />
        </ActionForm>}
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
