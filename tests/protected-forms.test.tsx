import { useEffect, type ReactNode } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { AppProvider, useApp } from "../src/app/context";
import { AdminAccounts, AdminOverview } from "../src/features/admin/Admin";
import { ClaimDetail, FoundDetail, RegisterFound, SearchItems } from "../src/features/lost-found/LostFound";
import { Notifications } from "../src/features/account/Account";
import { MaintenanceDetail, NewMaintenance } from "../src/features/maintenance/Maintenance";
import { directory } from "../src/services/admin";
import * as adminServices from "../src/services/admin";
import { OperationsStatus } from "../src/features/admin/OperationsStatus";
import { ServiceError, type Account } from "../src/services/contracts";
import { lostFound, type Claim, type FoundItem } from "../src/services/lostFound";
import { maintenance, type MaintenanceReport } from "../src/services/maintenance";
import { accountService } from "../src/services/frontend";
import * as http from "../src/services/http";

beforeEach(() => vi.spyOn(http, "departments").mockResolvedValue([
  { id: "test-department", code: "security", ar: "أمن الاختبار", en: "Security", canReceive: true, maintenance: false }
]));

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.restoreAllMocks();
});

function TestSession({ actor, children }: { actor: Account; children: ReactNode }) {
  const { account, updateAccount } = useApp();
  useEffect(() => {
    if (account.id !== actor.id) updateAccount(actor);
  }, [account.id, actor, updateAccount]);
  return account.id === actor.id ? children : null;
}
function renderForActor(actor: Account, children: ReactNode) {
  localStorage.setItem("smart-campus-language", "en");
  return render(<MemoryRouter><AppProvider><TestSession actor={actor}>{children}</TestSession></AppProvider></MemoryRouter>);
}

it("does not expose a found item's custody location to a potential claimant", async () => {
  const actor: Account = { id: "potential-claimant", name: "Member", email: "", role: "user" };
  vi.spyOn(lostFound, "detail").mockResolvedValue({ id: "item-1", description: "Blue bottle", place: "Main hall", date: "2026-11-13",
    storage: null, uploaderId: "finder", custodianId: "custodian", state: "available", createdAt: "2026-11-13T00:00:00Z",
    version: 1, canClaim: true, handover: { department: "security", office: "PRIVATE receiving office", status: "pending" } });
  localStorage.setItem("smart-campus-language", "en");
  render(<MemoryRouter initialEntries={["/found/item-1"]}><AppProvider><TestSession actor={actor}><Routes>
    <Route path="/found/:id" element={<FoundDetail />} />
  </Routes></TestSession></AppProvider></MemoryRouter>);
  expect(await screen.findByText("Blue bottle")).toBeTruthy();
  expect(screen.queryByText("Current custodian")).toBeNull();
  expect(screen.queryByText("PRIVATE receiving office")).toBeNull();
});

it("shows permitted custody details to an administrator granted as an additional role", async () => {
  const actor: Account = { id: "admin-extra", name: "Administrator", email: "", role: "user", roles: ["admin"] };
  vi.spyOn(lostFound, "detail").mockResolvedValue({ id: "item-1", description: "Blue bottle", place: "Main hall", date: "2026-11-13",
    storage: "ADMIN-ONLY STORAGE", uploaderId: "finder", custodianId: "custodian", state: "available", createdAt: "2026-11-13T00:00:00Z",
    version: 1, canClaim: false });
  render(<MemoryRouter initialEntries={["/found/item-1"]}><AppProvider><TestSession actor={actor}><Routes>
    <Route path="/found/:id" element={<FoundDetail />} />
  </Routes></TestSession></AppProvider></MemoryRouter>);
  expect(await screen.findByText("ADMIN-ONLY STORAGE")).toBeTruthy();
});

it("marks an owned notification read and refreshes its state", async () => {
  const actor: Account = { id: "notification-owner", name: "Member", email: "", role: "user" };
  let notices = [{ id: "notice-1", accountId: actor.id, ar: "تم إرسال البلاغ", en: "Report submitted", to: "/requests", read: false }];
  const load = vi.spyOn(accountService, "notifications").mockImplementation(async () => notices.map(notice => ({ ...notice })));
  const markRead = vi.spyOn(accountService, "markRead").mockImplementation(async (_account, id) => {
    notices = notices.map(notice => notice.id === id ? { ...notice, read: true } : notice);
  });
  renderForActor(actor, <Notifications />);
  await userEvent.click(await screen.findByRole("button", { name: "Mark as read" }));
  expect(markRead).toHaveBeenCalledWith(actor, "notice-1");
  expect(await screen.findByText("Read")).toBeTruthy();
  expect(load).toHaveBeenCalledTimes(2);
});

it("renders API operational counts and truthful worker unavailability", async () => {
  vi.spyOn(adminServices, "operations").mockResolvedValue({ sampledAt: "2026-11-09T12:00:00Z", deployment: "test-only",
    workingSetBytes: 64 * 1024 * 1024, worker: "unavailable", calibrationApproved: false,
    ai: { provider: "openrouter", textConfigured: false, imagesConfigured: false, taxonomyConfigured: true,
      textTransferEnabled: false, imageTransferEnabled: false, providerDataCollectionApproved: false },
    quotas: [{ provider: "openrouter", used: 7, dailyLimit: 50 }],
    jobs: [{ kind: "maintenance", state: "pending", count: 2, maximumAttempts: 1 }] });
  renderForActor({ id: "admin-test", name: "Administrator", email: "", role: "admin" }, <OperationsStatus />);
  expect(await screen.findByText(/Matching service: Unavailable/)).toBeTruthy();
  expect(screen.getByText(/Calibration approval: Not approved/)).toBeTruthy();
  expect(screen.getByText(/Provider data-collection policy approval: Denied by default/)).toBeTruthy();
  expect(screen.getByText(/Maintenance processing/).closest("li")?.textContent).toContain("2");
  expect(screen.getByText(/7 \/ 50/)).toBeTruthy();
});

it("shows when the provider collection policy is explicitly approved", async () => {
  vi.spyOn(adminServices, "operations").mockResolvedValue({ sampledAt: "2026-11-09T12:00:00Z", deployment: "test-only",
    workingSetBytes: 64 * 1024 * 1024, worker: "hosted_blocked", calibrationApproved: false,
    ai: { provider: "openrouter", textConfigured: true, imagesConfigured: false, taxonomyConfigured: true,
      textTransferEnabled: true, imageTransferEnabled: false, providerDataCollectionApproved: true },
    quotas: [], jobs: [] });
  renderForActor({ id: "admin-test", name: "Administrator", email: "", role: "admin" }, <OperationsStatus />);
  expect(await screen.findByText(/Provider data-collection policy approval: Allowed only for approved non-sensitive inputs/)).toBeTruthy();
});

it("does not infer provider collection approval from an older operations response", async () => {
  vi.spyOn(adminServices, "operations").mockResolvedValue({ sampledAt: "2026-11-09T12:00:00Z", deployment: "test-only",
    workingSetBytes: 64 * 1024 * 1024, worker: "unavailable", calibrationApproved: false,
    ai: { provider: "openrouter", textConfigured: false, imagesConfigured: false, taxonomyConfigured: true,
      textTransferEnabled: false, imageTransferEnabled: false }, quotas: [], jobs: [] });
  renderForActor({ id: "admin-test", name: "Administrator", email: "", role: "admin" }, <OperationsStatus />);
  expect(await screen.findByText(/Provider data-collection policy approval: Approval status unavailable/)).toBeTruthy();
});

it("shows the admin map editor to an account with an additional administrator grant", async () => {
  vi.spyOn(adminServices, "statistics").mockResolvedValue({
    totalReports: 0, byStatus: { submitted: 0, review: 0, repair: 0, resolved: 0, rejected: 0, cancelled: 0 },
    availableItems: 0, pendingClaims: 0, locations: {}, faultTypes: {},
    buildings: [{ id: "building-1", ar: "مبنى", en: "Building", count: 0, x: null, y: null, version: 1 }],
    unplacedBuildings: [{ id: "building-1", ar: "مبنى", en: "Building", count: 0, x: null, y: null, version: 1 }],
  });
  vi.spyOn(adminServices, "operations").mockResolvedValue({ sampledAt: "2026-11-13T12:00:00Z", deployment: "test-only",
    workingSetBytes: 64 * 1024 * 1024, worker: "hosted_blocked", calibrationApproved: false, quotas: [], jobs: [] });
  const actor: Account = { id: "admin-extra", name: "Administrator", email: "", role: "user", roles: ["admin"] };
  renderForActor(actor, <AdminOverview />);
  expect(await screen.findByRole("button", { name: "Set building position on the schematic" })).toBeTruthy();
});

it("requires a selected account before requesting a permission change", async () => {
  const actor: Account = { id: "admin-test", name: "Administrator", email: "", role: "admin" };
  const target: Account = { id: "target-test", name: "Member", email: "", role: "user" };
  vi.spyOn(directory, "list").mockResolvedValue([{ key: "target-test", account: target }]);
  const assign = vi.spyOn(directory, "assign");
  renderForActor(actor, <AdminAccounts />);
  const account = await screen.findByLabelText(/^Account/);
  const role = screen.getByLabelText(/^Role/);
  await userEvent.selectOptions(role, "user");
  await userEvent.click(screen.getByRole("button", { name: "Save permissions" }));
  expect(assign).not.toHaveBeenCalled();
  await userEvent.selectOptions(account, "target-test");
  await userEvent.selectOptions(role, "user");
  await userEvent.click(screen.getByRole("button", { name: "Save permissions" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
  expect(assign).toHaveBeenCalledWith(actor, { key: "target-test", role: "user", scope: "", confirmAdmin: false });
});

it("validates a maintenance draft and retains it when submission is unavailable", async () => {
  const actor: Account = { id: "maintenance-form-test", name: "Member", email: "", role: "user" };
  vi.spyOn(maintenance, "locations").mockResolvedValue([{ id: "f", ar: "مرفق", en: "Facility", buildings: [{ id: "b", ar: "مبنى", en: "Building", floors: [{ id: "0", ar: "الأرضي", en: "Ground" }] }] }]);
  const create = vi.spyOn(maintenance, "create");
  renderForActor(actor, <NewMaintenance />);
  const description = await screen.findByLabelText(/^Fault description/) as HTMLTextAreaElement;
  const preciseLocation = screen.getByLabelText(/^Precise location/) as HTMLInputElement;
  expect(description.maxLength).toBe(4000);
  expect(preciseLocation.maxLength).toBe(1000);
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect(create).not.toHaveBeenCalled();
  await userEvent.type(description, "   ");
  await userEvent.selectOptions(screen.getByLabelText(/^Faculty or facility/), "f");
  await userEvent.selectOptions(screen.getByLabelText(/^Building/), "b");
  await userEvent.selectOptions(screen.getByLabelText(/^Floor/), "0");
  await userEvent.type(preciseLocation, "Room 1");
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(create).not.toHaveBeenCalled();
  await userEvent.clear(description);
  await userEvent.type(description, "Leaking tap");
  const location = preciseLocation;
  await userEvent.clear(location);
  await userEvent.type(location, "   ");
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(create).not.toHaveBeenCalled();
  await userEvent.clear(location);
  await userEvent.type(location, "Room 1");
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
  expect((description as HTMLTextAreaElement).value).toBe("Leaking tap");
  expect(create).toHaveBeenCalledOnce();
});

it("requires a photo and nonfuture date and keeps a found-item draft on failure", async () => {
  const actor: Account = { id: "found-form-test", name: "Member", email: "", role: "user" };
  const register = vi.spyOn(lostFound, "register");
  renderForActor(actor, <RegisterFound />);
  const photo = await screen.findByLabelText(/^Item photo/);
  const description = screen.getByLabelText(/^Item description/) as HTMLTextAreaElement;
  const place = screen.getByLabelText(/^Place found/) as HTMLInputElement;
  const storage = screen.getByLabelText(/^Current storage/) as HTMLInputElement;
  expect(description.maxLength).toBe(4000);
  expect(place.maxLength).toBe(1000);
  expect(storage.maxLength).toBe(1000);
  await userEvent.type(description, "Blue watch");
  await userEvent.type(place, "Courtyard");
  await userEvent.type(storage, "Desk");
  const date = screen.getByLabelText(/^Date found/) as HTMLInputElement;
  await userEvent.type(date, "2999-01-01");
  expect(date.validity.rangeOverflow).toBe(true);
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect(register).not.toHaveBeenCalled();
  await userEvent.clear(date);
  await userEvent.type(date, "2025-01-01");
  await userEvent.upload(photo, new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])], "photo.png", { type: "image/png" }));
  await screen.findByRole("button", { name: "Remove photo" });
  for (const [field, original] of [
    [description, "Blue watch"],
    [place, "Courtyard"],
    [storage, "Desk"],
  ] as const) {
    await userEvent.clear(field);
    await userEvent.type(field, "   ");
    await userEvent.click(screen.getByRole("button", { name: "Register item" }));
    expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
    expect(register).not.toHaveBeenCalled();
    await userEvent.clear(field);
    await userEvent.type(field, original);
  }
  const destination = screen.getByLabelText(/^Proposed receiving department/);
  await screen.findByRole("option", { name: "Security" });
  await userEvent.selectOptions(destination, "security");
  const receivingOffice = screen.getByLabelText(/^Receiving office or location/) as HTMLInputElement;
  expect(receivingOffice.maxLength).toBe(1000);
  await userEvent.type(receivingOffice, "   ");
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(register).not.toHaveBeenCalled();
  await userEvent.selectOptions(destination, "");
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
  expect((description as HTMLTextAreaElement).value).toBe("Blue watch");
  expect(register).toHaveBeenCalledOnce();
}, 15_000);

it("submits a private text and image search and only renders returned matches", async () => {
  const actor: Account = { id: "search-form-test", name: "Member", email: "", role: "user" };
  const item: FoundItem = { id: "match-1", description: "Blue thermos with a star sticker", place: "Library", date: "2026-11-01", storage: "", uploaderId: "finder", custodianId: "finder", state: "available", createdAt: "2026-11-01T00:00:00Z", version: 1 };
  const search = vi.spyOn(lostFound, "search").mockResolvedValue([item]);
  renderForActor(actor, <SearchItems />);
  expect(screen.queryByText(/Blue thermos with a star sticker/)).toBeNull();
  await userEvent.type(await screen.findByLabelText(/^Description of the lost item/), "Blue thermos, star sticker");
  const image = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])], "thermos.png", { type: "image/png" });
  await userEvent.upload(screen.getByLabelText(/^Previous photo of the item/), image);
  await screen.findByRole("button", { name: "Remove photo" });
  await userEvent.click(screen.getByRole("button", { name: /^Search$/ }));
  expect((await screen.findByRole("link", { name: /Blue thermos with a star sticker/ })).getAttribute("href")).toBe("/found/match-1");
  expect(search).toHaveBeenCalledWith(actor, "Blue thermos, star sticker", image);
});

it("submits an image-only search without requiring or inventing a description", async () => {
  const actor: Account = { id: "image-only-search", name: "Member", email: "", role: "user" };
  const search = vi.spyOn(lostFound, "search").mockResolvedValue([]);
  renderForActor(actor, <SearchItems />);
  const description = await screen.findByLabelText(/^Description of the lost item/) as HTMLTextAreaElement;
  expect(description.required).toBe(true);
  const image = new File([new Uint8Array([0xff, 0xd8, 0xff])], "bottle.jpg", { type: "image/jpeg" });
  await userEvent.upload(screen.getByLabelText(/^Previous photo of the item/), image);
  await screen.findByRole("button", { name: "Remove photo" });
  expect(description.required).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: /^Search$/ }));
  await screen.findByText(/No qualifying results appeared/);
  expect(search).toHaveBeenCalledExactlyOnceWith(actor, "", image);
  await userEvent.click(screen.getByRole("button", { name: "Remove photo" }));
  expect(description.required).toBe(true);
});

it("rejects a blank search before sending an API request", async () => {
  const actor: Account = { id: "blank-search", name: "Member", email: "", role: "user" };
  const search = vi.spyOn(lostFound, "search");
  renderForActor(actor, <SearchItems />);
  await userEvent.type(await screen.findByLabelText(/^Description of the lost item/), "   ");
  await userEvent.click(screen.getByRole("button", { name: /^Search$/ }));
  expect((await screen.findByRole("alert")).textContent).toContain("Enter a description or choose an item photo.");
  expect(search).not.toHaveBeenCalled();
});

it("shows service failure separately from an empty search result", async () => {
  const actor: Account = { id: "search-form-failure-test", name: "Member", email: "", role: "user" };
  const search = vi.spyOn(lostFound, "search").mockRejectedValue(new ServiceError("unavailable", "search_calibration_required"));
  renderForActor(actor, <SearchItems />);
  await userEvent.type(await screen.findByLabelText(/^Description of the lost item/), "Black umbrella with a wooden handle");
  await userEvent.click(screen.getByRole("button", { name: /^Search$/ }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/quality is safely approved/i);
  expect(screen.queryByText(/No qualifying results appeared/i)).toBeNull();
  expect(search).toHaveBeenCalledOnce();
});

it("requires explicit text approval before staff can enqueue AI analysis", async () => {
  const actor: Account = { id: "ai-staff-test", name: "Staff", email: "", role: "maintenance" };
  const report: MaintenanceReport = { id: "reviewed-report", ownerId: "other", description: "Synthetic fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "submitted", version: 4, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canViewAssessment: true, canReviewAiInput: true, canReviewAiImage: false };
  vi.spyOn(maintenance, "detail").mockResolvedValueOnce(report).mockResolvedValueOnce({ ...report, version: 5, assessment: { state: "pending", type: null, priority: null, risk: null } });
  const review = vi.spyOn(maintenance, "reviewAi").mockResolvedValue(report);
  renderForActor(actor, <MaintenanceDetail />);
  const submit = await screen.findByRole("button", { name: "Send approved content for analysis" });
  const consent = screen.getByLabelText(/I confirm the description contains no personal or confidential data/);
  await userEvent.click(submit);
  expect(review).not.toHaveBeenCalled();
  await userEvent.click(consent);
  await userEvent.click(submit);
  expect(review).toHaveBeenCalledExactlyOnceWith("reviewed-report", 4, true, false);
  expect(await screen.findByText(/Analysis is queued/i)).toBeTruthy();
});

it("sends an image only after separate opt-in and displays AI suggestions apart from staff decisions", async () => {
  const actor: Account = { id: "ai-staff-image-test", name: "Staff", email: "", role: "maintenance" };
  const report: MaintenanceReport = { id: "reviewed-image-report", ownerId: "other", description: "Synthetic fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "submitted", version: 8, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canViewAssessment: true, canUpdate: true, canReviewAiInput: true, canReviewAiImage: true, decision: { type: "staff-confirmed", priority: "low", risk: "low" } };
  vi.spyOn(maintenance, "detail").mockResolvedValueOnce(report).mockResolvedValueOnce({ ...report, version: 9, assessment: { state: "completed", type: "plumbing", priority: "medium", risk: "high", explanation: "Synthetic explanation." } });
  const review = vi.spyOn(maintenance, "reviewAi").mockResolvedValue(report);
  renderForActor(actor, <MaintenanceDetail />);
  await userEvent.click(await screen.findByLabelText(/I confirm the description contains no personal or confidential data/));
  await userEvent.click(screen.getByLabelText(/Include the report photo/));
  await userEvent.click(screen.getByRole("button", { name: "Send approved content for analysis" }));
  expect(review).toHaveBeenCalledExactlyOnceWith("reviewed-image-report", 8, true, true);
  expect(await screen.findByText("AI suggestion — staff review required")).toBeTruthy();
  expect(screen.getByText("Suggested fault type").nextElementSibling?.textContent).toBe("Plumbing");
  expect(screen.getByText("Synthetic explanation.")).toBeTruthy();
  expect((screen.getByLabelText("Operational fault type") as HTMLInputElement).value).toBe("staff-confirmed");
});

it("does not offer AI submission when the server blocks external processing", async () => {
  const actor: Account = { id: "ai-staff-blocked-test", name: "Staff", email: "", role: "maintenance" };
  const report: MaintenanceReport = { id: "blocked-ai-report", ownerId: "other", description: "Synthetic fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "submitted", version: 4, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canViewAssessment: true, canReviewAiInput: false, canReviewAiImage: false };
  vi.spyOn(maintenance, "detail").mockResolvedValue(report);
  const review = vi.spyOn(maintenance, "reviewAi");
  renderForActor(actor, <MaintenanceDetail />);
  expect(await screen.findByText(/External processing is not enabled/i)).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Send approved content for analysis" })).toBeNull();
  expect(screen.queryByText("Suggested fault type")).toBeNull();
  expect(review).not.toHaveBeenCalled();
});

it("requires a nonblank rejection reason before requesting a report update", async () => {
  const actor: Account = { id: "staff-test", name: "Staff", email: "", role: "maintenance", department: "maintenance" };
  const report: MaintenanceReport = { id: "r", ownerId: "other", description: "Fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "review", version: 1, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canUpdate: true, assessment: { type: "", priority: "", risk: "" } };
  vi.spyOn(maintenance, "detail").mockResolvedValue(report);
  const update = vi.spyOn(maintenance, "update");
  renderForActor(actor, <MaintenanceDetail />);
  await userEvent.selectOptions(await screen.findByLabelText(/^Status/), "rejected");
  const note = screen.getByLabelText(/^Rejection reason/);
  expect((note as HTMLTextAreaElement).maxLength).toBe(4000);
  await userEvent.type(note, "   ");
  await userEvent.click(screen.getByRole("button", { name: "Save update" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(update).not.toHaveBeenCalled();
  await userEvent.clear(note);
  await userEvent.type(note, "Needs review");
  await userEvent.click(screen.getByRole("button", { name: "Save update" }));
  expect(await screen.findByText(/temporarily unavailable/i)).toBeTruthy();
  expect(update).toHaveBeenCalledOnce();
});

it("submits an explicit empty assignee when staff selects unassigned", async () => {
  const actor: Account = { id: "staff-unassign-test", name: "Staff", email: "", role: "maintenance", department: "maintenance" };
  const report: MaintenanceReport = { id: "r", ownerId: "other", description: "Fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "review", version: 3, createdAt: "2025-01-01T12:00:00Z", assignee: "staff-two", allowedAssignees: [{ id: "staff-two", name: "Second staff" }], history: [], canUpdate: true, assessment: { type: "", priority: "", risk: "" } };
  vi.spyOn(maintenance, "detail").mockResolvedValue(report);
  const update = vi.spyOn(maintenance, "update").mockResolvedValue(report);
  renderForActor(actor, <MaintenanceDetail />);
  await userEvent.selectOptions(await screen.findByLabelText(/^Assign to/), "");
  await userEvent.click(screen.getByRole("button", { name: "Save update" }));
  expect(update).toHaveBeenCalledWith(actor, expect.objectContaining({ id: "r", version: 3, assignee: "" }));
});

it("requires a nonblank office before requesting a handover", async () => {
  const actor: Account = { id: "holder-test", name: "Holder", email: "", role: "user" };
  const item: FoundItem & { canClaim: boolean } = { id: "item", description: "Watch", place: "Hall", date: "2025-01-01", storage: "Desk", uploaderId: actor.id, custodianId: actor.id, state: "available", createdAt: "2025-01-01T12:00:00Z", version: 1, canClaim: false };
  vi.spyOn(lostFound, "detail").mockResolvedValue(item);
  const handover = vi.spyOn(lostFound, "requestHandover");
  renderForActor(actor, <FoundDetail />);
  const office = await screen.findByLabelText(/^Office or location/);
  await screen.findByRole("option", { name: "Security" });
  await userEvent.selectOptions(screen.getByLabelText(/^Receiving department/), "security");
  await userEvent.type(office, "   ");
  await userEvent.click(screen.getByRole("button", { name: "Request departmental handover" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(handover).not.toHaveBeenCalled();
  await userEvent.clear(office);
  await userEvent.type(office, "Desk 1");
  await userEvent.click(screen.getByRole("button", { name: "Request departmental handover" }));
  expect(await screen.findByText(/temporarily unavailable/i)).toBeTruthy();
  expect(handover).toHaveBeenCalledOnce();
});

it.each([true, false])("uses server receipt permission for a staff member with multiple department scopes: %s", async (allowed) => {
  const actor: Account = { id: "scoped-receiver", name: "Receiver", email: "", role: "maintenance", roles: ["maintenance", "department"], department: "primary", departments: [{ id: "primary-id", code: "primary" }, { id: "secondary-id", code: "secondary" }] };
  const item: FoundItem = { id: "item", description: "Synthetic watch", place: "Hall", date: "2025-01-01", storage: "Desk", uploaderId: "finder", custodianId: "finder", state: "available", createdAt: "2025-01-01T12:00:00Z", version: 3, canReceive: allowed, canHandover: false, canCancelHandover: false, handover: { department: "secondary", office: "Test desk", status: "pending" } };
  vi.spyOn(lostFound, "detail").mockResolvedValue({ ...item, canClaim: false });
  const confirm = vi.spyOn(lostFound, "confirmHandover").mockRejectedValue(new ServiceError("unavailable"));
  renderForActor(actor, <FoundDetail />);
  await screen.findByText("Synthetic watch");
  if (allowed) {
    await userEvent.click(await screen.findByRole("button", { name: "Confirm physical receipt" }));
    expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
    expect(confirm).toHaveBeenCalledWith(actor, "item", 3);
  } else {
    expect(screen.queryByRole("button", { name: "Confirm physical receipt" })).toBeNull();
    expect(confirm).not.toHaveBeenCalled();
  }
});

it.each(["user", "department"] as const)("requires nonblank collection details for a %s custodian", async (role) => {
  const actor: Account = { id: `holder-${role}`, name: "Holder", email: "", role, department: role === "department" ? "security" : undefined };
  const item: FoundItem = { id: "item", description: "Watch", place: "Hall", date: "2025-01-01", storage: "Desk", uploaderId: actor.id, custodianId: actor.id, custodianDepartment: role === "department" ? "security" : undefined, state: "available", createdAt: "2025-01-01T12:00:00Z", version: 1 };
  const claim: Claim & { canApprove: boolean; canReject: boolean } = { id: "claim", itemId: item.id, claimantId: "requester", claimantName: "Requester", claimantRole: "user", createdAt: "2025-01-01T12:00:00Z", message: "", status: "pending", version: 1, claimantConfirmed: false, custodianConfirmed: false, canApprove: true, canReject: false };
  vi.spyOn(lostFound, "claimDetail").mockResolvedValue({ claim, item });
  const approve = vi.spyOn(lostFound, "approve");
  renderForActor(actor, <ClaimDetail />);
  const field = await screen.findByLabelText(role === "department" ? /^Collection place/ : /^Contact phone/);
  await userEvent.type(field, "   ");
  if (role === "department") await userEvent.type(screen.getByLabelText(/^Collection instructions/), "Collect at desk");
  await userEvent.click(screen.getByRole("button", { name: "Approve request" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(approve).not.toHaveBeenCalled();
  await userEvent.clear(field);
  await userEvent.type(field, role === "department" ? "Desk 1" : "01000000000");
  if (role === "department") {
    const instructions = screen.getByLabelText(/^Collection instructions/);
    await userEvent.clear(instructions);
    await userEvent.type(instructions, "   ");
    await userEvent.click(screen.getByRole("button", { name: "Approve request" }));
    expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
    expect(approve).not.toHaveBeenCalled();
    await userEvent.clear(instructions);
    await userEvent.type(instructions, "Collect at desk");
  }
  await userEvent.click(screen.getByRole("button", { name: "Approve request" }));
  expect(await screen.findByText(/temporarily unavailable/i)).toBeTruthy();
  expect(approve).toHaveBeenCalledOnce();
});
