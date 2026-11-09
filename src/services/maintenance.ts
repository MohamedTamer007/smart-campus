import type { Account } from "./contracts";
import { api, form, json, pages, withPhoto } from "./http";

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
  type: string | null;
  priority: string | null;
  risk: string | null;
}
export interface MaintenanceReport extends MaintenanceInput {
  id: string;
  ownerId: string;
  status: MaintenanceStatus;
  version: number;
  createdAt: string;
  assignee: string;
  history: MaintenanceEvent[];
  assessment?: { state?: string; type: string | null; priority: string | null; risk: string | null; inputReviewed?: boolean } | null;
  decision?: OperationalDecision | null;
  mediaUrl?: string | null;
  reference?: string;
  locationNames?: { faculty: { ar: string; en: string }; building: { ar: string; en: string }; floor: { ar: string; en: string } };
  cancelledAt?: string;
  canEdit?: boolean;
  canUpdate?: boolean;
  canViewAssessment?: boolean;
  canReviewAiInput?: boolean;
  canReviewAiImage?: boolean;
  departmentId?: string;
  allowedAssignees?: { id: string; name: string }[];
}
export interface LocationOption {
  id: string;
  ar: string;
  en: string;
  buildings: {
    id: string;
    ar: string;
    en: string;
    floors: { id: string; ar: string; en: string }[];
  }[];
}
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
interface MaintenanceService {
  locations(actor: Account): Promise<LocationOption[]>;
  create(actor: Account, input: MaintenanceInput): Promise<MaintenanceReport>;
  list(actor: Account): Promise<MaintenanceReport[]>;
  detail(actor: Account, id: string): Promise<MaintenanceReport>;
  edit(
    actor: Account,
    request: { id: string; version: number; input: MaintenanceInput },
  ): Promise<MaintenanceReport>;
  cancel(
    actor: Account,
    request: { id: string; version: number },
  ): Promise<MaintenanceReport>;
  update(
    actor: Account,
    request: {
      id: string;
      version: number;
      status: MaintenanceStatus;
      assignee: string;
      note: string;
      decision?: OperationalDecision;
    },
  ): Promise<MaintenanceReport>;
  reviewAi(id: string, version: number, confirmedNonSensitive: boolean, includeImage: boolean): Promise<MaintenanceReport>;
}
export const maintenance: MaintenanceService = {
  locations: () => api<LocationOption[]>("/directory/locations"),
  create: async (_actor, input) => normalize(await api<MaintenanceReport>("/maintenance", { method: "POST", body: form(input) })),
  list: async () => Promise.all((await pages<MaintenanceReport>("/maintenance")).map(normalize)),
  detail: async (_actor, id) => normalize(await api<MaintenanceReport>(`/maintenance/${id}`)),
  edit: async (_actor, request) => normalize(await api<MaintenanceReport>(`/maintenance/${request.id}`, { method: "PUT", body: form({ ...request.input, version: request.version }) })),
  cancel: async (_actor, request) => normalize(await api<MaintenanceReport>(`/maintenance/${request.id}/cancel`, json("POST", { version: request.version }))),
  update: async (_actor, request) => normalize(await api<MaintenanceReport>(`/maintenance/${request.id}/updates`, json("POST", request))),
  reviewAi: async (id, version, confirmedNonSensitive, includeImage) => normalize(await api<MaintenanceReport>(`/maintenance/${id}/ai-review`, json("POST", { version, confirmedNonSensitive, includeImage }))),
};
async function normalize(report: MaintenanceReport) {
  return withPhoto({ ...report, status: report.cancelledAt ? "cancelled" as const : report.status, assignee: report.assignee ?? "" });
}
