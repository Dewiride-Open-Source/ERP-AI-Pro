export type ListSortDirection = "asc" | "desc";

export interface ListSort {
  readonly field: string;
  readonly direction: ListSortDirection;
}

export interface TextFilterDefinition {
  readonly kind: "text";
  readonly param: string;
  readonly field: string;
  readonly operator: "contains" | "eq";
  readonly maxLength: number;
}

export interface OptionsFilterDefinition {
  readonly kind: "options";
  readonly param: string;
  readonly field: string;
  readonly values?: readonly string[] | undefined;
}

export interface DateRangeFilterDefinition {
  readonly kind: "dateRange";
  readonly param: string;
  readonly field: string;
  readonly instant: boolean;
}

export type ListFilterDefinition = TextFilterDefinition | OptionsFilterDefinition | DateRangeFilterDefinition;

export interface ListDefinition {
  readonly pageSizes: readonly number[];
  readonly defaultPageSize: number;
  readonly sortFields: readonly string[];
  readonly defaultSort: ListSort;
  readonly filters: readonly ListFilterDefinition[];
  readonly columns: readonly string[];
  readonly hideableColumns: readonly string[];
}

export const listParameters = {
  page: "page",
  pageSize: "size",
  sort: "sort",
  hiddenColumns: "hide",
} as const;

export const maxOptionValueLength = 200;

export const maxOptionValues = 100;

const apiMaxPageSize = 200;

const apiMaxFilterTerms = 10;

const fieldName = /^[A-Za-z][A-Za-z0-9]*$/;

const parameterName = /^[a-z][A-Za-z0-9]*$/;

export function filterParameters(filter: ListFilterDefinition): readonly string[] {
  return filter.kind === "dateRange" ? [`${filter.param}From`, `${filter.param}To`] : [filter.param];
}

function filterTermCount(filter: ListFilterDefinition): number {
  return filter.kind === "dateRange" ? 2 : 1;
}

function hasDuplicates(values: readonly string[]): boolean {
  return new Set(values).size !== values.length;
}

function definitionProblem(definition: ListDefinition): string | undefined {
  const { pageSizes, defaultPageSize, sortFields, defaultSort, filters, columns, hideableColumns } =
    definition;
  if (pageSizes.length === 0) return "pageSizes is empty";
  if (
    pageSizes.some(
      (size, index) =>
        !Number.isInteger(size) || size < 1 || size > apiMaxPageSize || size <= (pageSizes[index - 1] ?? 0),
    )
  ) {
    return `pageSizes must be whole numbers from 1 to ${apiMaxPageSize} in ascending order`;
  }
  if (!pageSizes.includes(defaultPageSize)) return "defaultPageSize is not one of pageSizes";
  if (sortFields.some((field) => !fieldName.test(field)) || hasDuplicates(sortFields)) {
    return "sortFields must be distinct API field names";
  }
  if (!sortFields.includes(defaultSort.field)) return "defaultSort.field is not one of sortFields";
  if (filters.some((filter) => !fieldName.test(filter.field)))
    return "a filter field is not an API field name";
  if (
    filters.some(
      (filter) => filter.kind === "text" && (!Number.isInteger(filter.maxLength) || filter.maxLength < 1),
    )
  ) {
    return "a text filter needs a positive whole maxLength";
  }
  if (filters.reduce((terms, filter) => terms + filterTermCount(filter), 0) > apiMaxFilterTerms) {
    return `the filters can send more than ${apiMaxFilterTerms} terms, the API's limit`;
  }
  const parameters = [...Object.values(listParameters), ...filters.flatMap(filterParameters)];
  if (parameters.some((name) => !parameterName.test(name)) || hasDuplicates(parameters)) {
    return "filter parameters must be distinct camelCase names that differ from page, size, sort and hide";
  }
  if (
    hasDuplicates(columns) ||
    hideableColumns.some((column) => !columns.includes(column)) ||
    hasDuplicates(hideableColumns)
  ) {
    return "hideableColumns must be distinct entries of columns";
  }
  return undefined;
}

export function defineList<const TDefinition extends ListDefinition>(definition: TDefinition): TDefinition {
  const problem = definitionProblem(definition);
  if (problem !== undefined) throw new Error(`Invalid list definition: ${problem}.`);
  return definition;
}
