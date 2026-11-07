import { financialYearsPage } from "../../list-pages/components/reference-list-pages.ts";
import type { SearchParameters } from "../../list-pages/components/reference-list-view.ts";

export interface FinancialYearChoice {
  readonly current: string;
  readonly from: string;
  readonly to: string;
}

export interface FinancialYearView {
  readonly year: string | undefined;
}

export interface ReadFinancialYearView {
  readonly view: FinancialYearView;
  readonly canonical: boolean;
}

export const financialYearParameter = "year";

const yearsBeforeCurrent = 10;

const yearsAfterCurrent = 1;

const firstMonth = 4;

const yearLabel = /^([0-9]{4})-([0-9]{2})$/;

function single(value: string | readonly string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function labelOf(startYear: number): string {
  return `${String(startYear).padStart(4, "0")}-${String((startYear + 1) % 100).padStart(2, "0")}`;
}

// The financial year starts on the first day of April, as Kernel.Time.FinancialYear has it, so a day from January to March
// belongs to the year that started the April before.
function startYearOfDay(isoDate: string): number {
  const year = Number(isoDate.slice(0, 4));
  return Number(isoDate.slice(5, 7)) >= firstMonth ? year : year - 1;
}

// A label names the calendar year its financial year starts in and the last two digits of the next, as the API writes it.
export function startYearOf(label: string): number | undefined {
  const match = yearLabel.exec(label);
  if (match === null) return undefined;
  const startYear = Number(match[1]);
  return startYear >= 1 && (startYear + 1) % 100 === Number(match[2]) ? startYear : undefined;
}

export function financialYearOf(isoDate: string): string {
  return labelOf(startYearOfDay(isoDate));
}

export function financialYearChoice(today: string): FinancialYearChoice {
  const startYear = startYearOfDay(today);
  return {
    current: labelOf(startYear),
    from: labelOf(startYear - yearsBeforeCurrent),
    to: labelOf(startYear + yearsAfterCurrent),
  };
}

function isOffered(choice: FinancialYearChoice, label: string): boolean {
  const startYear = startYearOf(label);
  const first = startYearOf(choice.from);
  const last = startYearOf(choice.to);
  return (
    startYear !== undefined &&
    first !== undefined &&
    last !== undefined &&
    startYear >= first &&
    startYear <= last
  );
}

// The current year is the default and is never named; a year the page does not offer, or a parameter it does not know, is
// dropped from the address.
export function readFinancialYearView(
  parameters: SearchParameters,
  choice: FinancialYearChoice,
): ReadFinancialYearView {
  const year = single(parameters[financialYearParameter]);
  const view: FinancialYearView = {
    year: year !== undefined && year !== choice.current && isOffered(choice, year) ? year : undefined,
  };
  const canonical =
    Object.entries(parameters).every(
      ([name, value]) =>
        value === undefined || (name === financialYearParameter && typeof value === "string"),
    ) && year === view.year;
  return { view, canonical };
}

export function financialYearsHref(
  view: FinancialYearView,
): typeof financialYearsPage.path | `${typeof financialYearsPage.path}?${string}` {
  if (view.year === undefined) return financialYearsPage.path;
  return `${financialYearsPage.path}?${new URLSearchParams({ [financialYearParameter]: view.year }).toString()}`;
}
