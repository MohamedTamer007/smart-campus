# Smart Campus

React/TypeScript frontend and ASP.NET Core 10 backend for maintenance and private lost-and-found workflows. Includes PostgreSQL/pgvector, Identity cookie sessions, private media, department scopes, transactional reservations, bilateral delivery, notifications, administration and AI adapters.

## حالة المشروع — 14 نوفمبر 2026

الموقع منشور على [smartcampus.tryasp.net](https://smartcampus.tryasp.net/) باستضافة MonsterASP. تم دمج `codex/release-readiness-20261111` في `main` عبر [PR #3](https://github.com/MohamedTamer007/smart-campus/pull/3)، بعملية الدمج `f8ef9df`.

النسخة المصدرية `ae96c51` اجتازت [CI](https://github.com/MohamedTamer007/smart-campus/actions/runs/43538691200) و[النشر التلقائي](https://github.com/MohamedTamer007/smart-campus/actions/runs/43538684658). هذه الأدلة تخص النسخة المصدرية، وليست إثباتًا لاختبارات جديدة على commit الدمج. النشر التلقائي إلى MonsterASP ما زال مربوطًا بفرع `codex/release-readiness-20261111`؛ لا يُحذف قبل نقل إعداد النشر والتحقق منه.

**لا يُعتبر المشروع مكتملًا 100%.** التنفيذ واختبارات السيناريوهات المحلية لا يثبتان اكتمال القبول على الموقع الفعلي.

## ما تم تنفيذه

| المجال | التنفيذ الموجود | حدود التحقق |
|---|---|---|
| الحسابات | التسجيل بالنطاق الدقيق `o6u.edu.eg`، التفعيل وإعادة الإرسال، الدخول والخروج، الاستعادة، الملف الشخصي وكلمة المرور، وجلسات بالخادم | اختبارات محلية للتدفقات والصلاحيات وانتهاء الرموز والجلسات؛ استلام البريد الحقيقي يحتاج قبولًا |
| دليل الجامعة | إدارة الجهات والمواقع الهرمية وخيارات المبنى والطابق وتوجيه البلاغات | اختُبر ببيانات خيالية؛ يلزم اعتماد بيانات الجامعة الفعلية |
| الصيانة | إنشاء البلاغ ومتابعته وتعديله وإلغاؤه وفق الحالة، مراجعة الموظف والتكليف وتغيير الحالات والسجل والإشعارات | اختبارات محلية لدورة العمل والصلاحيات والتعارض؛ القبول المنشور الكامل مفتوح |
| توصيات الصيانة | تحليل AI بعد المراجعة والموافقات، مع فصل الاقتراح عن قرار الموظف وعرض الفشل دون نتائج ثابتة | نجاح التكامل لا يثبت دقة التصنيف أو استمرار توافر المزوّد |
| المعثورات والصور | تسجيل الغرض والصورة والمكان والحيازة، تخزين خاص، فحص الصور وتحجيمها وضغطها بالخادم | اختبارات محلية للرفع والصلاحيات؛ استمرار الملفات في الإنتاج يحتاج تحققًا |
| العهدة | طلب نقل الحيازة وتأكيد الاستلام ضمن نطاق الجهة والإلغاء وعدم الاستلام | لا تنتقل الحيازة قبل التأكيد؛ قواعد الانتقال والحجز مختبرة محليًا |
| الاسترداد | تقديم الطلب وقبوله ورفضه وإلغاؤه، منع الحجوزات المتعارضة، تأكيد مستقل من الطرفين وإغلاق ذري | اختبارات محلية للتزامن والصلاحيات والتأكيدين |
| البحث | Embeddings للنص والصورة، فهرسة بإصدار مستقل، ترتيب pgvector ونتائج مؤهلة بتصاريح مشاهدة | اقتراحات غير معايرة متاحة؛ جودة المطابقة ورفض عدم التطابق غير معتمدين |
| DSS والإدارة | إحصاءات من قاعدة البيانات، فلاتر ومؤشرات صيانة وأعطال متكررة وخريطة قابلة للتحديث والطباعة | اختبارات محلية للحسابات والنطاق والتواريخ؛ القبول البصري والمنشور مفتوح |
| الإشعارات | حفظ إشعارات الأحداث وعرض المقروء وغير المقروء وتحديث القراءة للمستلم فقط | اختبارات محلية للملكية والأحداث وتحديث القراءة |
| الواجهة | صفحات تشغيلية عربية/إنجليزية وRTL/LTR وخطوط محلية وحالات تحميل وخطأ وفراغ وحماية المسارات | اختبارات DOM وفحوص آلية؛ المراجعة البصرية الشاملة ليست مكتملة |

راجع [مصفوفة تغطية المتطلبات الـ55](docs/REQUIREMENTS_COVERAGE_2026-11-14.md) لكل متطلب ومصدره وصفحاته وواجهات API والجداول والصلاحيات وأسماء اختبارات القبول. المصفوفة ليست شهادة اكتمال.

## حالة الذكاء الاصطناعي

| الوظيفة | المزوّد والموديل | الحالة |
|---|---|---|
| نصوص وصور الصيانة | OpenRouter — `nvidia/nemotron-3.5-super-vl:free` | التكامل موجود، مع سعر أقصى صفر و`allow_fallbacks=false`، دون تحويل تلقائي لموديل مدفوع |
| Embeddings المفقودات | Google مباشرة — `gemini-embedding-2`، بأبعاد 768 | إصدار منفصل عن المتجهات القديمة، والمفتاح والاستدعاءات على الخادم |

يوثق [تقرير Gemini](docs/GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) تجربة حقيقية محدودة بمدخلات خيالية: **20 طلبًا مكتملًا، دون أخطاء مزوّد أو مهلة في تلك التجربة؛ Top-1 وTop-5 بقيمة 6/6 للحالات الإيجابية الست**. حالتا الصورة الإيجابيتان استخدمتا صور الفهرس نفسها، فلا تثبتان المطابقة بين صور مختلفة. قبول الحالات غير المطابقة **Not Evaluated**. هذه ليست دقة عامة أو معايرة إنتاج مستقلة.

بناءً على طلب المالك، يسمح `Search:AllowUncalibratedCandidates` باقتراح حتى خمسة عناصر مؤهلة عند غياب معايرة معتمدة، مع `calibrated: false` وتحذير من احتمال ظهور عناصر غير مرتبطة. التشابه لا يثبت الملكية. تظل شروط الخصوصية والموافقات والمزوّد والحصة والفهرسة والصلاحيات سارية؛ تفعيل هذا الإعداد وحده لا يضمن عمل البحث في الإنتاج. راجع [وضع الاقتراحات وإعداد الرجوع](docs/SEARCH_CANDIDATES_MODE.md).

يُستخدم Gemini ضمن Free Tier فقط، دون تفعيل Billing أو ترقية تلقائية. المفتاح لا يفرض المجانية بذاته؛ يلزم إبقاء مشروع Google دون فوترة ومراقبة حصته. لا تُرسل بيانات أو صور طلاب حقيقية دون الموافقات المطلوبة. لا تُحفظ الأسرار في React أو Git أو السجلات.

## نتائج التحقق الموثقة

- النسخة `082fe18`: **226 اختبار Backend ناجحًا، 4 متخطاة، صفر فشل** محليًا على PostgreSQL معزول. الاختبارات الخارجية/الأدلة الاختيارية المتخطاة لا تُحسب نجاحًا.
- النسخة `ae96c51`: **96 اختبار Frontend ناجحًا** مع نجاح TypeScript وESLint وProduction Build، وCI والنشر المرتبطين أعلاه.
- فحوص منشورة موثقة للنسخة `082fe18`: `/` و`/health` أعادا 200، و`/api/v1/found` أعادت 401 دون جلسة، مع تطابق حزمة JavaScript المنشورة والبناء المحلي.
- الأدلة تخص النسخ المحددة ولا تشمل التعديلات المحلية التالية، ولا تثبت جميع الرحلات المنشورة أو مراجعة Brave البصرية.

## المتبقي قبل اعتماد الاكتمال

1. **معايرة مستقلة للبحث:** عربية وإنجليزية، صور مختلفة للغرض نفسه، نص مع صورة، أغراض متشابهة وحالات غير موجودة. معيار القبول المطلوب: استرجاع الصحيح ضمن أول خمس نتائج في 80% على الأقل من الإيجابيات، وصفر قبول خاطئ لحالات عدم المطابقة ضمن مجموعة القبول. الاقتراحات غير المعايرة لا تحقق المعيار تلقائيًا.
2. **الرحلات المنشورة كاملة:** الحسابات والصيانة والاسترداد والعهدة والتأكيدان والإشعارات والإحصاءات، بحسابات اختبار مصرح بها، مع تحقق الحفظ بعد التحديث والخروج والدخول وإعادة تشغيل التطبيق.
3. **البريد الفعلي:** استلام التفعيل والاستعادة واستخدام الروابط وصلاحيتها، وفحص الترويسات وتوثيق المرسل/الدومين حسب المزوّد. نجاح إرسال API لا يثبت Inbox؛ إعداد DNS قد يحتاج مسؤول الدومين.
4. **دليل الجامعة الحقيقي:** استكمال واعتماد الجهات والمباني والطوابق وتوجيه المسؤوليات؛ البيانات الخيالية لا تستوفي LOC-02 للإنتاج.
5. **الفحص البصري والتفاعلي:** جميع الصفحات والحالات بالعربية والإنجليزية عند 390 و768 و1440 بكسل، لوحة المفاتيح والتباين وRTL/LTR ومنع التداخل والقص. فحص Brave اليدوي مؤجل ولم يُعلن نجاحه.
6. **الاستضافة والتخزين:** تحقق مصرح به من الصور الخاصة واستمرار المهام والملفات بعد الخمول وإعادة التشغيل، ومراجعة الأخطاء والذاكرة أثناء الرحلات الفعلية.
7. **التعديلات المحلية التالية:** تحسين رسائل روابط الحساب المنتهية وعدم تطابق كلمات المرور، وإظهار استهلاك Gemini بجانب OpenRouter واختباراتهما. موجودة في مساحة العمل وغير مشمولة في PR #3؛ يلزم إنهاء مراجعتها والتحقق منها قبل نشرها.
8. **بعد النشر التالي:** انتظار CI والنشر لنفس SHA ثم التحقق من الموقع الفعلي. الدمج وحده ليس دليل نجاح النشر أو اكتمال المتطلبات.

## إعداد المشروع والتوثيق

- [Windows setup](docs/SETUP_WINDOWS.md)
- [API handoff](docs/API_HANDOFF.md) and [OpenAPI](docs/openapi.json)
- [Architecture](docs/ARCHITECTURE.md)
- [Deployment](docs/DEPLOYMENT.md)
- [Free hosting and Outlook setup](docs/HOSTING_SETUP.md)
- [Progress](docs/BACKEND_PROGRESS.md) and [acceptance evidence](docs/ACCEPTANCE.md)
- [Product requirements](docs/SMART_CAMPUS_REQUIREMENTS.md)

Configure the database and API using the setup guide. Run `npm ci`, then `dotnet run --project backend/SmartCampus.Api --urls http://127.0.0.1:5080` with Development environment enabled. In another terminal run `npm run dev -- --host 127.0.0.1`. Vite proxies API requests; no backend secret belongs in frontend variables. `scripts/publish.ps1` prepares the single-origin application without deployment.

Checks: `dotnet build backend/SmartCampus.slnx`, `dotnet test backend/SmartCampus.slnx`, `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`, `npm run test:e2e`, and `python -m unittest discover -s worker -v`.

PostgreSQL tests require `SMART_CAMPUS_TEST_DB` pointing to an isolated loopback database ending `_test`; they reset only its application schema. Without it they are skipped. Never point tests at production. Worker boundary tests use a fake model and do not establish inference quality. Search requires completed indexing and provider/privacy configuration; calibrated matching and explicitly enabled uncalibrated suggestions are distinct modes.

See [MonsterASP deployment](docs/MONSTERASP_DEPLOYMENT.md), [hosting configuration](docs/HOSTING_CONFIGURATION.md), and [security review](docs/SECURITY_REVIEW.md). The current full-stack live website is hosted on MonsterASP; historical frontend preview deployments do not establish backend acceptance.

