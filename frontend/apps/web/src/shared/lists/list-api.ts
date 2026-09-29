import type { DateRangeFilterDefinition, ListDefinition, ListFilterDefinition } from "./list-definition.ts";
import { isDateRange, type DateRangeValue, type ListFilterValue, type ListQuery } from "./list-query.ts";

export interface ListApiParameters {
  readonly page: number;
  readonly pageSize: number;
  readonly sort: string;
  readonly filter: string | undefined;
}

// India observes no daylight saving, so a fixed UTC+05:30 offset gives every India Standard Time midnight.
const indiaStandardTimeOffset = "+05:30";

const lastCalendarDate = "9999-12-31";

function encodeValue(value: string): string {
  return encodeURIComponent(value);
}

function istMidnight(isoDate: string): string {
  return new Date(`${isoDate}T00:00:00${indiaStandardTimeOffset}`).toISOString();
}

function nextIsoDate(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

function dateRangeTerms(filter: DateRangeFilterDefinition, range: DateRangeValue): string[] {
  const terms: string[] = [];
  if (filter.instant) {
    if (range.from !== "") terms.push(`${filter.field}:gte:${encodeValue(istMidnight(range.from))}`);
    if (range.to !== "" && range.to !== lastCalendarDate) {
      terms.push(`${filter.field}:lt:${encodeValue(istMidnight(nextIsoDate(range.to)))}`);
    }
    return terms;
  }
  if (range.from !== "") terms.push(`${filter.field}:gte:${encodeValue(range.from)}`);
  if (range.to !== "") terms.push(`${filter.field}:lte:${encodeValue(range.to)}`);
  return terms;
}

function filterTerms(filter: ListFilterDefinition, value: ListFilterValue): string[] {
  if (filter.kind === "text" && typeof value === "string") {
    return [`${filter.field}:${filter.operator}:${encodeValue(value)}`];
  }
  if (filter.kind === "options" && typeof value !== "string" && !isDateRange(value)) {
    return [`${filter.field}:in:${value.map(encodeValue).join("|")}`];
  }
  if (filter.kind === "dateRange" && isDateRange(value)) return dateRangeTerms(filter, value);
  return [];
}

export function listApiParameters(query: ListQuery, definition: ListDefinition): ListApiParameters {
  const terms = definition.filters.flatMap((filter) => {
    const value = query.filters[filter.param];
    return value === undefined ? [] : filterTerms(filter, value);
  });
  return {
    page: query.page,
    pageSize: query.pageSize,
    sort: `${query.sort.field}:${query.sort.direction}`,
    filter: terms.length === 0 ? undefined : terms.join(";"),
  };
}
