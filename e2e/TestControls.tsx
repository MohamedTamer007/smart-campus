import { useApp } from "../src/app/context";
import { actors, type Role, type Scenario } from "../src/services/contracts";

export function TestControls() {
  const { t, setActor, actorKey, scenario, setScenario, actionScenario, setActionScenario } = useApp();
  const roleNames: Record<Role, string> = {
    guest: t("زائر", "Guest"),
    unverified: t("غير موثق", "Unverified"),
    user: t("مستخدم", "User"),
    other: t("مستخدم إضافي", "Additional user"),
    finder: t("مستخدم آخر", "Another user"),
    secretariat: t("السكرتارية", "Secretariat staff"),
    maintenance: t("موظف صيانة", "Maintenance staff"),
    department: t("أمن", "Security staff"),
    admin: t("مدير", "Admin"),
  };
  return (
    <>
      <style>{`.demo-bar {
  background: #142b54;
  color: #fff;
  padding: 9px max(24px, calc((100vw - 1180px) / 2));
  font-size: 0.75rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  position: relative;
  z-index: 5;
}
.demo-bar summary {
  cursor: pointer;
  white-space: nowrap;
}
.demo-tools {
  position: absolute;
  inset-inline-end: 24px;
  top: 40px;
  background: #fff;
  color: var(--navy);
  padding: 20px;
  border: 1px solid var(--line);
  box-shadow: 0 8px 30px #142b5425;
  min-width: 260px;
  border-radius: 12px;
  display: grid;
  gap: 14px;
}
.demo-tools label {
  display: grid;
  gap: 8px;
}
@media (max-width: 700px) {
  .demo-bar {
    padding: 8px 16px;
    font-size: 0.65rem;
    gap: 10px;
    align-items: flex-start;
  }
  .demo-tools {
    inset-inline-end: 8px;
    top: 50px;
  }
}
`}</style>
      <div className="demo-bar">
        <details>
          <summary>{t("أدوات التجربة", "Demo controls")}</summary>
          <div className="demo-tools">
            <label>
              {t("محاكاة الحساب", "Simulate account")}
              <select
                value={actorKey}
                onChange={(e) => {
                  if (e.target.value !== "registered")
                    setActor(e.target.value as Role);
                }}
              >
                {actorKey === "registered" && (
                  <option value="registered">
                    {t("حساب تجريبي مسجل", "Registered demo account")}
                  </option>
                )}
                {Object.keys(actors).map((role) => (
                  <option value={role} key={role}>
                    {roleNames[role as Role]}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("محاكاة الاستجابة", "Simulate response")}
              <select
                value={scenario}
                onChange={(e) => setScenario(e.target.value as Scenario)}
              >
                {(
                  [
                    "normal",
                    "empty",
                    "error",
                    "expired",
                    "conflict",
                  ] as Scenario[]
                ).map((s, i) => (
                  <option value={s} key={s}>
                    {
                      [
                        t("عادية", "Normal"),
                        t("فارغة", "Empty"),
                        t("فشل الخدمة", "Service error"),
                        t("انتهاء الجلسة", "Expired session"),
                        t("تعارض", "Conflict"),
                      ][i]
                    }
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("محاكاة نتيجة الإجراء", "Simulate action response")}
              <select
                value={actionScenario}
                onChange={(e) => setActionScenario(e.target.value as Scenario)}
              >
                {(["normal", "error", "expired", "conflict"] as Scenario[]).map(
                  (value, index) => (
                    <option key={value} value={value}>
                      {
                        [
                          t("عادية", "Normal"),
                          t("فشل الخدمة", "Service error"),
                          t("انتهاء الجلسة", "Expired session"),
                          t("تعارض", "Conflict"),
                        ][index]
                      }
                    </option>
                  ),
                )}
              </select>
            </label>
          </div>
        </details>
      </div>
    </>
  );
}
