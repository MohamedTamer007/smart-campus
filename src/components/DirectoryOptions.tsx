import { useEffect, useState } from "react";
import { useApp } from "../app/context";
import { departments } from "../services/http";

export function DirectoryOptions({ kind = "receiving" }: { kind?: "receiving" | "maintenance" | "all" }) {
  const { t } = useApp();
  const [entries, setEntries] = useState<Awaited<ReturnType<typeof departments>>>([]);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let active = true;
    departments().then(list => { if (active) setEntries(list); }).catch(() => { if (active) setFailed(true); });
    return () => { active = false; };
  }, []);
  return <>{failed && <option disabled>{t("دليل الجهات غير متاح", "Department directory unavailable")}</option>}
    {entries.filter(entry => kind === "all" || (kind === "maintenance" ? entry.maintenance : entry.canReceive))
      .map(entry => <option key={entry.id} value={entry.code}>{t(entry.ar, entry.en)}</option>)}</>;
}
