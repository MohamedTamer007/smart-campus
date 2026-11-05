import { useCallback, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useApp } from "../../app/context";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  Password,
} from "../../components/ui";
import { AsyncView } from "../../components/AsyncView";
import { boundary, campus } from "../../services/demo";
import { ServiceError } from "../../services/contracts";
export function Settings() {
  const {
    t,
    account,
    updateAccount,
    language,
    changeLanguage,
    setActor,
    actionScenario,
  } = useApp();
  const [saved, setSaved] = useState(false);
  const navigate = useNavigate();
  return (
    <Page title={t("حسابي", "My account")}>
      <div className="panel narrow">
        <ActionForm
          label={t("حفظ التغييرات", "Save changes")}
          onSubmit={async (data) => {
            updateAccount(
              await campus.saveName(
                account,
                String(data.get("name")),
                actionScenario,
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
          <Field label={t("البريد الموثق", "Verified email")}>
            <input value={account.email} readOnly dir="ltr" />
          </Field>
          <button className="button" type="button" onClick={changeLanguage}>
            {t("اللغة الحالية", "Current language")}:{" "}
            {language === "ar" ? "العربية" : "English"}
          </button>
          {saved && (
            <Feedback kind="success">
              {t(
                "حُفظت التغييرات في هذه التجربة.",
                "Changes saved in this demo.",
              )}
            </Feedback>
          )}
        </ActionForm>
      </div>
      <div className="panel narrow">
        <h2>{t("تغيير كلمة المرور", "Change password")}</h2>
        <ActionForm
          label={t("محاكاة تغيير كلمة المرور", "Simulate password change")}
          onSubmit={async (data) => {
            if (data.get("password") !== data.get("confirmation"))
              throw new ServiceError("invalid");
            await boundary(actionScenario, account);
            setSaved(true);
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
      <div className="narrow">
        <button
          className="button danger"
          onClick={() => {
            setActor("guest");
            navigate("/login");
          }}
        >
          {t("تسجيل الخروج", "Sign out")}
        </button>
      </div>
    </Page>
  );
}
export function Notifications() {
  const { t, account, scenario, actionScenario } = useApp();
  const [revision, setRevision] = useState(0);
  const load = useCallback(
    () => campus.notifications(account, scenario),
    [account, scenario],
  );
  return (
    <Page
      title={t("الإشعارات", "Notifications")}
      subtitle={t("تحديثات داخل الموقع فقط.", "Updates stay inside the site.")}
    >
      <AsyncView key={`${account.id}-${scenario}-${revision}`} load={load}>
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
                          await campus.markRead(account, n.id, actionScenario);
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
