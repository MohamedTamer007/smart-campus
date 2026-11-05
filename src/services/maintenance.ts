import {
  actors,
  currentDemoAccount,
  type Account,
  type Scenario,
  ServiceError,
} from "./contracts";
import { boundary, campus } from "./demo";

export type MaintenanceStatus =
  "submitted" | "review" | "repair" | "resolved" | "rejected" | "cancelled";
export interface MaintenanceInput {
  description: string;
  faculty: string;
  building: string;
  floor: string;
  details: string;
  photo?: File;
}
export interface MaintenanceEvent {
  at: string;
  actor: string;
  status: MaintenanceStatus;
  note: string;
  assignee?: string;
  assigneeName?: string;
}
export interface OperationalDecision {
  type: string;
  priority: string;
  risk: string;
}
export interface MaintenanceReport extends MaintenanceInput {
  id: string;
  ownerId: string;
  status: MaintenanceStatus;
  version: number;
  createdAt: string;
  assignee: string;
  history: MaintenanceEvent[];
  assessment?: { type: string; priority: string; risk: string };
  decision?: OperationalDecision;
}
export const demoLocations = [
  {
    id: "demo-faculty",
    ar: "مرفق تجريبي",
    en: "Demo facility",
    buildings: [
      {
        id: "demo-building",
        ar: "مبنى تجريبي",
        en: "Demo building",
        floors: [
          { id: "ground", ar: "الأرضي", en: "Ground floor" },
          { id: "first", ar: "الأول", en: "First floor" },
        ],
      },
    ],
  },
];
export function maintenanceInput(input: MaintenanceInput): MaintenanceInput {
  return {
    description: input.description,
    faculty: input.faculty,
    building: input.building,
    floor: input.floor,
    details: input.details,
    photo: input.photo,
  };
}
export function validLocation(input: MaintenanceInput) {
  return demoLocations.some(
    (f) =>
      f.id === input.faculty &&
      f.buildings.some(
        (b) =>
          b.id === input.building &&
          b.floors.some((floor) => floor.id === input.floor),
      ),
  );
}
function checkInput(input: MaintenanceInput) {
  if (
    !input.description.trim() ||
    !input.details.trim() ||
    !validLocation(input) ||
    (input.photo && !input.photo.type.startsWith("image/"))
  )
    throw new ServiceError("invalid");
}
function isStaff(actor: Account) {
  return (
    actor.role === "admin" ||
    (actor.role === "maintenance" && actor.department === "maintenance")
  );
}
function cleanReport(
  report: MaintenanceReport,
  staff: boolean,
): MaintenanceReport {
  const copy = {
    ...report,
    history: report.history.map((event) => ({ ...event })),
  };
  if (!staff) {
    delete copy.assessment;
    delete copy.decision;
  }
  return copy;
}
function requireReport(
  reports: MaintenanceReport[],
  actor: Account,
  id: string,
) {
  const report = reports.find((record) => record.id === id);
  if (!report || (!isStaff(actor) && report.ownerId !== actor.id))
    throw new ServiceError("forbidden");
  return report;
}
function requireVersion(report: MaintenanceReport, version: number) {
  if (report.version !== version) throw new ServiceError("conflict");
}
function updateReport(
  report: MaintenanceReport,
  actor: Account,
  status: MaintenanceStatus,
  note = "",
  assigneeName = "",
) {
  report.status = status;
  report.version += 1;
  report.history.push({
    at: new Date().toISOString(),
    actor: actor.name,
    status,
    note,
    assignee: report.assignee,
    assigneeName,
  });
}
export class MaintenanceDemo {
  constructor(
    private readonly accounts: () => Account[] = () => [
      ...Object.values(actors),
      ...campus.registeredAccounts(),
    ],
  ) {}
  private reports: MaintenanceReport[] = [];
  private counter = 0;
  async create(
    actor: Account,
    input: MaintenanceInput,
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    checkInput(input);
    const createdAt = new Date().toISOString();
    const report: MaintenanceReport = {
      ...maintenanceInput(input),
      id: `SC-${++this.counter}`,
      ownerId: actor.id,
      status: "submitted",
      version: 1,
      createdAt,
      assignee: "",
      history: [
        { at: createdAt, actor: actor.name, status: "submitted", note: "" },
      ],
      assessment: { type: "pending", priority: "pending", risk: "pending" },
    };
    this.reports.push(report);
    return cleanReport(report, false);
  }
  async list(actor: Account, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (scenario === "empty") return [];
    return this.reports
      .filter((report) => isStaff(actor) || report.ownerId === actor.id)
      .map((report) => cleanReport(report, isStaff(actor)));
  }
  async detail(actor: Account, id: string, scenario: Scenario = "normal") {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    return cleanReport(requireReport(this.reports, actor, id), isStaff(actor));
  }
  async edit(
    actor: Account,
    request: { id: string; version: number; input: MaintenanceInput },
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const report = requireReport(this.reports, actor, request.id);
    requireVersion(report, request.version);
    if (report.ownerId !== actor.id || report.status !== "submitted")
      throw new ServiceError("forbidden");
    checkInput(request.input);
    Object.assign(report, maintenanceInput(request.input));
    updateReport(report, actor, "submitted");
    return cleanReport(report, false);
  }
  async cancel(
    actor: Account,
    request: { id: string; version: number },
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    const report = requireReport(this.reports, actor, request.id);
    requireVersion(report, request.version);
    if (report.ownerId !== actor.id || report.status !== "submitted")
      throw new ServiceError("forbidden");
    updateReport(report, actor, "cancelled");
    return cleanReport(report, false);
  }
  async update(
    actor: Account,
    request: {
      id: string;
      version: number;
      status: MaintenanceStatus;
      assignee: string;
      note: string;
      decision?: OperationalDecision;
    },
    scenario: Scenario = "normal",
  ) {
    await boundary(scenario);
    actor = currentDemoAccount(actor, this.accounts());
    if (!isStaff(actor)) throw new ServiceError("forbidden");
    const report = requireReport(this.reports, actor, request.id);
    requireVersion(report, request.version);
    const allowed: Partial<Record<MaintenanceStatus, MaintenanceStatus[]>> = {
      submitted: ["review", "rejected"],
      review: ["review", "repair", "rejected"],
      repair: ["repair", "resolved", "rejected"],
    };
    if (
      !allowed[report.status]?.includes(request.status) ||
      (request.status === "rejected" && !request.note.trim()) ||
      (request.assignee &&
        !this.accounts().some(
          (candidate) =>
            candidate.id === request.assignee &&
            isStaff(candidate) &&
            candidate.role !== "admin",
        ))
    )
      throw new ServiceError("invalid");
    report.assignee = request.assignee;
    if (request.decision) report.decision = { ...request.decision };
    updateReport(
      report,
      actor,
      request.status,
      request.note.trim(),
      this.accounts().find((candidate) => candidate.id === request.assignee)
        ?.name ?? "",
    );
    campus.notify(
      report.ownerId,
      "تم تحديث حالة بلاغ الصيانة.",
      "Your maintenance report has been updated.",
      `/maintenance/${report.id}`,
    );
    return cleanReport(report, true);
  }
}
export const maintenance = new MaintenanceDemo();
