import type {
  ParameterAsOf,
  ParameterCellResponse,
  ParameterColumnKind,
  ParameterColumnResponse,
  ParameterRowResponse,
  ParameterSetBasis,
  ParameterSetResponse,
  ParameterSetSummaryResponse,
  ParameterVersionResponse,
  ParameterVersionSummaryResponse,
} from "@dewiride/erp-api-client";

import { isoDateOf, type ApiDate } from "@/shared/api/dates/date-only";

export interface VersionSummary {
  readonly from: string;
  readonly until: string | null;
  readonly instruments: readonly string[];
  readonly addedCount: number;
  readonly changedCount: number;
  readonly removedCount: number;
}

export interface ParameterSetSummary {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly basis: ParameterSetBasis;
  readonly asOf: ParameterAsOf;
  readonly current: VersionSummary | null;
  readonly next: VersionSummary | null;
  readonly entryCount: number;
}

export interface ParameterChoice {
  readonly value: string;
  readonly title: string;
}

export interface ParameterColumn {
  readonly name: string;
  readonly title: string;
  readonly kind: ParameterColumnKind;
  readonly isKey: boolean;
  readonly isRowTitle: boolean;
  readonly choices: readonly ParameterChoice[];
}

export interface ParameterCell {
  readonly column: string;
  readonly value: string | null;
  readonly stateName: string | null;
}

export interface ParameterRow {
  readonly key: string;
  readonly from: string;
  readonly until: string | null;
  readonly source: string;
  readonly cells: readonly ParameterCell[];
}

export interface RowChange {
  readonly before: ParameterRow;
  readonly after: ParameterRow;
}

export interface ShownVersion {
  readonly from: string;
  readonly until: string | null;
  readonly instruments: readonly string[];
  readonly rows: readonly ParameterRow[];
  readonly added: readonly ParameterRow[];
  readonly changed: readonly RowChange[];
  readonly removed: readonly ParameterRow[];
}

export interface ParameterSetDetail {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly basis: ParameterSetBasis;
  readonly asOf: ParameterAsOf;
  readonly columns: readonly ParameterColumn[];
  readonly versions: readonly VersionSummary[];
  readonly currentFrom: string | null;
  readonly shown: ShownVersion | null;
}

// The API's contract names every member below as present, so a member that arrives without a value means the answer cannot be
// shown faithfully, and the page reports the set as unreadable rather than show part of it.
function required<T>(value: T | null | undefined, member: string): T {
  if (value === null || value === undefined || value === "") {
    throw new TypeError(`The API answered a parameter set without its ${member}.`);
  }
  return value;
}

// Kiota reads a JSON null where an object belongs as an object without members, so such an object is the API's null.
function present<T extends object>(value: T | null | undefined): T | null {
  return value === null || value === undefined || Object.keys(value).length === 0 ? null : value;
}

function date(value: ApiDate | null | undefined, member: string): string {
  return required(isoDateOf(value), member);
}

function optionalDate(value: ApiDate | null | undefined): string | null {
  return isoDateOf(value) ?? null;
}

function versionSummaryOf(version: ParameterVersionSummaryResponse): VersionSummary {
  return {
    from: date(version.from, "version's first day"),
    until: optionalDate(version.until),
    instruments: [...(version.instruments ?? [])],
    addedCount: required(version.addedCount, "count of added entries"),
    changedCount: required(version.changedCount, "count of changed entries"),
    removedCount: required(version.removedCount, "count of removed entries"),
  };
}

function optionalVersionSummaryOf(
  version: ParameterVersionSummaryResponse | null | undefined,
): VersionSummary | null {
  const answered = present(version);
  return answered === null ? null : versionSummaryOf(answered);
}

function cellOf(cell: ParameterCellResponse): ParameterCell {
  return {
    column: required(cell.column, "cell's column"),
    value: cell.value ?? null,
    stateName: cell.stateName ?? null,
  };
}

function rowOf(row: ParameterRowResponse | null | undefined): ParameterRow {
  const answered = required(present(row), "entry");
  return {
    key: required(answered.key, "entry's key"),
    from: date(answered.from, "entry's first day"),
    until: optionalDate(answered.until),
    source: required(answered.source, "entry's source"),
    cells: (answered.cells ?? []).map(cellOf),
  };
}

function columnOf(column: ParameterColumnResponse): ParameterColumn {
  return {
    name: required(column.name, "column's name"),
    title: required(column.title, "column's title"),
    kind: required(column.kind, "column's kind"),
    isKey: column.isKey === true,
    isRowTitle: column.isRowTitle === true,
    choices: (column.choices ?? []).map((choice) => ({
      value: required(choice.value, "choice's value"),
      title: required(choice.title, "choice's title"),
    })),
  };
}

function shownVersionOf(answered: ParameterVersionResponse | null | undefined): ShownVersion | null {
  const version = present(answered);
  if (version === null) return null;
  return {
    from: date(version.from, "version's first day"),
    until: optionalDate(version.until),
    instruments: [...(version.instruments ?? [])],
    rows: (version.rows ?? []).map(rowOf),
    added: (version.added ?? []).map(rowOf),
    changed: (version.changed ?? []).map((change) => ({
      before: rowOf(change.before),
      after: rowOf(change.after),
    })),
    removed: (version.removed ?? []).map(rowOf),
  };
}

export function setSummaryOf(set: ParameterSetSummaryResponse): ParameterSetSummary {
  return {
    key: required(set.key, "key"),
    title: required(set.title, "title"),
    description: required(set.description, "description"),
    basis: required(set.basis, "basis"),
    asOf: required(set.asOf, "dates' meaning"),
    current: optionalVersionSummaryOf(set.current),
    next: optionalVersionSummaryOf(set.next),
    entryCount: required(set.entryCount, "count of entries in force today"),
  };
}

export function setDetailOf(set: ParameterSetResponse): ParameterSetDetail {
  return {
    key: required(set.key, "key"),
    title: required(set.title, "title"),
    description: required(set.description, "description"),
    basis: required(set.basis, "basis"),
    asOf: required(set.asOf, "dates' meaning"),
    columns: (set.columns ?? []).map(columnOf),
    versions: (set.versions ?? []).map(versionSummaryOf),
    currentFrom: optionalDate(set.currentFrom),
    shown: shownVersionOf(set.shown),
  };
}
