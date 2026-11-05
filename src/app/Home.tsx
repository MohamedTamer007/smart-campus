import { Link } from "react-router-dom";
import { useApp } from "./context";
import { Icon } from "../components/Icon";
import { verified } from "../services/contracts";
export function Home() {
  const { t, account } = useApp();
  const ready = verified(account);
  return (
    <div className="home">
      <section className="hero">
        <h1>
          {ready
            ? `${t("مرحباً،", "Welcome,")} ${account.name}`
            : t("أهلاً بك في Smart Campus", "Welcome to Smart Campus")}
        </h1>
        <p>
          {t(
            "أبلغ عن عطل أو ابحث عن مفقوداتك",
            "Report a fault or find something you lost",
          )}
        </p>
        <span className="photo-note">
          {t(
            "مساحة مؤقتة — صورة الجامعة الأصلية لم تُرفق بعد",
            "Temporary space — original university photo not yet supplied",
          )}
        </span>
        {account.role === "guest" && (
          <Link className="text-link" to="/register">
            {t("إنشاء حساب", "Create account")}
          </Link>
        )}
      </section>
      <section className="services" aria-label={t("الخدمات", "Services")}>
        <article className="service-card">
          <span className="service-icon orange">
            <Icon name="wrench" />
          </span>
          <h2>{t("الصيانة", "Maintenance")}</h2>
          <p>
            {t(
              "مكان أفضل يبدأ بملاحظة صغيرة. أبلغ عن مشكلة في مرافق الحرم الجامعي وتابع حلها.",
              "A better campus starts with a small observation. Report an issue and follow its progress.",
            )}
          </p>
          <Link
            className="button primary"
            to={ready ? "/maintenance/new" : "/login"}
          >
            {t("الإبلاغ عن عطل", "Report a fault")} <Icon name="arrow" />
          </Link>
        </article>
        <article className="service-card">
          <span className="service-icon blue">
            <Icon name="search" />
          </span>
          <h2>{t("المفقودات والمعثورات", "Lost and found")}</h2>
          <p>
            {t(
              "صف ما فقدته للبحث الخاص عن تطابق محتمل، أو سجّل غرضاً عثرت عليه.",
              "Describe your lost item to privately search for a potential match, or register something you found.",
            )}
          </p>
          <Link className="button secondary" to={ready ? "/search" : "/login"}>
            {t("ابحث عن مفقوداتك", "Find a lost item")} <Icon name="arrow" />
          </Link>
          <Link className="text-link" to={ready ? "/found/new" : "/login"}>
            ＋ {t("سجّل غرضاً عثرت عليه", "Register a found item")}
          </Link>
        </article>
      </section>
      <div className="home-note">
        <span className="mini-mark">
          <Icon name="home" />
        </span>
        <p>
          {t(
            "خدمتان، وحرم جامعي أقرب للجميع.",
            "Two services. A campus that looks out for everyone.",
          )}
        </p>
      </div>
    </div>
  );
}
