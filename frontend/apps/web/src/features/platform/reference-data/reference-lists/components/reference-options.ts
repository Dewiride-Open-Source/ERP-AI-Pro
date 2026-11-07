import type { IndianStateKind } from "@dewiride/erp-api-client";
import { isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";

import { searchReferenceList } from "../../list-pages/components/reference-list-view.ts";

export type ListRead<T> =
  | { readonly status: "read"; readonly items: readonly T[] }
  | { readonly status: "failed" }
  | { readonly status: "disabled" };

export interface CountryItem {
  readonly code: string;
  readonly name: string;
}

export interface IndianStateItem {
  readonly gstCode: string;
  readonly name: string;
  readonly kind: IndianStateKind;
  readonly validFrom: string;
  readonly validUntil: string | null;
}

export interface CurrencyItem {
  readonly code: string;
  readonly name: string;
  readonly minorUnits: number;
}

export interface FinancialQuarterItem {
  readonly number: number;
  readonly label: string;
  readonly start: string;
  readonly end: string;
}

export interface FinancialPeriodItem {
  readonly number: number;
  readonly label: string;
  readonly quarter: number;
  readonly start: string;
  readonly end: string;
}

export interface FinancialYearItem {
  readonly label: string;
  readonly start: string;
  readonly end: string;
  readonly quarters: readonly FinancialQuarterItem[];
  readonly periods: readonly FinancialPeriodItem[];
}

export interface ReferenceLists {
  readonly countries?: ListRead<CountryItem>;
  readonly indianStates?: ListRead<IndianStateItem>;
  readonly currencies?: ListRead<CurrencyItem>;
  readonly financialYears?: ListRead<FinancialYearItem>;
}

export interface ReferenceOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
}

type FallbackLabel = (value: string) => string | undefined;

const periodMonth = new Intl.DateTimeFormat("en-IN", { month: "long", year: "numeric", timeZone: "UTC" });

export function displayDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

// A value the options lack stays chosen, and first, so editing a record never loses what the record holds: named as the list
// still names it, or by its code alone when no entry does (a code the list dropped, or a list that could not be read).
function keepChosen(
  options: readonly ReferenceOption[],
  value: string | null,
  fallbackLabel: FallbackLabel = () => undefined,
): readonly ReferenceOption[] {
  if (value === null || options.some((option) => option.value === value)) return options;
  return [{ value, label: fallbackLabel(value) ?? `${value} (name not available)` }, ...options];
}

function itemsOf<T>(list: ListRead<T>): readonly T[] {
  return list.status === "read" ? list.items : [];
}

function latestEntry(states: readonly IndianStateItem[], code: string): IndianStateItem | undefined {
  let latest: IndianStateItem | undefined;
  for (const state of states.filter((entry) => entry.gstCode === code)) {
    if (latest === undefined || state.validFrom > latest.validFrom) latest = state;
  }
  return latest;
}

// The pickers search as the reference list pages do, an option's value being its code, so a code typed in full puts its
// entry first: typing IN offers India before every name that holds "in".
export function matchReferenceOptions<T extends { readonly value: string; readonly label: string }>(
  options: readonly T[],
  typed: string,
): readonly T[] {
  return searchReferenceList(options, typed, (option) => ({ code: option.value, name: option.label }));
}

export function countryOptions(
  list: ListRead<CountryItem>,
  value: string | null,
): readonly ReferenceOption[] {
  return keepChosen(
    itemsOf(list).map((country) => ({ value: country.code, label: `${country.name} (${country.code})` })),
    value,
  );
}

export function appliesOn(state: IndianStateItem, isoDate: string): boolean {
  return state.validFrom <= isoDate && (state.validUntil === null || state.validUntil >= isoDate);
}

export function indianStateOptions(
  list: ListRead<IndianStateItem>,
  on: string | null,
  today: string,
  value: string | null,
): readonly ReferenceOption[] {
  const day = on ?? today;
  const states = itemsOf(list);
  return keepChosen(
    states
      .filter((state) => appliesOn(state, day))
      .map((state) => ({ value: state.gstCode, label: `${state.gstCode} ${state.name}` })),
    value,
    (code) => {
      const latest = latestEntry(states, code);
      return latest === undefined
        ? undefined
        : `${code} ${latest.name} (not in force on ${displayDate(day)})`;
    },
  );
}

export function currencyOptions(
  list: ListRead<CurrencyItem>,
  value: string | null,
  excluded: readonly string[] = [],
): readonly ReferenceOption[] {
  const currencies = itemsOf(list);
  const label = (currency: CurrencyItem) => `${currency.code} ${currency.name}`;
  return keepChosen(
    currencies
      .filter((currency) => !excluded.includes(currency.code))
      .map((currency) => ({ value: currency.code, label: label(currency) })),
    value,
    (code) => {
      const held = currencies.find((currency) => currency.code === code);
      return held === undefined ? undefined : label(held);
    },
  );
}

export function financialYearOptions(
  list: ListRead<FinancialYearItem>,
  value: string | null,
): readonly ReferenceOption[] {
  return keepChosen(
    itemsOf(list).map((year) => ({
      value: year.label,
      label: year.label,
      description: `${displayDate(year.start)} to ${displayDate(year.end)}`,
    })),
    value,
  );
}

// A period is named after the month it covers, as the API names it, so a kept period is named from its first day even when
// the list does not hold its year.
export function financialPeriodOptions(
  list: ListRead<FinancialYearItem>,
  year: string | null,
  value: string | null,
): readonly ReferenceOption[] {
  const periods = itemsOf(list).find((item) => item.label === year)?.periods ?? [];
  return keepChosen(
    periods.map((period) => ({
      value: period.start,
      label: period.label,
      description: `Period ${period.number}, quarter ${period.quarter} of ${year}`,
    })),
    value,
    (start) =>
      isIsoDate(start) && start.endsWith("-01")
        ? periodMonth.format(new Date(`${start}T00:00:00Z`))
        : undefined,
  );
}

export function listMessage(list: ListRead<unknown>, listName: string, noMatch: string): string {
  switch (list.status) {
    case "read":
      return noMatch;
    case "failed":
      return `The list of ${listName} could not be read. Try again later.`;
    case "disabled":
      return "The reference lists are switched off.";
  }
}
