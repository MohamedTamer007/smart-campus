import { useState } from "react";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { operations, smokeAi } from "../../services/admin";

export function OperationsStatus() {
  const { t, account } = useApp();
  const [smokeBusy, setSmokeBusy] = useState(false);
  const [smokeMessage, setSmokeMessage] = useState<string | null>(null);
  async function testSyntheticAi() {
    setSmokeBusy(true);
    setSmokeMessage(null);
    try {
      const result = await smokeAi();
      setSmokeMessage(t("تم اختبار الاستجابة الحقيقية والتحقق من صيغة النتيجة", "Real provider response and result format validated") +
        ` (${result.provider}: ${result.model})`);
    } catch {
      setSmokeMessage(t("اختبار المودل لم ينجح. افحص حالة المفتاح والحصة والمودل من الخادم.", "Model test failed. Check server key, quota, and provider status."));
    } finally {
      setSmokeBusy(false);
    }
  }
  const kinds: Record<string, string> = { maintenance: t("تقييم الصيانة", "Maintenance assessment"), index: t("فهرسة المعثورات", "Found-item indexing"), email: t("البريد", "Email") };
  const states: Record<string, string> = { pending: t("بالانتظار", "Pending"), running: t("قيد التنفيذ", "Running"), completed: t("مكتملة", "Completed"), unavailable: t("متعذرة", "Unavailable") };
  return <section className="panel">
    <h2>{t("حالة التشغيل", "Operational status")}</h2>
    <AsyncView key={account.id} load={operations}>
      {(status) => <>
        <p>{t("خدمة المطابقة", "Matching worker")}: {status.worker === "available" ? t("متاحة", "Available")
          : status.worker === "hosted_configured" ? t("API مُعدّة — لم تُختبر النتائج بعد", "API configured — retrieval not yet validated")
          : status.worker === "hosted_blocked" ? t("API معطلة حتى اعتماد الخصوصية", "API disabled pending privacy review")
          : t("غير متاحة", "Unavailable")}</p>
        <p>{t("معايرة البحث", "Search calibration")}: {status.calibrationApproved ? t("مُراجعة ومعتمدة", "Reviewed and approved") : t("بانتظار الاعتماد", "Awaiting approval")}</p>
        {status.ai && <>
          <h3>{t("جاهزية إعدادات الذكاء الاصطناعي", "AI configuration readiness")}</h3>
          <p>{t("توجيه المودلز", "Model routing")}: <bdi>{status.ai.provider}</bdi></p>
          <p>{t("تحليل النصوص", "Text analysis")}: {status.ai.textConfigured
            ? t("الإعدادات جاهزة — الاستجابة الفعلية غير مختبرة", "Configured — actual provider response not tested")
            : t("الإعدادات غير مكتملة", "Configuration incomplete")}</p>
          <p>{t("تحليل الصور", "Image analysis")}: {status.ai.imagesConfigured
            ? t("الإعدادات جاهزة — الاستجابة الفعلية غير مختبرة", "Configured — actual provider response not tested")
            : t("معطل أو الإعدادات غير مكتملة", "Disabled or configuration incomplete")}</p>
          <p>{t("مفاتيح الخادم (دون عرض قيمها)", "Server API keys (values hidden)")}: NVIDIA {status.ai.nvidiaKeyPresent ? "✓" : "✗"} · OpenRouter {status.ai.openRouterKeyPresent ? "✓" : "✗"}</p>
          <p>{t("ميزانية الطلبات", "Request budgets")}: NVIDIA {status.ai.nvidiaBudgetConfigured ? "✓" : "✗"} · OpenRouter {status.ai.openRouterBudgetConfigured ? "✓" : "✗"}</p>
          <p>{t("تصنيفات الصيانة", "Maintenance taxonomy")}: {status.ai.taxonomyConfigured ? t("مُدخلة", "Present") : t("ناقصة", "Missing")}</p>
          <p>{t("تفعيل إرسال النص والصور للمزودين الخارجيين", "External-provider text/image switches")}: {status.ai.textTransferEnabled ? t("النص مفعّل", "Text on") : t("النص معطّل", "Text off")} · {status.ai.imageTransferEnabled ? t("الصور مفعّلة", "Images on") : t("الصور معطّلة", "Images off")}</p>
          <p>{t("دي فحوص إعدادات فقط، مش إثبات إن المودلز اشتغلت. لازم مراجعة الخصوصية قبل تشغيل مشاركة بيانات فعلية.", "These are configuration checks, not successful provider tests. Complete privacy review before sharing real data.")}</p>
          {status.ai.syntheticSmokeTestsEnabled && status.ai.textConfigured && (
            <div>
              <p>{t("اختبار اختياري لبلاغ وهمي ثابت. بيستهلك طلب واحد من رصيد المزود، وممنوع تشغيله إلا في بيئة اختبار معزولة.", "Optional test with a fixed fictional report. Uses one provider request; run only in an isolated test environment.")}</p>
              <button type="button" className="button" disabled={smokeBusy} onClick={() => { void testSyntheticAi(); }}>
                {smokeBusy ? t("جارٍ اختبار المودل...", "Testing model...") : t("اختبار المودل بالنص الوهمي", "Test model with fictional text")}
              </button>
              {smokeMessage && <p role="status">{smokeMessage}</p>}
            </div>
          )}
        </>}
        <p>{t("ذاكرة الخدمة", "Service memory")}: {Math.round(status.workingSetBytes / 1024 / 1024)} MiB</p>
        <p>{t("محاولات هذا التطبيق وحدّه اليومي للذكاء الاصطناعي بتوقيت UTC", "App AI attempts / daily budget in UTC")}</p>
        <ul>{status.quotas.map((quota) => <li key={quota.provider}><bdi>{quota.provider}</bdi>: {quota.used} / {quota.dailyLimit}</li>)}</ul>
        <p>{t("مهام المعالجة", "Background processing")}</p>
        <ul>{status.jobs.map((job) => <li key={`${job.kind}-${job.state}`}>{kinds[job.kind] ?? t("معالجة أخرى", "Other processing")} · {states[job.state] ?? t("غير معروفة", "Unknown")}: {job.count}</li>)}</ul>
      </>}
    </AsyncView>
  </section>;
}
