import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";

export const parameterSetsPath = "/compliance/statutory-parameters/parameter-sets";

export const parameterSetsApi = "/api/compliance/statutory-parameters/parameter-sets";

export type ParameterSetBasis = "law" | "interpretation";

export type ParameterAsOf = "supplyDate" | "taxPeriodStart";

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

function versionSnapshot(version: VersionSummary): string[] {
  return [
    `      - text: ${quoted(fromText(version.from))}`,
    ...(version.instruments.length === 0
      ? []
      : [
          "      - list:",
          ...version.instruments.map((instrument) => `        - listitem: ${quoted(instrument)}`),
        ]),
  ];
}

// The list as a person meets it, built from the API's own answer: each set's title as a link to its page, whether it is a
// government table or the ERP's reading, its description, the version in force today with its instruments, the next
// change when one is coming, and the entries in force today.
export function parameterSetsSnapshot(sets: readonly ParameterSetSummary[]): string {
  return [
    `- list "Parameter sets":`,
    ...sets.flatMap((set) => [
      "  - listitem:",
      `    - ${yamlKey(`heading ${quoted(set.title)} [level=2]`)}:`,
      `      - ${yamlKey(`link ${quoted(set.title)}`)}:`,
      `        - /url: ${parameterSetPath(set.key)}`,
      `    - text: ${quoted(basisLabels[set.basis])}`,
      `    - paragraph: ${quoted(set.description)}`,
      "    - term: In force today",
      ...(set.current === null
        ? ["    - definition: Nothing is in force yet."]
        : ["    - definition:", ...versionSnapshot(set.current)]),
      ...(set.next === null
        ? []
        : ["    - term: Next change", "    - definition:", ...versionSnapshot(set.next)]),
      "    - term: Entries in force today",
      `    - definition: ${quoted(entryCountText(set.entryCount))}`,
    ]),
  ].join("\n");
}

export class ParameterSetsPage {
  readonly heading: Locator;
  readonly overview: Locator;
  readonly list: Locator;
  readonly cards: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Statutory parameters", level: 1 });
    this.overview = page.getByTestId("parameter-sets-overview");
    this.list = page.getByRole("list", { name: "Parameter sets", exact: true });
    this.cards = page.getByTestId("parameter-set-card");
    this.unavailable = page.getByTestId("parameter-sets-unavailable");
  }

  async goto(): Promise<void> {
    await this.page.goto(parameterSetsPath);
    await expect(this.heading).toBeVisible();
  }

  card(title: string): Locator {
    return this.cards.filter({ has: this.page.getByRole("heading", { name: title, exact: true, level: 2 }) });
  }

  cardLink(title: string): Locator {
    return this.card(title).getByRole("link", { name: title, exact: true });
  }
}
