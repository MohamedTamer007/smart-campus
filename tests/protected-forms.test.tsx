import { useEffect, type ReactNode } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AppProvider, useApp } from "../src/app/context";
import { AdminAccounts } from "../src/features/admin/Admin";
import { ClaimDetail, FoundDetail, RegisterFound, SearchItems } from "../src/features/lost-found/LostFound";
import { MaintenanceDetail, NewMaintenance } from "../src/features/maintenance/Maintenance";
import { directory } from "../src/services/admin";
import * as adminServices from "../src/services/admin";
import { OperationsStatus } from "../src/features/admin/OperationsStatus";
import { ServiceError, type Account } from "../src/services/contracts";
import { lostFound, type Claim, type FoundItem } from "../src/services/lostFound";
import { maintenance, type MaintenanceReport } from "../src/services/maintenance";
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

it("renders API operational counts and truthful worker unavailability", async () => {
  vi.spyOn(adminServices, "operations").mockResolvedValue({ sampledAt: "2026-11-09T12:00:00Z", deployment: "test-only",
    workingSetBytes: 64 * 1024 * 1024, worker: "unavailable", calibrationApproved: false,
    quotas: [{ provider: "openrouter", used: 7, dailyLimit: 50 }],
    jobs: [{ kind: "maintenance", state: "pending", count: 2, maximumAttempts: 1 }] });
  renderForActor({ id: "admin-test", name: "Administrator", email: "", role: "admin" }, <OperationsStatus />);
  expect(await screen.findByText("Matching worker: Unavailable")).toBeTruthy();
  expect(screen.getByText("Search calibration: Awaiting approval")).toBeTruthy();
  expect(screen.getByText("Maintenance assessment · Pending: 2")).toBeTruthy();
  expect(screen.getByText(/7 \/ 50/)).toBeTruthy();
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
  const description = await screen.findByLabelText(/^Fault description/);
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect(create).not.toHaveBeenCalled();
  await userEvent.type(description, "   ");
  await userEvent.selectOptions(screen.getByLabelText(/^Faculty or facility/), "f");
  await userEvent.selectOptions(screen.getByLabelText(/^Building/), "b");
  await userEvent.selectOptions(screen.getByLabelText(/^Floor/), "0");
  await userEvent.type(screen.getByLabelText(/^Precise location/), "Room 1");
  await userEvent.click(screen.getByRole("button", { name: "Submit report" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(create).not.toHaveBeenCalled();
  await userEvent.clear(description);
  await userEvent.type(description, "Leaking tap");
  const location = screen.getByLabelText(/^Precise location/);
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
  const description = screen.getByLabelText(/^Item description/);
  await userEvent.type(description, "Blue watch");
  await userEvent.type(screen.getByLabelText(/^Place found/), "Courtyard");
  await userEvent.type(screen.getByLabelText(/^Current storage/), "Desk");
  const date = screen.getByLabelText(/^Date found/) as HTMLInputElement;
  await userEvent.type(date, "2999-01-01");
  expect(date.validity.rangeOverflow).toBe(true);
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect(register).not.toHaveBeenCalled();
  await userEvent.clear(date);
  await userEvent.type(date, "2025-01-01");
  await userEvent.upload(photo, new File(["image"], "photo.png", { type: "image/png" }));
  for (const [field, original] of [
    [description, "Blue watch"],
    [screen.getByLabelText(/^Place found/), "Courtyard"],
    [screen.getByLabelText(/^Current storage/), "Desk"],
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
  await userEvent.type(screen.getByLabelText(/^Receiving office or location/), "   ");
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(register).not.toHaveBeenCalled();
  await userEvent.selectOptions(destination, "");
  await userEvent.click(screen.getByRole("button", { name: "Register item" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
  expect((description as HTMLTextAreaElement).value).toBe("Blue watch");
  expect(register).toHaveBeenCalledOnce();
});

it("rejects an empty search before requesting matches", async () => {
  const actor: Account = { id: "search-form-test", name: "Member", email: "", role: "user" };
  const search = vi.spyOn(lostFound, "search");
  renderForActor(actor, <SearchItems />);
  const description = await screen.findByLabelText(/^Lost item description/);
  await userEvent.type(description, "   ");
  await userEvent.click(screen.getByRole("button", { name: "Search for matches" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/check required fields/i);
  expect(search).not.toHaveBeenCalled();
  await userEvent.clear(description);
  await userEvent.type(description, "watch");
  const photo = new File(["image"], "photo.png", { type: "image/png" });
  await userEvent.upload(screen.getByLabelText(/^Previous photo/), photo);
  await userEvent.click(screen.getByRole("button", { name: "Search for matches" }));
  expect((await screen.findByRole("alert")).textContent).toMatch(/temporarily unavailable/i);
  expect(search).toHaveBeenCalledWith(actor, "watch", photo);
});

it("requires content review and separate image consent before external analysis", async () => {
  const actor: Account = { id: "ai-staff-test", name: "Staff", email: "", role: "maintenance" };
  const report: MaintenanceReport = { id: "reviewed-report", ownerId: "other", description: "Synthetic fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "submitted", version: 4, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canViewAssessment: true, canReviewAiInput: true, canReviewAiImage: true, assessment: { state: "unavailable", type: "", priority: "", risk: "" } };
  vi.spyOn(maintenance, "detail").mockResolvedValue(report);
  const review = vi.spyOn(maintenance, "reviewAi").mockResolvedValue({ ...report, canReviewAiInput: false });
  renderForActor(actor, <MaintenanceDetail />);
  const submit = await screen.findByRole("button", { name: "Send reviewed content for analysis" });
  await userEvent.click(submit);
  expect(review).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole("checkbox", { name: /I reviewed the description/ }));
  expect((screen.getByRole("checkbox", { name: /I also reviewed the image/ }) as HTMLInputElement).checked).toBe(false);
  await userEvent.click(submit);
  expect(review).toHaveBeenCalledWith("reviewed-report", 4, true, false);
});

it("requires a nonblank rejection reason before requesting a report update", async () => {
  const actor: Account = { id: "staff-test", name: "Staff", email: "", role: "maintenance", department: "maintenance" };
  const report: MaintenanceReport = { id: "r", ownerId: "other", description: "Fault", faculty: "f", building: "b", floor: "0", details: "Room", status: "review", version: 1, createdAt: "2025-01-01T12:00:00Z", assignee: "", history: [], canUpdate: true, assessment: { type: "", priority: "", risk: "" } };
  vi.spyOn(maintenance, "detail").mockResolvedValue(report);
  const update = vi.spyOn(maintenance, "update");
  renderForActor(actor, <MaintenanceDetail />);
  await userEvent.selectOptions(await screen.findByLabelText(/^Status/), "rejected");
  const note = screen.getByLabelText(/^Rejection reason/);
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
