import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppProvider } from "../src/app/context";
import { ActionForm } from "../src/components/ui";
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
