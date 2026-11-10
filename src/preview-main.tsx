import { StrictMode, useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { Icon } from "./components/Icon";
import { PreviewDemo } from "./preview-demo";
import "./styles/preview-demo.css";
import "./styles/app.css";

function PublicPreview() {
  const [language, setLanguage] = useState<"ar" | "en">("ar");
  const t = (ar: string, en: string) => language === "ar" ? ar : en;
  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = language === "ar" ? "rtl" : "ltr";
  }, [language]);

  return (
    <>
      <style>{`
        .preview-note { background: #edf4ff; color: #142b54; border-bottom: 1px solid #dce5ef; text-align: center; padding: 12px 24px; font-size: .86rem; line-height: 1.75; }
        .preview-note strong { font-weight: 800; }
        .preview-header { background: #fff; }
        .preview-main { padding: 0 20px 36px; }
        .preview-header nav { margin-inline: auto; flex: 1; }
        .preview-cta { display: inline-flex; justify-content: center; align-items: center; gap: 10px; cursor: not-allowed; opacity: .72; width: max-content; }
        .preview-cta:focus { outline: none; }
        .preview-service-detail { font-size: .8rem; margin-top: 12px; color: #566780; }
        .preview-footer { display: flex; justify-content: center; gap: 16px; align-items: center; text-align: center; padding: 26px; border-top: 1px solid #dce5ef; background: #fff; flex-wrap: wrap; }
        @media (max-width: 700px) { .preview-header { flex-wrap: wrap; justify-content: center; } .preview-header nav { justify-content: center; width: 100%; order: 3; } .preview-header .header-actions { margin-inline-start: auto; } .preview-main { padding-inline: 14px; } .services { grid-template-columns: 1fr; } }
      `}</style>
      <div className="preview-note" role="status">
        <strong>{t("نسخة معاينة عامة — ", "Public design preview — ")}</strong>
        {t(
          "الخدمات الحقيقية غير مفعّلة. التجربة التفاعلية وهمية بالكامل، فلا تُدخل بيانات شخصية.",
          "Real services are not live. The interactive demo uses fictional data only; do not submit personal information.",
        )}
      </div>
      <div className="preview-header">
        <header className="header">
          <a className="brand" href="#top" aria-label="Smart Campus">
            <img className="brand-mark" src={import.meta.env.BASE_URL + "o6u-mark.png"} alt="" aria-hidden="true" width="28" height="28" />
            <bdi>Smart Campus</bdi>
          </a>
          <nav aria-label={t("التنقل", "Navigation")}>
            <a href="#top">{t("الرئيسية", "Home")}</a>
            <a href="#services">{t("الخدمات", "Services")}</a>
            <a href="#demo">{t("جرّب النظام", "Try demo")}</a>
          </nav>
          <div className="header-actions">
            <button className="language" onClick={() => setLanguage(language === "ar" ? "en" : "ar")}>
              {language === "ar" ? "English" : "العربية"}
            </button>
          </div>
        </header>
      </div>
      <main id="top" className="preview-main">
        <div className="home">
          <section className="hero">
            <h1>{t("أهلاً بك في Smart Campus", "Welcome to Smart Campus")}</h1>
            <p>{t("أبلغ عن عطل أو ابحث عن مفقوداتك", "Report a fault or find something you lost")}</p>
            <span className="eyebrow" style={{ color: "#fff", letterSpacing: 0 }}>
              {t("معاينة تفاعلية للتصميم — دون حسابات حقيقية", "Interactive design preview — no real accounts")}
            </span>
          </section>
          <section className="services" id="services" aria-label={t("الخدمات", "Services")}>
            <article className="service-card">
              <span className="service-icon orange"><Icon name="wrench" /></span>
              <h2>{t("الصيانة", "Maintenance")}</h2>
              <p>{t(
                "مكان أفضل يبدأ بملاحظة صغيرة. أبلغ عن مشكلة في مرافق الحرم الجامعي وتابع حلها.",
                "A better campus starts with a small observation. Report an issue and follow its progress."
              )}</p>
              <span className="button primary preview-cta" aria-disabled="true">
                {t("الإبلاغ عن عطل", "Report a fault")} <Icon name="arrow" />
              </span>
              <span className="preview-service-detail">{t("متاحة بعد تشغيل النظام", "Available when the application goes live")}</span>
            </article>
            <article className="service-card">
              <span className="service-icon blue"><Icon name="search" /></span>
              <h2>{t("المفقودات والمعثورات", "Lost and found")}</h2>
              <p>{t(
                "صف ما فقدته للبحث الخاص عن تطابق محتمل، أو سجّل غرضاً عثرت عليه.",
                "Describe your lost item to privately search for a potential match, or register something you found."
              )}</p>
              <span className="button secondary preview-cta" aria-disabled="true">
                {t("ابحث عن مفقوداتك", "Find a lost item")} <Icon name="arrow" />
              </span>
              <span className="preview-service-detail">{t("متاحة بعد تشغيل النظام", "Available when the application goes live")}</span>
            </article>
          </section>
          <div className="home-note">
            <span className="mini-mark"><Icon name="home" /></span>
            <p>{t("خدمتان، وحرم جامعي أقرب للجميع.", "Two services. A campus that looks out for everyone.")}</p>
          </div>
        </div>
        <PreviewDemo language={language} />
      </main>
      <footer className="preview-footer">
        <strong>Smart Campus</strong>
        <span>{t("الصيانة والمفقودات والمعثورات", "Maintenance and lost and found")}</span>
        <span>{t("معاينة غير متصلة بالبيانات", "Offline design preview")}</span>
      </footer>
    </>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode><PublicPreview /></StrictMode>,
);
