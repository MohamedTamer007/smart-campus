import type { Account } from "./contracts";
import { backendUnavailable } from "./unavailable";

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
}
export const maintenance: MaintenanceService = {
  locations: backendUnavailable,
  create: backendUnavailable,
  list: backendUnavailable,
  detail: backendUnavailable,
  edit: backendUnavailable,
  cancel: backendUnavailable,
  update: backendUnavailable,
};
