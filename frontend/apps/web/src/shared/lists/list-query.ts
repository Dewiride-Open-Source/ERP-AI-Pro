import { compareIsoDates, isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import type { Route } from "next";

import { readPageParameter } from "../api/paging/paging.ts";

import {
  filterParameters,
  listParameters,
  maxOptionValueLength,
  maxOptionValues,
  type ListDefinition,
  type ListFilterDefinition,
  type ListSort,
  type OptionsFilterDefinition,
  type TextFilterDefinition,
} from "./list-definition.ts";

export interface DateRangeValue {
  readonly from: string;
  readonly to: string;
}

export type ListFilterValue = string | readonly string[] | DateRangeValue;

export type ListFilterValues = Readonly<Record<string, ListFilterValue>>;

export interface ListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly sort: ListSort;
  readonly filters: ListFilterValues;
  readonly hiddenColumns: readonly string[];
}

export type SearchParameters = Readonly<Record<string, string | readonly string[] | undefined>>;

export interface ReadListQuery {
  readonly query: ListQuery;
  readonly canonical: boolean;
}

const sortTerm = /^([A-Za-z][A-Za-z0-9]*):(asc|desc)$/;

const wholeNumber = /^[0-9]+$/;

function single(value: string | readonly string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function values(value: string | readonly string[] | undefined): readonly string[] {
  if (value === undefined) return [];
  return typeof value === "string" ? [value] : value;
}

function readPageSize(value: string | readonly string[] | undefined, definition: ListDefinition): number {
  const text = single(value);
  if (text === undefined || !wholeNumber.test(text)) return definition.defaultPageSize;
  const size = Number(text);
  return definition.pageSizes.includes(size) ? size : definition.defaultPageSize;
}

function readSort(value: string | readonly string[] | undefined, definition: ListDefinition): ListSort {
  const match = sortTerm.exec(single(value) ?? "");
  const field = match?.[1];
  const direction = match?.[2];
  if (field === undefined || !definition.sortFields.includes(field)) return definition.defaultSort;
  return { field, direction: direction === "desc" ? "desc" : "asc" };
}

function readText(
  value: string | readonly string[] | undefined,
  filter: TextFilterDefinition,
): string | undefined {
  const text = single(value)?.trim() ?? "";
  return text === "" || text.length > filter.maxLength ? undefined : text;
}

function readOptions(
  value: string | readonly string[] | undefined,
  filter: OptionsFilterDefinition,
): readonly string[] | undefined {
  const chosen = new Set(
    values(value)
      .map((option) => option.trim())
      .filter((option) => option !== "" && option.length <= maxOptionValueLength),
  );
  const allowed = filter.values;
  const ordered = allowed === undefined ? [...chosen].sort() : allowed.filter((option) => chosen.has(option));
  return ordered.length === 0 || ordered.length > maxOptionValues ? undefined : ordered;
}

function readDateRange(
  from: string | readonly string[] | undefined,
  to: string | readonly string[] | undefined,
): DateRangeValue | undefined {
  const start = single(from) ?? "";
  const end = single(to) ?? "";
  const range = { from: isIsoDate(start) ? start : "", to: isIsoDate(end) ? end : "" };
  if (range.from === "" && range.to === "") return undefined;
  if (range.from !== "" && range.to !== "" && compareIsoDates(range.to, range.from) < 0) return undefined;
  return range;
}

function readFilter(parameters: SearchParameters, filter: ListFilterDefinition): ListFilterValue | undefined {
  switch (filter.kind) {
    case "text":
      return readText(parameters[filter.param], filter);
    case "options":
      return readOptions(parameters[filter.param], filter);
    case "dateRange":
      return readDateRange(parameters[`${filter.param}From`], parameters[`${filter.param}To`]);
  }
}

export function readFilters(parameters: SearchParameters, definition: ListDefinition): ListFilterValues {
  const filters: Record<string, ListFilterValue> = {};
  for (const filter of definition.filters) {
    const value = readFilter(parameters, filter);
    if (value !== undefined) filters[filter.param] = value;
  }
  return filters;
}

export function readHiddenColumns(
  value: string | readonly string[] | undefined,
  definition: ListDefinition,
): readonly string[] {
  const requested = new Set((single(value) ?? "").split(",").map((column) => column.trim()));
  const hidden = definition.hideableColumns.filter((column) => requested.has(column));
  return hidden.length === definition.columns.length ? [] : hidden;
}

function filterSearch(search: URLSearchParams, filter: ListFilterDefinition, value: ListFilterValue): void {
  if (typeof value === "string") {
    search.append(filter.param, value);
  } else if (isDateRange(value)) {
    if (value.from !== "") search.append(`${filter.param}From`, value.from);
    if (value.to !== "") search.append(`${filter.param}To`, value.to);
  } else {
    for (const option of value) search.append(filter.param, option);
  }
}

export function isDateRange(value: ListFilterValue): value is DateRangeValue {
  return typeof value === "object" && !Array.isArray(value);
}

export function listSearchParameters(query: ListQuery, definition: ListDefinition): URLSearchParams {
  const search = new URLSearchParams();
  for (const filter of definition.filters) {
    const value = query.filters[filter.param];
    if (value !== undefined) filterSearch(search, filter, value);
  }
  const { defaultSort } = definition;
  if (query.sort.field !== defaultSort.field || query.sort.direction !== defaultSort.direction) {
    search.append(listParameters.sort, `${query.sort.field}:${query.sort.direction}`);
  }
  if (query.pageSize !== definition.defaultPageSize)
    search.append(listParameters.pageSize, String(query.pageSize));
  if (query.page > 1) search.append(listParameters.page, String(query.page));
  if (query.hiddenColumns.length > 0) {
    search.append(listParameters.hiddenColumns, query.hiddenColumns.join(","));
  }
  return search;
}

export function listSearch(query: ListQuery, definition: ListDefinition): "" | `?${string}` {
  const search = listSearchParameters(query, definition).toString();
  return search === "" ? "" : `?${search}`;
}

export function listHref<TBasePath extends string>(
  basePath: TBasePath,
  query: ListQuery,
  definition: ListDefinition,
): TBasePath | `${TBasePath}?${string}` {
  const search = listSearch(query, definition);
  return search === "" ? basePath : `${basePath}${search}`;
}

export function listLink(basePath: Route, query: ListQuery, definition: ListDefinition): Route {
  const search = listSearch(query, definition);
  return search === "" ? basePath : search;
}

function entries(parameters: SearchParameters): string[] {
  return Object.entries(parameters)
    .flatMap(([name, value]) => values(value).map((item) => `${name}=${item}`))
    .sort();
}

export function readListQuery(parameters: SearchParameters, definition: ListDefinition): ReadListQuery {
  const pageSize = readPageSize(parameters[listParameters.pageSize], definition);
  const query: ListQuery = {
    page: readPageParameter(parameters[listParameters.page], pageSize) ?? 1,
    pageSize,
    sort: readSort(parameters[listParameters.sort], definition),
    filters: readFilters(parameters, definition),
    hiddenColumns: readHiddenColumns(parameters[listParameters.hiddenColumns], definition),
  };
  const canonical = [...listSearchParameters(query, definition)]
    .map(([name, value]) => `${name}=${value}`)
    .sort();
  const given = entries(parameters);
  return {
    query,
    canonical: canonical.length === given.length && canonical.every((entry, index) => entry === given[index]),
  };
}

export function withPage(query: ListQuery, page: number): ListQuery {
  return { ...query, page };
}

export function withPageSize(query: ListQuery, pageSize: number): ListQuery {
  return { ...query, pageSize, page: 1 };
}

export function withSort(query: ListQuery, sort: ListSort): ListQuery {
  return { ...query, sort, page: 1 };
}

export function withFilters(query: ListQuery, filters: ListFilterValues): ListQuery {
  return { ...query, filters, page: 1 };
}

export function withHiddenColumns(query: ListQuery, hiddenColumns: readonly string[]): ListQuery {
  return { ...query, hiddenColumns };
}

export function filtersFromForm(form: FormData, definition: ListDefinition): ListFilterValues {
  const parameters: Record<string, string[]> = {};
  for (const name of definition.filters.flatMap(filterParameters)) {
    parameters[name] = form.getAll(name).filter((value): value is string => typeof value === "string");
  }
  const flattened = Object.fromEntries(
    Object.entries(parameters).map(([name, given]) => [name, given.length === 1 ? given[0] : given]),
  );
  return readFilters(flattened, definition);
}

export function filterSignature(filters: ListFilterValues, definition: ListDefinition): string {
  return listSearch(
    {
      page: 1,
      pageSize: definition.defaultPageSize,
      sort: definition.defaultSort,
      filters,
      hiddenColumns: [],
    },
    definition,
  );
}
