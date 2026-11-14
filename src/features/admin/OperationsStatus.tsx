import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { operations } from "../../services/admin";

export function OperationsStatus() {
  const { t, account } = useApp();
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
        {(status) => (
          <>
            <p>
              {t("خدمة المطابقة", "Matching service")}: {status.worker === "available"
                ? t("متاحة", "Available")
                : status.worker === "hosted_configured"
                  ? t("إعداد مزود المطابقة مكتمل", "Matching provider configured")
                  : status.worker === "hosted_blocked"
                    ? t("متوقفة حتى استيفاء الخصوصية والمعايرة", "Blocked until privacy and calibration requirements pass")
                    : t("غير متاحة", "Unavailable")}
            </p>
            <p>
              {t("اعتماد المعايرة", "Calibration approval")}: {status.calibrationApproved
                ? t("معتمدة", "Approved")
                : t("غير معتمدة", "Not approved")}
            </p>
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
            <p>{t("مهام المعالجة", "Background processing")}</p>
            <ul>{status.jobs.map((job) => <li key={`${job.kind}-${job.state}`}>{kinds[job.kind] ?? t("معالجة أخرى", "Other processing")} · {states[job.state] ?? t("غير معروفة", "Unknown")}: {job.count}</li>)}</ul>
          </>
        )}
      </AsyncView>
    </section>
  );
}
