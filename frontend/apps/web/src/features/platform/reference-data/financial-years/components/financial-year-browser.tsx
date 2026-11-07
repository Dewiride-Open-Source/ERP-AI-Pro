"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import Form from "next/form";
import { useRef, useState, type FormEvent, type ReactNode } from "react";

import { useFilterFormState } from "@/shared/lists/filter-state/use-filter-form-state";
import { ListNavigationProvider, useListNavigation } from "@/shared/lists/list-navigation";

import { financialYearsPage } from "../../list-pages/components/reference-list-pages";

import { FinancialYearPicker } from "./financial-year-picker";
import { financialYearsHref } from "./financial-year-view";

const noYearChosen = "Choose a financial year.";

export function FinancialYearBrowser({
  year,
  current,
  children,
}: {
  year: string;
  current: string;
  children: ReactNode;
}) {
  return (
    <ListNavigationProvider>
      <FinancialYearContent year={year} current={current}>
        {children}
      </FinancialYearContent>
    </ListNavigationProvider>
  );
}

// The chooser stays in place when it changed the address itself, keeping the focus a person gave it, and starts again for
// any other change (Back, a link), so its field shows the year of the address.
function FinancialYearContent({
  year,
  current,
  children,
}: {
  year: string;
  current: string;
  children: ReactNode;
}) {
  const { pending } = useListNavigation();
  const chooser = useFilterFormState(year);

  return (
    <div className="grid min-w-0 gap-6" data-testid="financial-year">
      <div className="grid gap-3">
        <FinancialYearChooser key={chooser.key} year={year} current={current} onApply={chooser.onApply} />
        <p role="status" className="text-sm text-muted-foreground" data-testid="financial-year-status">
          {pending ? "Loading…" : `Showing financial year ${year}.`}
        </p>
      </div>
      <div
        data-pending={pending || undefined}
        aria-busy={pending || undefined}
        className="grid min-w-0 gap-6 transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
      >
        {children}
      </div>
    </div>
  );
}

// The picker works only through React, so the form waits for the page to hydrate; a cleared choice is named when the form
// is sent, and then until a year is chosen.
function FinancialYearChooser({
  year,
  current,
  onApply,
}: {
  year: string;
  current: string;
  onApply: (signature: string) => void;
}) {
  const hydrated = useHydrated();
  const { navigate } = useListNavigation();
  const pickerRef = useRef<HTMLInputElement>(null);
  const [chosen, setChosen] = useState<string | null>(year);
  const [sent, setSent] = useState(false);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (chosen === null) {
      setSent(true);
      pickerRef.current?.focus();
      return;
    }
    onApply(chosen);
    navigate(financialYearsHref({ year: chosen === current ? undefined : chosen }));
  }

  return (
    <Form
      action={financialYearsPage.path}
      aria-label="Choose a financial year"
      noValidate
      onSubmit={submit}
      data-hydrating={hydrated ? undefined : ""}
      data-testid="financial-year-chooser"
      className="flex flex-wrap items-end gap-3"
    >
      <FormField
        label="Financial year"
        errors={sent && chosen === null ? [noYearChosen] : undefined}
        className="w-full sm:w-64"
      >
        {(control) => (
          <FinancialYearPicker
            {...control}
            ref={pickerRef}
            value={chosen}
            onValueChange={setChosen}
            disabled={!hydrated}
          />
        )}
      </FormField>
      <Button type="submit" variant="secondary" disabled={!hydrated}>
        Show
      </Button>
    </Form>
  );
}
