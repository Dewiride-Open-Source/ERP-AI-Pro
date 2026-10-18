import { expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { open, readFile, rm, stat } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { addDirectoryPerson, type DirectoryAccountState } from "./directory";
import { peopleApi, type PersonRecord } from "./person-records";
import { apiBaseURL } from "./targets";
import type { SignedInApi } from "./test";

export const accountCheckApi = `${peopleApi}/directory/account-check`;

export type LinkedPerson = PersonRecord & { readonly objectId: string; readonly userPrincipalName: string };

export type CheckedAccounts = {
  readonly checkedCount: number;
  readonly deactivated: readonly {
    readonly id: string;
    readonly displayName: string;
    readonly deactivatedBecause: string;
  }[];
};

export type Deactivation = {
  readonly status: "active" | "deactivated";
  readonly deactivatedBecause: string | null;
  readonly managerId: string | null;
  readonly managerActive: boolean | null;
};

const turnFile = join(tmpdir(), `erp-ai-pro-e2e-account-check-${new URL(apiBaseURL).port}.lock`);

const turnWait = 75_000;

const unwrittenTurnAge = 10_000;

// The check reads only the people linked to an account, and an invitation from the directory links the record from the start.
export async function inviteLinkedPerson(
  api: SignedInApi,
  request: APIRequestContext,
  { displayName, userPrincipalName }: { readonly displayName: string; readonly userPrincipalName: string },
): Promise<LinkedPerson> {
  const objectId = randomUUID();
  await addDirectoryPerson(request, { objectId, displayName, userPrincipalName });
  const response = await api.post(`${peopleApi}/invitations`, {
    headers: { "Idempotency-Key": randomUUID() },
    data: { entraObjectId: objectId, employeeCode: null, phoneNumber: null, dateOfJoining: null, roles: [] },
  });
  expect(response.status(), `the invitation of ${displayName}`).toBe(201);
  return { ...((await response.json()) as PersonRecord), objectId, userPrincipalName };
}

export async function setAccountState(
  request: APIRequestContext,
  person: LinkedPerson,
  accountState: DirectoryAccountState,
): Promise<void> {
  await addDirectoryPerson(request, {
    objectId: person.objectId,
    displayName: person.displayName,
    userPrincipalName: person.userPrincipalName,
    accountState,
  });
}

export async function checkAccounts(api: SignedInApi): Promise<CheckedAccounts> {
  const response = await api.post(accountCheckApi);
  expect(response.status(), "the check of the Microsoft accounts").toBe(200);
  return (await response.json()) as CheckedAccounts;
}

export async function deactivationOf(api: SignedInApi, id: string): Promise<Deactivation> {
  const response = await api.get(`${peopleApi}/${id}`);
  expect(response.status(), `the record ${id}`).toBe(200);
  const { status, deactivatedBecause, managerId, managerActive } = (await response.json()) as Deactivation;
  return { status, deactivatedBecause, managerId, managerActive };
}

// Every check deactivates each active, linked person whose account the test directory holds as disabled or deleted, whoever
// marked it, and every worker and project of a run shares the API host's one database, so the tests that check take turns: a
// test marks accounts and runs its checks only in its turn, which starts with a check of its own that deactivates whatever an
// interrupted turn left marked. A turn is a file its holder creates and removes; Playwright replaces the worker of a failed
// test, so a turn whose process is gone, or is this worker, which runs one test at a time, was left behind and is taken over.
export async function takeAccountCheckTurn(api: SignedInApi): Promise<() => Promise<void>> {
  await expect
    .poll(claimTurn, {
      message: "a turn to check the Microsoft accounts",
      timeout: turnWait,
      intervals: [100, 250, 500],
    })
    .toBe(true);
  const release = () => rm(turnFile, { force: true });
  try {
    await checkAccounts(api);
  } catch (error) {
    await release();
    throw error;
  }
  return release;
}

async function claimTurn(): Promise<boolean> {
  try {
    const turn = await open(turnFile, "wx");
    try {
      await turn.writeFile(String(process.pid));
    } finally {
      await turn.close();
    }
    return true;
  } catch (error) {
    if (!hasCode(error, "EEXIST")) throw error;
  }
  if (await turnLeftBehind()) await rm(turnFile, { force: true });
  return false;
}

async function turnLeftBehind(): Promise<boolean> {
  let holder: number;
  let writtenAt: number;
  try {
    [holder, writtenAt] = await Promise.all([
      readFile(turnFile, "utf8").then((text) => Number.parseInt(text, 10)),
      stat(turnFile).then((file) => file.mtimeMs),
    ]);
  } catch (error) {
    if (hasCode(error, "ENOENT")) return false;
    throw error;
  }
  if (!Number.isInteger(holder) || holder <= 0) return Date.now() - writtenAt > unwrittenTurnAge;
  if (holder === process.pid) return true;
  try {
    process.kill(holder, 0);
    return false;
  } catch (error) {
    return hasCode(error, "ESRCH");
  }
}

function hasCode(error: unknown, code: string): boolean {
  return error instanceof Error && "code" in error && error.code === code;
}
