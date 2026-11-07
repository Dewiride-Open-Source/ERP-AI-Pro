export type ListRead<T> =
  | { readonly status: "read"; readonly items: readonly T[] }
  | { readonly status: "failed" }
  | { readonly status: "disabled" };

export interface CountryItem {
  readonly code: string;
  readonly name: string;
}

export type TerritoryKindName = "state" | "unionTerritory" | "otherTerritory";

export interface IndianStateItem {
  readonly gstCode: string;
  readonly name: string;
  readonly kind: TerritoryKindName;
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

export function displayDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

// The combobox searches labels alone, so every label carries the code a person types; and a value the list does not hold
// (a code the list dropped, or a list that could not be read) stays chosen under its own code, so editing a record never
// loses what the record holds.
function keepChosen(options: readonly ReferenceOption[], value: string | null): readonly ReferenceOption[] {
  if (value === null || options.some((option) => option.value === value)) return options;
  return [{ value, label: `${value} (name not available)` }, ...options];
}

function itemsOf<T>(list: ListRead<T>): readonly T[] {
  return list.status === "read" ? list.items : [];
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
  value: string | null,
): readonly ReferenceOption[] {
  const inForce = itemsOf(list).filter((state) =>
    on === null ? state.validUntil === null : appliesOn(state, on),
  );
  return keepChosen(
    inForce.map((state) => ({ value: state.gstCode, label: `${state.gstCode} ${state.name}` })),
    value,
  );
}

export function currencyOptions(
  list: ListRead<CurrencyItem>,
  value: string | null,
  excluded: readonly string[] = [],
): readonly ReferenceOption[] {
  return keepChosen(
    itemsOf(list)
      .filter((currency) => !excluded.includes(currency.code))
      .map((currency) => ({ value: currency.code, label: `${currency.code} ${currency.name}` })),
    value,
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
