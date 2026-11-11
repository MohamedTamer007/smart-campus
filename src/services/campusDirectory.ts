import { api, departments, json } from "./http";
import type { LocationOption } from "./maintenance";

export interface CampusDepartment {
  id: string;
  code: string;
  ar: string;
  en: string;
  canReceive: boolean;
  maintenance: boolean;
}
export interface DepartmentDraft {
  code: string;
  ar: string;
  en: string;
  canReceive: boolean;
  maintenance: boolean;
}
export interface LocationDraft {
  kind: "faculty" | "building" | "floor";
  parentId: string | null;
  ar: string;
  en: string;
  maintenanceDepartmentId: string | null;
}

export const campusDirectory = {
  departments: (): Promise<CampusDepartment[]> => departments(),
  locations: (): Promise<LocationOption[]> => api<LocationOption[]>("/directory/locations"),
  createDepartment: (draft: DepartmentDraft): Promise<{ id: string }> =>
    api<{ id: string }>("/admin/departments", json("POST", draft)),
  createLocation: (draft: LocationDraft): Promise<{ id: string }> =>
    api<{ id: string }>("/admin/locations", json("POST", draft)),
};
