import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import { operations } from "../../services/admin";

export function OperationsStatus() {
  const { t, account } = useApp();
  const kinds: Record<string, string> = { maintenance: t("تقييم الصيانة", "Maintenance assessment"), index: t("فهرسة المعثورات", "Found-item indexing"), email: t("البريد", "Email") };
  const states: Record<string, string> = { pending: t("بالانتظار", "Pending"), running: t("قيد التنفيذ", "Running"), completed: t("مكتملة", "Completed"), unavailable: t("متعذرة", "Unavailable") };
  return <section className="panel">
    <h2>{t("حالة التشغيل", "Operational status")}</h2>
    <AsyncView key={account.id} load={operations}>
      {(status) => <>
        <p>{t("خدمة المطابقة", "Matching worker")}: {status.worker === "available" ? t("متاحة", "Available") : t("غير متاحة", "Unavailable")}</p>
        <p>{t("معايرة البحث", "Search calibration")}: {status.calibrationApproved ? t("مُراجعة ومعتمدة", "Reviewed and approved") : t("بانتظار الاعتماد", "Awaiting approval")}</p>
        <p>{t("ذاكرة الخدمة", "Service memory")}: {Math.round(status.workingSetBytes / 1024 / 1024)} MiB</p>
        <p>{t("محاولات هذا التطبيق وحدّه اليومي للذكاء الاصطناعي بتوقيت UTC", "App AI attempts / daily budget in UTC")}</p>
        <ul>{status.quotas.map((quota) => <li key={quota.provider}><bdi>{quota.provider}</bdi>: {quota.used} / {quota.dailyLimit}</li>)}</ul>
        <p>{t("مهام المعالجة", "Background processing")}</p>
        <ul>{status.jobs.map((job) => <li key={`${job.kind}-${job.state}`}>{kinds[job.kind] ?? t("معالجة أخرى", "Other processing")} · {states[job.state] ?? t("غير معروفة", "Unknown")}: {job.count}</li>)}</ul>
      </>}
    </AsyncView>
  </section>;
}
