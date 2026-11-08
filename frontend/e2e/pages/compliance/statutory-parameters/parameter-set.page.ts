import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";

import { SearchedListRegion, type ShownRow } from "../../shared/lists/searched-list.page";

import {
  basisLabels,
  changeCounts,
  longDate,
  longDayMonth,
  parameterSetApi,
  parameterSetPath,
  quoted,
  versionRange,
  yamlKey,
  type ParameterAsOf,
  type ParameterSetBasis,
  type VersionSummary,
} from "./parameter-sets.page";

export const centralTaxRatesKey = "gst.central-tax-rates";

export const sacCodesKey = "gst.sac-codes";

export const tdsEntriesKey = "income-tax.tds-entries";

export const tdsCrosswalkKey = "income-tax.tds-crosswalk";

export const companyRatesKey = "income-tax.company-rates";

export const usefulLivesKey = "companies.schedule-ii-useful-lives";

export const professionalTaxSlabsKey = "professional-tax.salary-slabs";

export const esiContributionPeriodsKey = "esi.contribution-periods";

export const welfareFundContributionsKey = "labour-welfare-fund.contributions";

export const labourSetPrefixes = [
  "bonus.",
  "esi.",
  "gratuity.",
  "labour-codes.",
  "labour-welfare-fund.",
  "professional-tax.",
  "provident-fund.",
];

export const entriesPerPage = 10;

export type ParameterColumnKind =
  "text" | "code" | "integer" | "percent" | "amount" | "choice" | "stateCode" | "date" | "dayMonth";

export type ParameterColumn = {
  readonly name: string;
  readonly title: string;
  readonly kind: ParameterColumnKind;
  readonly isKey: boolean;
  readonly isRowTitle: boolean;
  readonly isOptional: boolean;
  readonly choices: readonly { readonly value: string; readonly title: string }[];
};

export type ParameterRow = {
  readonly key: string;
  readonly from: string;
  readonly until: string | null;
  readonly source: string;
  readonly cells: readonly {
    readonly column: string;
    readonly value: string | null;
    readonly stateName: string | null;
  }[];
};

export type RowChange = { readonly before: ParameterRow; readonly after: ParameterRow };

export type ShownVersion = {
  readonly from: string;
  readonly until: string | null;
  readonly instruments: readonly string[];
  readonly rows: readonly ParameterRow[];
  readonly added: readonly ParameterRow[];
  readonly changed: readonly RowChange[];
  readonly removed: readonly ParameterRow[];
};

export type ParameterSet = {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly basis: ParameterSetBasis;
  readonly asOf: ParameterAsOf;
  readonly columns: readonly ParameterColumn[];
  readonly versions: readonly VersionSummary[];
  readonly currentFrom: string | null;
  readonly shown: ShownVersion | null;
};

export type ShownValue = { readonly label: string; readonly value: string; readonly was?: string };

export const asOfTexts: Readonly<Record<ParameterAsOf, string>> = {
  supplyDate: "Each version applies to supplies made on or after the day it starts.",
  taxPeriodStart:
    "Each version applies to the tax periods that begin on or after the day it starts: a month, a quarter or a financial year.",
  deductionDate:
    "Each version applies to tax deducted on or after the day it starts: on the credit or the payment at which the Act requires the deduction.",
  financialYearStart: "Each version applies to the financial years that begin on or after the day it starts.",
  wageDate: "Each version applies to wages earned on or after the day it starts.",
  wagePeriodEnd: "Each version applies to the wage periods that end on or after the day it starts.",
  employmentEndDate: "Each version applies to employment that ends on or after the day it starts.",
  accountingYearEnd: "Each version applies to the accounting years that end on or after the day it starts.",
  contributionPeriodEnd:
    "Each version applies to the contribution periods that end on or after the day it starts.",
};

const indianGrouping = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2, minimumFractionDigits: 0 });

const withPaise = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2, minimumFractionDigits: 2 });

export function parameterSetAddress(
  key: string,
  { version, q, page }: { readonly version?: string; readonly q?: string; readonly page?: number } = {},
): string {
  const search = new URLSearchParams();
  if (version !== undefined) search.set("version", version);
  if (q !== undefined) search.set("q", q);
  if (page !== undefined && page > 1) search.set("page", String(page));
  const query = search.toString();
  return query === "" ? parameterSetPath(key) : `${parameterSetPath(key)}?${query}`;
}

export async function readParameterSet(
  request: APIRequestContext,
  key: string,
  on?: string,
): Promise<ParameterSet> {
  const path = on === undefined ? parameterSetApi(key) : `${parameterSetApi(key)}?on=${on}`;
  const response = await request.get(path);
  expect(response.status(), path).toBe(200);
  return (await response.json()) as ParameterSet;
}

export function valueOf(row: ParameterRow, column: string): string | null {
  return row.cells.find((cell) => cell.column === column)?.value ?? null;
}

// How each kind of value reads on the page, written apart from the page's own code: a percentage with its sign, rupees grouped
// in lakhs and crores with paise only when there are some, a date and a day of the year in words, a choice by its title, a GST
// State code after the name of its State and an empty value as None.
export function cellText(
  column: ParameterColumn,
  value: string | null,
  stateName: string | null = null,
): string {
  if (value === null) return "None";
  switch (column.kind) {
    case "percent":
      return `${value}%`;
    case "amount":
      return `₹${(value.includes(".") ? withPaise : indianGrouping).format(Number(value))}`;
    case "date":
      return longDate(value);
    case "dayMonth":
      return longDayMonth(value);
    case "stateCode":
      return stateName === null ? value : `${stateName} (${value})`;
    case "choice":
      return column.choices.find((choice) => choice.value === value)?.title ?? value;
    default:
      return value;
  }
}

export function rowCellText(column: ParameterColumn, row: ParameterRow): string {
  const cell = row.cells.find((candidate) => candidate.column === column.name);
  return cellText(column, cell?.value ?? null, cell?.stateName ?? null);
}

export function titleColumn(set: ParameterSet): ParameterColumn {
  const column =
    set.columns.find((candidate) => candidate.isRowTitle) ?? set.columns.find((candidate) => candidate.isKey);
  expect(column, `the column the entries of ${set.key} are titled by`).toBeDefined();
  return column as ParameterColumn;
}

export function entryRow(set: ParameterSet, row: ParameterRow): ShownRow {
  return {
    ...Object.fromEntries(set.columns.map((column) => [column.title, rowCellText(column, row)])),
    Source: row.source,
  };
}

// What a person may search an entry by: its key, and the words of its text, its codes, the titles of its choices and its States.
export function entryKeys(
  set: ParameterSet,
  row: ParameterRow,
): { readonly code: string; readonly name: string } {
  return {
    code: row.key,
    name: set.columns
      .filter(
        (column) =>
          ["text", "code", "choice", "stateCode"].includes(column.kind) && valueOf(row, column.name) !== null,
      )
      .map((column) => rowCellText(column, row))
      .join(" "),
  };
}

export function entryTitle(set: ParameterSet, row: ParameterRow): string {
  const rowTitle = set.columns.find((column) => column.isRowTitle);
  if (rowTitle !== undefined) return rowCellText(rowTitle, row);
  const key = set.columns.find((column) => column.isKey);
  return key === undefined ? row.key : `${key.title} ${row.key}`;
}

export function entryValues(set: ParameterSet, row: ParameterRow): ShownValue[] {
  const title = titleColumn(set);
  return [
    ...set.columns
      .filter((column) => column !== title)
      .map((column) => ({ label: column.title, value: rowCellText(column, row) })),
    { label: "Source", value: row.source },
  ];
}

export function changedValues(set: ParameterSet, change: RowChange): ShownValue[] {
  const values: ShownValue[] = set.columns
    .filter((column) => valueOf(change.before, column.name) !== valueOf(change.after, column.name))
    .map((column) => ({
      label: column.title,
      value: rowCellText(column, change.after),
      was: rowCellText(column, change.before),
    }));
  return change.before.source === change.after.source
    ? values
    : [...values, { label: "Source", value: change.after.source, was: change.before.source }];
}

function standing(set: ParameterSet, version: VersionSummary): string | undefined {
  if (version.from === set.currentFrom) return "In force today";
  return set.currentFrom === null || version.from > set.currentFrom ? "Upcoming" : undefined;
}

export function versionAddress(set: ParameterSet, version: VersionSummary): string {
  return version.from === set.currentFrom
    ? parameterSetPath(set.key)
    : parameterSetAddress(set.key, { version: version.from });
}

// The versions as a person meets them, newest first, built from the API's own answer: each one's days as a link to it, its
// standing today, its instruments and what it added, changed and removed.
export function versionsSnapshot(set: ParameterSet): string {
  return [
    "- list:",
    ...set.versions.flatMap((version) => {
      const badge = standing(set, version);
      return [
        "  - listitem:",
        `    - ${yamlKey(`link ${quoted(versionRange(version))}`)}:`,
        `      - /url: ${versionAddress(set, version)}`,
        ...(badge === undefined ? [] : [`    - text: ${quoted(badge)}`]),
        ...(version.instruments.length === 0
          ? []
          : [
              "    - list:",
              ...version.instruments.map((instrument) => `      - listitem: ${quoted(instrument)}`),
            ]),
        `    - paragraph: ${quoted(changeCounts(version))}`,
      ];
    }),
  ].join("\n");
}

export function shownText(value: ShownValue): string {
  return value.was === undefined ? value.value : `${value.value} (was ${value.was})`;
}

export function changeEntrySnapshot(title: string, values: readonly ShownValue[]): string {
  return [
    "- listitem:",
    `  - paragraph: ${quoted(title)}`,
    ...values.flatMap((value) => [
      `  - term: ${quoted(value.label)}`,
      `  - definition: ${quoted(shownText(value))}`,
    ]),
  ].join("\n");
}

export class ParameterSetPage {
  readonly heading: Locator;
  readonly overview: Locator;
  readonly basis: Locator;
  readonly description: Locator;
  readonly asOf: Locator;
  readonly versionsCard: Locator;
  readonly versions: Locator;
  readonly entriesCard: Locator;
  readonly entriesHeading: Locator;
  readonly changesCard: Locator;
  readonly firstVersion: Locator;
  readonly added: Locator;
  readonly changed: Locator;
  readonly removed: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("main").getByRole("heading", { level: 1 });
    this.overview = page.getByTestId("parameter-set-overview");
    this.basis = this.overview.getByTestId("parameter-set-basis");
    this.description = page.getByTestId("parameter-set-description");
    this.asOf = page.getByTestId("parameter-set-as-of");
    this.versionsCard = page.getByTestId("parameter-set-versions-card");
    this.versions = page.getByTestId("parameter-set-versions");
    this.entriesCard = page.getByTestId("parameter-set-entries");
    this.entriesHeading = this.entriesCard.getByRole("heading", { name: "Entries", level: 2 });
    this.changesCard = page.getByTestId("parameter-set-changes");
    this.firstVersion = page.getByTestId("parameter-set-first-version");
    this.added = page.getByTestId("parameter-set-added");
    this.changed = page.getByTestId("parameter-set-changed");
    this.removed = page.getByTestId("parameter-set-removed");
    this.unavailable = page.getByTestId("parameter-set-unavailable");
  }

  async goto(address: string, title: string): Promise<void> {
    await this.page.goto(address);
    await expect(this.heading).toHaveText(title);
  }

  // The entries and the version links work through React, so they mark themselves as rendering the server's state until the
  // page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  entries(set: ParameterSet): SearchedListRegion {
    return new SearchedListRegion(this.entriesCard, {
      label: "Entries",
      searchLabel: "Code or words",
      titleHeading: titleColumn(set).title,
    });
  }

  versionLink(version: VersionSummary): Locator {
    return this.versions.getByRole("link", { name: versionRange(version), exact: true });
  }

  changeEntries(section: Locator): Locator {
    return section.getByTestId("parameter-set-change");
  }
}

export function basisLabel(basis: ParameterSetBasis): string {
  return basisLabels[basis];
}
