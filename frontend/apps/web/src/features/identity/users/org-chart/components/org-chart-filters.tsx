"use client";

import { Combobox } from "@dewiride/erp-ui/components/combobox/combobox";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { Button, buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import Form from "next/form";
import { useEffect, useRef, useState, type FormEvent } from "react";

import { ListLink, useListNavigation } from "@/shared/lists/list-navigation";

import { noChoiceMessage } from "../../positions/components/move-text";

import type { DepartmentChoices } from "./org-chart";
import { departmentsUnreadable, departmentsUnreadableWhileChosen } from "./org-chart-text";
import {
  filterSignature,
  isChartDate,
  orgChartHref,
  orgChartPath,
  type OrgChartView,
} from "./org-chart-view";

const notADate = "Enter a real date as day-month-year, for example 31-03-2026.";

// The fields stay disabled until the page is interactive, because the chart's address is built from them in the browser;
// a date that cannot be read is named once the form is sent, and then on every change, and keeps the address as it is.
export function OrgChartFilters({
  view,
  today,
  choosesDate,
  departments,
  focusFirstField,
  onApply,
  onClear,
}: {
  view: OrgChartView;
  today: string;
  choosesDate: boolean;
  departments: DepartmentChoices;
  focusFirstField: boolean;
  onApply: (signature: string) => void;
  onClear: () => void;
}) {
  const hydrated = useHydrated();
  const { navigate } = useListNavigation();
  const formRef = useRef<HTMLFormElement>(null);
  const dateRef = useRef<HTMLInputElement>(null);
  const [department, setDepartment] = useState<string | null>(view.department ?? null);
  const [day, setDay] = useState(view.on ?? today);
  const [dateChecked, setDateChecked] = useState(false);
  const dateProblem = day === "" || isChartDate(day) ? undefined : notADate;

  useEffect(() => {
    if (focusFirstField)
      formRef.current?.querySelector<HTMLElement>("input:not([type=hidden]), button")?.focus();
  }, [focusFirstField]);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (dateProblem !== undefined) {
      setDateChecked(true);
      dateRef.current?.focus();
      return;
    }
    const next: OrgChartView = {
      department: department ?? undefined,
      on: day === "" || day === today ? undefined : day,
      page: 1,
    };
    onApply(filterSignature(next));
    navigate(orgChartHref(next));
  }

  return (
    <Form
      ref={formRef}
      action={orgChartPath}
      role="search"
      aria-label="Choose what the chart shows"
      noValidate
      onSubmit={submit}
      data-hydrating={hydrated ? undefined : ""}
      data-testid="org-chart-filters"
      className="flex flex-wrap items-end gap-3"
    >
      {departments.status === "read" ? (
        <FormField
          label="Department"
          description="Shows the people in it and in the departments under it."
          className="w-full sm:w-80"
        >
          {(control) => (
            <Combobox
              {...control}
              options={departments.options}
              value={department}
              onValueChange={setDepartment}
              disabled={!hydrated}
              placeholder="Every department"
              emptyMessage={noChoiceMessage("department", departments.options.length)}
            />
          )}
        </FormField>
      ) : (
        <p
          role="status"
          className="w-full text-sm text-muted-foreground"
          data-testid="org-chart-departments-unavailable"
        >
          {view.department === undefined ? departmentsUnreadable : departmentsUnreadableWhileChosen}
        </p>
      )}
      {choosesDate ? (
        <FormField
          label="Date"
          description="Today unless you choose another day, past or future."
          errors={dateChecked && dateProblem !== undefined ? [dateProblem] : undefined}
          className="w-full sm:w-60"
        >
          {(control) => (
            <DateInput {...control} ref={dateRef} value={day} onValueChange={setDay} disabled={!hydrated} />
          )}
        </FormField>
      ) : null}
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" variant="secondary" disabled={!hydrated} data-testid="org-chart-show">
          Show
        </Button>
        {view.department === undefined ? null : (
          <ListLink
            href={orgChartHref({ ...view, department: undefined, page: 1 })}
            disabled={false}
            aria-label="Show every department"
            className={buttonVariants({ variant: "ghost" })}
            onFollow={onClear}
          >
            Show every department
          </ListLink>
        )}
        {view.on === undefined ? null : (
          <ListLink
            href={orgChartHref({ ...view, on: undefined, page: 1 })}
            disabled={false}
            aria-label="Show today"
            className={buttonVariants({ variant: "ghost" })}
            onFollow={onClear}
          >
            Show today
          </ListLink>
        )}
      </div>
    </Form>
  );
}
