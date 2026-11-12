import { useState } from "react";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { operations, smokeAi, demoAi, smokeOpenRouter, type AiDemoResult, type OpenRouterSmokeResult } from "../../services/admin";
import { ServiceError } from "../../services/contracts";
const openRouterErrorMessages: Record<string, [string, string]> = {
  ai_smoke_disabled: ["تشخيص OpenRouter غير مفعّل.", "OpenRouter diagnostics are disabled."],
  ai_smoke_limit: ["تم استخدام اختبار OpenRouter اليوم؛ يتجدد الحد عند منتصف الليل UTC.", "Today's OpenRouter diagnostic was used; the limit resets at midnight UTC."],
  ai_not_configured: ["مفتاح OpenRouter أو ميزانيته غير جاهزة.", "The OpenRouter key or budget is not configured."],
  ai_quota_exhausted: ["حصة OpenRouter اليومية أو للدقيقة لا تكفي للفحص.", "OpenRouter's daily or per-minute budget is too low for this diagnostic."],
  ai_rate_limited: ["OpenRouter وصل حد الطلبات.", "OpenRouter rate-limited the diagnostic."],
  search_provider_unavailable: ["خدمة OpenRouter أو embeddings غير متاحة.", "OpenRouter or its embeddings endpoint is unavailable."],
  invalid_embedding: ["وصل متجه غير صالح من OpenRouter.", "OpenRouter returned an invalid embedding."],
  invalid_ai_response: ["رد تحليل الصورة ناقص أو غير صالح.", "The image-analysis response was incomplete or invalid."],
  ai_key_rejected: ["OpenRouter رفض المفتاح.", "OpenRouter rejected the key."],
  ai_access_denied: ["OpenRouter رفض صلاحية الوصول.", "OpenRouter denied access."],
  ai_request_rejected: ["OpenRouter رفض صيغة طلب تحليل الصورة.", "OpenRouter rejected the image-analysis request."],
};

export function OperationsStatus() {
  const { t, account } = useApp();
  const [smokeBusy, setSmokeBusy] = useState(false);
  const [smokeMessage, setSmokeMessage] = useState<string | null>(null);
  const [demoBusy, setDemoBusy] = useState(false);
  const [demoResult, setDemoResult] = useState<AiDemoResult | null>(null);
  const [demoError, setDemoError] = useState<string | null>(null);
  const [openRouterBusy, setOpenRouterBusy] = useState(false);
  const [openRouterResult, setOpenRouterResult] = useState<OpenRouterSmokeResult | null>(null);
  const [openRouterError, setOpenRouterError] = useState<string | null>(null);
  async function testRealSyntheticClassification() {
    setDemoBusy(true);
    setDemoResult(null);
    setDemoError(null);
    try {
      setDemoResult(await demoAi());
    } catch (error) {
      const code = error instanceof ServiceError ? (error.detail ?? error.code) : "unknown";
      const explanations: Record<string, [string, string]> = {
        ai_connection_failed: ["السيرفر مش قادر يتصل بـ NVIDIA عبر HTTPS. راجع صلاحية الاتصال الخارجي من الاستضافة.", "The host could not connect to NVIDIA over HTTPS. Check outbound host connectivity."],
        ai_timeout: ["NVIDIA لم يرد خلال المهلة المحددة. قد يكون المودل بطيئًا أو الاتصال متعطلًا.", "NVIDIA did not respond before timeout; the model or connection may be slow."],
        ai_key_rejected: ["NVIDIA رفض مفتاح الـ API (401).", "NVIDIA rejected the API key (401)."],
        ai_access_denied: ["NVIDIA رفض صلاحية الوصول (403).", "NVIDIA denied access (403)."],
        ai_request_rejected: ["NVIDIA رفض صيغة الطلب (400/422).", "NVIDIA rejected the request format (400/422)."],
        ai_rate_limited: ["NVIDIA وصل لحد الطلبات (429).", "NVIDIA rate-limited the request (429)."],
        ai_unavailable: ["NVIDIA رجع خطأ في الخدمة.", "NVIDIA returned a provider error."],
        invalid_ai_response: ["الرد وصل لكن تصنيفه غير صالح أو غير مكتمل.", "NVIDIA responded but the classification was invalid or incomplete."],
        ai_smoke_limit: ["استهلكت 5 محاولات تشخيص اليوم؛ الحد بيتجدد منتصف الليل UTC.", "Five diagnostic attempts are used; resets at midnight UTC."],
        ai_quota_exhausted: ["رصيد طلبات التطبيق انتهى.", "The application request budget is exhausted."],
        internal: ["السيرفر واجه استثناء غير متوقع؛ راجع سجلات الاستضافة.", "The server encountered an unexpected error; check hosting logs."]
      };
      const explanation = explanations[code];
      setDemoError(explanation
        ? t(explanation[0], explanation[1])
        : t("فشل تحليل العطل الوهمي، رمز التشخيص: ", "Fictional classification failed, code: ") + code);
    } finally {
      setDemoBusy(false);
    }
  }
  async function testSyntheticAi() {
    setSmokeBusy(true);
    setSmokeMessage(null);
    try {
      const result = await smokeAi();
      setSmokeMessage(t("نجح الاتصال الفعلي بـ NVIDIA (تصنيف الصيانة لم يُختبر)", "NVIDIA live connectivity passed (maintenance classification not tested)") +
        ` (${result.provider}: ${result.model})`);
    } catch (error) {
      const code = error instanceof ServiceError ? error.detail : undefined;
      const reasons: Record<string, [string, string]> = {
        ai_key_rejected: ["NVIDIA رفض مفتاح الـ API (401).", "NVIDIA rejected the API key (401)."],
        ai_access_denied: ["NVIDIA رفض الصلاحية أو الحساب (403).", "NVIDIA denied access (403)."],
        ai_provider_rate_limited: ["NVIDIA رفض الطلب بسبب حد الاستخدام (429).", "NVIDIA provider rate limit reached (429)."],
        ai_request_rejected: ["NVIDIA رفض صيغة طلب الاختبار (400/422).", "NVIDIA rejected the request parameters (400/422)."],
        ai_provider_unavailable: ["خدمة NVIDIA غير متاحة حاليًا.", "NVIDIA service is currently unavailable."],
        ai_connection_failed: ["السيرفر مش قادر يتصل بـ NVIDIA عبر HTTPS.", "The server could not establish HTTPS to NVIDIA."],
        ai_timeout: ["انتهت مهلة اتصال السيرفر بـ NVIDIA.", "NVIDIA connection timed out."],
        ai_invalid_response: ["NVIDIA رجّع استجابة ناقصة أو غير متوقعة.", "NVIDIA returned an incomplete or unexpected response."],
        ai_smoke_limit: ["وصلت لـ 5 محاولات تشخيص اليوم (UTC).", "Five diagnostic attempts used today (UTC)."],
        ai_quota_exhausted: ["حصة NVIDIA في التطبيق خلصت.", "The application NVIDIA quota is exhausted."],
        ai_not_configured: ["مفتاح NVIDIA أو ميزانية الطلبات ناقصة.", "NVIDIA key or quota configuration is incomplete."]
      };
      const reason = code ? reasons[code] : undefined;
      setSmokeMessage(reason
        ? t(reason[0], reason[1])
        : t("اختبار الاتصال فشل. رمز التشخيص: " + (code ?? "unknown"), "Connectivity test failed. Diagnostic code: " + (code ?? "unknown")));
    } finally {
      setSmokeBusy(false);
    }
  }
  async function testSyntheticOpenRouter() {
    setOpenRouterBusy(true);
    setOpenRouterResult(null);
    setOpenRouterError(null);
    try {
      setOpenRouterResult(await smokeOpenRouter());
    } catch (error) {
      const code = error instanceof ServiceError ? (error.detail ?? error.code) : "unknown";
      setOpenRouterError(openRouterDiagnosticError(code, t));
    } finally {
      setOpenRouterBusy(false);
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
          {status.ai.adminSyntheticDiagnosticEnabled && status.ai.nvidiaKeyPresent && status.ai.nvidiaBudgetConfigured && (
            <div>
              <p>{t("اختبار آمن لنص وهمي ثابت على NVIDIA فقط. لا يرسل أي بلاغ أو صورة أو بيانات مستخدم، ولا يفعّل تحليل الصيانة الحقيقي. الحد 5 محاولات يوميًا، وكل محاولة تستهلك طلبًا من حصة المزود.", "Admin-only diagnostic using a fixed fictional text on NVIDIA. Never sends reports, photos or user data, and does not enable actual maintenance AI. Up to 5 attempts per UTC day; each attempt uses one provider request.")}</p>
              <button type="button" className="button" disabled={smokeBusy} onClick={() => { void testSyntheticAi(); }}>
                {smokeBusy ? t("جارٍ اختبار المودل...", "Testing model...") : t("اختبار المودل بالنص الوهمي", "Test model with fictional text")}
              </button>
              {smokeMessage && <p role="status">{smokeMessage}</p>}
              <p>{t("اختبار تحليل حقيقي: يرسل عطلًا خياليًا ثابتًا فقط، ويعرض التصنيف الذي يرجعه مودل NVIDIA. يشترك مع اختبار الاتصال في نفس حد 5 محاولات يوميًا.", "Live classification demo: sends only a fixed fictional fault and displays the actual NVIDIA model classification. Shares the same 5-per-day limit.")}</p>
              <button type="button" className="button secondary" disabled={smokeBusy || demoBusy} onClick={() => { void testRealSyntheticClassification(); }}>
                {demoBusy ? t("جارٍ التحليل…", "Classifying…") : t("جرّب تحليل عطل وهمي بالـ AI الحقيقي", "Classify a fictional fault with real AI")}
              </button>
              {demoError && <p role="status">{demoError}</p>}
              {demoResult && <div role="status">
                <p><strong>{t("نتيجة التحليل من NVIDIA:", "NVIDIA classification result:")}</strong></p>
                <p>{t("النوع", "Type")}: {demoResult.type} · {t("الأولوية", "Priority")}: {demoResult.priority} · {t("الخطورة", "Risk")}: {demoResult.risk}</p>
                <p>{t("الشرح", "Explanation")}: {demoResult.explanation}</p>
                <p>{t("بلاغ خيالي فقط — لم يتم تحليل بلاغات المستخدمين.", "Fictional example only — no user report was processed.")}</p>
              </div>}
            </div>
          )}
          {status.ai.adminSyntheticDiagnosticEnabled && status.ai.openRouterKeyPresent && status.ai.openRouterBudgetConfigured && (
            <div>
              <p>{t("فحص OpenRouter ثابت بمدخلات خيالية فقط: تحليل صورة وembedding للنص والصورة. يستهلك 3 طلبات كحد أقصى، مرة واحدة يوميًا، ولا يفتح البحث العام.", "Fixed OpenRouter diagnostic using fictional inputs only: image analysis plus text and image embeddings. Uses up to 3 requests, at most once daily, and does not enable public search.")}</p>
              <button type="button" className="button secondary" disabled={openRouterBusy} onClick={() => { void testSyntheticOpenRouter(); }}>
                {openRouterBusy ? t("جارٍ فحص OpenRouter...", "Testing OpenRouter...") : t("اختبار الصور والبحث الوهمي على OpenRouter", "Test fictional image and search on OpenRouter")}
              </button>
              {openRouterError && <p role="status">{openRouterError}</p>}
              {openRouterResult && <p role="status">{t("نجحت الفحوص الثلاثة", "All three checks passed")}: {openRouterResult.visionModel} · {openRouterResult.embeddingModel} · {openRouterResult.dimension}D ({openRouterResult.revision})</p>}
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

function openRouterDiagnosticError(code: string, translate: (arabic: string, english: string) => string) {
  const message = openRouterErrorMessages[code];
  return message ? translate(message[0], message[1]) : translate(`فشل الفحص، رمز التشخيص: ${code}`, `Diagnostic failed, code: ${code}`);
}