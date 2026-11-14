import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AppProvider } from "../src/app/context";
import { ActionForm } from "../src/components/ui";
import { AsyncView } from "../src/components/AsyncView";
afterEach(cleanup);
it("does not send duplicate requests when the form is submitted twice before rendering", () => {
  const submit = vi.fn(() => new Promise<void>(() => {}));
  const view = render(
    <MemoryRouter>
      <AppProvider>
        <ActionForm label="Send" onSubmit={submit}>
          <span>Form content</span>
        </ActionForm>
      </AppProvider>
    </MemoryRouter>,
  );
  const form = view.getByRole("button", { name: "Send" }).closest("form")!;
  act(() => {
    fireEvent.submit(form);
    fireEvent.submit(form);
  });
  expect(submit).toHaveBeenCalledTimes(1);
});

it("reports unexpected action failures and permits a second attempt", async () => {
  const submit = vi.fn().mockRejectedValueOnce(new Error("unexpected transport response")).mockResolvedValue(undefined);
  render(
    <MemoryRouter>
      <AppProvider>
        <ActionForm label="Send" onSubmit={submit}>
          <span>Form content</span>
        </ActionForm>
      </AppProvider>
    </MemoryRouter>,
  );
  await userEvent.click(screen.getByRole("button", { name: "Send" }));
  expect((await screen.findByRole("alert")).textContent).toContain("غير متاحة");
  await userEvent.click(screen.getByRole("button", { name: "Send" }));
  expect(submit).toHaveBeenCalledTimes(2);
  expect(screen.queryByRole("alert")).toBeNull();
});

it("offers retry after an unexpected data-loading failure", async () => {
  const load = vi.fn(async () => "Recovered").mockRejectedValueOnce(new Error("malformed response"));
  render(
    <MemoryRouter>
      <AppProvider>
        <AsyncView load={load}>{value => <span>{value}</span>}</AsyncView>
      </AppProvider>
    </MemoryRouter>,
  );
  expect((await screen.findByRole("alert")).textContent).toContain("غير متاحة");
  await userEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));
  expect(await screen.findByText("Recovered")).toBeTruthy();
  expect(load).toHaveBeenCalledTimes(2);
});
