import { useEffect, useState } from "react";
import { useApp } from "../app/context";
import { departments } from "../services/http";

export function DirectoryOptions({ kind = "receiving" }: { kind?: "receiving" | "maintenance" | "all" }) {
  const { t } = useApp();
  const [entries, setEntries] = useState<Awaited<ReturnType<typeof departments>>>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    let active = true;
    departments()
      .then(list => {
        if (!active) return;
        setEntries(list);
        setStatus("ready");
      })
      .catch(() => {
        if (active) setStatus("error");
      });
    return () => { active = false; };
  }, []);

  const available = entries.filter(entry =>
    kind === "all" || (kind === "maintenance" ? entry.maintenance : entry.canReceive));

  return <>
    {status === "loading" && <option value="" disabled>{t("جارٍ تحميل الجهات...", "Loading departments...")}</option>}
    {status === "error" && <option value="" disabled>{t("دليل الجهات غير متاح", "Department directory unavailable")}</option>}
    {status === "ready" && available.length === 0 && (
      <option value="" disabled>{kind === "receiving"
        ? t("لا توجد جهات مستلمة مسجلة بعد", "No receiving departments are configured yet")
        : kind === "maintenance"
          ? t("لا توجد جهات صيانة مسجلة بعد", "No maintenance departments are configured yet")
          : t("لا توجد جهات مسجلة بعد", "No departments are configured yet")}</option>
    )}
    {status === "ready" && available.map(entry =>
      <option key={entry.id} value={entry.code}>{t(entry.ar, entry.en)}</option>)}
  </>;
}
