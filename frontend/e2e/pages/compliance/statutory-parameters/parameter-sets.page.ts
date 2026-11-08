import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";

import { SearchedListRegion, type ShownRow } from "../../shared/lists/searched-list.page";

export const parameterSetsPath = "/compliance/statutory-parameters/parameter-sets";

export const parameterSetsApi = "/api/compliance/statutory-parameters/parameter-sets";

export const setsPerPage = 10;

export type ParameterSetBasis = "law" | "interpretation";

export type ParameterAsOf = "supplyDate" | "taxPeriodStart" | "deductionDate" | "financialYearStart";

export type VersionSummary = {
  readonly from: string;
  readonly until: string | null;
  readonly instruments: readonly string[];
  readonly addedCount: number;
  readonly changedCount: number;
  readonly removedCount: number;
};

export type ParameterSetSummary = {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly basis: ParameterSetBasis;
  readonly asOf: ParameterAsOf;
  readonly firstVersionFrom: string;
  readonly versionCount: number;
  readonly current: VersionSummary | null;
  readonly next: VersionSummary | null;
  readonly entryCount: number;
};

export const basisLabels: Readonly<Record<ParameterSetBasis, string>> = {
  law: "Government table",
  interpretation: "The ERP's reading",
};

const longDates = new Intl.DateTimeFormat("en-IN", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

const counts = new Intl.NumberFormat("en-IN");

export const quoted = JSON.stringify;

// A snapshot key names a role and its accessible name, and a name such as "GST rates on services: central tax (CGST)" holds a
// colon, so every key is written as a single-quoted YAML scalar, as Playwright writes the keys that need it.
export function yamlKey(text: string): string {
  return `'${text.replaceAll("'", "''")}'`;
}

export function parameterSetPath(key: string): string {
  return `${parameterSetsPath}/${key}`;
}

export function parameterSetApi(key: string): string {
  return `${parameterSetsApi}/${key}`;
}

export function longDate(isoDate: string): string {
  return longDates.format(new Date(`${isoDate}T00:00:00Z`));
}

export function fromText(isoDate: string): string {
  return `From ${longDate(isoDate)}`;
}

export function versionRange(version: { readonly from: string; readonly until: string | null }): string {
  return version.until === null
    ? fromText(version.from)
    : `${fromText(version.from)} to ${longDate(version.until)}`;
}

export function changeCounts(version: VersionSummary): string {
  return [
    `${counts.format(version.addedCount)} added`,
    `${counts.format(version.changedCount)} changed`,
    `${counts.format(version.removedCount)} removed`,
  ].join(" · ");
}

export function entryCountText(entries: number): string {
  if (entries === 0) return "None";
  return entries === 1 ? "1 entry" : `${counts.format(entries)} entries`;
}

export async function readParameterSets(request: APIRequestContext): Promise<ParameterSetSummary[]> {
  const response = await request.get(parameterSetsApi);
  expect(response.status(), parameterSetsApi).toBe(200);
  const { sets } = (await response.json()) as { sets?: readonly ParameterSetSummary[] };
  expect(sets?.length, "the statutory parameter sets the API answers").toBeGreaterThan(0);
  return [...(sets ?? [])];
}

// A set as the list shows it, built from the API's own answer: its title, whether it is a government table or the ERP's
// reading, the version in force today, the next change and the entries in force today.
export function setRow(set: ParameterSetSummary): ShownRow {
  return {
    Set: set.title,
    Kind: basisLabels[set.basis],
    "In force today": set.current === null ? "Nothing is in force yet." : fromText(set.current.from),
    "Next change": set.next === null ? "None" : fromText(set.next.from),
    "Entries in force today": entryCountText(set.entryCount),
  };
}

// What a search finds a set by: its key, and the words of its title and description.
export function setKeys(set: ParameterSetSummary): { readonly code: string; readonly name: string } {
  return { code: set.key, name: `${set.title} ${set.description}` };
}

export function setsShownCount(total: number): string {
  if (total === 1) return "Showing 1 set.";
  if (total <= setsPerPage) return `Showing all ${counts.format(total)} sets.`;
  return `Showing 1–${counts.format(setsPerPage)} of ${counts.format(total)} sets.`;
}

export class ParameterSetsPage {
  readonly heading: Locator;
  readonly overview: Locator;
  readonly region: Locator;
  readonly entries: SearchedListRegion;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Statutory parameters", level: 1 });
    this.overview = page.getByTestId("parameter-sets-overview");
    this.region = page.getByRole("region", { name: "Parameter sets", exact: true });
    this.entries = new SearchedListRegion(this.region, {
      label: "Parameter sets",
      searchLabel: "Title or words",
      titleHeading: "Set",
    });
    this.unavailable = page.getByTestId("parameter-sets-unavailable");
  }

  async goto(address = parameterSetsPath): Promise<void> {
    await this.page.goto(address);
    await expect(this.heading).toBeVisible();
  }

  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  // The list draws each set's link twice, in its table and in its cards, and shows one of them by the width of its box.
  setLink(title: string): Locator {
    return this.region.getByRole("link", { name: title, exact: true }).filter({ visible: true });
  }
}
