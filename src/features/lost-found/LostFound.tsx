import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useApp } from "../../app/context";
import { AsyncView } from "../../components/AsyncView";
import {
  ActionForm,
  Feedback,
  Field,
  Page,
  useAction,
} from "../../components/ui";
import { ImageField } from "../maintenance/Maintenance";
import { ServiceError } from "../../services/contracts";
import {
  lostFound,
  currentCustodian,
  departmentStats,
  receiving,
  type Department,
  type FoundItem,
  type Claim,
  type ClaimStatus,
} from "../../services/lostFound";

type FoundDraft = {
  description: string;
  place: string;
  date: string;
  storage: string;
  destination: string;
  office: string;
};
const foundDrafts = new Map<string, FoundDraft>();
const emptyDraft = (): FoundDraft => ({
  description: "",
  place: "",
  date: "",
  storage: "",
  destination: "",
  office: "",
});
const claimLabels: Record<ClaimStatus, [string, string]> = {
  pending: ["قيد المراجعة", "Pending"],
  approved: ["مقبول — بانتظار التسليم", "Approved — awaiting delivery"],
  rejected: ["مرفوض", "Rejected"],
  delivered: ["تم التسليم", "Delivered"],
  closed: ["أُغلق", "Closed"],
};
function isCustodian(
  item: FoundItem,
  actor: { id: string; role: string; department?: string },
) {
  return item.custodianDepartment
    ? ["department", "secretariat"].includes(actor.role) &&
        actor.department === item.custodianDepartment
    : item.custodianId === actor.id;
}
function Photo({ file }: { file?: File }) {
  const [preview, setPreview] = useState<{ file: File; url: string }>();
  const { t } = useApp();
  useEffect(() => {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string")
        setPreview({ file, url: reader.result });
    };
    reader.readAsDataURL(file);
    return () => {
      reader.onload = null;
      if (reader.readyState === FileReader.LOADING) reader.abort();
    };
  }, [file]);
  return file && preview?.file === file ? (
    <img
      className="item-photo"
      src={preview.url}
      alt={t("صورة توضيحية للغرض المُسجّل", "Photo of the registered item")}
    />
  ) : null;
}
function ClaimStatusBadge({ status }: { status: ClaimStatus }) {
  const { t } = useApp();
  return <span className="tag">{t(...claimLabels[status])}</span>;
}
function departmentName(id: Department, t: (ar: string, en: string) => string) {
  return id === "security"
    ? t("الأمن", "Security")
    : t("السكرتارية", "Secretariat");
}
function FoundCard({ item }: { item: FoundItem }) {
  const { t } = useApp();
  return (
    <Link className="record-card found-card" to={`/found/${item.id}`}>
      <Photo file={item.photo} />
      <div>
        <div className="record-top">
          <bdi>{item.id}</bdi>
          <span className="tag">
            {item.state === "available"
              ? t("متاح", "Available")
              : item.state === "reserved"
                ? t("محجوز", "Reserved")
                : t("تم التسليم", "Delivered")}
          </span>
        </div>
        <h3>{item.description}</h3>
        <p>
          {item.place} · <bdi>{item.date}</bdi>
        </p>
      </div>
    </Link>
  );
}
export function RegisterFound() {
  const { t, account, actionScenario } = useApp();
  const navigate = useNavigate();
  const [photo, setPhoto] = useState<File>();
  const [draft, setDraft] = useState<FoundDraft>(
    () => foundDrafts.get(account.id) ?? emptyDraft(),
  );
  function change(partial: Partial<FoundDraft>) {
    setDraft((previous) => {
      const next = { ...previous, ...partial };
      foundDrafts.set(account.id, next);
      return next;
    });
  }
  return (
    <Page
      title={t("سجّل غرضاً عثرت عليه", "Register a found item")}
      subtitle={t(
        "أضف بيانات تساعد صاحب الغرض دون إظهار بيانات هوية شخصية.",
        "Add useful details without showing personal identity information.",
      )}
    >
      <div className="panel narrow">
        <ActionForm
          label={t("تسجيل الغرض", "Register item")}
          onSubmit={async () => {
            if (draft.destination === "other")
              throw new ServiceError("invalid");
            const item = await lostFound.register(
              account,
              {
                description: draft.description,
                place: draft.place,
                date: draft.date,
                storage: draft.storage,
                photo,
                destination: draft.destination
                  ? (draft.destination as Department)
                  : undefined,
                office: draft.office,
              },
              actionScenario,
            );
            foundDrafts.delete(account.id);
            navigate(`/found/${item.id}`);
          }}
        >
          <ImageField required photo={photo} onChange={setPhoto} />
          <Field label={t("وصف الغرض", "Item description")} required>
            <textarea
              required
              name="description"
              value={draft.description}
              onChange={(e) => change({ description: e.target.value })}
            />
          </Field>
          <Field
            label={t("مكان العثور", "Place found")}
            required
            hint={t(
              "يمكن أن يكون المكان خارج المبنى.",
              "Outdoor locations are accepted.",
            )}
          >
            <input
              required
              name="place"
              value={draft.place}
              onChange={(e) => change({ place: e.target.value })}
            />
          </Field>
          <Field label={t("تاريخ العثور", "Date found")} required>
            <input
              required
              name="date"
              type="date"
              value={draft.date}
              onChange={(e) => change({ date: e.target.value })}
            />
          </Field>
          <Field label={t("مكان الحفظ الحالي", "Current storage")} required>
            <input
              required
              name="storage"
              value={draft.storage}
              onChange={(e) => change({ storage: e.target.value })}
            />
          </Field>
          <Field
            label={t("جهة التسليم المقترحة", "Proposed receiving department")}
          >
            <select
              value={draft.destination}
              onChange={(e) =>
                change({ destination: e.target.value, office: "" })
              }
            >
              <option value="">{t("معي حالياً", "With me for now")}</option>
              <option value="security">{t("الأمن", "Security")}</option>
              <option value="secretariat">
                {t("السكرتارية", "Secretariat")}
              </option>
              <option value="other">
                {t("جهة مسجلة أخرى", "Other registered department")}
              </option>
            </select>
          </Field>
          {draft.destination === "other" && (
            <Feedback>
              {t(
                "الجهات المسجلة الأخرى غير متاحة حتى يتم توفير الدليل.",
                "Other registered departments are unavailable until the directory is supplied.",
              )}
            </Feedback>
          )}
          {draft.destination && draft.destination !== "other" && (
            <>
              <Field
                label={t(
                  "مكتب أو موقع التسليم",
                  "Receiving office or location",
                )}
                required
              >
                <input
                  required
                  name="office"
                  value={draft.office}
                  onChange={(e) => change({ office: e.target.value })}
                />
              </Field>
              <Feedback>
                {t(
                  "يبقى الغرض في عهدتك حتى تؤكد الجهة الاستلام الفعلي.",
                  "You remain custodian until the department confirms physical receipt.",
                )}
              </Feedback>
            </>
          )}
          <Feedback>
            {t(
              "لا تضع بيانات بطاقات الهوية الشخصية في الصورة أو الوصف.",
              "Do not expose personal identity-card details in photos or descriptions.",
            )}
          </Feedback>
        </ActionForm>
      </div>
    </Page>
  );
}
export function SearchItems() {
  const { t, account, scenario } = useApp();
  const [items, setItems] = useState<FoundItem[] | null>(null);
  const [photo, setPhoto] = useState<File>();
  return (
    <Page
      title={t("البحث عن مفقوداتك", "Search for a lost item")}
      subtitle={t(
        "اكتب وصفاً يميز غرضك. نتائج البحث خاصة بك.",
        "Describe your item. Search results are private.",
      )}
    >
      <div className="panel narrow">
        <ActionForm
          label={t("البحث عن تطابق", "Search for matches")}
          onSubmit={async (data) => {
            setItems(null);
            const result = await lostFound.search(
              account,
              String(data.get("description")),
              scenario,
            );
            setItems(result);
          }}
        >
          <Field
            label={t("وصف الغرض المفقود", "Lost item description")}
            required
          >
            <textarea required name="description" />
          </Field>
          <ImageField
            photo={photo}
            onChange={setPhoto}
            label={t("صورة سابقة — اختيارية", "Previous photo — optional")}
          />
          <Feedback>
            {t(
              "في التجربة، البحث يطابق النص فقط ولا يحلل الصورة. النتائج المحتملة لا تثبت الملكية.",
              "In the demo, search matches text only and does not analyze the image. A potential match does not prove ownership.",
            )}
          </Feedback>
        </ActionForm>
      </div>
      {items !== null &&
        (items.length ? (
          <section>
            <h2>{t("تطابقات محتملة", "Potential matches")}</h2>
            <div className="list">
              {items.map((item) => (
                <FoundCard key={item.id} item={item} />
              ))}
            </div>
          </section>
        ) : (
          <Feedback>
            {t(
              "لم نجد تطابقاً مناسباً. عدّل الوصف وحاول مجدداً.",
              "No suitable matches. Revise the description and try again.",
            )}
          </Feedback>
        ))}
    </Page>
  );
}
export function FoundList({
  department = false,
  all = false,
}: {
  department?: boolean;
  all?: boolean;
}) {
  const { t, account, scenario } = useApp();
  const load = useCallback(async () => {
    const [items, claims] = await Promise.all([
      lostFound.itemsFor(account, scenario),
      department ? lostFound.claims(account, scenario) : Promise.resolve([]),
    ]);
    return { items, claims };
  }, [account, scenario, department]);
  return (
    <Page
      title={
        all
          ? t("جميع المعثورات", "All found items")
          : department
            ? t("عهدة المعثورات الواردة", "Department found items")
            : t("المعثورات الخاصة بي", "My found items")
      }
    >
      <div className="actions">
        <Link className="button primary" to="/found/new">
          {t("سجّل غرضاً", "Register an item")}
        </Link>
        <Link className="button" to="/claims">
          {t("طلبات الاسترداد", "Recovery requests")}
        </Link>
      </div>
      <div className="spacer" />
      <AsyncView
        key={`${account.id}-${scenario}-${department}-${all}`}
        load={load}
      >
        {({ items, claims }) => {
          const counts = departmentStats(items, claims, account);
          const visible = all
            ? items
            : department
              ? items.filter(
                  (item) =>
                    currentCustodian(item, account) || receiving(item, account),
                )
              : items.filter(
                  (item) =>
                    item.uploaderId === account.id ||
                    (item.state !== "delivered" &&
                      currentCustodian(item, account)),
                );
          return (
            <>
              {department && (
                <div className="stats">
                  <div className="stat">
                    <span>{t("الأغراض في العهدة", "Items in custody")}</span>
                    <strong>{counts.held}</strong>
                  </div>
                  <div className="stat">
                    <span>{t("بانتظار الاستلام", "Pending receipt")}</span>
                    <strong>{counts.incoming}</strong>
                  </div>
                  <div className="stat">
                    <span>
                      {t(
                        "طلبات الاسترداد المعلقة",
                        "Pending recovery requests",
                      )}
                    </span>
                    <strong>{counts.pendingClaims}</strong>
                  </div>
                </div>
              )}
              {visible.length ? (
                <div className="list">
                  {visible.map((item) => (
                    <FoundCard key={item.id} item={item} />
                  ))}
                </div>
              ) : (
                <Feedback>
                  {t(
                    "لا توجد أغراض في هذه القائمة.",
                    "There are no items in this list.",
                  )}
                </Feedback>
              )}
            </>
          );
        }}
      </AsyncView>
    </Page>
  );
}
function HandoverPanel({
  item,
  changed,
}: {
  item: FoundItem;
  changed: () => void;
}) {
  const { t, account, actionScenario } = useApp();
  const owner = isCustodian(item, account);
  const recipient =
    ["department", "secretariat"].includes(account.role) &&
    account.department === item.handover?.department;
  const pending = item.handover?.status === "pending";
  return (
    <div className="panel">
      <h2>{t("عهدة الغرض", "Item custody")}</h2>
      <p>
        {item.custodianDepartment
          ? `${item.state === "delivered" ? t("الجهة السابقة", "Previous department") : t("الجهة الحالية", "Current department")}: ${departmentName(item.custodianDepartment, t)}`
          : item.state === "delivered"
            ? t(
                "كان الغرض في عهدة الحائز السابق.",
                "The item was held by its previous custodian.",
              )
            : t(
                "الغرض مع الحائز الحالي.",
                "The item is with its current custodian.",
              )}
      </p>
      {item.handover && (
        <Feedback>
          {t("حالة التسليم", "Handover status")}:{" "}
          {
            {
              pending: t("بانتظار الاستلام", "Pending receipt"),
              confirmed: t("تم الاستلام", "Receipt confirmed"),
              "not-received": t("لم يصل الغرض", "Not received"),
              cancelled: t("ملغى", "Cancelled"),
            }[item.handover.status]
          }{" "}
          · {departmentName(item.handover.department, t)} ·{" "}
          {item.handover.office}
        </Feedback>
      )}
      {owner && pending && (
        <ActionForm
          label={t("إلغاء طلب التسليم", "Cancel handover")}
          onSubmit={async () => {
            await lostFound.cancelHandover(
              account,
              item.id,
              item.version,
              actionScenario,
            );
            changed();
          }}
        >
          <p>
            {t(
              "تبقى مسؤولاً عن الغرض حتى يؤكد المستلم.",
              "You remain responsible until receipt is confirmed.",
            )}
          </p>
        </ActionForm>
      )}
      {recipient && pending && (
        <div className="actions">
          <ActionForm
            label={t("تأكيد الاستلام الفعلي", "Confirm physical receipt")}
            onSubmit={async () => {
              await lostFound.confirmHandover(
                account,
                item.id,
                item.version,
                actionScenario,
              );
              changed();
            }}
          >
            <p>
              {t(
                "أكد فقط بعد استلام الغرض فعلياً.",
                "Confirm only after receiving the item.",
              )}
            </p>
          </ActionForm>
          <ActionForm
            label={t("لم نستلم الغرض", "Item not received")}
            onSubmit={async () => {
              await lostFound.notReceived(
                account,
                item.id,
                item.version,
                actionScenario,
              );
              changed();
            }}
          >
            <span className="sr-only">
              {t("إبلاغ عن عدم الاستلام", "Report non-receipt")}
            </span>
          </ActionForm>
        </div>
      )}
      {owner && !pending && item.state === "available" && (
        <ActionForm
          label={t("طلب تسليم لجهة", "Request departmental handover")}
          onSubmit={async (data) => {
            await lostFound.requestHandover(
              account,
              item.id,
              String(data.get("department")) as Department,
              String(data.get("office")),
              item.version,
              actionScenario,
            );
            changed();
          }}
        >
          <Field label={t("الجهة المستلمة", "Receiving department")} required>
            <select name="department" required>
              <option value="security">{t("الأمن", "Security")}</option>
              <option value="secretariat">
                {t("السكرتارية", "Secretariat")}
              </option>
            </select>
          </Field>
          <Field label={t("المكتب أو الموقع", "Office or location")} required>
            <input required name="office" />
          </Field>
        </ActionForm>
      )}
    </div>
  );
}
function ItemDetail({
  item,
  changed,
}: {
  item: FoundItem;
  changed: () => void;
}) {
  const { t, account, actionScenario } = useApp();
  const navigate = useNavigate();
  const owner = isCustodian(item, account);
  return (
    <>
      <div className="panel">
        <div className="record-top">
          <bdi>{item.id}</bdi>
          <span className="tag">
            {item.state === "available"
              ? t("متاح", "Available")
              : item.state === "reserved"
                ? t("محجوز", "Reserved")
                : t("تم التسليم", "Delivered")}
          </span>
        </div>
        <Photo file={item.photo} />
        <h2>{item.description}</h2>
        <dl className="details-grid">
          <div>
            <dt>{t("مكان العثور", "Place found")}</dt>
            <dd>{item.place}</dd>
          </div>
          <div>
            <dt>{t("تاريخ العثور", "Date found")}</dt>
            <dd>
              <bdi>{item.date}</bdi>
            </dd>
          </div>
          <div>
            <dt>
              {item.state === "delivered"
                ? t("الحائز السابق", "Previous custodian")
                : t("الحائز الحالي", "Current custodian")}
            </dt>
            <dd>
              {item.custodianDepartment
                ? `${item.state === "delivered" ? t("الحائز السابق", "Previous custodian") : t("الحائز الحالي", "Current custodian")}: ${departmentName(item.custodianDepartment, t)}`
                : item.storage}
            </dd>
            {item.custodianDepartment && item.custodianOffice && (
              <dd>{item.custodianOffice}</dd>
            )}
          </div>
        </dl>
      </div>
      {(owner ||
        item.uploaderId === account.id ||
        item.handover?.department === account.department) && (
        <HandoverPanel item={item} changed={changed} />
      )}
      {item.canClaim && (
        <div className="panel">
          <h2>{t("طلب استرداد الغرض", "Request recovery")}</h2>
          <Feedback>
            {t(
              "التطابق محتمل ولا يعني إثبات الملكية. يراجع الطلب الحائز الحالي.",
              "A potential match does not prove ownership. The current custodian reviews your request.",
            )}
          </Feedback>
          <ActionForm
            label={t("إرسال طلب الاسترداد", "Send recovery request")}
            onSubmit={async (data) => {
              const claim = await lostFound.claim(
                account,
                item.id,
                String(data.get("message")),
                actionScenario,
              );
              navigate(`/claims/${claim.id}`);
            }}
          >
            <Field
              label={t(
                "رسالة إضافية — اختيارية",
                "Additional message — optional",
              )}
            >
              <textarea name="message" />
            </Field>
          </ActionForm>
        </div>
      )}
      {owner && (
        <Link className="button" to="/claims">
          {t("مراجعة طلبات الاسترداد", "Review recovery requests")}
        </Link>
      )}
    </>
  );
}
export function FoundDetail() {
  const { id = "" } = useParams();
  const { t, account, scenario } = useApp();
  const [revision, setRevision] = useState(0);
  const load = useCallback(
    () => lostFound.detail(account, id, scenario),
    [account, id, scenario],
  );
  return (
    <Page title={t("تفاصيل الغرض", "Item details")}>
      <AsyncView
        key={`${id}-${account.id}-${scenario}-${revision}`}
        load={load}
      >
        {(item) => (
          <ItemDetail item={item} changed={() => setRevision((v) => v + 1)} />
        )}
      </AsyncView>
      <button className="button" onClick={() => setRevision((v) => v + 1)}>
        {t("تحديث بيانات الغرض", "Refresh current item")}
      </button>
      <Link className="text-link" to="/found">
        {t("المعثورات الخاصة بي", "My found items")}
      </Link>
    </Page>
  );
}
export function ClaimsList() {
  const { t, account, scenario } = useApp();
  const load = useCallback(
    () => lostFound.claims(account, scenario),
    [account, scenario],
  );
  return (
    <Page title={t("طلبات الاسترداد", "Recovery requests")}>
      <AsyncView key={`${account.id}-${scenario}`} load={load}>
        {(claims) =>
          claims.length ? (
            <div className="list">
              {claims.map((claim) => (
                <Link
                  className="record-card"
                  to={`/claims/${claim.id}`}
                  key={claim.id}
                >
                  <div className="record-top">
                    <bdi>
                      {claim.id} · {claim.itemId}
                    </bdi>
                    <ClaimStatusBadge status={claim.status} />
                  </div>
                  <h3>{claim.claimantName}</h3>
                </Link>
              ))}
            </div>
          ) : (
            <Feedback>
              {t("لا توجد طلبات استرداد.", "There are no recovery requests.")}
            </Feedback>
          )
        }
      </AsyncView>
    </Page>
  );
}
function ClaimView({
  claim,
  item,
  changed,
}: {
  claim: Claim;
  item: FoundItem;
  changed: () => void;
}) {
  const { t, account, actionScenario } = useApp();
  const { pending, message, run } = useAction();
  const [confirm, setConfirm] = useState(false);
  const custodian = isCustodian(item, account);
  const claimant = claim.claimantId === account.id;
  return (
    <>
      <div className="panel">
        <div className="record-top">
          <bdi>
            {claim.id} · {item.id}
          </bdi>
          <ClaimStatusBadge status={claim.status} />
        </div>
        <Photo file={item.photo} />
        <h2>{item.description}</h2>
        <p>
          {t("صاحب الطلب", "Requester")}: <bdi>{claim.claimantName}</bdi>
        </p>
        {claim.message && <p>{claim.message}</p>}
        {claim.status === "approved" || claim.status === "delivered" ? (
          <>
            <h3>{t("معلومات الاستلام", "Collection information")}</h3>
            {claim.phone && (
              <p>
                <bdi>{claim.phone}</bdi>
              </p>
            )}
            {claim.place && <p>{claim.place}</p>}
            {claim.instructions && <p>{claim.instructions}</p>}
          </>
        ) : null}
      </div>
      {(claim.canApprove || claim.canReject) && (
        <div className="panel">
          <h2>{t("مراجعة الطلب", "Review request")}</h2>
          {claim.canApprove && (
            <ActionForm
              label={t("الموافقة على الطلب", "Approve request")}
              onSubmit={async (data) => {
                await lostFound.approve(
                  account,
                  claim.id,
                  {
                    phone: String(data.get("phone") ?? ""),
                    place: String(data.get("place") ?? ""),
                    instructions: String(data.get("instructions") ?? ""),
                  },
                  actionScenario,
                );
                changed();
              }}
            >
              {item.custodianDepartment ? (
                <>
                  <Field
                    label={t("مكان الاستلام", "Collection place")}
                    required
                  >
                    <input name="place" required />
                  </Field>
                  <Field
                    label={t("تعليمات الاستلام", "Collection instructions")}
                    required
                  >
                    <textarea name="instructions" required />
                  </Field>
                </>
              ) : (
                <Field label={t("هاتف للتواصل", "Contact phone")} required>
                  <input dir="ltr" type="tel" name="phone" required />
                </Field>
              )}
            </ActionForm>
          )}
          {claim.canReject && (
            <ActionForm
              label={t("رفض الطلب", "Reject request")}
              onSubmit={async () => {
                await lostFound.reject(account, claim.id, actionScenario);
                changed();
              }}
            >
              <span className="sr-only">
                {t("رفض الطلب", "Reject request")}
              </span>
            </ActionForm>
          )}
        </div>
      )}
      {(claim.status === "approved" || claim.status === "delivered") &&
        (custodian || claimant) && (
          <div className="panel">
            <h2>{t("تأكيد التسليم والاستلام", "Delivery confirmations")}</h2>
            <p>
              {claim.custodianConfirmed
                ? t("أكد الحائز التسليم.", "Custodian confirmed delivery.")
                : t(
                    "بانتظار تأكيد الحائز.",
                    "Waiting for custodian confirmation.",
                  )}
            </p>
            <p>
              {claim.claimantConfirmed
                ? t("أكد المستلم الاستلام.", "Requester confirmed receipt.")
                : t(
                    "بانتظار تأكيد المستلم.",
                    "Waiting for requester confirmation.",
                  )}
            </p>
            {claim.status === "approved" &&
              ((custodian && !claim.custodianConfirmed) ||
                (claimant && !claim.claimantConfirmed)) && (
                <>
                  <button
                    className="button primary"
                    onClick={() => setConfirm(true)}
                  >
                    {custodian
                      ? t("لقد سلّمت الغرض", "I delivered the item")
                      : t("لقد استلمت الغرض", "I received the item")}
                  </button>
                  {confirm && (
                    <div className="confirmation">
                      <p>{t("هل تؤكد هذا الإجراء؟", "Confirm this action?")}</p>
                      {message && <Feedback kind="error">{message}</Feedback>}
                      <div className="actions">
                        <button
                          className="button primary"
                          disabled={pending}
                          onClick={() =>
                            void run(async () => {
                              await lostFound.confirmDelivery(
                                account,
                                claim.id,
                                actionScenario,
                              );
                              setConfirm(false);
                              changed();
                            })
                          }
                        >
                          {t("تأكيد", "Confirm")}
                        </button>
                        <button
                          className="button"
                          onClick={() => setConfirm(false)}
                        >
                          {t("تراجع", "Go back")}
                        </button>
                      </div>
                    </div>
                  )}
                </>
              )}
          </div>
        )}
    </>
  );
}
export function ClaimDetail() {
  const { id = "" } = useParams();
  const { t, account, scenario } = useApp();
  const [revision, setRevision] = useState(0);
  const load = useCallback(
    () => lostFound.claimDetail(account, id, scenario),
    [account, id, scenario],
  );
  return (
    <Page title={t("تفاصيل طلب الاسترداد", "Recovery request details")}>
      <AsyncView
        key={`${id}-${account.id}-${scenario}-${revision}`}
        load={load}
      >
        {(result) => (
          <ClaimView {...result} changed={() => setRevision((v) => v + 1)} />
        )}
      </AsyncView>
      <button className="button" onClick={() => setRevision((v) => v + 1)}>
        {t("تحديث الطلب", "Refresh request")}
      </button>
      <Link className="text-link" to="/claims">
        {t("كل طلبات الاسترداد", "All recovery requests")}
      </Link>
    </Page>
  );
}
