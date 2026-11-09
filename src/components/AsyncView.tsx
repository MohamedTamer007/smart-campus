import { useEffect, useState, type ReactNode } from "react";
import { useApp } from "../app/context";
import { ServiceError } from "../services/contracts";
import { Feedback } from "./ui";
import { Link } from "react-router-dom";
export function AsyncView<T>({
  load,
  children,
}: {
  load: () => Promise<T>;
  children: (data: T) => ReactNode;
}) {
  const { t, expireSession } = useApp();
  const [state, setState] = useState<{ data?: T; error?: string }>({});
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    load()
      .then((data) => {
        if (active) setState({ data });
      })
      .catch((error) => {
        if (!(error instanceof ServiceError)) throw error;
        if (active) {
          if (error.code === "expired") expireSession();
          else setState({ error: error.code });
        }
      });
    return () => {
      active = false;
    };
  }, [load, attempt, expireSession]);
  if (state.error)
    return (
      <div className="panel">
        <Feedback kind="error">
          {state.error === "expired"
            ? t(
                "انتهت الجلسة. سجل الدخول مجدداً.",
                "Session expired. Sign in again.",
              )
            : state.error === "unavailable" ? t(
                "الخدمة غير متاحة مؤقتاً. حاول مجدداً.",
                "The service is temporarily unavailable. Please retry.",
              ) : t(
                "تعذر تحميل البيانات. حاول مجدداً.",
                "Could not load data. Please retry.",
              )}
        </Feedback>
        {state.error === "expired" ? (
          <Link className="button" to="/login">
            {t("تسجيل الدخول", "Sign in")}
          </Link>
        ) : (
          <button
            className="button"
            onClick={() => {
              setState({});
              setAttempt(attempt + 1);
            }}
          >
            {t("إعادة المحاولة", "Retry")}
          </button>
        )}
      </div>
    );
  if (state.data === undefined)
    return <Feedback>{t("جارٍ التحميل…", "Loading…")}</Feedback>;
  return children(state.data);
}
