import { useEffect, type ReactNode } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AppProvider, useApp } from "../src/app/context";
import { AdminCampusDirectory } from "../src/features/admin/CampusDirectory";
import { campusDirectory } from "../src/services/campusDirectory";
import type { Account } from "../src/services/contracts";

const admin: Account = {
  id: "approved-admin", name: "Admin", email: "admin@o6u.edu.eg", role: "admin",
};
function Session({ children }: { children: ReactNode }) {
  const { account, updateAccount } = useApp();
  useEffect(() => {
    if (account.id !== admin.id) updateAccount(admin);
  }, [account.id, updateAccount]);
  return account.id === admin.id ? children : null;
}
function renderAdmin() {
  localStorage.setItem("smart-campus-language", "en");
  render(<MemoryRouter><AppProvider><Session><AdminCampusDirectory /></Session></AppProvider></MemoryRouter>);
}
beforeEach(() => {
  vi.spyOn(campusDirectory, "departments").mockResolvedValue([]);
  vi.spyOn(campusDirectory, "locations").mockResolvedValue([]);
});
afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.restoreAllMocks();
});

it("adds an institution-approved receiving department without creating users or roles", async () => {
  const create = vi.spyOn(campusDirectory, "createDepartment").mockResolvedValue({ id: "new-security" });
  renderAdmin();
  await screen.findByText("No departments have been configured yet.");
  await userEvent.type(screen.getAllByLabelText("Department code")[0], "security");
  await userEvent.type(screen.getAllByLabelText("Arabic name")[0], "الأمن");
  await userEvent.type(screen.getAllByLabelText("English name")[0], "Security");
  await userEvent.click(screen.getByLabelText("Authorized to receive found items"));
  await userEvent.click(screen.getByRole("button", { name: "Add department" }));
  await waitFor(() => expect(create).toHaveBeenCalledWith({
    code: "security", ar: "الأمن", en: "Security",
    canReceive: true, maintenance: false,
  }));
});

it("assigns an official maintenance department to the top-level faculty route", async () => {
  vi.spyOn(campusDirectory, "departments").mockResolvedValue([
    { id: "maintenance-id", code: "maintenance", ar: "الصيانة", en: "Maintenance", canReceive: false, maintenance: true },
  ]);
  const create = vi.spyOn(campusDirectory, "createLocation").mockResolvedValue({ id: "faculty-id" });
  renderAdmin();
  await screen.findByRole("option", { name: "Maintenance" });
  await userEvent.type(screen.getAllByLabelText("Arabic name")[1], "كلية");
  await userEvent.type(screen.getAllByLabelText("English name")[1], "Faculty");
  await userEvent.selectOptions(screen.getByLabelText("Responsible maintenance department"), "maintenance-id");
  await userEvent.click(screen.getByRole("button", { name: "Add location" }));
  await waitFor(() => expect(create).toHaveBeenCalledWith({
    kind: "faculty", parentId: null, ar: "كلية", en: "Faculty",
    maintenanceDepartmentId: "maintenance-id",
  }));
});

it("creates a building under the selected faculty without inventing a department", async () => {
  vi.spyOn(campusDirectory, "locations").mockResolvedValue([
    { id: "faculty-id", ar: "كلية", en: "Faculty", buildings: [] },
  ]);
  const create = vi.spyOn(campusDirectory, "createLocation").mockResolvedValue({ id: "building-id" });
  renderAdmin();
  await screen.findByText("Faculty");
  await userEvent.selectOptions(screen.getByLabelText("Location kind"), "building");
  await userEvent.selectOptions(screen.getByLabelText("Faculty or facility"), "faculty-id");
  await userEvent.type(screen.getAllByLabelText("Arabic name")[1], "مبنى");
  await userEvent.type(screen.getAllByLabelText("English name")[1], "Building A");
  await userEvent.click(screen.getByRole("button", { name: "Add location" }));
  await waitFor(() => expect(create).toHaveBeenCalledWith({
    kind: "building", parentId: "faculty-id", ar: "مبنى", en: "Building A",
    maintenanceDepartmentId: null,
  }));
});
