"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { useState, type FormEvent } from "react";

import {
  CountryPicker,
  CurrencyPicker,
  FinancialPeriodPicker,
  FinancialYearPicker,
  IndianStatePicker,
} from "@/features/platform/reference-data";
import { leavesGroup } from "@/shared/forms/client/field-focus";

const foreignCurrenciesOnly = ["INR"] as const;

const notADate = "Enter a real date as day-month-year, for example 31-07-2020.";

function keepOnPage(event: FormEvent<HTMLFormElement>): void {
  event.preventDefault();
}

// Each picker is a controlled field, as a module's form holds it; the values are shown as each picker emits them, a period
// by its first day, and a period belongs to its year, so choosing another year clears it.
export function ReferencePickersForm({ labelledBy }: { labelledBy: string }) {
  const hydrated = useHydrated();
  const [country, setCountry] = useState<string | null>(null);
  const [documentDate, setDocumentDate] = useState("");
  const [documentDateLeft, setDocumentDateLeft] = useState(false);
  const [indianState, setIndianState] = useState<string | null>(null);
  const [currency, setCurrency] = useState<string | null>(null);
  const [year, setYear] = useState<string | null>(null);
  const [period, setPeriod] = useState<string | null>(null);
  const on = isIsoDate(documentDate) ? documentDate : null;
  const dateProblem = documentDateLeft && documentDate !== "" && on === null ? notADate : undefined;

  function chooseYear(next: string | null): void {
    if (next !== year) setPeriod(null);
    setYear(next);
  }

  const chosen = [
    { id: "country", label: "Country", value: country },
    { id: "document-date", label: "Document date", value: on },
    { id: "indian-state", label: "State or union territory", value: indianState },
    { id: "currency", label: "Currency", value: currency },
    { id: "financial-year", label: "Financial year", value: year },
    { id: "period", label: "Period", value: period },
  ];

  return (
    <form
      noValidate
      aria-labelledby={labelledBy}
      onSubmit={keepOnPage}
      data-hydrating={hydrated ? undefined : ""}
      data-testid="reference-pickers-form"
      className="grid min-w-0 gap-6"
    >
      <FieldGroup>
        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <FormField label="Country" description="Type its name or its two-letter code.">
            {(control) => <CountryPicker {...control} value={country} onValueChange={setCountry} />}
          </FormField>
          <FormField label="Currency" description="A foreign currency, so the Indian rupee is left out.">
            {(control) => (
              <CurrencyPicker
                {...control}
                value={currency}
                onValueChange={setCurrency}
                excluded={foreignCurrenciesOnly}
              />
            )}
          </FormField>
        </div>

        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <FormField
            label="Document date"
            description="The State codes in force on this day are offered; today's when it is empty."
            errors={dateProblem === undefined ? undefined : [dateProblem]}
          >
            {(control) => (
              <div
                className="min-w-0"
                onBlur={(event) => {
                  if (leavesGroup(event)) setDocumentDateLeft(true);
                }}
              >
                <DateInput {...control} value={documentDate} onValueChange={setDocumentDate} />
              </div>
            )}
          </FormField>
          <FormField label="State or union territory" description="Type its name or its GST State code.">
            {(control) => (
              <IndianStatePicker {...control} on={on} value={indianState} onValueChange={setIndianState} />
            )}
          </FormField>
        </div>

        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <FormField label="Financial year">
            {(control) => <FinancialYearPicker {...control} value={year} onValueChange={chooseYear} />}
          </FormField>
          <FormField label="Period" description="The months of the chosen financial year.">
            {(control) => (
              <FinancialPeriodPicker {...control} year={year} value={period} onValueChange={setPeriod} />
            )}
          </FormField>
        </div>
      </FieldGroup>

      <div className="grid gap-2">
        <h3 className="font-medium">Chosen values</h3>
        <dl
          className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1.5 text-sm"
          data-testid="reference-pickers-values"
        >
          {chosen.map((item) => (
            <div key={item.id} className="contents">
              <dt className="text-muted-foreground">{item.label}</dt>
              <dd className="min-w-0 break-words" data-testid={`chosen-${item.id}`}>
                {item.value ?? "None"}
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </form>
  );
}
