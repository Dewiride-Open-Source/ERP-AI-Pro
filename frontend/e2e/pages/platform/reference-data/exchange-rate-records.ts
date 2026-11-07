import { expect, type APIRequestContext, type TestInfo } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { ownPersonRecordId, peopleApi } from "../../../fixtures/person-records";
import {
  requestTokenCookie,
  requestTokenHeader,
  signIn,
  type SignedInPerson,
} from "../../../fixtures/sign-in";
import type { SignedInApi, Theme } from "../../../fixtures/test";

export const exchangeRatesApi = "/api/platform/reference-data/exchange-rates";

export const exchangeRateImportsApi = `${exchangeRatesApi}/imports`;

export type RecordedRate = {
  readonly id: string;
  readonly currency: string;
  readonly rateDate: string;
  readonly rate: number;
  readonly unit: number;
  readonly source: string | null;
  readonly version: string;
};

export type RateToRecord = {
  readonly currency: string;
  readonly rateDate: string;
  readonly rate: number;
  readonly unit?: number;
  readonly source?: string;
};

const projects = ["chromium", "firefox", "webkit", "mobile-android", "mobile-ios", "tablet-ios"];

const windowDays = 40;

const firstDay = Date.UTC(1901, 0, 1);

const dayMilliseconds = 86_400_000;

export function exchangeRateApi(id: string): string {
  return `${exchangeRatesApi}/${id}`;
}

export function typedDate(isoDate: string): string {
  return `${isoDate.slice(8, 10)}-${isoDate.slice(5, 7)}-${isoDate.slice(0, 4)}`;
}

// Every project, theme and repetition writes into one database, and a currency has one rate a day, so each test takes a
// currency no other test uses and days of a window that its project, theme and repetition alone use, far in the past where
// no person records a rate.
export function rateDays(testInfo: TestInfo, theme: Theme, count: number): readonly string[] {
  const project = projects.indexOf(testInfo.project.name);
  expect(project, `${testInfo.project.name} is a project with days of its own`).toBeGreaterThanOrEqual(0);
  expect(count, "the days a test takes").toBeLessThanOrEqual(windowDays);
  const window = (testInfo.repeatEachIndex * projects.length + project) * 2 + (theme === "dark" ? 1 : 0);
  return Array.from({ length: count }, (_, day) =>
    new Date(firstDay + (window * windowDays + day) * dayMilliseconds).toISOString().slice(0, 10),
  );
}

export async function recordRate(api: SignedInApi, rate: RateToRecord): Promise<RecordedRate> {
  const response = await api.post(exchangeRatesApi, {
    headers: { "Idempotency-Key": randomUUID() },
    data: { unit: 1, ...rate },
  });
  expect(response.status(), `the recording of ${rate.currency} for ${rate.rateDate}`).toBe(201);
  return (await response.json()) as RecordedRate;
}

export async function readRate(api: SignedInApi, id: string): Promise<RecordedRate> {
  const response = await api.get(exchangeRateApi(id));
  expect(response.status(), `the rate ${id}`).toBe(200);
  return (await response.json()) as RecordedRate;
}

// A retried test meets the rates its earlier attempt left, so a test removes the rates of its currency and days before it
// starts as well as when it ends.
export async function removeRates(
  api: SignedInApi,
  currency: string,
  days: readonly string[],
): Promise<void> {
  const sorted = [...days].sort();
  const query = new URLSearchParams({
    filter: `currency:eq:${currency};rateDate:gte:${sorted[0] ?? ""};rateDate:lte:${sorted.at(-1) ?? ""}`,
    pageSize: "200",
  });
  const response = await api.get(`${exchangeRatesApi}?${query.toString()}`);
  expect(response.status(), `the rates of ${currency} to remove`).toBe(200);
  const { items } = (await response.json()) as { items?: readonly { readonly id?: string }[] };
  for (const { id } of items ?? []) {
    const removed = await api.delete(exchangeRateApi(id ?? ""));
    expect([204, 404], `the removal of the rate ${id ?? ""}`).toContain(removed.status());
  }
}

// An administrator assigns the role from a request context of their own, so the browser keeps the accountant's session, and
// the role counts from the accountant's next request.
export async function grantFinanceRole(
  request: APIRequestContext,
  accountant: SignedInPerson | undefined,
): Promise<void> {
  await signIn(request, "administrator");
  const id = await ownPersonRecordId(request, accountant);
  const record = await request.get(`${peopleApi}/${id}`);
  expect(record.status(), "the accountant's record").toBe(200);
  const { version } = (await record.json()) as { version: string };
  const token = (await request.storageState()).cookies.find((cookie) => cookie.name === requestTokenCookie);
  expect(token, "the administrator's request token").toBeDefined();
  const assigned = await request.put(`${peopleApi}/${id}/roles`, {
    headers: { [requestTokenHeader]: decodeURIComponent(token?.value ?? "") },
    data: { roles: ["finance"], version },
  });
  expect(assigned.status(), "the Finance role of the accountant").toBe(200);
}
