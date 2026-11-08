import type { Page, Request } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  issueNumbers,
  numberedDocumentsApi,
  numberingKind,
  numberSeriesApi,
  numberSeriesApiOf,
  readNumberedDocument,
  readNumberedDocuments,
  resetNumbering,
  setUpNumbering,
  untouchedKind,
  type NumberedDocument,
  type NumberFormat,
  type SerialNumberRule,
  type TestKind,
} from "../../../fixtures/records/numbering-records";
import { assignRoles } from "../../../fixtures/records/person-records";
import { expect, forEachTheme, pressArrowUntilChecked, test, themes } from "../../../fixtures/test";
import {
  allowedCharacters,
  characterName,
  eitherCharacter,
  followingYear,
  isNumberingSave,
  lastRunningNumber,
  NumberingPage,
  numberingPath,
  numberText,
  yearText,
} from "../../../pages/platform/numbering/numbering.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const quoted = JSON.stringify;

const noAccess = "You do not have access to this page";

const yearHyphen = "-";

const plainFormat: NumberFormat = { prefix: "", year: "none", separator: "", digits: 1 };

type Rules = { readonly rule: SerialNumberRule; readonly currentYear: string };

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

// Every number a test expects is built from the rule the API answers, never from a statutory value.
function rulesOf(document: NumberedDocument): Rules {
  expect(document.rule, `the rule of ${document.documentType} for ${document.currentYear}`).not.toBeNull();
  const rule = document.rule as SerialNumberRule;
  expect(rule.specialCharacters, "the rule lets a number show the financial year").toContain(yearHyphen);
  return { rule, currentYear: document.currentYear };
}

// The year and the running number are kept apart by a special character of the rule other than the year's own hyphen, when the
// rule has one, so the two read apart in every number a test expects.
function separatorOf(rule: SerialNumberRule): string {
  return rule.specialCharacters.split("").find((character) => character !== yearHyphen) ?? yearHyphen;
}

function invoiceFormat(rule: SerialNumberRule, prefix = "INV"): NumberFormat {
  const separator = separatorOf(rule);
  return { prefix: `${prefix}${separator}`, year: "abbreviated", separator, digits: 3 };
}

function unusedCharacter(rule: SerialNumberRule): string {
  const character = ["#", "*", "@", "&", "+"].find(
    (candidate) => !rule.specialCharacters.includes(candidate),
  );
  expect(character, "a character the rule does not allow").toBeDefined();
  return character ?? "";
}

function previewOf({ rule }: Rules, format: NumberFormat, financialYear: string, start = 1n): string {
  const last = lastRunningNumber(format, financialYear, rule.maxLength);
  return `${financialYear} starts at ${numberText(format, financialYear, start)}; the last number that fits in ${rule.maxLength} characters is ${numberText(format, financialYear, last)}.`;
}

function capitalised(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function tooLongStatus(rule: SerialNumberRule): string {
  return `The numbers would have more than the ${rule.maxLength} characters a number may have.`;
}

function setUpDialogSnapshot(kind: TestKind, { rule, currentYear }: Rules): string {
  const title = `Set up the numbering of ${kind.title}`;
  return [
    `- dialog ${quoted(title)}:`,
    `  - heading ${quoted(title)} [level=2]`,
    `  - paragraph: ${quoted(`Its numbers start in ${currentYear} at the next number you give, and every later financial year starts again at 1.`)}`,
    "  - group:",
    "    - text: Starts with",
    '    - textbox "Starts with"',
    `    - paragraph: ${quoted(`Optional. ${capitalised(allowedCharacters(rule.specialCharacters))}; the whole number may have at most ${rule.maxLength} characters.`)}`,
    '  - group "Financial year":',
    "    - text: Financial year",
    '    - radiogroup "Financial year":',
    '      - radio "Not shown" [checked]',
    "      - text: Not shown",
    `      - radio ${quoted(yearText(currentYear, "full"))}`,
    `      - text: ${quoted(yearText(currentYear, "full"))}`,
    `      - radio ${quoted(yearText(currentYear, "abbreviated"))}`,
    `      - text: ${quoted(yearText(currentYear, "abbreviated"))}`,
    `    - paragraph: ${quoted("Shown after the text the numbers start with, in full or short.")}`,
    "  - group:",
    "    - text: Digits",
    '    - textbox "Digits": "1"',
    `    - paragraph: ${quoted("The running number is padded with zeros to this many digits, from 1 to 18: 3 gives 001.")}`,
    "  - group:",
    "    - text: Next number",
    '    - textbox "Next number": "1"',
    `    - paragraph: ${quoted(`The running number of the next document in ${currentYear}: 1, or the number after the last one given outside the ERP this year.`)}`,
    "  - paragraph: Preview",
    `  - paragraph: ${quoted(previewOf({ rule, currentYear }, plainFormat, currentYear))}`,
    "  - status",
    '  - button "Cancel"',
    '  - button "Set up the numbering"',
    '  - button "Close"',
  ].join("\n");
}

function documentSnapshot(
  document: NumberedDocument,
  action: "Set up" | "Change" | null,
  lines: readonly (readonly [year: string, text: string])[],
  pending?: string,
): string {
  const button =
    action === null ? [] : [`  - button ${quoted(`${action} the numbering of ${document.title}`)}`];
  return [
    "- listitem:",
    `  - heading ${quoted(document.title)} [level=3]`,
    `  - paragraph: ${quoted(document.description)}`,
    ...button,
    ...(lines.length === 0 ? ["  - paragraph: Not set up yet."] : []),
    ...lines.flatMap(([year, text]) => [`  - term: ${quoted(year)}`, `  - definition: ${quoted(text)}`]),
    ...(pending === undefined ? [] : [`  - paragraph: ${quoted(pending)}`]),
  ].join("\n");
}

function givenText(first: string, last: string, count: number, next: string): string {
  return `${count} numbers given, ${first} to ${last}; next ${next}.`;
}

// A test that changes a numbering takes the kind of its project and theme, so the tests of one theme run one after another and
// each starts from a kind without numbering.
test.describe("document numbering for a finance person", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.describe.configure({ mode: "default" });
      test.use({ theme });

      test("finds the page and sets a numbering up after refusing what the rule does not allow", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = numberingKind(testInfo, theme);
        const numbering = new NumberingPage(page);
        const shell = new AppShell(page);
        const saves = watch(page, isNumberingSave);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          const document = await readNumberedDocument(api, kind);
          const rules = rulesOf(document);
          const { rule, currentYear } = rules;
          const format = invoiceFormat(rule);

          await page.goto("/");
          await shell.waitUntilInteractive();
          await shell.openNavigation();
          await shell.navigationLink("Document numbering").click();
          await expect(page).toHaveURL((url) => url.pathname === numberingPath);
          await expect(page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
          await expect(numbering.heading).toBeVisible();
          await expect(page).toHaveTitle("Document numbering · ERP-AI-Pro");
          await numbering.waitUntilInteractive();
          await expect(numbering.list.getByRole("listitem")).toHaveCount(
            (await readNumberedDocuments(api)).length,
          );
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(document, "Set up", []),
          );
          await capture("numbering-not-set-up", numbering.document(kind.title));

          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toMatchAriaSnapshot(setUpDialogSnapshot(kind, rules));
          await capture("numbering-set-up", numbering.dialog);

          const refused = unusedCharacter(rule);
          await numbering.prefix.fill(`inv${refused}`);
          await expect(numbering.prefix, "the text is upper-cased as it is typed").toHaveValue(
            `INV${refused}`,
          );
          await numbering.save.click();
          await expect(numbering.prefix).toBeFocused();
          await expect(numbering.prefix).toHaveAttribute("aria-invalid", "true");
          await expect(numbering.fieldOf(numbering.prefix)).toContainText(
            `Use only ${allowedCharacters(rule.specialCharacters)}.`,
          );
          expect(saves.count(), "Server Function calls").toBe(0);
          await capture("numbering-set-up-refused", numbering.dialog);

          const tooLong = { ...plainFormat, prefix: "X".repeat(rule.maxLength) };
          const tooLongFirst = numberText(tooLong, currentYear, 1n);
          await numbering.prefix.fill(tooLong.prefix);
          await expect(numbering.fieldOf(numbering.prefix)).not.toContainText("Use only");
          await expect(numbering.preview).toContainText(
            `${tooLongFirst} has ${tooLongFirst.length} characters, more than the ${rule.maxLength} a number may have.`,
          );
          await expect(numbering.previewStatus).toHaveText(tooLongStatus(rule));
          await numbering.save.click();
          await expect(numbering.summary).toBeFocused();
          await expect(numbering.summary).toContainText(
            `The first number, ${tooLongFirst}, has ${tooLongFirst.length} characters; a number may have at most ${rule.maxLength}.`,
          );
          expect(saves.count(), "Server Function calls").toBe(0);

          await numbering.prefix.press("End");
          await numbering.prefix.press("Backspace");
          await numbering.prefix.pressSequentially("y");
          await expect(numbering.prefix).toHaveValue(`${"X".repeat(rule.maxLength - 1)}Y`);
          await expect(
            numbering.previewStatus,
            "the status keeps its sentence while the problem lasts, so typing announces nothing new",
          ).toHaveText(tooLongStatus(rule));

          await numbering.prefix.fill(format.prefix.toLowerCase());
          await expect(numbering.prefix).toHaveValue(format.prefix);
          await expect(numbering.previewStatus, "the problem cleared").toHaveText("");
          await numbering.yearChoice("Not shown").focus();
          await pressArrowUntilChecked(
            page,
            "ArrowRight",
            numbering.yearChoice(yearText(currentYear, "full")),
          );
          await pressArrowUntilChecked(
            page,
            "ArrowRight",
            numbering.yearChoice(yearText(currentYear, "abbreviated")),
          );
          await expect(numbering.separatorChoice("Nothing")).toBeChecked();
          await numbering.separatorChoice(characterName(format.separator)).check();
          await numbering.digits.fill(String(format.digits));
          await expect(numbering.preview).toContainText(previewOf(rules, format, currentYear));
          await numbering.save.click();

          await expect(shell.toast(`Set up the numbering of ${kind.title}.`)).toBeVisible();
          await numbering.expectDialogClosed();
          await expect(numbering.action(kind.title)).toBeFocused();
          await expect(numbering.action(kind.title)).toHaveAccessibleName(
            `Change the numbering of ${kind.title}`,
          );
          const first = numberText(format, currentYear, 1n);
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(document, "Change", [
              [currentYear, `No number given yet; the first will be ${first}.`],
            ]),
          );
          expect(saves.count(), "Server Function calls").toBe(1);
          await capture("numbering-set-up-done", numbering.document(kind.title));

          const saved = await readNumberedDocument(api, kind);
          expect(saved.series?.format, "the numbering's format").toEqual(format);
          expect(saved.series?.years.map(({ year, nextText }) => ({ year, nextText }))).toEqual([
            { year: currentYear, nextText: first },
          ]);
        } finally {
          await resetNumbering(api, kind);
        }
      });

      test("changes the format and next number of a year that has given no number, and keeps them on Cancel", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = numberingKind(testInfo, theme);
        const numbering = new NumberingPage(page);
        const shell = new AppShell(page);
        const saves = watch(page, isNumberingSave);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          const rules = rulesOf(await readNumberedDocument(api, kind));
          const { rule, currentYear } = rules;
          const format = invoiceFormat(rule);
          const changed = { ...format, digits: 4 };
          const document = await setUpNumbering(api, kind, format);

          await numbering.goto();
          await numbering.waitUntilInteractive();
          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toBeVisible();
          await expect(numbering.dialog.getByRole("heading", { level: 2 })).toHaveText(
            `Change the numbering of ${kind.title}`,
          );
          await expect(numbering.dialog).toContainText(
            `${currentYear} has given no number yet, so a change applies to it at once.`,
          );
          await expect(numbering.prefix).toHaveValue(format.prefix);
          await expect(numbering.yearChoice(yearText(currentYear, "abbreviated"))).toBeChecked();
          await expect(numbering.separatorChoice(characterName(format.separator))).toBeChecked();
          await expect(numbering.digits).toHaveValue(String(format.digits));
          await expect(numbering.nextNumber).toHaveValue("1");

          await numbering.digits.fill("5");
          await numbering.cancel.click();
          await numbering.expectDialogClosed();
          await expect(numbering.action(kind.title)).toBeFocused();
          await numbering.action(kind.title).click();
          await expect(numbering.digits, "the dialog opens on the numbering the page shows").toHaveValue(
            String(format.digits),
          );
          await numbering.close.click();
          await numbering.expectDialogClosed();
          await expect(numbering.action(kind.title)).toBeFocused();
          await numbering.action(kind.title).press("Enter");
          await expect(numbering.dialog).toBeVisible();
          await page.keyboard.press("Escape");
          await numbering.expectDialogClosed();
          await expect(numbering.action(kind.title)).toBeFocused();
          expect(saves.count(), "Server Function calls").toBe(0);

          await numbering.action(kind.title).click();
          await numbering.digits.fill(String(changed.digits));
          const last = lastRunningNumber(changed, currentYear, rule.maxLength);
          await numbering.nextNumber.fill("0");
          await numbering.nextNumber.blur();
          await expect(numbering.fieldOf(numbering.nextNumber)).toContainText(
            `Use a number from 1 to ${last}.`,
          );
          await numbering.nextNumber.fill("101");
          await expect(numbering.preview).toContainText(previewOf(rules, changed, currentYear, 101n));
          await capture("numbering-change", numbering.dialog);
          await numbering.save.click();

          await expect(shell.toast(`Saved the numbering of ${kind.title}.`)).toBeVisible();
          await numbering.expectDialogClosed();
          await expect(numbering.action(kind.title)).toBeFocused();
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(document, "Change", [
              [
                currentYear,
                `No number given yet; the first will be ${numberText(changed, currentYear, 101n)}.`,
              ],
            ]),
          );
          await capture("numbering-changed", numbering.document(kind.title));
          expect((await readNumberedDocument(api, kind)).series?.format, "the changed format").toEqual(
            changed,
          );

          const number = (running: bigint) => numberText(changed, currentYear, running);
          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toContainText(
            `${currentYear} has given no number yet, so a change applies to it at once.`,
          );
          await expect(numbering.nextNumber).toHaveValue("101");
          expect(await issueNumbers(api, kind, 2), "the numbers given meanwhile").toEqual([
            number(101n),
            number(102n),
          ]);
          await numbering.nextNumber.fill("201");
          await numbering.save.click();
          await expect(numbering.summary).toBeFocused();
          await expect(numbering.summary).toContainText(
            "Numbers were given in this financial year after the page was loaded, so its next number can no longer change. Show the latest numbering, then try again.",
          );
          await expect(numbering.fieldOf(numbering.nextNumber)).not.toHaveAttribute("data-invalid", "true");
          await capture("numbering-year-in-use", numbering.dialog);
          await numbering.reload.click();
          await numbering.expectDialogClosed();
          await expect(numbering.years(kind.title)).toContainText(
            givenText(number(101n), number(102n), 2, number(103n)),
          );
          await numbering.action(kind.title).click();
          await expect(numbering.nextNumber).toBeDisabled();
          await expect(numbering.nextNumber).toHaveValue("103");
          await numbering.close.click();
          await numbering.expectDialogClosed();
        } finally {
          await resetNumbering(api, kind);
        }
      });

      test("keeps the numbers of a year that has given some and applies a change from the next year", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = numberingKind(testInfo, theme);
        const numbering = new NumberingPage(page);
        const shell = new AppShell(page);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          const rules = rulesOf(await readNumberedDocument(api, kind));
          const { rule, currentYear } = rules;
          const format = invoiceFormat(rule);
          const changed = invoiceFormat(rule, "BIL");
          const next = followingYear(currentYear);
          const document = await setUpNumbering(api, kind, format);
          const given = await issueNumbers(api, kind, 3);
          expect(given, "the numbers taken").toEqual(
            [1n, 2n, 3n].map((n) => numberText(format, currentYear, n)),
          );
          const number = (running: bigint) => numberText(format, currentYear, running);

          await numbering.goto();
          await numbering.waitUntilInteractive();
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(document, "Change", [
              [currentYear, givenText(number(1n), number(3n), 3, number(4n))],
            ]),
          );
          await capture("numbering-in-use", numbering.document(kind.title));

          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toContainText(
            `${currentYear} has given numbers, so they keep their format, and a change applies from ${next}.`,
          );
          await expect(numbering.nextNumber).toBeDisabled();
          await expect(numbering.nextNumber).toHaveValue("4");
          await expect(numbering.fieldOf(numbering.nextNumber)).toContainText(
            `${currentYear} has given numbers, so it goes on from ${number(4n)}.`,
          );
          await numbering.prefix.fill(changed.prefix);
          await expect(numbering.preview).toContainText(previewOf(rules, changed, next));
          await capture("numbering-change-in-use", numbering.dialog);
          await numbering.save.click();

          const pending = `The new format applies from ${next}, whose first number will be ${numberText(changed, next, 1n)}.`;
          await expect(shell.toast(`Saved the numbering of ${kind.title}.`)).toBeVisible();
          await numbering.expectDialogClosed();
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(
              document,
              "Change",
              [[currentYear, givenText(number(1n), number(3n), 3, number(4n))]],
              pending,
            ),
          );
          await capture("numbering-change-waits", numbering.document(kind.title));
          expect(await issueNumbers(api, kind, 1), "the year keeps its format").toEqual([number(4n)]);

          const current = await readNumberedDocument(api, kind);
          await numbering.goto();
          await numbering.waitUntilInteractive();
          await expect(numbering.years(kind.title)).toContainText(
            givenText(number(1n), number(4n), 4, number(5n)),
          );
          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toBeVisible();
          const elsewhere = invoiceFormat(rule, "CRN");
          const changedElsewhere = await api.put(numberSeriesApiOf(current.series?.id ?? ""), {
            data: { format: elsewhere, nextNumber: null, version: current.series?.version },
          });
          expect(changedElsewhere.status(), "the change made elsewhere").toBe(200);
          await numbering.digits.fill("4");
          await numbering.save.click();
          await expect(numbering.summary).toBeFocused();
          await expect(numbering.summary).toContainText(
            "Someone changed this numbering, or a document of this kind was being numbered, while you worked. Show the latest numbering, then try again.",
          );
          await capture("numbering-changed-meanwhile", numbering.dialog);
          await numbering.reload.click();
          await numbering.expectDialogClosed();
          await expect(numbering.pending(kind.title)).toHaveText(
            `The new format applies from ${next}, whose first number will be ${numberText(elsewhere, next, 1n)}.`,
          );
        } finally {
          await resetNumbering(api, kind);
        }
      });

      test("says when a year has used every number that fits", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = numberingKind(testInfo, theme);
        const numbering = new NumberingPage(page);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          const { rule, currentYear } = rulesOf(await readNumberedDocument(api, kind));
          const format = { ...plainFormat, prefix: "X".repeat(rule.maxLength - 1) };
          const last = lastRunningNumber(format, currentYear, rule.maxLength);
          const document = await setUpNumbering(api, kind, format);
          await issueNumbers(api, kind, Number(last));
          const number = (running: bigint) => numberText(format, currentYear, running);

          await numbering.goto();
          await numbering.waitUntilInteractive();
          await expect(numbering.document(kind.title)).toMatchAriaSnapshot(
            documentSnapshot(document, "Change", [
              [
                currentYear,
                `Every number that fits is used: ${last} numbers, ${number(1n)} to ${number(last)}.`,
              ],
            ]),
          );
          await capture("numbering-used-up", numbering.document(kind.title));

          await numbering.action(kind.title).click();
          await expect(numbering.nextNumber).toBeDisabled();
          await expect(numbering.fieldOf(numbering.nextNumber)).toContainText(
            `${currentYear} has used every number that fits, so its next number cannot change.`,
          );
          await numbering.close.click();
          await numbering.expectDialogClosed();
        } finally {
          await resetNumbering(api, kind);
        }
      });

      test("the API refuses characters the rule does not allow in the words the page uses", async ({
        api,
        person,
        request,
      }, testInfo) => {
        const kind = numberingKind(testInfo, theme);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          const { rule } = rulesOf(await readNumberedDocument(api, kind));
          const refused = unusedCharacter(rule);
          const response = await api.post(numberSeriesApi, {
            headers: { "Idempotency-Key": randomUUID() },
            data: {
              documentType: kind.documentType,
              format: { prefix: `INV${refused}`, year: "full", separator: refused, digits: 3 },
              nextNumber: 1,
            },
          });

          expect(response.status(), "the set-up refused").toBe(400);
          expect(await response.json()).toMatchObject({
            code: "numbering.format-invalid",
            errors: {
              "format.prefix": [`Use only ${allowedCharacters(rule.specialCharacters)}.`],
              "format.separator": [`Use ${eitherCharacter(rule.specialCharacters)}, or nothing.`],
            },
          });
          expect((await readNumberedDocument(api, kind)).series, "the numbering left unset").toBeNull();
        } finally {
          await resetNumbering(api, kind);
        }
      });

      test("is told a numbering was not set up when an administrator took the Finance role away while the dialog was open", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = numberingKind(testInfo, theme);
        const numbering = new NumberingPage(page);
        await assignRoles(request, person, ["finance"]);
        await resetNumbering(api, kind);
        try {
          await numbering.goto();
          await numbering.waitUntilInteractive();
          await numbering.action(kind.title).click();
          await expect(numbering.dialog).toBeVisible();
          await assignRoles(request, person, []);

          await numbering.save.click();
          await expect(numbering.summary).toBeFocused();
          await expect(numbering.summary).toContainText("You do not have permission to do this.");
          await expect(numbering.dialog).toBeVisible();
          await capture("numbering-set-up-forbidden", numbering.dialog);
          expect((await readNumberedDocument(request, kind)).series, "the numbering left unset").toBeNull();
        } finally {
          await resetNumbering(api, kind);
        }
      });
    });
  }
});

test.describe("document numbering for an administrator", () => {
  test.use({ persona: "administrator" });

  forEachTheme("shows every kind of document and offers to set one up", async ({ page, capture }) => {
    const numbering = new NumberingPage(page);
    const documents = await readNumberedDocuments(page.request);
    const untouched = documents.find((document) => document.documentType === untouchedKind.documentType);
    expect(untouched, "the kind no browser test changes").toBeDefined();

    await numbering.goto();
    await numbering.waitUntilInteractive();
    await expect(numbering.description).toHaveText(
      "Each kind of document has numbers of its own. Every financial year starts again at 1 and its numbers follow each other without a gap. Once a year has given a number, its format and next number stay as they are, and a change applies from the next year.",
    );
    await expect(
      numbering.card.getByText(`The current financial year is ${untouched?.currentYear ?? ""}.`),
    ).toBeVisible();
    await expect(numbering.list.getByRole("heading", { level: 3 })).toHaveText(
      documents.map((document) => document.title),
    );
    await expect(numbering.document(untouchedKind.title)).toMatchAriaSnapshot(
      documentSnapshot(untouched as NumberedDocument, "Set up", []),
    );
    await capture("numbering");

    await numbering.action(untouchedKind.title).click();
    await expect(numbering.dialog).toBeVisible();
    await expect(numbering.prefix).toBeFocused();
    await page.keyboard.press("Escape");
    await numbering.expectDialogClosed();
    await expect(numbering.action(untouchedKind.title)).toBeFocused();
  });
});

test.describe("document numbering before the page is interactive", () => {
  test.use({ persona: "administrator" });

  test("keeps every Set up and Change button inert so no click is lost", async ({ page }) => {
    const numbering = new NumberingPage(page);
    await withholdClientBundles(page);

    await numbering.goto();
    await expect(numbering.action(untouchedKind.title)).toBeDisabled();
    await expect(numbering.action(untouchedKind.title)).toHaveAttribute("data-hydrating", "");
  });
});

test.describe("document numbering for an employee", () => {
  forEachTheme(
    "leaves the entry out of the navigation and tells them they have no access to the page",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      const numbering = new NumberingPage(page);

      await page.goto(numberingPath);
      await expect(page.getByRole("heading", { name: noAccess, level: 1 })).toBeVisible();
      await expect(page).toHaveTitle("No access · ERP-AI-Pro");
      await expect(numbering.card).toHaveCount(0);
      await capture("numbering-no-access");
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Document numbering")).toHaveCount(0);
      await shell.closeDrawer();
    },
  );

  test("the API refuses them the kinds of document, a numbering, a set-up and a change", async ({ api }) => {
    const series = numberSeriesApiOf(randomUUID());
    const format = { prefix: "", year: "none", separator: "", digits: 1 };
    const answers = [
      { request: "the kinds of document", response: await api.get(numberedDocumentsApi) },
      { request: "a numbering", response: await api.get(series) },
      {
        request: "a set-up",
        response: await api.post(numberSeriesApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { documentType: untouchedKind.documentType, format, nextNumber: 1 },
        }),
      },
      {
        request: "a change",
        response: await api.put(series, { data: { format, nextNumber: null, version: "AAAAAAAAB9E=" } }),
      },
    ];

    for (const { request, response } of answers) {
      expect(response.status(), request).toBe(403);
      expect(await response.json(), request).toMatchObject({ code: "request.forbidden" });
    }
  });
});
