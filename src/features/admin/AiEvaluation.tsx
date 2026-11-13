import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { Feedback, Page } from "../../components/ui";
import { useApp } from "../../app/context";
import { aiEvaluations, type EvaluationCase, type EvaluationPreflight, type EvaluationRun, type EvaluationSnapshot, type EvaluationSuite } from "../../services/admin";

const suites: { id: EvaluationSuite; calls: number; title: [string, string]; detail: [string, string] }[] = [
  { id: "integration", calls: 4, title: ["فحص التكامل", "Integration checks"], detail: ["نص وصورة صيانة وembedding للنص والصورة", "Maintenance text/image and text/image embeddings"] },
  { id: "maintenance-text", calls: 12, title: ["تصنيف نص الصيانة", "Maintenance text"], detail: ["٤ عربية فصحى، ٤ مصرية، و٤ إنجليزية", "4 formal Arabic, 4 Egyptian Arabic, 4 English"] },
  { id: "maintenance-image", calls: 4, title: ["تحليل صور الصيانة", "Maintenance images"], detail: ["٤ صور توضيحية خيالية، كل صورة طلب مستقل", "4 fictional illustrative images, one request each"] },
  { id: "lost-found", calls: 20, title: ["مطابقة المفقودات", "Lost and found retrieval"], detail: ["٦ أوصاف + ٦ صور أغراض + ٨ استعلامات نصية/صورية", "6 text + 6 image gallery embeddings + 8 text/image queries"] },
];

function readText(record: Record<string, unknown> | null, key: string) {
  const value = record?.[key];
  return typeof value === "string" || typeof value === "number" || typeof value === "boolean" ? String(value) : "";
}

function matchesReference(row: EvaluationCase, field: string) {
  return readText(row.actual, field) !== "" && readText(row.expected, field) === readText(row.actual, field);
}

function measuredReference(rows: EvaluationCase[], field: string) {
  return rows.filter(row => readText(row.expected, field) && readText(row.actual, field));
}

function historicalLabel(label: string, t: (ar: string, en: string) => string) {
  const labels: Record<string, [string, string]> = {
    "Category agreement (%)": ["اتفاق نوع العطل (%)", "Category agreement (%)"],
    "Priority agreement (%)": ["اتفاق الأولوية (%)", "Priority agreement (%)"],
    "Urgent cases (%)": ["الحالات العاجلة (%)", "Urgent cases (%)"],
    "Agreement (%)": ["اتفاق مع التسميات المصدرية (%)", "Source-label agreement (%)"],
    "Top-1 (%)": ["Top-1 (%)", "Top-1 (%)"],
    "Top-5 (%)": ["Top-5 (%)", "Top-5 (%)"],
    "Threshold .85 false accepts (%)": ["قبول خاطئ عند .85 (%)", "False accepts at .85 (%)"],
    ".90 false accepts (%)": ["قبول خاطئ عند .90 (%)", "False accepts at .90 (%)"],
    ".95 false accepts (%)": ["قبول خاطئ عند .95 (%)", "False accepts at .95 (%)"],
    Text: ["نص", "Text"], Image: ["صورة", "Image"],
    "Text embedding": ["embedding نص", "Text embedding"], "Image embedding": ["embedding صورة", "Image embedding"],
  };
  return labels[label] ? t(...labels[label]) : label;
}

function EvaluationChart({ title, labels, values, color = "#397dc2", t }: {
  title: string; labels: string[]; values: number[]; color?: string; t: (ar: string, en: string) => string;
}) {
  const max = Math.max(1, ...values);
  return <figure className="evaluation-chart">
    <figcaption>{title}</figcaption>
    <div className="evaluation-chart-rows" role="img" aria-label={title}>
      {labels.map((label, index) => <div className="evaluation-chart-row" key={label}>
        <span className="evaluation-chart-label">{label}</span>
        <span className="evaluation-chart-track"><span className="evaluation-chart-bar" style={{ width: `${Math.max(0, values[index] / max * 100)}%`, backgroundColor: color }} /></span>
        <bdi className="evaluation-chart-value">{values[index]}</bdi>
      </div>)}
    </div>
    <p className="muted">{t("المخطط يعرض القياسات المحفوظة لهذه المجموعة فقط.", "Chart uses only saved measurements in this run.")}</p>
  </figure>;
}

function CaseDetails({ row, t }: { row: EvaluationCase; t: (ar: string, en: string) => string }) {
  const inputText = readText(row.input, "text");
  const image = readText(row.input, "imageDataUrl");
  const evaluated = ["type", "priority", "risk"].filter(field => readText(row.expected, field) && readText(row.actual, field)).length;
  const agreements = ["type", "priority", "risk"].filter(field => readText(row.expected, field) && readText(row.actual, field) && matchesReference(row, field)).length;
  const candidates = row.actual?.candidates;
  return <details className="evaluation-case">
    <summary><bdi>{row.caseKey}</bdi><span className="evaluation-case-summary-meta">
      <span className="evaluation-case-model"><bdi>{row.model ?? row.kind}</bdi></span>
      <span className={"evaluation-status status-" + row.state}>{row.state}</span>
    </span></summary>
    <div className="evaluation-case-grid">
      <div><h4>{t("المدخل", "Input")}</h4>{inputText && <p dir="auto">{inputText}</p>}
        {image && <img className="evaluation-fixture" src={image} alt={t("صورة اختبار اصطناعية", "Synthetic test image")} />}
        {!inputText && !image && <p>{readText(row.input, "itemId")} · {readText(row.input, "modality")}</p>}
      </div>
      <div><h4>{t("المتوقع", "Expected")}</h4><pre dir="auto">{JSON.stringify(row.expected, null, 2)}</pre>
        {["type", "priority", "risk"].some(field => readText(row.expected, field)) && <p>{t("اتفاق مع المرجع", "Reference agreement")}: {evaluated ? agreements + "/" + evaluated : "Not Evaluated"}</p>}
      </div>
      <div><h4>{t("الفعلي من النموذج", "Actual model output")}</h4>
        {row.actual ? <pre dir="auto">{JSON.stringify(row.actual, null, 2)}</pre> : <p>—</p>}
        {Array.isArray(candidates) && <ol className="retrieval-candidates">{candidates.map((candidate, index) => {
          const entry = candidate as Record<string, unknown>;
          return <li key={String(entry.itemId)}><bdi>#{index + 1} {String(entry.name)}</bdi><span>{String(entry.similarity)}</span></li>;
        })}</ol>}
      </div>
    </div>
    <p className="case-metadata"><bdi>{row.model ?? "—"}</bdi> · {row.elapsedMilliseconds == null ? "—" : row.elapsedMilliseconds + " ms"} · {row.errorCode ?? t("دون خطأ API مسجل", "No API error recorded")}</p>
  </details>;
}

function PrintReport({ snapshot, run, rows, t }: { snapshot: EvaluationSnapshot; run: EvaluationRun | null; rows: EvaluationCase[]; t: (ar: string, en: string) => string }) {
  const maintenance = rows.filter(row => row.kind.startsWith("maintenance"));
  const fields = ["type", "priority", "risk"];
  const timed = rows.filter(row => row.elapsedMilliseconds != null);
  const positive = rows.filter(row => row.expected.itemId && typeof row.actual?.top1 === "boolean");
  const top1 = positive.filter(row => row.actual?.top1 === true).length;
  const top5 = positive.filter(row => row.actual?.top5 === true).length;
  return <section className="evaluation-print">
    <header><h1>{t("نتائج تقييم الذكاء الاصطناعي — Smart Campus", "AI Evaluation — Smart Campus")}</h1>
      <p>{run ? t("التشغيل المحدد: ", "Selected run: ") + run.suite + " · " + run.suiteVersion : t("لا يوجد تشغيل محدد", "No run selected")}</p>
      <p>{t("الموديلات المثبتة", "Pinned models")}: <bdi>{snapshot.usage.classificationModel}</bdi> · <bdi>{snapshot.usage.embeddingModel}</bdi></p>
      <p>{t("سقف السعر", "Maximum price")}: 0 · allow_fallbacks: false</p></header>
    <section><h2>{t("الملخص والمقاييس المقاسة", "Summary and measured metrics")}</h2>
      <p>{t("الحالات", "Cases")}: {rows.length} · {t("نجاح API", "API passed")}: {rows.filter(x => x.state === "completed" || x.state === "cached").length} · {t("فشل", "Failed")}: {rows.filter(x => x.state === "failed" || x.state === "interrupted").length}</p>
      <EvaluationChart title={t("نتيجة API للحالات", "API case outcomes")}
        labels={[t("نجح", "Succeeded"), t("فشل", "Failed"), t("معلق", "Pending")]}
        values={[rows.filter(x => x.state === "completed" || x.state === "cached").length,
          rows.filter(x => x.state === "failed" || x.state === "interrupted").length,
          rows.filter(x => x.state === "pending" || x.state === "interrupted").length]} t={t} />
      <p>{t("اتفاق الصيانة مع المرجع الأولي", "Maintenance agreement with provisional references")}: {fields.map(field => { const measured = measuredReference(maintenance, field); return measured.length ? measured.filter(row => matchesReference(row, field)).length + "/" + measured.length : "Not Evaluated"; }).join(" · ")} {t("(النوع، الأولوية، الخطورة)", "(type, priority, risk)")}</p>
      <p>{t("الاسترجاع الإيجابي المقاس", "Measured positive retrieval")}: Top-1 {positive.length ? top1 + "/" + positive.length : "Not Evaluated"} · Top-5 {positive.length ? top5 + "/" + positive.length : "Not Evaluated"}. {t("قبول غير الموجود: Not Evaluated بلا عتبة مستقلة.", "No-match acceptance: Not Evaluated without an independent threshold.")}</p>
      <p>{t("متوسط زمن الحالات المقاسة", "Mean measured duration")}: {timed.length ? Math.round(timed.reduce((sum, row) => sum + (row.elapsedMilliseconds ?? 0), 0) / timed.length) + " ms" : "Not Evaluated"}</p>
      <EvaluationChart title={t("متوسط زمن الطلب حسب الوظيفة (ms)", "Mean request duration by function (ms)")}
        labels={["maintenance-text", "maintenance-image", "embedding-query-text", "embedding-query-image", "embedding-passage-text", "embedding-passage-image"]}
        values={["maintenance-text", "maintenance-image", "embedding-query-text", "embedding-query-image", "embedding-passage-text", "embedding-passage-image"].map(kind => {
          const group = timed.filter(row => row.kind === kind);
          return group.length ? Math.round(group.reduce((sum, row) => sum + (row.elapsedMilliseconds ?? 0), 0) / group.length) : 0;
        })} t={t} />
    </section>
    <section className="historical-print"><h2>{t("النتائج السابقة — منفصلة عن التشغيل المحدد", "Historical results — separate from selected run")}</h2>
      {snapshot.history.map(x => <article key={x.date + x.source}><h3>{x.date} · {x.source}</h3><p>{x.metrics}</p><p>{x.limits}</p>
        <EvaluationChart title={t("مؤشرات المصدر التاريخي المحفوظة", "Recorded source metrics")} labels={x.chartLabels.map(label => historicalLabel(label, t))} values={x.chartValues} color="#8091a8" t={t} />
      </article>)}
      <p>{t("المجموعات والموديلات القديمة لا تندمج مع التشغيل المحدد ولا تمثل دقة عامة.", "Historical sets and models are not combined with the selected run and do not represent general accuracy.")}</p>
    </section>
    <section><h2>{t("تفاصيل كل حالة في التشغيل المحدد", "Every case in the selected run")}</h2>
      {rows.length ? rows.map(row => <CaseDetails key={row.id} row={row} t={t} />) : <p>{t("لا توجد حالات محفوظة.", "No saved cases.")}</p>}
    </section>
  </section>;
}

export function AiEvaluation() {
  const { t, language } = useApp();
  const [snapshot, setSnapshot] = useState<EvaluationSnapshot | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [suite, setSuite] = useState<EvaluationSuite>("integration");
  const [preflight, setPreflight] = useState<EvaluationPreflight | null>(null);
  const [selectedRunId, setSelectedRunId] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const stop = useRef(false);
  const refresh = useCallback(async (runId?: string) => {
    const fresh = await aiEvaluations.snapshot(runId ?? (selectedRunId || undefined));
    setSnapshot(fresh);
    setSelectedRunId(current => runId || current || fresh.runs[0]?.id || "");
    return fresh;
  }, [selectedRunId]);
  useEffect(() => {
    const timer = window.setTimeout(() => {
      void refresh().catch(() => setError(document.documentElement.lang === "ar"
        ? "تعذر تحميل النتائج المحفوظة." : "Could not load saved results."));
    }, 0);
    return () => window.clearTimeout(timer);
  }, [refresh]);

  const run = snapshot?.runs.find(value => value.id === selectedRunId) ?? null;
  useEffect(() => {
    if (!selectedRunId || run?.state !== "running") return;
    const timer = window.setInterval(() => { void refresh(selectedRunId).catch(() => undefined); }, 15_000);
    return () => window.clearInterval(timer);
  }, [refresh, run?.state, selectedRunId]);
  const allRows = useMemo(() => snapshot?.cases.filter(row => row.runId === selectedRunId) ?? [], [snapshot, selectedRunId]);
  const rows = allRows.filter(row => statusFilter === "all" || row.state === statusFilter);

  async function executeUntilStopped(runId: string) {
    stop.current = false;
    setBusy(true);
    setError("");
    try {
      for (let count = 0; count < 50 && !stop.current; count++) {
        const step = await aiEvaluations.next(runId);
        await refresh(runId);
        if (step.pauseReason === "quota") {
          setError(t("توقف عند حد التطبيق قبل الطلب التالي. حُفظ التقدم؛ استأنفه بعد تجدد الحصة.", "Paused at the application quota before the next request. Progress is saved; resume after reset."));
          break;
        }
        if (step.case?.state === "failed" || step.case?.state === "interrupted") {
          setError(t("توقف بعد هذه الحالة. راجع الخطأ ثم أكمل يدويًا؛ لم يُعَد إرسال الطلب.", "Stopped after this case. Review the error, then continue manually; the request was not resent."));
          break;
        }
        if (step.runState === "completed" || !step.case) break;
      }
      if (stop.current) await aiEvaluations.pause(runId);
      await refresh(runId);
    } catch {
      setError(t("انقطع التنفيذ. الحالات المكتملة محفوظة وأي طلب غير محسوم لن يعاد تلقائيًا.", "Execution interrupted. Completed cases are saved; unresolved requests are not retried automatically."));
      await refresh(runId).catch(() => undefined);
    } finally { setBusy(false); }
  }

  async function startSuite(selected: EvaluationSuite) {
    setBusy(true);
    setError("");
    try {
      const created = await aiEvaluations.create(selected);
      setPreflight(null);
      setSuite(selected);
      setSelectedRunId(created.id);
      await refresh(created.id);
      await executeUntilStopped(created.id);
    } catch {
      setError(t("تعذر بدء التشغيل؛ تحقق من الحصة أو التشغيل السابق.", "Could not start; check quotas or another active run."));
      setBusy(false);
    }
  }

  async function prepareSuite() {
    setBusy(true);
    setError("");
    try { setPreflight(await aiEvaluations.preflight(suite)); }
    catch { setError(t("تعذر حساب الطلبات أو قراءة العداد الحالي.", "Could not calculate requests or read current counters.")); }
    finally { setBusy(false); }
  }

  async function resumeRun() {
    if (!run) return;
    setBusy(true);
    try { await aiEvaluations.resume(run.id); await executeUntilStopped(run.id); }
    catch { setError(t("تعذر الاستئناف؛ الحالات المكتملة لا يعاد إرسالها.", "Could not resume; completed cases are not resent.")); setBusy(false); }
  }

  async function exportCsv() {
    if (!run) return;
    try {
      const blob = await aiEvaluations.csv(run.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = "smart-campus-ai-evaluation.csv";
      anchor.click();
      URL.revokeObjectURL(url);
    } catch { setError(t("تعذر تصدير CSV.", "CSV export failed.")); }
  }

  const maintenance = allRows.filter(row => row.kind.startsWith("maintenance"));
  const fields = ["type", "priority", "risk"];
  const referenceMetrics = fields.map(field => {
    const relevant = maintenance.filter(row => readText(row.expected, field));
    return relevant.length ? relevant.filter(row => matchesReference(row, field)).length / relevant.length * 100 : 0;
  });
  const timed = allRows.filter(row => row.elapsedMilliseconds != null);
  const retrievalRows = allRows.filter(row => row.expected.itemId);
  const retrievalTop1 = retrievalRows.filter(row => row.actual?.top1 === true).length;
  const retrievalTop5 = retrievalRows.filter(row => row.actual?.top5 === true).length;
  const responseGroups = ["maintenance-text", "maintenance-image", "embedding-query-text", "embedding-query-image", "embedding-passage-text", "embedding-passage-image"];
  const averageResponse = responseGroups.map(kind => {
    const group = timed.filter(row => row.kind === kind);
    return group.length ? Math.round(group.reduce((sum, row) => sum + (row.elapsedMilliseconds ?? 0), 0) / group.length) : 0;
  });
  const success = allRows.filter(row => row.state === "completed" || row.state === "cached").length;
  const failed = allRows.filter(row => row.state === "failed" || row.state === "interrupted").length;
  const modelCards = [
    { name: snapshot?.usage.classificationModel ?? "—", rows: allRows.filter(row => row.kind.startsWith("maintenance")), kind: "maintenance" },
    { name: snapshot?.usage.embeddingModel ?? "—", rows: allRows.filter(row => row.kind.startsWith("embedding")), kind: "embedding" },
  ].map(model => {
    const completed = model.rows.filter(row => row.state === "completed" || row.state === "cached");
    const failedCount = model.rows.filter(row => row.state === "failed" || row.state === "interrupted").length;
    const durations = completed.flatMap(row => row.elapsedMilliseconds == null ? [] : [row.elapsedMilliseconds]);
    const modelStatus = !model.rows.length ? t("لم يُختبر", "Not Run") : failedCount ? t("يوجد فشل", "Failures") :
      completed.length === model.rows.length ? t("اكتمل", "Completed") : t("قيد التنفيذ", "In progress");
    const measuredPositives = model.rows.filter(row => row.expected.itemId && typeof row.actual?.top1 === "boolean");
    const typeRows = model.rows.filter(row => row.expected.type && row.actual?.type);
    const metric = model.kind === "maintenance"
      ? typeRows.length ? t("اتفاق النوع", "Type agreement") + ": " + typeRows.filter(row => matchesReference(row, "type")).length + "/" + typeRows.length : "Not Evaluated"
      : measuredPositives.length ? "Top-1 " + measuredPositives.filter(row => row.actual?.top1 === true).length + "/" + measuredPositives.length + " · Top-5 " + measuredPositives.filter(row => row.actual?.top5 === true).length + "/" + measuredPositives.length : "Not Evaluated";
    return { ...model, completed: completed.length, failedCount, durations, modelStatus, metric };
  });
  const currentSuite = suites.find(value => value.id === suite)!;
  return <Page title={t("نتائج تقييم الذكاء الاصطناعي", "AI Evaluation")}
    subtitle={t("الاختبارات المحفوظة بالتفصيل: المدخل، المرجع، المخرج، الزمن والأخطاء.", "Saved evaluations with full inputs, references, outputs, timings, and errors.")}>
    <div className="evaluation-screen">
      <div className="actions no-print">
        <Link className="button secondary" to="/admin">{t("العودة للإدارة", "Back to admin")}</Link>
        <button className="button secondary" type="button" onClick={() => window.print()}>{t("تصدير PDF", "Export PDF")}</button>
        {run && <button className="button secondary" type="button" onClick={() => { void exportCsv(); }}>{t("تصدير CSV", "Export CSV")}</button>}
        <button className="button secondary" type="button" disabled={busy} onClick={() => { void refresh(); }}>{t("تحديث المحفوظ", "Refresh saved")}</button>
      </div>
      {error && <Feedback kind="error">{error}</Feedback>}
      {!snapshot && !error && <p role="status">{t("جارٍ تحميل النتائج المحفوظة…", "Loading saved results…")}</p>}
      {snapshot && <>
        <section className="panel evaluation-safety">
          <h2>{t("الموديلات والحدود", "Models and limits")}</h2>
          <p>{t("التصنيف", "Classification")}: <bdi>{snapshot.usage.classificationModel}</bdi></p>
          <p>Embeddings: <bdi>{snapshot.usage.embeddingModel}</bdi></p>
          <p>{t("السعر الأقصى", "Maximum price")}: {snapshot.usage.maximumPrice} · allow_fallbacks: {String(snapshot.usage.paidFallbacks)}</p>
          <p>{t("عداد التطبيق اليومي", "App daily counter")}: {snapshot.usage.dailyUsed}/{snapshot.usage.dailyLimit} ({t("المتاح", "remaining")} {snapshot.usage.dailyRemaining})</p>
          <p>{t("عداد التطبيق للدقيقة الحالية", "App current-minute counter")}: {snapshot.usage.minuteUsed}/{snapshot.usage.minuteLimit} ({t("المتاح", "remaining")} {snapshot.usage.minuteRemaining})</p>
          <p className="muted">{t("العداد خاص بالتطبيق؛ حصة وفاتورة OpenRouter لا يمكن قراءتهما مستقلًا من هنا. الطلبات مثبتة على :free بلا fallback مدفوع.", "The counter is application-side; the site cannot read OpenRouter account quota/billing independently. Requests are pinned to :free without paid fallback.")}</p>
          <p>{t("كل المدخلات خيالية. لا تتغير موافقات إرسال بيانات المستخدمين الحقيقية.", "All inputs are fictional. User-data transfer approvals are unchanged.")}</p>
        </section>
        <section className="panel">
          <h2>{t("تشغيل مجموعة", "Run a suite")}</h2>
          <label className="field">{t("المجموعة", "Suite")}
            <select value={suite} onChange={event => { setSuite(event.target.value as EvaluationSuite); setPreflight(null); }} disabled={busy}>
              {suites.map(option => <option key={option.id} value={option.id}>{t(...option.title)}</option>)}
            </select>
          </label>
          <p>{t(...currentSuite.detail)} · {t("أقصى عدد أولي", "Maximum initial calls")}: {currentSuite.calls}</p>
          {suite === "lost-found" && <p>{t("٦ أغراض × embedding وصف وصورة منفصلين (١٢)، ثم ٨ استعلامات. الإجمالي الأولي ٢٠؛ المتجهات السابقة المتوافقة تُخصم.", "6 items × separate description and image embeddings (12), then 8 queries. Initial total 20; compatible saved vectors are subtracted.")}</p>}
          <button className="button secondary" disabled={busy || snapshot.usage.dailyRemaining === 0 || snapshot.usage.minuteRemaining === 0}
            onClick={() => { void prepareSuite(); }}>{busy ? t("جارٍ حساب الطلبات…", "Calculating requests…") : t("احسب الطلبات أولًا", "Calculate requests first")}</button>
          {preflight?.suite === suite && <div className="evaluation-preflight" role="status">
            <h3>{t("فحص قبل التشغيل", "Preflight")}: {preflight.apiCallsRequired} {t("طلب API فعلي مطلوب", "actual API calls required")}</h3>
            <p>{t("الحالات", "Cases")}: {preflight.caseCount} · {t("المتاح اليوم في التطبيق", "App daily remaining")}: {preflight.usage.dailyRemaining}/{preflight.usage.dailyLimit} · {t("المتاح في الدقيقة الحالية", "Current-minute remaining")}: {preflight.usage.minuteRemaining}/{preflight.usage.minuteLimit}</p>
            <p>{t("السعر الأقصى 0، Paid fallback مغلق. يتوقف الحفظ عند حد التطبيق أو عند أول فشل؛ لا توجد إعادة تلقائية.", "Maximum price 0, paid fallback disabled. Work pauses at application limits or first failure; no automatic retry.")}</p>
            <button className="button" disabled={busy || preflight.apiCallsRequired > 0 && preflight.usage.dailyRemaining === 0}
              onClick={() => { void startSuite(suite); }}>{t("ابدأ هذا التشغيل", "Start this run")}</button>
          </div>}
          {busy && <button className="button secondary" onClick={() => { stop.current = true; }}>{t("إيقاف بعد الطلب الجاري", "Stop after current request")}</button>}
          {(run?.state === "paused" || run?.state === "pending") && !busy && <button className="button" onClick={() => { void resumeRun(); }}>{t("استكمال الحالات غير المنفذة", "Resume unstarted cases")}</button>}
        </section>
        <section className="stats admin-stats">
          <div className="stat"><span>{t("حالات التشغيل", "Run cases")}</span><strong>{allRows.length}</strong></div>
          <div className="stat"><span>{t("نجاح API والتحقق", "API/validation passed")}</span><strong>{success}</strong></div>
          <div className="stat"><span>{t("فشل أو غير محسوم", "Failed or unresolved")}</span><strong>{failed}</strong></div>
          <div className="stat"><span>{t("اتفاق النوع مع المرجع الأولي", "Fault-type agreement")}</span><strong>{maintenance.some(row => row.expected.type) ? Math.round(referenceMetrics[0]) + "%" : "Not Evaluated"}</strong></div>
          <div className="stat"><span>{t("استرجاع الإيجابيات — Top-1", "Positive retrieval — Top-1")}</span><strong>{retrievalRows.length ? retrievalTop1 + "/" + retrievalRows.length : "Not Evaluated"}</strong></div>
          <div className="stat"><span>{t("استرجاع الإيجابيات — Top-5", "Positive retrieval — Top-5")}</span><strong>{retrievalRows.length ? retrievalTop5 + "/" + retrievalRows.length : "Not Evaluated"}</strong></div>
        </section>
        <section className="evaluation-model-grid" aria-label={t("ملخص كل موديل", "Per-model summary")}>
          {modelCards.map(model => <article className="panel evaluation-model-card" key={model.name}>
            <h2><bdi>{model.name}</bdi></h2>
            <p>{t("حالة الموديل في التشغيل المحدد", "Model status in selected run")}: <strong>{model.modelStatus}</strong></p>
            <p>{t("الحالات", "Cases")}: {model.rows.length} · {t("ناجح", "Passed")}: {model.completed} · {t("فاشل/غير محسوم", "Failed/unresolved")}: {model.failedCount}</p>
            <p>{model.metric}</p>
            <p>{t("متوسط الزمن", "Mean latency")}: {model.durations.length ? Math.round(model.durations.reduce((sum, value) => sum + value, 0) / model.durations.length) + " ms" : "Not Evaluated"}</p>
          </article>)}
        </section>
        <section className="split evaluation-charts">
          <EvaluationChart title={t("الاتفاق مع المراجع الأولية — ليس دقة عامة", "Agreement with provisional references — not general accuracy")}
            labels={[t("النوع", "Type"), t("الأولوية", "Priority"), t("الخطورة", "Risk")]} values={referenceMetrics} color="#e18a34" t={t} />
          <EvaluationChart title={t("متوسط زمن الطلب حسب الوظيفة (ms)", "Mean request duration by function (ms)")}
            labels={responseGroups.map(kind => kind.replace("embedding-", "embed "))} values={averageResponse} t={t} />
          <EvaluationChart title={t("نتيجة API للحالات", "API outcomes")} labels={[t("نجح", "Succeeded"), t("فشل", "Failed"), t("قيد التنفيذ/معلق", "Pending")]}
            values={[success, allRows.filter(row => row.state === "failed" || row.state === "interrupted").length,
              allRows.filter(row => row.state === "pending" || row.state === "running").length]} color="#557d65" t={t} />
        </section>
        <section className="panel">
          <h2>{t("التشغيلات المحفوظة", "Saved runs")}</h2>
          <label className="field">{t("اختر تشغيلًا", "Select run")}
            <select value={selectedRunId} onChange={event => setSelectedRunId(event.target.value)}>
              <option value="">{t("لا يوجد", "None")}</option>
              {snapshot.runs.map(item => <option key={item.id} value={item.id}>{item.suite} · {item.state} · {new Date(item.createdAt).toLocaleString(language)}</option>)}
            </select>
          </label>
          <p>{t("قراءة النتائج من قاعدة البيانات لا تستدعي AI.", "Reading results from the database does not call AI.")}</p>
        </section>
        <section className="panel">
          <div className="actions no-print"><h2>{t("تفاصيل الحالات", "Case details")}</h2>
            <label className="field">{t("التصفية", "Filter")}
              <select value={statusFilter} onChange={event => setStatusFilter(event.target.value)}>
                <option value="all">{t("كل الحالات", "All states")}</option>
                {["completed", "cached", "failed", "interrupted", "pending"].map(value => <option key={value}>{value}</option>)}
              </select>
            </label>
          </div>
          {rows.length ? rows.map(row => <CaseDetails key={row.id} row={row} t={t} />) : <p>{t("لا توجد حالات في التشغيل المحدد.", "No cases in the selected run.")}</p>}
        </section>
        <section className="panel historical-section"><h2>{t("مصادر تاريخية منفصلة", "Separate historical sources")}</h2>
          {snapshot.history.map(entry => <article key={entry.date + entry.source}><h3>{entry.date} · {entry.source}</h3><p>{entry.metrics}</p><p className="muted">{entry.limits}</p>
            <EvaluationChart title={t("مؤشرات المصدر المحفوظة", "Recorded source metrics")} labels={entry.chartLabels.map(label => historicalLabel(label, t))} values={entry.chartValues} color="#8091a8" t={t} />
          </article>)}
        </section>
        <section className="panel"><h2>{t("أهداف المشروع", "Project objectives")}</h2>
          <p>{t("الصيانة: تصنيف النوع والأولوية والخطورة كتوصية يراجعها الموظف. المفقودات: تضمينات مستقلة للوصف والصورة وقياس Top-1/Top-5. المجموعة الاصطناعية أولية وليست تحققًا أكاديميًا مستقلًا.", "Maintenance: type, priority, and risk as staff-reviewed suggestions. Lost and found: separate text/image embeddings and Top-1/Top-5. The synthetic set is provisional, not independent academic validation.")}</p>
        </section>
      </>}
    </div>
    {snapshot && <PrintReport snapshot={snapshot} run={run} rows={allRows} t={t} />}
  </Page>;
}
