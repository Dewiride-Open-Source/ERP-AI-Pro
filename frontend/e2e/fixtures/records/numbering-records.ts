import { expect, type APIRequestContext, type TestInfo } from "@playwright/test";
import { randomUUID } from "node:crypto";

import type { SignedInApi, Theme } from "../test";

export const numberedDocumentsApi = "/api/platform/numbering/documents";

export const numberSeriesApi = "/api/platform/numbering/series";

export const numberIssuesApi = "/api/__test/numbering/issues";

export const numberingResetsApi = "/api/__test/numbering/resets";

export type YearStyle = "none" | "full" | "abbreviated";

export type NumberFormat = {
  readonly prefix: string;
  readonly year: YearStyle;
  readonly separator: string;
  readonly digits: number;
};

export type SerialNumberRule = { readonly maxLength: number; readonly specialCharacters: string };

export type NumberingYear = {
  readonly year: string;
  readonly format: NumberFormat;
  readonly maxLength: number;
  readonly inUse: boolean;
  readonly firstText: string;
  readonly nextText: string | null;
  readonly lastText: string;
  readonly lastIssuedText: string | null;
};

export type NumberedDocument = {
  readonly documentType: string;
  readonly title: string;
  readonly description: string;
  readonly currentYear: string;
  readonly rule: SerialNumberRule | null;
  readonly series: {
    readonly id: string;
    readonly format: NumberFormat;
    readonly version: string;
    readonly years: readonly NumberingYear[];
  } | null;
};

export type TestKind = { readonly documentType: string; readonly title: string };

const projects = ["chromium", "firefox", "webkit", "mobile-android", "mobile-ios", "tablet-ios"];

export function numberSeriesApiOf(id: string): string {
  return `${numberSeriesApi}/${id}`;
}

// The end-to-end host declares sixteen kinds of document of its own, and every project, theme and repetition writes into one
// database, so the tests that change a numbering take the kind of their project and theme, which no other test running beside
// them takes, and run one after another; kinds 13 to 16 are left to the API's own tests. A repetition takes the kind of a later
// project, which is free while one project's test is repeated on its own, as a flake is investigated.
export function numberingKind(testInfo: TestInfo, theme: Theme): TestKind {
  const project = projects.indexOf(testInfo.project.name);
  expect(
    project,
    `${testInfo.project.name} is a project with a kind of document of its own`,
  ).toBeGreaterThanOrEqual(0);
  const slot = (project + testInfo.repeatEachIndex) % projects.length;
  return testKind(slot * 2 + (theme === "dark" ? 2 : 1));
}

// A kind no browser test changes, so a test that only reads finds it as the API leaves it: not set up.
export const untouchedKind: TestKind = testKind(projects.length * 2 + 1);

export function testKind(number: number): TestKind {
  const suffix = String(number).padStart(2, "0");
  return number % 2 === 1
    ? { documentType: `test.numbering.invoices-${suffix}`, title: `Test invoices ${suffix}` }
    : { documentType: `test.numbering.notes-${suffix}`, title: `Test notes ${suffix}` };
}

export async function readNumberedDocuments(
  request: APIRequestContext | SignedInApi,
): Promise<NumberedDocument[]> {
  const response = await request.get(numberedDocumentsApi);
  expect(response.status(), "the kinds of document").toBe(200);
  return (await response.json()) as NumberedDocument[];
}

export async function readNumberedDocument(
  request: APIRequestContext | SignedInApi,
  kind: TestKind,
): Promise<NumberedDocument> {
  const document = (await readNumberedDocuments(request)).find(
    (candidate) => candidate.documentType === kind.documentType,
  );
  expect(document, `the kind ${kind.documentType}`).toBeDefined();
  return document as NumberedDocument;
}

// A retried test meets the numbering its earlier attempt left, so a test removes its kind's numbering before it starts as well
// as when it ends.
export async function resetNumbering(
  request: APIRequestContext | SignedInApi,
  kind: TestKind,
): Promise<void> {
  const response = await request.post(numberingResetsApi, { data: { documentType: kind.documentType } });
  expect(response.status(), `the reset of ${kind.documentType}`).toBe(204);
}

export async function setUpNumbering(
  api: SignedInApi,
  kind: TestKind,
  format: NumberFormat,
  nextNumber = 1,
): Promise<NumberedDocument> {
  const response = await api.post(numberSeriesApi, {
    headers: { "Idempotency-Key": randomUUID() },
    data: { documentType: kind.documentType, format, nextNumber },
  });
  expect(response.status(), `the numbering of ${kind.documentType}`).toBe(201);
  return (await response.json()) as NumberedDocument;
}

// The issues route takes numbers dated today, exactly as a command that issues a document takes them.
export async function issueNumbers(
  request: APIRequestContext | SignedInApi,
  kind: TestKind,
  count: number,
): Promise<readonly string[]> {
  const response = await request.post(numberIssuesApi, { data: { documentType: kind.documentType, count } });
  expect(response.status(), `${count} numbers of ${kind.documentType}`).toBe(200);
  const { numbers } = (await response.json()) as { numbers: readonly string[] };
  return numbers;
}
