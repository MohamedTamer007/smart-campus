import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useApp } from "../../app/context";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  Password,
  useAction,
} from "../../components/ui";
import { AsyncView } from "../../components/AsyncView";
import { accountService } from "../../services/frontend";
import { auth } from "../../services/auth";
import { guestAccount, ServiceError } from "../../services/contracts";
export function Settings() {
  const {
    t,
    account,
    updateAccount,
    language,
    changeLanguage,
  } = useApp();
  const [saved, setSaved] = useState(false);
  const navigate = useNavigate();
  const { pending: logoutPending, message: logoutError, run: runLogout } = useAction();
  return (
    <Page title={t("حسابي", "My account")}>
      <div className="panel narrow">
        <ActionForm
          label={t("حفظ التغييرات", "Save changes")}
          onSubmit={async (data) => {
            updateAccount(
              await accountService.saveName(
                account,
                String(data.get("name")),
                language,
              ),
            );
            setSaved(true);
          }}
        >
          <Field label={t("الاسم المعروض", "Display name")} required>
            <input
              name="name"
              defaultValue={account.name}
              required
              pattern=".*\S.*"
            />
          </Field>
          <Field label={t("البريد الجامعي", "University email")}>
            <input value={account.email} readOnly dir="ltr" />
          </Field>
          <Feedback kind="success">
            {t("✓ حسابك مؤكد بالفعل — مش محتاج تأكيد البريد تاني.", "✓ Your account is verified — no further email confirmation needed.")}
          </Feedback>
          <button className="button" type="button" onClick={changeLanguage}>
            {t("اللغة الحالية", "Current language")}:{" "}
            {language === "ar" ? "العربية" : "English"}
          </button>
          {saved && (
            <Feedback kind="success">
              {t(
                "حُفظت التغييرات.",
                "Changes saved.",
              )}
            </Feedback>
          )}
        </ActionForm>
      </div>
      <div className="panel narrow">
        <h2>{t("تغيير كلمة المرور", "Change password")}</h2>
        <ActionForm
          label={t("تغيير كلمة المرور", "Change password")}
          onSubmit={async (data) => {
            if (data.get("password") !== data.get("confirmation"))
              throw new ServiceError("invalid");
            await accountService.changePassword(
              account,
              String(data.get("currentPassword") ?? ""),
              String(data.get("password") ?? ""),
            );
            updateAccount(guestAccount);
            navigate("/login");
          }}
        >
          <Password
            name="currentPassword"
            label={t("كلمة المرور الحالية", "Current password")}
          />
          <Password label={t("كلمة المرور الجديدة", "New password")} />
          <Password
            name="confirmation"
            label={t("تأكيد كلمة المرور", "Confirm password")}
          />
        </ActionForm>
      </div>
      <div className="panel narrow">
        <h2>{t("حذف الحساب", "Delete account")}</h2>
        <p>{t(
          "هيتم مسح بيانات تسجيل الدخول واسمك وإيميلك وتعطيل حسابك. السجلات القديمة للبلاغات والمعثورات بتفضل محفوظة باسم مجهول عشان ما يتأثرش سجل النظام.",
          "Your sign-in credentials, name, and email will be erased and your account disabled. Historical maintenance and lost-property records remain anonymously to preserve the records.",
        )}</p>
        <ActionForm
          label={t("حذف حسابي", "Delete my account")}
          danger
          onSubmit={async data => {
            if (data.get("confirmDelete") !== "yes") throw new ServiceError("invalid");
            await auth.deleteAccount(String(data.get("currentPassword") ?? ""));
            updateAccount(guestAccount);
            navigate("/login?deleted=1");
          }}
        >
          <Password name="currentPassword" label={t("كلمة المرور الحالية للتأكيد", "Current password to confirm")} />
          <label className="field">
            <input type="checkbox" required name="confirmDelete" value="yes" />
            {" "}{t("فاهم إن حذف الحساب مش ممكن أتراجع عنه.", "I understand that this account deletion cannot be undone.")}
          </label>
        </ActionForm>
      </div>
      <div className="narrow">
        <button
          type="button"
          className="button danger"
          disabled={logoutPending}
          onClick={() => void runLogout(async () => {
            await auth.logout();
            updateAccount(guestAccount);
            navigate("/login");
          })}
        >
          {logoutPending ? t("جارٍ التنفيذ…", "Working…") : t("تسجيل الخروج", "Sign out")}
        </button>
        {logoutError && <Feedback kind="error">{logoutError}</Feedback>}
      </div>
    </Page>
  );
}
export function Notifications() {
  const { t, account } = useApp();
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const timer = window.setInterval(() => setRevision(value => value + 1), 30000);
    return () => window.clearInterval(timer);
  }, []);
  const load = useCallback(
    () => accountService.notifications(account),
    [account],
  );
  return (
    <Page
      title={t("الإشعارات", "Notifications")}
      subtitle={t("تحديثات داخل الموقع فقط.", "Updates stay inside the site.")}
    >
      <AsyncView key={`${account.id}-${revision}`} load={load}>
        {(notices) =>
          notices.length ? (
            <div className="list">
              {notices.map((n) => (
                <article className="panel" key={n.id}>
                  <span className="tag">
                    {n.read ? t("مقروء", "Read") : t("جديد", "Unread")}
                  </span>
                  <h2>{t(n.ar, n.en)}</h2>
                  <div className="actions">
                    <Link className="button" to={n.to}>
                      {t("فتح التفاصيل", "Open details")}
                    </Link>
                    {!n.read && (
                      <ActionForm
                        label={t("تحديد كمقروء", "Mark as read")}
                        onSubmit={async () => {
                          await accountService.markRead(account, n.id);
                          setRevision(revision + 1);
                        }}
                      >
                        <span className="sr-only">{t(n.ar, n.en)}</span>
                      </ActionForm>
                    )}
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <Feedback>
              {t("لا توجد إشعارات بعد.", "No notifications yet.")}
            </Feedback>
          )
        }
      </AsyncView>
    </Page>
  );
}
