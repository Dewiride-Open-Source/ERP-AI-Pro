import { expect, type APIRequest, type APIRequestContext, type TestInfo } from "@playwright/test";
import { randomUUID } from "node:crypto";

import {
  requestTokenCookie,
  requestTokenHeader,
  signIn,
  type Persona,
  type SignedInPerson,
} from "../sign-in";
import type { SignedInApi, Theme } from "../test";

import { ownPersonRecordId, recordMoveRecord } from "./person-records";

export const approvalRequestsApi = "/api/platform/approvals/requests";

export const approvalCountsApi = "/api/platform/approvals/requests/counts";

export const approvalKindsApi = "/api/platform/approvals/kinds";

export const approvalKindSettingsApi = "/api/platform/approvals/kind-settings";

export const approvalRequestsSeedApi = "/api/__test/approvals/requests";

export type ApprovalState = "submitted" | "approved" | "returned" | "rejected" | "withdrawn";

export type ApprovalStepState = "pending" | "waiting" | "approved" | "returned" | "rejected" | "closed";

export type ApprovalDecision = "approve" | "return" | "reject";

export type ApprovalRole = "employee" | "finance" | "hr" | "manager" | "administrator";

export type ApprovalStep = {
  readonly order: number;
  readonly title: string;
  readonly rule: "manager" | "role";
  readonly state: ApprovalStepState;
  readonly approverKind: "person" | "role";
  readonly approverId: string | null;
  readonly approverName: string | null;
  readonly approverRole: ApprovalRole | null;
  readonly workingDays: number;
  readonly startedAt: string | null;
  readonly dueAt: string | null;
  readonly late: boolean;
  readonly decidedById: string | null;
  readonly decidedByName: string | null;
  readonly decidedAt: string | null;
  readonly inPlaceOfApprover: boolean;
  readonly comment: string | null;
};

export type ApprovalRequestSummary = {
  readonly id: string;
  readonly kind: string;
  readonly kindTitle: string;
  readonly subjectId: string;
  readonly requestedForId: string;
  readonly requestedForName: string | null;
  readonly submittedById: string;
  readonly submittedByName: string | null;
  readonly submittedAt: string;
  readonly state: ApprovalState;
  readonly waitingStepTitle: string | null;
  readonly waitingForKind: "person" | "role" | null;
  readonly waitingForId: string | null;
  readonly waitingForName: string | null;
  readonly waitingForRole: ApprovalRole | null;
  readonly dueAt: string | null;
  readonly late: boolean;
  readonly closedAt: string | null;
};

export type ApprovalRequest = ApprovalRequestSummary & {
  readonly version: string;
  readonly steps: readonly ApprovalStep[];
  readonly earlierRequests: readonly {
    readonly id: string;
    readonly state: ApprovalState;
    readonly submittedAt: string;
    readonly closedAt: string | null;
  }[];
  readonly canDecide: boolean;
  readonly decidesInPlace: boolean;
  readonly canWithdraw: boolean;
};

export type ApprovalKindSetting = {
  readonly id: string;
  readonly required: boolean;
  readonly workingDaysToDecide: number;
  readonly version: string;
};

export type ApprovalKindAnswer = {
  readonly kind: string;
  readonly title: string;
  readonly description: string;
  readonly steps: readonly {
    readonly order: number;
    readonly title: string;
    readonly rule: "manager" | "role";
    readonly role: ApprovalRole | null;
  }[];
  readonly declaredWorkingDays: number;
  readonly setting: ApprovalKindSetting | null;
};

export type ApprovalBox = "waiting" | "mine" | "decided" | "all";

export type ApprovalScenario = "manager" | "chain" | "finance" | "nomanager" | "refused" | "settings";

export type TestApprovalKind = { readonly key: string; readonly title: string };

// Another person of the test, signed in through a request context of their own, so the browser keeps one person's session.
export type Colleague = {
  readonly request: APIRequestContext;
  readonly api: SignedInApi;
  readonly person: SignedInPerson;
};

export type ApprovalAsk = {
  readonly requestedFor?: string;
  readonly submittedWorkingDaysAgo?: number;
  readonly subjectId?: string;
};

const projects = ["chromium", "firefox", "webkit", "mobile-android", "mobile-ios", "tablet-ios"];

const scenarioTitles: Readonly<Record<ApprovalScenario, string>> = {
  manager: "Manager test approval",
  chain: "Manager and Finance test approval",
  finance: "Finance test approval",
  nomanager: "No-manager test approval",
  refused: "Refused test approval",
  settings: "Settings test approval",
};

// The manager of a person asking in a test is recorded from this many days back, before the earliest day the test host can
// date a request (thirty working days ago), so every request a test asks for goes to that manager whatever day it is dated.
const reportingDaysBack = 60;

const dayMilliseconds = 86_400_000;

const indiaIsoDate = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Kolkata",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

export function approvalRequestApi(id: string): string {
  return `${approvalRequestsApi}/${id}`;
}

export function approvalKindSettingApi(id: string): string {
  return `${approvalKindSettingsApi}/${id}`;
}

// The end-to-end host declares twelve kinds of each scenario, and every project, theme and repetition writes into one
// database. A request routed to a role waits for every holder of the role, and a kind's setting belongs to the whole kind, so
// a test that reads a list of a role, every request or a setting takes the kind of its project and theme, which no other
// project or theme takes, and filters by it: kind 2n - 1 for the light theme and 2n for the dark one, n the project's place. A
// repetition takes the kinds of a later project, which are free while one project's test is repeated on its own, as a flake
// is investigated; the seventh repetition would come back to the first project's kinds, so --repeat-each above six fails at
// once.
export function approvalKind(testInfo: TestInfo, theme: Theme, scenario: ApprovalScenario): TestApprovalKind {
  const project = projects.indexOf(testInfo.project.name);
  expect(
    project,
    `${testInfo.project.name} is a project with kinds of request of its own`,
  ).toBeGreaterThanOrEqual(0);
  expect(
    testInfo.repeatEachIndex,
    `repetition ${testInfo.repeatEachIndex + 1} has kinds of request of its own`,
  ).toBeLessThan(projects.length);
  const slot = (project + testInfo.repeatEachIndex) % projects.length;
  return testApprovalKind(scenario, slot * 2 + (theme === "dark" ? 2 : 1));
}

// A kind no test decides a request of or sets up, so a test that only reads finds it as its declaration leaves it.
export const untouchedApprovalKind: TestApprovalKind = {
  key: "test.approvals.untouched-01",
  title: "Untouched test approval 01",
};

export function testApprovalKind(scenario: ApprovalScenario, slot: number): TestApprovalKind {
  const suffix = String(slot).padStart(2, "0");
  return { key: `test.approvals.${scenario}-${suffix}`, title: `${scenarioTitles[scenario]} ${suffix}` };
}

// A request context sends the request token of its own session on every change, as the pages do.
export function requestApi(request: APIRequestContext): SignedInApi {
  const change =
    (method: string) => async (path: string, options?: Parameters<APIRequestContext["fetch"]>[1]) =>
      request.fetch(path, {
        ...options,
        method,
        headers: { ...options?.headers, [requestTokenHeader]: await requestTokenOf(request) },
      });
  return {
    get: (path, options) => request.get(path, options),
    post: change("POST"),
    put: change("PUT"),
    delete: change("DELETE"),
  };
}

export async function signInColleague(
  requests: APIRequest,
  baseURL: string | undefined,
  persona: Persona,
): Promise<Colleague> {
  const request = await requests.newContext({
    ...(baseURL === undefined ? {} : { baseURL }),
    ignoreHTTPSErrors: true,
  });
  const { person } = await signIn(request, persona);
  return { request, api: requestApi(request), person };
}

export async function signInAdministrator(request: APIRequestContext): Promise<SignedInApi> {
  await signIn(request, "administrator");
  return requestApi(request);
}

export async function reportTo(
  administrator: SignedInApi,
  report: SignedInPerson | undefined,
  manager: SignedInPerson | undefined,
): Promise<void> {
  const managerId = await ownPersonRecordId(administrator, manager);
  const reportId = await ownPersonRecordId(administrator, report);
  await recordMoveRecord(administrator, reportId, {
    effectiveFrom: indiaIsoDate.format(new Date(Date.now() - reportingDaysBack * dayMilliseconds)),
    managerId,
  });
}

// The host's own route submits a request of a test kind as the person signed in to the call, for that person unless the call
// names another, on a new record unless it names one; it takes no antiforgery token.
export async function askForApproval(
  asker: APIRequestContext | SignedInApi,
  kind: TestApprovalKind,
  ask: ApprovalAsk = {},
): Promise<string> {
  const requestId = await submitApproval(asker, kind, ask);
  expect(requestId, `the request of ${kind.key}`).not.toBeNull();
  return requestId ?? "";
}

// A kind the administrators switched off asks for no approval, so its record goes through at once and the answer names no
// request.
export async function submitApproval(
  asker: APIRequestContext | SignedInApi,
  kind: TestApprovalKind,
  ask: ApprovalAsk = {},
): Promise<string | null> {
  const response = await asker.post(approvalRequestsSeedApi, { data: { kind: kind.key, ...ask } });
  expect(response.status(), `the submission of ${kind.key}`).toBe(200);
  const { requestId } = (await response.json()) as { requestId: string | null };
  return requestId;
}

export async function readApprovalRequest(api: SignedInApi, id: string): Promise<ApprovalRequest> {
  const response = await api.get(approvalRequestApi(id));
  expect(response.status(), `the approval request ${id}`).toBe(200);
  return (await response.json()) as ApprovalRequest;
}

export async function listApprovalRequests(
  api: SignedInApi,
  box: ApprovalBox,
  query: Readonly<Record<string, string>> = {},
): Promise<readonly ApprovalRequestSummary[]> {
  const parameters = new URLSearchParams({ box, pageSize: "100", ...query });
  const response = await api.get(`${approvalRequestsApi}?${parameters.toString()}`);
  expect(response.status(), `the ${box} requests`).toBe(200);
  return ((await response.json()) as { items?: readonly ApprovalRequestSummary[] }).items ?? [];
}

export async function decideApproval(
  api: SignedInApi,
  id: string,
  decision: ApprovalDecision,
  comment: string | null = null,
): Promise<ApprovalRequest> {
  const { version } = await readApprovalRequest(api, id);
  const response = await api.post(`${approvalRequestApi(id)}/${decision}`, { data: { version, comment } });
  expect(response.status(), `the decision ${decision} on ${id}`).toBe(200);
  return (await response.json()) as ApprovalRequest;
}

export async function withdrawApproval(
  api: SignedInApi,
  id: string,
  comment: string | null = null,
): Promise<ApprovalRequest> {
  const { version } = await readApprovalRequest(api, id);
  const response = await api.post(`${approvalRequestApi(id)}/withdraw`, { data: { version, comment } });
  expect(response.status(), `the withdrawal of ${id}`).toBe(200);
  return (await response.json()) as ApprovalRequest;
}

// A request a test leaves waiting would wait for its role's holders for the rest of the run, so the person who asked
// withdraws every one the test asked for that still waits.
export async function withdrawWaiting(asker: SignedInApi, ids: readonly string[]): Promise<void> {
  for (const id of ids) {
    const request = await readApprovalRequest(asker, id);
    if (request.state === "submitted") await withdrawApproval(asker, id);
  }
}

export async function readApprovalKinds(api: SignedInApi): Promise<readonly ApprovalKindAnswer[]> {
  const response = await api.get(approvalKindsApi);
  expect(response.status(), "the kinds of request").toBe(200);
  return (await response.json()) as ApprovalKindAnswer[];
}

export async function readApprovalKind(
  api: SignedInApi,
  kind: TestApprovalKind,
): Promise<ApprovalKindAnswer> {
  const answer = (await readApprovalKinds(api)).find((candidate) => candidate.kind === kind.key);
  expect(answer, `the kind ${kind.key}`).toBeDefined();
  return answer as ApprovalKindAnswer;
}

export async function changeApprovalKind(
  administrator: SignedInApi,
  setting: ApprovalKindSetting,
  required: boolean,
  workingDaysToDecide: number,
): Promise<ApprovalKindAnswer> {
  const response = await administrator.put(approvalKindSettingApi(setting.id), {
    data: { required, workingDaysToDecide, version: setting.version },
  });
  expect(response.status(), `the change of the setting ${setting.id}`).toBe(200);
  return (await response.json()) as ApprovalKindAnswer;
}

export async function setUpApprovalKind(
  administrator: SignedInApi,
  kind: TestApprovalKind,
  required: boolean,
  workingDaysToDecide: number,
): Promise<ApprovalKindAnswer> {
  const response = await administrator.post(approvalKindSettingsApi, {
    headers: { "Idempotency-Key": randomUUID() },
    data: { kind: kind.key, required, workingDaysToDecide },
  });
  expect(response.status(), `the setting of ${kind.key}`).toBe(201);
  return (await response.json()) as ApprovalKindAnswer;
}

// The host keeps a kind's setting for the rest of the run and has no route that removes one, so a test that changes a kind
// starts and ends by putting it back as the kind declares it: needing approval, with its own working days.
export async function restoreApprovalKind(
  administrator: SignedInApi,
  kind: TestApprovalKind,
): Promise<ApprovalKindAnswer> {
  const current = await readApprovalKind(administrator, kind);
  const { setting, declaredWorkingDays } = current;
  if (setting === null || (setting.required && setting.workingDaysToDecide === declaredWorkingDays)) {
    return current;
  }
  return changeApprovalKind(administrator, setting, true, declaredWorkingDays);
}

async function requestTokenOf(request: APIRequestContext): Promise<string> {
  const token = (await request.storageState()).cookies.find((cookie) => cookie.name === requestTokenCookie);
  expect(token, `the ${requestTokenCookie} cookie of the request context`).toBeDefined();
  return decodeURIComponent(token?.value ?? "");
}
