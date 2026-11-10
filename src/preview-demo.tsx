import { useState } from "react";

type Language = "ar" | "en";
type Role = "student" | "staff" | "admin";
type Stage = 0 | 1 | 2;
type ReportType = "lighting" | "water" | "furniture";
type Building = "science" | "library" | "engineering";
type ItemType = "keys" | "wallet" | "notebook";

interface Report {
  id: number;
  kind: ReportType;
  building: Building;
  stage: Stage;
}
interface FoundItem {
  id: number;
  kind: ItemType;
  building: Building;
  stage: "available" | "requested" | "returned";
}

const initialReports: Report[] = [
  { id: 101, kind: "lighting", building: "science", stage: 1 },
  { id: 102, kind: "water", building: "library", stage: 0 },
];
const initialItems: FoundItem[] = [
  { id: 201, kind: "keys", building: "library", stage: "available" },
  { id: 202, kind: "wallet", building: "engineering", stage: "available" },
  { id: 203, kind: "notebook", building: "science", stage: "available" },
];

const reportOptions: Record<ReportType, [string, string]> = {
  lighting: ["عطل في الإضاءة", "Lighting issue"],
  water: ["تسريب مياه", "Water leak"],
  furniture: ["أثاث تالف", "Broken furniture"],
};
const buildingOptions: Record<Building, [string, string]> = {
  science: ["مبنى العلوم", "Science building"],
  library: ["المكتبة", "Library"],
  engineering: ["مبنى الهندسة", "Engineering building"],
};
const itemOptions: Record<ItemType, [string, string]> = {
  keys: ["مجموعة مفاتيح", "A set of keys"],
  wallet: ["محفظة", "Wallet"],
  notebook: ["كراسة", "Notebook"],
};
const reportStages: [string, string][] = [
  ["قيد المراجعة", "Pending review"],
  ["قيد التنفيذ", "In progress"],
  ["تم الإصلاح", "Resolved"],
];
const itemStages: Record<FoundItem["stage"], [string, string]> = {
  available: ["في العهدة", "In custody"],
  requested: ["طلب استرداد", "Recovery requested"],
  returned: ["تم التسليم", "Returned"],
};

function nextStage(value: Stage): Stage {
  return (Math.min(value + 1, 2)) as Stage;
}

export function PreviewDemo({ language }: { language: Language }) {
  const t = (ar: string, en: string) => language === "ar" ? ar : en;
  const localize = (values: [string, string]) => language === "ar" ? values[0] : values[1];
  const [role, setRole] = useState<Role>("student");
  const [reports, setReports] = useState<Report[]>(initialReports);
  const [items, setItems] = useState<FoundItem[]>(initialItems);
  const [reportKind, setReportKind] = useState<ReportType>("lighting");
  const [building, setBuilding] = useState<Building>("science");
  const [search, setSearch] = useState("");
  const [notice, setNotice] = useState("");
  const filteredItems = items.filter(item =>
    (itemOptions[item.kind][0] + " " + itemOptions[item.kind][1] + " " +
     buildingOptions[item.building][0] + " " + buildingOptions[item.building][1])
      .toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()),
  );

  function reset() {
    setRole("student");
    setReports(initialReports);
    setItems(initialItems);
    setReportKind("lighting");
    setBuilding("science");
    setSearch("");
    setNotice(t("تمت إعادة البيانات الوهمية لحالتها الأولى.", "Synthetic sample data was reset."));
  }
  function addReport(event: React.FormEvent) {
    event.preventDefault();
    setReports(prev => [
      { id: Math.max(100, ...prev.map(x => x.id)) + 1, kind: reportKind, building, stage: 0 },
      ...prev,
    ]);
    setNotice(t("اتضاف بلاغ وهمي داخل المتصفح فقط.", "A simulated report was added in this browser only."));
  }

  const roles: { value: Role; ar: string; en: string }[] = [
    { value: "student", ar: "الطالب", en: "Student" },
    { value: "staff", ar: "الموظف", en: "Staff" },
    { value: "admin", ar: "الإدارة", en: "Admin" },
  ];
  const pending = reports.filter(report => report.stage !== 2).length;
  const returns = items.filter(item => item.stage === "returned").length;

  return (
    <section className="preview-demo" id="demo" aria-label={t("تجربة تفاعلية ببيانات وهمية", "Interactive synthetic-data demo")}>
      <div className="demo-intro">
        <div>
          <span className="eyebrow">{t("جرب المشروع", "Try the project")}</span>
          <h2>{t("تجربة تفاعلية بدون تسجيل", "Interactive demo without signup")}</h2>
          <p>{t(
            "كل الأسماء والأرقام والحالات في التجربة دي وهمية، والتغييرات مؤقتة داخل الصفحة فقط. لا توجد طلبات لخادم أو قاعدة بيانات.",
            "All examples are fictional and changes are temporary in this page only. No requests are sent to an API or database.",
          )}</p>
        </div>
        <button className="demo-reset" type="button" onClick={reset}>
          {t("إعادة التجربة", "Reset demo")}
        </button>
      </div>

      <div className="demo-roles" role="group" aria-label={t("اختر دور تجريبي", "Choose a demo role")}>
        {roles.map(option => (
          <button
            type="button"
            key={option.value}
            onClick={() => { setRole(option.value); setNotice(""); }}
            aria-pressed={role === option.value}
            className={role === option.value ? "demo-role active" : "demo-role"}
          >
            {t(option.ar, option.en)}
          </button>
        ))}
      </div>

      {notice && <p className="demo-notice" role="status">{notice}</p>}

      {role === "admin" && (
        <div className="demo-panel" data-testid="preview-admin">
          <h3>{t("ملخص الإدارة (وهمي)", "Sample admin overview")}</h3>
          <div className="demo-stats">
            <div><strong>{reports.length}</strong><span>{t("إجمالي البلاغات", "Total reports")}</span></div>
            <div><strong>{pending}</strong><span>{t("بلاغات مفتوحة", "Open reports")}</span></div>
            <div><strong>{items.length}</strong><span>{t("المعثورات", "Found items")}</span></div>
            <div><strong>{returns}</strong><span>{t("تم تسليمها", "Returned items")}</span></div>
          </div>
          <p>{t("الأرقام دي بتتغير بناءً على خطواتك داخل التجربة بس.", "These numbers update only from your actions in this demo.")}</p>
        </div>
      )}

      {(role === "student" || role === "staff") && (
        <div className="demo-columns">
          <div className="demo-panel">
            <h3>{t("بلاغات الصيانة", "Maintenance reports")}</h3>
            {role === "student" && (
              <form onSubmit={addReport} className="demo-form">
                <label htmlFor="demo-problem">{t("نوع المشكلة", "Issue type")}</label>
                <select id="demo-problem" value={reportKind} onChange={event => setReportKind(event.target.value as ReportType)}>
                  {Object.entries(reportOptions).map(([key, values]) => (
                    <option value={key} key={key}>{localize(values)}</option>
                  ))}
                </select>
                <label htmlFor="demo-building">{t("المكان", "Location")}</label>
                <select id="demo-building" value={building} onChange={event => setBuilding(event.target.value as Building)}>
                  {Object.entries(buildingOptions).map(([key, values]) => (
                    <option value={key} key={key}>{localize(values)}</option>
                  ))}
                </select>
                <button className="button primary" type="submit">
                  {t("إضافة بلاغ تجريبي", "Add sample report")}
                </button>
              </form>
            )}
            <ul className="demo-list">
              {reports.map(report => (
                <li key={report.id}>
                  <div>
                    <strong>{localize(reportOptions[report.kind])}</strong>
                    <span>{localize(buildingOptions[report.building])} · #{report.id}</span>
                    <small>{localize(reportStages[report.stage])}</small>
                  </div>
                  {role === "staff" && report.stage !== 2 &&
                    <button type="button" onClick={() => setReports(prev => prev.map(x => x.id === report.id ? { ...x, stage: nextStage(x.stage) } : x))}>
                      {t("تحديث الحالة", "Update status")}
                    </button>}
                </li>
              ))}
            </ul>
          </div>

          <div className="demo-panel">
            <h3>{t("المفقودات والمعثورات", "Lost and found")}</h3>
            <label htmlFor="demo-search">{t("البحث في أمثلة وهمية", "Search sample items")}</label>
            <input
              id="demo-search"
              value={search}
              type="search"
              placeholder={t("مفاتيح، محفظة، مكتبة...", "Keys, wallet, library...")}
              onChange={event => setSearch(event.target.value)}
            />
            <ul className="demo-list">
              {filteredItems.map(item => (
                <li key={item.id}>
                  <div>
                    <strong>{localize(itemOptions[item.kind])}</strong>
                    <span>{localize(buildingOptions[item.building])} · #{item.id}</span>
                    <small>{localize(itemStages[item.stage])}</small>
                  </div>
                  {role === "student" && item.stage === "available" && (
                    <button type="button" onClick={() => setItems(prev => prev.map(x => x.id === item.id ? { ...x, stage: "requested" } : x))}>
                      {t("طلب استرداد وهمي", "Simulate claim")}
                    </button>
                  )}
                  {role === "staff" && item.stage === "requested" && (
                    <button type="button" onClick={() => setItems(prev => prev.map(x => x.id === item.id ? { ...x, stage: "returned" } : x))}>
                      {t("تأكيد تسليم وهمي", "Simulate return")}
                    </button>
                  )}
                </li>
              ))}
              {filteredItems.length === 0 && <li>{t("لا يوجد تطابق في الأمثلة.", "No matching examples.")}</li>}
            </ul>
          </div>
        </div>
      )}
    </section>
  );
}
