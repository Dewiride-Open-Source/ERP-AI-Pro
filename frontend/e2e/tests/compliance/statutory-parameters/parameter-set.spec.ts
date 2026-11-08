import type { Locator, Page, Route } from "@playwright/test";

import { expect, forEachTheme, tabOntoLink, test } from "../../../fixtures/test";
import {
  asOfTexts,
  basisLabel,
  bonusStateReadingsKey,
  centralTaxRatesKey,
  changedValues,
  changeEntrySnapshot,
  entriesPerPage,
  entryKeys,
  entryRow,
  entryTitle,
  entryValues,
  parameterSetAddress,
  ParameterSetPage,
  readParameterSet,
  cellText,
  companyRatesKey,
  esiContributionPeriodsKey,
  labourSetKeys,
  labourSetPrefixes,
  professionalTaxSlabsKey,
  sacCodesKey,
  tdsCrosswalkKey,
  tdsEntriesKey,
  usefulLivesKey,
  valueOf,
  versionAddress,
  versionsSnapshot,
  welfareFundContributionsKey,
  type ParameterSet,
  type ShownValue,
  type ShownVersion,
} from "../../../pages/compliance/statutory-parameters/parameter-set.page";
import {
  parameterSetApi,
  parameterSetPath,
  readParameterSets,
  yamlKey,
  type VersionSummary,
} from "../../../pages/compliance/statutory-parameters/parameter-sets.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";
import { expectedMatches } from "../../../pages/shared/lists/searched-list.page";

const counts = new Intl.NumberFormat("en-IN");

const noMatch = "zz-no-such-entry";

function shownCount(total: number): string {
  if (total === 0) return "No entries to show.";
  if (total === 1) return "Showing 1 entry.";
  if (total <= entriesPerPage) return `Showing all ${counts.format(total)} entries.`;
  return `Showing 1–${counts.format(entriesPerPage)} of ${counts.format(total)} entries.`;
}

// The address writes its parameters as a form does, so brackets and spaces are escaped as a form escapes them.
function query(parameters: Readonly<Record<string, string>>): string {
  return `?${new URLSearchParams(parameters).toString()}`;
}

function dayAfter(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() + 1);
  return day.toISOString().slice(0, 10);
}

function shownVersionOf(set: ParameterSet): ShownVersion {
  expect(set.shown, `the version of ${set.key} the API shows`).not.toBeNull();
  return set.shown as ShownVersion;
}

function versionOf(
  set: ParameterSet,
  matches: (version: VersionSummary) => boolean,
  what: string,
): VersionSummary {
  const version = set.versions.find(matches);
  expect(version, what).toBeDefined();
  return version as VersionSummary;
}

function sectionSnapshot(
  title: string,
  entries: readonly { title: string; values: readonly ShownValue[] }[],
): string {
  return [
    `- ${yamlKey(`heading ${JSON.stringify(title)} [level=3]`)}`,
    "- list:",
    ...entries.map((entry) =>
      changeEntrySnapshot(entry.title, entry.values)
        .split("\n")
        .map((line) => `  ${line}`)
        .join("\n"),
    ),
  ].join("\n");
}

async function expectSection(
  section: Locator,
  title: string,
  entries: readonly { title: string; values: readonly ShownValue[] }[],
): Promise<void> {
  if (entries.length === 0) {
    await expect(section, title).toHaveCount(0);
    return;
  }
  await expect(section, title).toMatchAriaSnapshot(sectionSnapshot(title, entries));
}

// What a version changed, built from the API's own answer for that version: the first version says it is the first, and any
// later one lists the entries it added and removed with their values, and the entries it changed with each changed value
// beside the one before it.
async function expectChanges(
  setPage: ParameterSetPage,
  set: ParameterSet,
  shown: ShownVersion,
): Promise<void> {
  if (set.versions.at(-1)?.from === shown.from) {
    await expect(setPage.firstVersion).toHaveText("This is the first version of the set.");
    for (const section of [setPage.added, setPage.changed, setPage.removed])
      await expect(section).toHaveCount(0);
    await expect(setPage.changeEntries(setPage.changesCard)).toHaveCount(0);
    return;
  }
  await expect(setPage.firstVersion).toHaveCount(0);
  await expectSection(
    setPage.added,
    "Added",
    shown.added.map((row) => ({ title: entryTitle(set, row), values: entryValues(set, row) })),
  );
  await expectSection(
    setPage.changed,
    "Changed",
    shown.changed.map((change) => ({
      title: entryTitle(set, change.after),
      values: changedValues(set, change),
    })),
  );
  await expectSection(
    setPage.removed,
    "Removed",
    shown.removed.map((row) => ({ title: entryTitle(set, row), values: entryValues(set, row) })),
  );
}

async function holdSetNavigation(page: Page, key: string): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    (url) => url.pathname === parameterSetPath(key),
    async (route: Route) => {
      if (route.request().headers()["rsc"] !== "1") {
        await route.fallback();
        return;
      }
      await released;
      await route.fallback();
    },
  );
  return release;
}

// The word the most entries of a version hold, so a search for it finds more than one page of them.
function commonWord(set: ParameterSet, shown: ShownVersion): string {
  const tally = new Map<string, number>();
  for (const row of shown.rows) {
    const words = new Set(
      entryKeys(set, row)
        .name.toLowerCase()
        .match(/\p{L}{5,}/gu) ?? [],
    );
    for (const word of words) tally.set(word, (tally.get(word) ?? 0) + 1);
  }
  const [word] =
    [...tally.entries()].sort(([a, first], [b, second]) => second - first || a.localeCompare(b))[0] ?? [];
  expect(word, `a word the entries of ${set.key} share`).toBeDefined();
  return word as string;
}

test.describe("a statutory parameter set", () => {
  test.use({ persona: "administrator" });

  forEachTheme(
    "shows the central tax set with its versions, the entries in force today and what their version changed",
    async ({ page, capture }, isMobile) => {
      const set = await readParameterSet(page.request, centralTaxRatesKey);
      const shown = shownVersionOf(set);
      const today = versionOf(
        set,
        (version) => version.from === set.currentFrom,
        "the version in force today",
      );
      const setPage = new ParameterSetPage(page);
      const entries = setPage.entries(set);

      await setPage.goto(parameterSetPath(set.key), set.title);
      await setPage.waitUntilInteractive();
      await expect(page).toHaveTitle(`${set.title} · ERP-AI-Pro`);
      await expect(setPage.basis).toHaveText(basisLabel(set.basis));
      await expect(setPage.description).toHaveText(set.description);
      await expect(setPage.asOf).toHaveText(asOfTexts[set.asOf]);
      await expect(setPage.versions).toMatchAriaSnapshot(versionsSnapshot(set));
      await expect(setPage.versionLink(today)).toHaveAttribute("aria-current", "page");
      for (const version of set.versions.filter((candidate) => candidate !== today)) {
        await expect(setPage.versionLink(version), version.from).not.toHaveAttribute("aria-current");
      }
      await expect(entries.status).toHaveText(shownCount(shown.rows.length));
      expect(await entries.rows()).toEqual(
        shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
      );
      await expectChanges(setPage, set, shown);
      // A phone stacks the versions, the entries and the changes in one tall column, so it captures each card apart, which
      // keeps every capture below the height a capture may have as the sets gain versions.
      if (isMobile) {
        await capture("central-tax-rates-versions", setPage.versionsCard);
        await capture("central-tax-rates-entries", setPage.entriesCard);
        await capture("central-tax-rates-changes", setPage.changesCard);
      } else {
        await capture("central-tax-rates");
      }
    },
  );

  test("follows an earlier version by keyboard, busy until it arrives, and searches its entries", async ({
    page,
  }) => {
    const set = await readParameterSet(page.request, centralTaxRatesKey);
    const earlier = versionOf(
      set,
      (version) => version.from !== set.currentFrom && version.addedCount > 0 && version.changedCount > 0,
      "an earlier version that added and changed entries",
    );
    const shown = shownVersionOf(await readParameterSet(page.request, set.key, earlier.from));
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);
    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();

    const release = await holdSetNavigation(page, set.key);
    await tabOntoLink(page, setPage.versionLink(earlier));
    await page.keyboard.press("Enter");
    await expect(setPage.entriesHeading).toBeFocused();
    await expect(entries.status).toHaveText("Loading…");
    await expect(setPage.versions).toHaveAttribute("aria-busy", "true");
    release();
    await expect(page).toHaveURL(
      (url) => url.pathname === parameterSetPath(set.key) && url.search === `?version=${earlier.from}`,
    );
    await expect(setPage.versions).not.toHaveAttribute("aria-busy");
    await expect(setPage.versionLink(earlier)).toHaveAttribute("aria-current", "page");
    await expect(setPage.entriesHeading).toBeFocused();
    await expect(entries.status).toHaveText(shownCount(shown.rows.length));
    await expect.poll(() => entries.rows()).toEqual(shown.rows.map((row) => entryRow(set, row)));
    await expectChanges(setPage, set, shown);

    const target = shown.rows.at(-1);
    expect(target, "an entry of the earlier version").toBeDefined();
    const found = expectedMatches(shown.rows, target!.key, (row) => entryKeys(set, row));
    await entries.searchFor(target!.key);
    await expect(page).toHaveURL(
      (url) => url.searchParams.get("version") === earlier.from && url.searchParams.get("q") === target!.key,
    );
    await expect.poll(() => entries.rows()).toEqual(found.map((row) => entryRow(set, row)));
    expect((await entries.rows())[0], "the entry whose key was typed in full").toEqual(
      entryRow(set, target!),
    );
    await expect(entries.searchBox).toBeFocused();

    await entries.searchFor(noMatch);
    await expect(entries.noMatches).toContainText("No entries match this search.");
    await expect(entries.status).toHaveText("No entries to show.");
    await entries.noMatches.getByRole("link", { name: "Clear the search", exact: true }).click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}`);
    await expect(entries.searchBox).toHaveValue("");
    await expect(entries.searchBox).toBeFocused();
  });

  test("says the first version of the central tax set is its first and lists no change for it", async ({
    page,
  }) => {
    const set = await readParameterSet(page.request, centralTaxRatesKey);
    const first = set.versions.at(-1);
    expect(first, "the first version").toBeDefined();
    const shown = shownVersionOf(await readParameterSet(page.request, set.key, first!.from));
    expect(shown.from, "the version the API shows on the first version's first day").toBe(first!.from);
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(versionAddress(set, first!), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.versionLink(first!)).toHaveAttribute("aria-current", "page");
    await expect(entries.status).toHaveText(shownCount(shown.rows.length));
    expect(await entries.rows()).toEqual(
      shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
    );
    await expectChanges(setPage, set, shown);
  });

  test("keeps an earlier version of the SAC codes while searching and paging their entries", async ({
    page,
  }) => {
    const set = await readParameterSet(page.request, sacCodesKey);
    const earlier = versionOf(set, (version) => version.from !== set.currentFrom, "an earlier version");
    const shown = shownVersionOf(await readParameterSet(page.request, set.key, earlier.from));
    const pageCount = Math.ceil(shown.rows.length / entriesPerPage);
    expect(pageCount, "pages of the earlier version's entries").toBeGreaterThan(2);
    const rowsOf = (rows: readonly (typeof shown.rows)[number][]) => rows.map((row) => entryRow(set, row));
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);
    await setPage.goto(parameterSetAddress(set.key, { version: earlier.from }), set.title);
    await setPage.waitUntilInteractive();

    await expect(setPage.versionLink(earlier)).toHaveAttribute("aria-current", "page");
    await expect(entries.pageLabel).toHaveText(`Page 1 of ${pageCount}`);
    await expect(entries.search.locator("input[type=hidden][name=version]")).toHaveValue(earlier.from);
    await entries.pageLink("Next page").click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}&page=2`);
    await expect
      .poll(() => entries.rows())
      .toEqual(rowsOf(shown.rows.slice(entriesPerPage, 2 * entriesPerPage)));
    await entries.pageLink("Last page").click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}&page=${pageCount}`);
    await expect
      .poll(() => entries.rows())
      .toEqual(rowsOf(shown.rows.slice((pageCount - 1) * entriesPerPage)));
    await entries.pageLink("Previous page").click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}&page=${pageCount - 1}`);
    await expect
      .poll(() => entries.rows())
      .toEqual(rowsOf(shown.rows.slice((pageCount - 2) * entriesPerPage, (pageCount - 1) * entriesPerPage)));
    await entries.pageLink("First page").click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}`);
    await expect.poll(() => entries.rows()).toEqual(rowsOf(shown.rows.slice(0, entriesPerPage)));
    await expect(entries.pageLink("First page")).toHaveAttribute("aria-disabled", "true");
    await expect(entries.pageLink("Previous page")).toHaveAttribute("aria-disabled", "true");

    const word = commonWord(set, shown);
    const found = expectedMatches(shown.rows, word, (row) => entryKeys(set, row));
    expect(found.length, `entries holding "${word}"`).toBeGreaterThan(entriesPerPage);
    await entries.searchBox.fill(word);
    await entries.searchButton.click();
    await expect(page).toHaveURL((url) => url.search === query({ version: earlier.from, q: word }));
    await expect.poll(() => entries.rows()).toEqual(rowsOf(found.slice(0, entriesPerPage)));
    await entries.pageLink("Next page").click();
    await expect(page).toHaveURL(
      (url) => url.search === query({ version: earlier.from, q: word, page: "2" }),
    );
    await expect.poll(() => entries.rows()).toEqual(rowsOf(found.slice(entriesPerPage, 2 * entriesPerPage)));
    await expect(setPage.versionLink(earlier)).toHaveAttribute("aria-current", "page");

    await entries.clearSearch.click();
    await expect(page).toHaveURL((url) => url.search === `?version=${earlier.from}`);
    await expect.poll(() => entries.rows()).toEqual(rowsOf(shown.rows.slice(0, entriesPerPage)));
  });

  forEachTheme(
    "marks a set that is the ERP's reading rather than a government table",
    async ({ page, capture }) => {
      const sets = await readParameterSets(page.request);
      const reading = sets.find((set) => set.basis === "interpretation");
      expect(reading, "a set that is the ERP's reading").toBeDefined();
      const setPage = new ParameterSetPage(page);

      await setPage.goto(parameterSetPath(reading!.key), reading!.title);
      await expect(setPage.basis).toHaveText("The ERP's reading");
      await expect(setPage.description).toHaveText(reading!.description);
      await capture("reading-set", page.getByRole("main").locator("header"));
    },
  );

  test("answers every other way of writing an address with its one canonical address", async ({ page }) => {
    const set = await readParameterSet(page.request, centralTaxRatesKey);
    const shown = shownVersionOf(set);
    const earliest = set.versions.at(-1);
    expect(earliest, "the first version").toBeDefined();
    const notAVersion = dayAfter(earliest!.from);
    expect(set.versions.map((version) => version.from)).not.toContain(notAVersion);
    const firstKey = shown.rows[0]?.key ?? "";
    const setPage = new ParameterSetPage(page);

    for (const { address, canonical } of [
      { address: `?version=${set.currentFrom}`, canonical: "" },
      { address: `?version=${notAVersion}`, canonical: "" },
      { address: "?version=yesterday", canonical: "" },
      { address: `?version=${earliest!.from}&version=${earliest!.from}`, canonical: "" },
      { address: `?version=${earliest!.from}&page=1`, canonical: `?version=${earliest!.from}` },
      { address: `?version=${earliest!.from}&page=99`, canonical: `?version=${earliest!.from}` },
      { address: "?page=two", canonical: "" },
      { address: query({ q: ` ${firstKey} ` }), canonical: query({ q: firstKey }) },
      { address: query({ version: set.currentFrom ?? "", q: firstKey }), canonical: query({ q: firstKey }) },
      { address: "?sort=name", canonical: "" },
      { address: "?on=2020-01-01", canonical: "" },
    ]) {
      await page.goto(`${parameterSetPath(set.key)}${address}`);
      await expect(page, address).toHaveURL(
        (url) => url.pathname === parameterSetPath(set.key) && url.search === canonical,
      );
      await expect(setPage.heading, address).toHaveText(set.title);
    }
  });

  test.describe("that does not exist", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    test("answers the not-found page inside the shell, and the API its own problem", async ({ page }) => {
      const shell = new AppShell(page);

      for (const key of ["gst.no-such-set", "Not-A-Key", "gst"]) {
        const response = await page.goto(parameterSetPath(key));

        expect(response?.status(), key).toBe(404);
        await expect(shell.banner, key).toBeVisible();
        await expect(page.getByRole("heading", { name: "This page does not exist" }), key).toBeVisible();
      }

      const missing = parameterSetApi("gst.no-such-set");
      const answer = await page.request.get(missing);
      expect(answer.status()).toBe(404);
      expect(await answer.json()).toMatchObject({ code: "parameter-set.not-found", instance: missing });
    });
  });
});

// The direct-tax sets (ADR-0064): entries of many columns read as cards at every width, the crosswalk is the ERP's reading, the
// Schedule II lives answer by financial year, and a date reads in words; every expectation is built from the API's answer.
test.describe("the direct-tax sets", () => {
  test.use({ persona: "administrator" });

  forEachTheme("show the TDS entries as cards, each value under its heading", async ({ page, capture }) => {
    const set = await readParameterSet(page.request, tdsEntriesKey);
    const shown = shownVersionOf(set);
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.basis).toHaveText(basisLabel(set.basis));
    await expect(setPage.asOf).toHaveText(asOfTexts[set.asOf]);
    await expect(setPage.versions).toMatchAriaSnapshot(versionsSnapshot(set));
    expect(await entries.showsCards(), "entries of many columns as cards at every width").toBe(true);
    expect(
      await entries.narrowestValueShare(entryTitle(set, shown.rows[0]!)),
      "every value of a card keeps more than a third of the card's width",
    ).toBeGreaterThan(1 / 3);
    await expect(entries.status).toHaveText(shownCount(shown.rows.length));
    expect(await entries.rows()).toEqual(
      shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
    );
    await capture("tds-entries-header", setPage.overview.locator("header"));
    await capture("tds-entries-entry", entries.row(entryTitle(set, shown.rows[0]!)));
  });

  forEachTheme(
    "mark the crosswalk as the ERP's reading and find a pair by its old section",
    async ({ page, capture }) => {
      const set = await readParameterSet(page.request, tdsCrosswalkKey);
      const shown = shownVersionOf(set);
      const target = shown.rows[0];
      expect(target, "a pair of the crosswalk").toBeDefined();
      const oldSection = valueOf(target!, "oldProvision");
      expect(oldSection, "the pair's provision of the Income-tax Act, 1961").not.toBeNull();
      const found = expectedMatches(shown.rows, oldSection!, (row) => entryKeys(set, row));
      const setPage = new ParameterSetPage(page);
      const entries = setPage.entries(set);

      await setPage.goto(parameterSetPath(set.key), set.title);
      await setPage.waitUntilInteractive();
      await expect(setPage.basis).toHaveText(basisLabel("interpretation"));
      await expect(setPage.asOf).toHaveText(asOfTexts[set.asOf]);
      await capture("tds-crosswalk-header", setPage.overview.locator("header"));
      await entries.searchFor(oldSection!);
      await expect
        .poll(() => entries.rows())
        .toEqual(found.slice(0, entriesPerPage).map((row) => entryRow(set, row)));
      await capture("tds-crosswalk-found", entries.row(entryTitle(set, found[0]!)));
    },
  );

  forEachTheme("show Schedule II's useful lives by financial year", async ({ page, capture }) => {
    const set = await readParameterSet(page.request, usefulLivesKey);
    const shown = shownVersionOf(set);
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.asOf).toHaveText(asOfTexts.financialYearStart);
    await expect(setPage.versions).toMatchAriaSnapshot(versionsSnapshot(set));
    await expect(entries.status).toHaveText(shownCount(shown.rows.length));
    expect(await entries.rows()).toEqual(
      shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
    );
    await capture("schedule-ii-useful-lives-versions", setPage.versionsCard);
  });

  test("writes the company options' dates in words", async ({ page }) => {
    const set = await readParameterSet(page.request, companyRatesKey);
    const shown = shownVersionOf(set);
    const dated = set.columns.filter((column) => column.kind === "date");
    expect(dated.length, "the date columns of the company rates").toBeGreaterThan(0);
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();
    expect(await entries.rows()).toEqual(
      shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
    );
    const withDate = shown.rows.find((row) => dated.some((column) => valueOf(row, column.name) !== null));
    expect(withDate, "an option with a date").toBeDefined();
    const column = dated.find((candidate) => valueOf(withDate!, candidate.name) !== null)!;
    await expect(entries.row(entryTitle(set, withDate!))).toContainText(
      cellText(column, valueOf(withDate!, column.name)),
    );
  });
});

test.describe("the labour sets", () => {
  test.use({ persona: "administrator" });

  forEachTheme(
    "show the professional-tax slabs with each State named beside its code",
    async ({ page, capture }) => {
      const set = await readParameterSet(page.request, professionalTaxSlabsKey);
      const shown = shownVersionOf(set);
      const found = expectedMatches(shown.rows, "Maharashtra", (row) => entryKeys(set, row));
      expect(found.length, "Maharashtra's slabs").toBeGreaterThan(0);
      const setPage = new ParameterSetPage(page);
      const entries = setPage.entries(set);

      await setPage.goto(parameterSetPath(set.key), set.title);
      await setPage.waitUntilInteractive();
      await expect(setPage.asOf).toHaveText(asOfTexts.taxPeriodStart);
      expect(await entries.rows()).toEqual(
        shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
      );
      await entries.searchFor("Maharashtra");
      await expect
        .poll(() => entries.rows())
        .toEqual(found.slice(0, entriesPerPage).map((row) => entryRow(set, row)));
      await expect(entries.row(entryTitle(set, found[0]!))).toContainText("Maharashtra (27)");
      await capture("professional-tax-slab", entries.row(entryTitle(set, found[0]!)));
    },
  );

  test("find a State's bonus limits by the State's name, which no other cell holds", async ({ page }) => {
    const set = await readParameterSet(page.request, bonusStateReadingsKey);
    const shown = shownVersionOf(set);
    const kerala = shown.rows.find((row) => valueOf(row, "stateCode") === "32");
    expect(kerala, "Kerala's reading").toBeDefined();
    const found = expectedMatches(shown.rows, "Kerala", (row) => entryKeys(set, row));
    expect(found, "the readings Kerala's name finds").toContainEqual(kerala);
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.basis).toHaveText(basisLabel("interpretation"));
    await entries.searchFor("Kerala");
    await expect
      .poll(() => entries.rows())
      .toEqual(found.slice(0, entriesPerPage).map((row) => entryRow(set, row)));
    await expect(entries.row("Kerala (32)")).toBeVisible();
  });

  forEachTheme("show the ESI contribution periods' days and months in words", async ({ page, capture }) => {
    const set = await readParameterSet(page.request, esiContributionPeriodsKey);
    const first = set.versions.at(-1);
    expect(first, "the version of the 1950 Regulations' periods").toBeDefined();
    const shown = shownVersionOf(await readParameterSet(page.request, set.key, first!.from));
    const dayMonths = set.columns.filter((column) => column.kind === "dayMonth");
    const april = shown.rows.find((row) => dayMonths.some((column) => valueOf(row, column.name) === "04-01"));
    expect(april, "the period that opens on 1 April").toBeDefined();
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(versionAddress(set, first!), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.asOf).toHaveText(asOfTexts.wagePeriodEnd);
    await expect(setPage.versions).toMatchAriaSnapshot(versionsSnapshot(set));
    expect(await entries.rows()).toEqual(
      shown.rows.slice(0, entriesPerPage).map((row) => entryRow(set, row)),
    );
    await expect(entries.row(entryTitle(set, april!))).toContainText("1 April");
    await capture("esi-contribution-periods-versions", setPage.versionsCard);
    await capture("esi-contribution-period", entries.row(entryTitle(set, april!)));
  });

  test("show the welfare fund's contributions in rupees and paise", async ({ page }) => {
    const set = await readParameterSet(page.request, welfareFundContributionsKey);
    const shown = shownVersionOf(set);
    const delhi = shown.rows.find((row) => valueOf(row, "stateCode") === "07");
    expect(delhi, "Delhi's contributions").toBeDefined();
    const setPage = new ParameterSetPage(page);
    const entries = setPage.entries(set);

    await setPage.goto(parameterSetPath(set.key), set.title);
    await setPage.waitUntilInteractive();
    await expect(setPage.asOf).toHaveText(asOfTexts.contributionPeriodEnd);
    await entries.searchFor("Delhi");
    await expect.poll(() => entries.rows()).toContainEqual(entryRow(set, delhi!));
    await expect(entries.row(entryTitle(set, delhi!))).toContainText("₹0.75");
  });

  test("open, below, every labour set the API lists", async ({ page }) => {
    const keys = (await readParameterSets(page.request))
      .map((summary) => summary.key)
      .filter((key) => labourSetPrefixes.some((prefix) => key.startsWith(prefix)));

    expect([...keys].sort()).toEqual([...labourSetKeys].sort());
  });

  for (const key of labourSetKeys) {
    forEachTheme(
      `open ${key} with its basis, the days it answers for and today's entries`,
      async ({ page, capture }) => {
        const set = await readParameterSet(page.request, key);
        const shown = shownVersionOf(set);
        const setPage = new ParameterSetPage(page);

        await setPage.goto(parameterSetPath(key), set.title);
        await setPage.waitUntilInteractive();
        await expect(setPage.basis).toHaveText(basisLabel(set.basis));
        await expect(setPage.asOf).toHaveText(asOfTexts[set.asOf]);
        await expect(setPage.entries(set).status).toHaveText(shownCount(shown.rows.length));
        await capture(`${key} header`, setPage.overview.locator("header"));
        await capture(`${key} first entry`, firstEntryOrEmptyList(setPage, set, shown));
      },
    );
  }
});

function firstEntryOrEmptyList(setPage: ParameterSetPage, set: ParameterSet, shown: ShownVersion): Locator {
  const first = shown.rows[0];
  return first === undefined ? setPage.entriesCard : setPage.entries(set).row(entryTitle(set, first)).first();
}
