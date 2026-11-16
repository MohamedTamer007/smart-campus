import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { operations } from "../../services/admin";

export function OperationsStatus() {
  const { t, account, language } = useApp();
  const kinds: Record<string, string> = {
    maintenance: t("معالجة الصيانة", "Maintenance processing"),
    index: t("فهرسة المعثورات", "Found-item indexing"),
    email: t("البريد", "Email"),
  };
  const states: Record<string, string> = {
    pending: t("بالانتظار", "Pending"),
    running: t("قيد التنفيذ", "Running"),
    completed: t("مكتملة", "Completed"),
    unavailable: t("متعذرة", "Unavailable"),
  };

  return (
    <section className="panel">
      <h2>{t("حالة التشغيل", "Operational status")}</h2>
      <AsyncView key={account.id} load={operations}>
        {(status) => {
          const count = (state: string) => status.jobs
            .filter((job) => job.state === state)
            .reduce((total, job) => total + job.count, 0);
          const workerStatus = status.worker === "available"
            ? t("متاحة", "Available")
            : status.worker === "hosted_configured"
              ? t("الإعداد مكتمل؛ الجاهزية غير مفحوصة", "Configured; readiness is not checked")
              : status.worker === "hosted_blocked"
                ? t("غير جاهزة؛ إعدادات الخدمة ناقصة", "Not ready; service setup is incomplete")
                : t("غير متاحة", "Unavailable");
          const checkedAt = new Intl.DateTimeFormat(language === "ar" ? "ar-EG" : "en-GB", {
            dateStyle: "medium",
            timeStyle: "short",
          }).format(new Date(status.sampledAt));
          return (
            <>
              <p><span className="tag">{t("متصل", "Online")}</span> {t("واجهة النظام تستجيب", "The application API is responding")}</p>
              <p>{t("خدمة المطابقة", "Matching service")}: {workerStatus}</p>
              <p>{t("اعتماد المعايرة", "Calibration approval")}: {status.calibrationApproved ? t("معتمدة", "Approved") : t("غير معتمدة", "Not approved")}</p>
              <p>{t("المهام الخلفية", "Background jobs")}: {t("بالانتظار", "Pending")} {count("pending")} · {t("قيد التنفيذ", "Running")} {count("running")} · {t("متعذرة", "Unavailable")} {count("unavailable")}</p>
              <p>{t("آخر تحديث للحالة", "Last status check")}: <time dateTime={status.sampledAt}>{checkedAt}</time></p>
              <details>
                <summary>{t("تفاصيل الخدمات والمعالجة", "Service and processing details")}</summary>
                <div>
                  {status.ai && (
                    <>
                      <h3>{t("إعدادات الذكاء الاصطناعي", "AI configuration")}</h3>
                      <p>{t("المزود المحدد", "Selected provider")}: <bdi>{status.ai.provider}</bdi></p>
                      <p>
                        {t("تحليل نص الصيانة", "Maintenance text analysis")}: {status.ai.textConfigured
                          ? t("الإعداد جاهز؛ التوصيات تظهر بعد تشغيل البلاغات ومراجعتها", "Configured; suggestions appear through reviewed maintenance reports")
                          : t("الإعداد غير مكتمل", "Configuration incomplete")}
                      </p>
                      <p>
                        {t("تحليل صور الصيانة", "Maintenance image analysis")}: {status.ai.imagesConfigured
                          ? t("الإعداد جاهز؛ يتطلب موافقة مستقلة على الصورة", "Configured; each image requires separate approval")
                          : t("غير متاح أو الإعداد غير مكتمل", "Unavailable or configuration incomplete")}
                      </p>
                      <p>
                        {t("الموافقة على المعالجة الخارجية", "External processing approval")}: {status.ai.textTransferEnabled
                          ? t("النص موافق عليه", "Text approved")
                          : t("النص غير موافق عليه", "Text not approved")} · {status.ai.imageTransferEnabled
                          ? t("الصور موافق عليها", "Images approved")
                          : t("الصور غير موافق عليها", "Images not approved")}
                      </p>
                      <p>
                        {t("موافقة سياسة جمع البيانات لدى المزوّد", "Provider data-collection policy approval")}: {status.ai.providerDataCollectionApproved === undefined
                          ? t("الحالة غير متاحة من إصدار الخدمة الحالي", "Approval status unavailable in this server version")
                          : status.ai.providerDataCollectionApproved
                            ? t("مسموح للمدخلات غير الحساسة المعتمدة فقط", "Allowed only for approved non-sensitive inputs")
                            : t("مرفوضة افتراضيًا؛ المعالجة الخارجية متوقفة", "Denied by default; external processing is blocked")}
                      </p>
                    </>
                  )}
                  <p>{t("استخدام طلبات الذكاء الاصطناعي لهذا التطبيق وحده اليوم، بتوقيت UTC", "This app's AI request usage today (UTC)")}</p>
                  <ul>{status.quotas.map((quota) => <li key={quota.provider}><bdi>{quota.provider}</bdi>: {quota.used} / {quota.dailyLimit}</li>)}</ul>
                  <p>{t("تفصيل مهام المعالجة", "Processing job breakdown")}</p>
                  <ul>{status.jobs.map((job) => <li key={`${job.kind}-${job.state}`}>{kinds[job.kind] ?? t("معالجة أخرى", "Other processing")} · {states[job.state] ?? t("غير معروفة", "Unknown")}: {job.count}</li>)}</ul>
                </div>
              </details>
            </>
          );
        }}
      </AsyncView>
    </section>
  );
}

