import { afterEach, beforeEach, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AppProvider } from "../src/app/context";
import { CampusMap } from "../src/features/campus/CampusMap";

function renderMap() {
  localStorage.setItem("smart-campus-language", "en");
  render(<AppProvider><CampusMap /></AppProvider>);
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

it("loads the official campus building names without admin-entered directory rows", () => {
  renderMap();
  expect(screen.getByRole("button", { name: "Female Students Hotel" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Sports Fields" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "University Hospital" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Administrative Building" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "Engineering Building (Building 1)" })).toBeTruthy();
  expect(screen.getByText(/No buildings need to be entered/)).toBeTruthy();
});

it("shows the source and location caveat when a building is selected", async () => {
  renderMap();
  await userEvent.click(screen.getByRole("button", { name: "University Hospital" }));
  expect(screen.getByText(/Faculty of Dentistry is located inside the hospital building/)).toBeTruthy();
  expect(screen.getByRole("link", { name: /Dentistry guide 2024\/25/ })).toBeTruthy();
});

it("marks older guide locations as approximate instead of presenting them as current surveyed coordinates", async () => {
  renderMap();
  await userEvent.click(screen.getByRole("button", { name: "Pharmacy Building" }));
  expect(screen.getByText(/approximate readings of published plans/)).toBeTruthy();
  expect(screen.getByRole("link", { name: /Information Systems guide 2021\/22/ })).toBeTruthy();
});
