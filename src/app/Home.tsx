import { Link } from "react-router-dom";
import { useApp } from "./context";
import { Icon } from "../components/Icon";
import { verified } from "../services/contracts";
export function Home() {
  const { t, account } = useApp();
  const ready = verified(account);
  const guest = account.role === "guest";
  return (
    <div className={guest ? "home guest-home" : "home"}>
      <section className="hero">
        {guest && <span className="guest-eyebrow">{t("وضع الزائر", "Guest view")}</span>}
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
        {guest && <div className="guest-actions">
          <Link className="button primary" to="/login">{t("سجّل الدخول لبدء الاستخدام", "Sign in to use services")}</Link>
          <Link className="text-link" to="/register">{t("إنشاء حساب جامعي", "Create a university account")}</Link>
        </div>}
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
      <section className="campus-map-promo panel">
        <div>
          <span className="tag">{t("خريطة تفاعلية للحرم", "Interactive campus map")}</span>
          <h3>{t("اعرف مكان كل مبنى", "Find your way around campus")}</h3>
          <p>{t(
            "استكشف مباني ومرافق جامعة 6 أكتوبر على صورة أقمار صناعية، واختر أي علامة لعرض اسم المكان ومصدره.",
            "Explore October 6 University buildings on satellite imagery. Select a marker to see the place name and its source.",
          )}</p>
        </div>
        <Link className="button primary" to="/campus-map">
          {t("افتح خريطة الجامعة", "Open campus map")} <Icon name="arrow" />
        </Link>
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
