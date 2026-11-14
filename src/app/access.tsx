import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useApp } from "./context";
import { Feedback, Page } from "../components/ui";
import { hasRole, verified, type Role } from "../services/contracts";
export function Access({
  children,
  roles,
}: {
  children: ReactNode;
  roles?: Role[];
}) {
  const { t, account } = useApp();
  if (!verified(account))
    return (
      <Page title={t("تسجيل الدخول مطلوب", "Sign in required")}>
        <div className="panel narrow">
          <Feedback>
            {account.role === "unverified"
              ? t(
                  "أكد بريدك الإلكتروني لاستخدام الخدمات.",
                  "Verify your email to use services.",
                )
              : t(
                  "سجّل الدخول للوصول إلى بياناتك الخاصة.",
                  "Sign in to access your private records.",
                )}
          </Feedback>
          <Link
            className="button primary"
            to={account.role === "unverified" ? "/verify" : "/login"}
          >
            {account.role === "unverified"
              ? t("تأكيد البريد", "Verify email")
              : t("تسجيل الدخول", "Sign in")}
          </Link>
        </div>
      </Page>
    );
  if (roles && !roles.some(role => hasRole(account, role)))
    return (
      <Page title={t("غير مصرح", "Access denied")}>
        <Feedback kind="error">
          {t(
            "هذه الصفحة خارج نطاق صلاحيات حسابك.",
            "This page is outside your account permissions.",
          )}
        </Feedback>
      </Page>
    );
  return children;
}
