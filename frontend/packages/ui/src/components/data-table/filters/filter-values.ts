import { compareIsoDates, isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";

export interface DataTableFilterOption {
  readonly value: string;
  readonly label: string;
}

export interface DateRangeFilterValue {
  readonly from: string;
  readonly to: string;
}

export interface DateRangeProblems {
  readonly from: string | undefined;
  readonly to: string | undefined;
}

const notADate = "Enter a real date as day-month-year, for example 31-03-2026.";

const endBeforeStart = "The end date is before the start date.";

export function toggleOption(
  selected: readonly string[],
  value: string,
  checked: boolean,
  options: readonly DataTableFilterOption[],
): readonly string[] {
  const next = new Set(selected);
  if (checked) next.add(value);
  else next.delete(value);
  return options.filter((option) => next.has(option.value)).map((option) => option.value);
}

export function optionsSummary(
  selected: readonly string[],
  options: readonly DataTableFilterOption[],
): string {
  if (selected.length === 0) return "Any";
  if (selected.length > 1) return `${selected.length} selected`;
  return options.find((option) => option.value === selected[0])?.label ?? "1 selected";
}

export function dateRangeProblems(value: DateRangeFilterValue): DateRangeProblems {
  const from = value.from !== "" && !isIsoDate(value.from) ? notADate : undefined;
  const to = value.to !== "" && !isIsoDate(value.to) ? notADate : undefined;
  if (from !== undefined || to !== undefined || value.from === "" || value.to === "") return { from, to };
  return { from: undefined, to: compareIsoDates(value.to, value.from) < 0 ? endBeforeStart : undefined };
}

export function submittedDate(text: string): string {
  return isIsoDate(text) ? text : "";
}
