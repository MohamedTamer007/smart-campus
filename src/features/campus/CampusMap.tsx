import { useRef, useState } from "react";
import type { PointerEvent } from "react";
import { useApp } from "../../app/context";
import { Feedback, Page } from "../../components/ui";

const DENTISTRY_GUIDE = "https://o6u.edu.eg/uploads/1%20dent/Departments/-%D8%AF%D9%84%D9%8A%D9%84%20%D8%A7%D9%84%D8%B7%D8%A7%D9%84%D8%A8%20--Final-%202024-2025.pdf?FactId=11&id=3243";
const ISCS_GUIDE = "https://o6u.edu.eg/uploads/IsCS/updates2023/Student%20Guide%281%29.pdf?FactId=14&id=920";
const ENGINEERING_GUIDE = "https://o6u.edu.eg/uploads/h/%D8%AF%D9%84%D9%8A%D9%84%20%D8%A7%D9%84%D8%B7%D8%A7%D9%84%D8%A8%202022.pdf";
const STUDENT_SUPPORT = "https://o6u.edu.eg/dpages.aspx?FactId=21&id=3431";
const UNIVERSITY_PROFILE = "https://o6u.edu.eg/dpagesuni.aspx?FactId=0&id=667";

type CampusPlace = {
  id: string;
  ar: string;
  en: string;
  x: number;
  y: number;
  sourceEra: "2024" | "older";
  detailAr: string;
  detailEn: string;
  sourceUrl: string;
  sourceAr: string;
  sourceEn: string;
  mapSourceUrl?: string;
  mapSourceAr?: string;
  mapSourceEn?: string;
};

const CAMPUS_PLACES: CampusPlace[] = [
  {
    id: "hotel",
    ar: "فندق الطالبات",
    en: "Female Students Hotel",
    x: 17,
    y: 25,
    sourceEra: "2024",
    detailAr: "يظهر في خريطة الجامعة باسم الفندق الجامعي. وتؤكد صفحة خدمات الطلاب أن فندق الطالبات يقع داخل الحرم الجامعي.",
    detailEn: "Shown as the university hotel on the 2024/25 campus map. The university's student-support page confirms that the female students hotel is on campus.",
    sourceUrl: STUDENT_SUPPORT,
    sourceAr: "خدمات الطلاب — موقع الجامعة",
    sourceEn: "Student support — university website",
  },
  {
    id: "playgrounds",
    ar: "الملاعب الرياضية",
    en: "Sports Fields",
    x: 43.9,
    y: 30.6,
    sourceEra: "2024",
    detailAr: "الملاعب موضحة في الجزء الشمالي الأوسط من مخطط الحرم الجامعي الرسمي.",
    detailEn: "The sports fields are identified in the north-central part of the official campus plan.",
    sourceUrl: DENTISTRY_GUIDE + "#page=10",
    sourceAr: "دليل طب الأسنان 2024/2025 — خريطة الحرم، صفحة 9",
    sourceEn: "Dentistry guide 2024/25 — campus map, page 9",
    mapSourceUrl: "https://mapcarta.com/W1224226240",
    mapSourceAr: "إحداثيات مجمع الملاعب — OpenStreetMap / Mapcarta",
    mapSourceEn: "Playground coordinates — OpenStreetMap / Mapcarta",
  },
  {
    id: "hospital",
    ar: "المستشفى الجامعي",
    en: "University Hospital",
    x: 75,
    y: 13.4,
    sourceEra: "2024",
    detailAr: "المستشفى محدد في خريطة الحرم الرسمية. ويذكر دليل طب الأسنان 2024/2025 أن كلية طب الأسنان تقع داخل مبنى المستشفى.",
    detailEn: "The hospital is identified on the official campus map. The 2024/25 Dentistry Guide states that the Faculty of Dentistry is located inside the hospital building.",
    sourceUrl: DENTISTRY_GUIDE + "#page=6",
    sourceAr: "دليل طب الأسنان 2024/2025 — موقع الكلية",
    sourceEn: "Dentistry guide 2024/25 — faculty location",
    mapSourceUrl: "https://mapcarta.com/W1225193484",
    mapSourceAr: "إحداثيات المستشفى — OpenStreetMap / Mapcarta",
    mapSourceEn: "Hospital coordinates — OpenStreetMap / Mapcarta",
  },
  {
    id: "administration",
    ar: "المبنى الإداري",
    en: "Administrative Building",
    x: 17,
    y: 66,
    sourceEra: "2024",
    detailAr: "موضع المبنى الإداري ظاهر في الجزء الأوسط الغربي من خريطة الجامعة المنشورة في دليل 2024/2025.",
    detailEn: "The administrative building is shown in the west-central part of the university map published in the 2024/25 guide.",
    sourceUrl: DENTISTRY_GUIDE + "#page=10",
    sourceAr: "دليل طب الأسنان 2024/2025 — خريطة الحرم، صفحة 9",
    sourceEn: "Dentistry guide 2024/25 — campus map, page 9",
  },
  {
    id: "iscs",
    ar: "كلية نظم المعلومات وعلوم الحاسب",
    en: "Faculty of Information Systems & Computer Science",
    x: 82,
    y: 55,
    sourceEra: "older",
    detailAr: "الاسم ظاهر على مخطط كلية نظم المعلومات وعلوم الحاسب المنشور في دليل 2021/2022. موضع العلامة تقريبي من المخطط القديم، وليس إحداثية مبنى مؤكدة.",
    detailEn: "This name appears on the Faculty of Information Systems and Computer Science guide map (2021/22). The marker is an approximate reading of that older plan, not a verified building coordinate.",
    sourceUrl: ISCS_GUIDE + "#page=7",
    sourceAr: "دليل كلية نظم المعلومات 2021/2022 — مخطط الحرم",
    sourceEn: "Information Systems guide 2021/22 — campus plan",
  },
  {
    id: "educational-compound",
    ar: "المجمع التعليمي",
    en: "Educational Compound",
    x: 17,
    y: 87,
    sourceEra: "older",
    detailAr: "المجمع التعليمي مسمى في مخطط كلية نظم المعلومات وعلوم الحاسب. لا تفصل المصادر الرسمية المتاحة كل كلية داخله إلى مبنى مستقل.",
    detailEn: "The educational compound is named on the Information Systems and Computer Science guide map. The available official sources do not identify every faculty inside it as a separate building.",
    sourceUrl: ISCS_GUIDE + "#page=7",
    sourceAr: "دليل كلية نظم المعلومات 2021/2022 — مخطط الحرم",
    sourceEn: "Information Systems guide 2021/22 — campus plan",
  },
  {
    id: "pharmacy",
    ar: "مبنى الصيدلة",
    en: "Pharmacy Building",
    x: 43,
    y: 87,
    sourceEra: "older",
    detailAr: "مبنى الصيدلة مسمى في المخطط الرسمي المنشور بدليل كلية نظم المعلومات وعلوم الحاسب 2021/2022. موضع العلامة تقريبي.",
    detailEn: "The Pharmacy Building is named on the official 2021/22 Information Systems and Computer Science guide map. Its marker is approximate.",
    sourceUrl: ISCS_GUIDE + "#page=7",
    sourceAr: "دليل كلية نظم المعلومات 2021/2022 — مخطط الحرم",
    sourceEn: "Information Systems guide 2021/22 — campus plan",
  },
  {
    id: "engineering",
    ar: "مبنى الهندسة (مبنى 1)",
    en: "Engineering Building (Building 1)",
    x: 64,
    y: 87,
    sourceEra: "older",
    detailAr: "مخطط كلية الهندسة يسمي مبنى الهندسة، ويذكر الدليل أن كلية الهندسة تشغل المبنى رقم 1 بالحرم الجامعي.",
    detailEn: "The engineering campus plan labels the Engineering Building, and the guide states that the Faculty of Engineering occupies Building 1 on campus.",
    sourceUrl: ENGINEERING_GUIDE + "#page=9",
    sourceAr: "دليل كلية الهندسة 2022 — مخطط الحرم وموقع الكلية",
    sourceEn: "Engineering guide 2022 — campus plan and faculty location",
  },
  {
    id: "mosque",
    ar: "مسجد الجامعة",
    en: "University Mosque",
    x: 26,
    y: 61,
    sourceEra: "older",
    detailAr: "يظهر مسجد الجامعة ضمن المعالم المسماة في مخطط الحرم المنشور بدليل كلية الهندسة 2022. موضع العلامة تقريبي.",
    detailEn: "The University Mosque is named among the campus landmarks in the 2022 Engineering Guide plan. Its marker is approximate.",
    sourceUrl: ENGINEERING_GUIDE + "#page=9",
    sourceAr: "دليل كلية الهندسة 2022 — مخطط الحرم",
    sourceEn: "Engineering guide 2022 — campus plan",
  },
];

const TILE_COLUMNS = 8;
const TILE_ROWS = 5;
const TILE_ZOOM = 19;
const TILE_START_X = 307212;
const TILE_START_Y = 216344;
const TILE_BASE = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/";

const MAP_TILES = Array.from({ length: TILE_COLUMNS * TILE_ROWS }, (_, index) => ({
  key: index,
  x: TILE_START_X + (index % TILE_COLUMNS),
  y: TILE_START_Y + Math.floor(index / TILE_COLUMNS),
}));

export function CampusMap() {
  const { t } = useApp();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const dragStart = useRef<{ x: number; y: number; panX: number; panY: number } | null>(null);
  const filteredPlaces = CAMPUS_PLACES.filter(place => {
    const value = query.trim().toLocaleLowerCase();
    return !value || place.ar.toLocaleLowerCase().includes(value) || place.en.toLocaleLowerCase().includes(value);
  });
  const selectedPlace = CAMPUS_PLACES.find(place => place.id === selectedId) ?? null;

  function startPan(event: PointerEvent<HTMLDivElement>) {
    if ((event.target as HTMLElement).closest("button")) return;
    dragStart.current = { x: event.clientX, y: event.clientY, panX: pan.x, panY: pan.y };
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function movePan(event: PointerEvent<HTMLDivElement>) {
    if (!dragStart.current) return;
    setPan({
      x: dragStart.current.panX + event.clientX - dragStart.current.x,
      y: dragStart.current.panY + event.clientY - dragStart.current.y,
    });
  }

  function endPan() {
    dragStart.current = null;
  }

  function resetMap() {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  }

  return (
    <Page
      title={t("خريطة جامعة 6 أكتوبر", "October 6 University Map")}
      subtitle={t(
        "خريطة أقمار صناعية للحرم الجامعي، مع أسماء المواقع المستخرجة من أدلة الجامعة الرسمية.",
        "Satellite view of the campus, with place names taken from official university guides.",
      )}
    >
      <div className="campus-explorer">
        <section className="panel campus-explorer-panel">
          <div className="campus-explorer-toolbar">
            <div>
              <span className="tag">{t("أسماء موثقة من أدلة الجامعة", "Names sourced from university guides")}</span>
              <p className="muted">{t("اضغط على أي مبنى لعرض التفاصيل والمصدر.", "Select a building to see its details and source.")}</p>
            </div>
            <div className="campus-explorer-controls" aria-label={t("أدوات الخريطة", "Map controls")}>
              <button type="button" aria-label={t("تكبير الخريطة", "Zoom in")} onClick={() => setZoom(value => Math.min(2.4, value + 0.2))}>+</button>
              <button type="button" aria-label={t("تصغير الخريطة", "Zoom out")} onClick={() => setZoom(value => Math.max(1, value - 0.2))}>−</button>
              <button type="button" aria-label={t("إعادة ضبط الخريطة", "Reset map")} onClick={resetMap}>↺</button>
            </div>
          </div>
          <div
            className="campus-explorer-viewport"
            role="group"
            aria-label={t("خريطة الأقمار الصناعية لجامعة 6 أكتوبر", "October 6 University satellite map")}
            onPointerDown={startPan}
            onPointerMove={movePan}
            onPointerUp={endPan}
            onPointerCancel={endPan}
          >
            <div
              className="campus-explorer-layer"
              style={{ transform: "translate(" + pan.x + "px, " + pan.y + "px) scale(" + zoom + ")" }}
            >
              <div className="campus-explorer-tiles" aria-hidden="true">
                {MAP_TILES.map(tile => (
                  <img
                    key={tile.key}
                    src={TILE_BASE + TILE_ZOOM + "/" + tile.y + "/" + tile.x}
                    alt=""
                    draggable={false}
                    loading="lazy"
                  />
                ))}
              </div>
              <div className="campus-explorer-markers">
                {filteredPlaces.map(place => (
                  <button
                    type="button"
                    key={place.id}
                    className={"campus-explorer-marker " + place.sourceEra + (selectedId === place.id ? " selected" : "")}
                    style={{ left: place.x + "%", top: place.y + "%" }}
                    aria-label={t(place.ar, place.en)}
                    aria-pressed={selectedId === place.id}
                    onClick={() => setSelectedId(place.id)}
                  >
                    <span className="campus-explorer-marker-dot" aria-hidden="true" />
                    <span className="campus-explorer-marker-label">{t(place.ar, place.en)}</span>
                  </button>
                ))}
              </div>
            </div>
            <span className="campus-explorer-hint">{t("اسحب لتحريك الخريطة", "Drag to move the map")}</span>
          </div>
          <div className="campus-explorer-attribution">
            Imagery © Esri, Maxar, Earthstar Geographics, and the GIS User Community
          </div>
        </section>

        <section className="panel campus-explorer-directory">
          <div className="campus-explorer-directory-head">
            <h2>{t("المباني والمرافق", "Buildings and facilities")}</h2>
            <label className="campus-explorer-search">
              <span className="sr-only">{t("ابحث عن مبنى", "Search buildings")}</span>
              <input
                type="search"
                value={query}
                onChange={event => setQuery(event.target.value)}
                placeholder={t("ابحث باسم المبنى...", "Search by building name...")}
              />
            </label>
          </div>
          <div className="campus-explorer-place-list">
            {filteredPlaces.map(place => (
              <button
                type="button"
                key={place.id}
                className={"campus-explorer-place" + (selectedId === place.id ? " selected" : "")}
                onClick={() => setSelectedId(place.id)}
                aria-pressed={selectedId === place.id}
              >
                <strong>{t(place.ar, place.en)}</strong>
                <span className="campus-explorer-place-status">
                  {place.sourceEra === "2024"
                    ? t("مذكور في دليل 2024/2025", "Listed in the 2024/25 guide")
                    : t("مذكور في مخطط رسمي أقدم", "Listed in an earlier official plan")}
                </span>
                <small>{t("اضغط لعرض الموقع والمصدر", "Select to view location and source")}</small>
              </button>
            ))}
            {!filteredPlaces.length && <Feedback>{t("لا توجد مبانٍ مطابقة للبحث.", "No matching buildings.")}</Feedback>}
          </div>
        </section>

        {selectedPlace && (
          <section className="panel campus-explorer-detail" aria-live="polite">
            <div className="campus-explorer-detail-head">
              <h2>{t(selectedPlace.ar, selectedPlace.en)}</h2>
              <span className="tag">{selectedPlace.sourceEra === "2024"
                ? t("دليل 2024/2025", "2024/25 guide")
                : t("مخطط أقدم", "Earlier plan")}</span>
            </div>
            <p>{t(selectedPlace.detailAr, selectedPlace.detailEn)}</p>
            <p className="muted campus-explorer-note">{t(
              "تنبيه: مواضع العلامات تقريبية ومطابقة للمخططات المنشورة، وليست مسحًا هندسيًا أو إحداثيات رسمية لكل مبنى.",
              "Note: marker positions are approximate readings of published plans, not a surveyed building footprint or official coordinate for every building.",
            )}</p>
            <a href={selectedPlace.sourceUrl} target="_blank" rel="noreferrer">
              {t(selectedPlace.sourceAr, selectedPlace.sourceEn)}
            </a>
            {selectedPlace.mapSourceUrl && (
              <a href={selectedPlace.mapSourceUrl} target="_blank" rel="noreferrer">
                {t(selectedPlace.mapSourceAr ?? "", selectedPlace.mapSourceEn ?? "")}
              </a>
            )}
          </section>
        )}

        <section className="panel campus-explorer-outside">
          <h2>{t("مرافق قريبة خارج حدود الحرم", "Nearby facilities outside the campus boundary")}</h2>
          <p>{t(
            "تذكر صفحة الجامعة أن المكتبة المركزية وفندق الطلاب الذكور يقعان على بُعد نحو 150 مترًا خارج الحرم الرئيسي. لم أضع لهما دبوسًا لأن المصدر لا يحدد إحداثيات دقيقة لهما.",
            "The university profile places the central library and male students hotel about 150 metres outside the main campus. They are not pinned because the source does not provide exact coordinates.",
          )}</p>
          <a href={UNIVERSITY_PROFILE} target="_blank" rel="noreferrer">
            {t("نبذة الجامعة — الموقع الرسمي", "University profile — official website")}
          </a>
        </section>

        <Feedback>
          {t(
            "الخريطة لا تحتاج إلى إدخال المباني من لوحة الإدارة. أسماء المواقع محمّلة تلقائيًا من أدلة الجامعة. بعض الأسماء مأخوذ من مخطط 2021/2022، لذلك أُشير إليها كمواقع تقريبية؛ ولن نضيف اسم مبنى غير مثبت في مصدر رسمي.",
            "No buildings need to be entered through the admin panel. Names load automatically from university guides. Some names come from the 2021/22 plan and are marked as approximate; no unverified building name is added.",
          )}
        </Feedback>
      </div>
    </Page>
  );
}
