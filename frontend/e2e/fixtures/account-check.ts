import { expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { link, open, readFile, rename, rm, stat, utimes } from "node:fs/promises";
import { join } from "node:path";

import { turnFolderVariable } from "../global-setup";
import { addDirectoryPerson, type DirectoryAccountState } from "./directory";
import { peopleApi, type PersonRecord } from "./person-records";
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

const turnWait = 75_000;

const turnTouchInterval = 2_000;

const turnLeftAfter = 20_000;

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
// interrupted turn left marked. A turn is a file in the run's folder for turns, which its holder creates with a token of its own
// and touches while it holds it. A turn untouched for longer than a holder ever pauses, or held by this worker, which runs one
// test at a time, was left behind: it is moved aside first, which only one worker can do, and removed only when it is still the
// turn found left behind, so a turn claimed meanwhile is put back, and a holder removes its turn only while it still holds its
// token.
export async function takeAccountCheckTurn(api: SignedInApi): Promise<() => Promise<void>> {
  const file = turnFile();
  const token = randomUUID();
  await expect
    .poll(() => claimTurn(file, token), {
      message: "a turn to check the Microsoft accounts",
      timeout: turnWait,
      intervals: [100, 250, 500],
    })
    .toBe(true);
  const touching = setInterval(() => void touchTurn(file, token), turnTouchInterval);
  touching.unref();
  const release = async () => {
    clearInterval(touching);
    await releaseTurn(file, token);
  };
  try {
    await checkAccounts(api);
  } catch (error) {
    await release();
    throw error;
  }
  return release;
}

type Turn = { readonly token: string; readonly pid: number };

// Playwright makes what the global setup sets available inside tests only, so the folder is read when a test asks for a turn.
function turnFile(): string {
  const folder = process.env[turnFolderVariable];
  if (!folder) {
    throw new Error(`${turnFolderVariable} is not set: the global setup makes the run's folder for turns`);
  }
  return join(folder, "account-check.lock");
}

async function claimTurn(file: string, token: string): Promise<boolean> {
  try {
    const turn = await open(file, "wx", 0o600);
    try {
      await turn.writeFile(JSON.stringify({ token, pid: process.pid } satisfies Turn));
    } finally {
      await turn.close();
    }
    return true;
  } catch (error) {
    if (!hasCode(error, "EEXIST")) throw error;
  }
  const held = await heldTurn(file);
  if (held !== undefined && (held.turn?.pid === process.pid || Date.now() - held.touchedAt > turnLeftAfter)) {
    await takeOver(file, held.turn);
  }
  return false;
}

async function heldTurn(
  file: string,
): Promise<{ readonly turn: Turn | undefined; readonly touchedAt: number } | undefined> {
  try {
    const [text, written] = await Promise.all([readFile(file, "utf8"), stat(file)]);
    return { turn: turnOf(text), touchedAt: written.mtimeMs };
  } catch (error) {
    if (hasCode(error, "ENOENT")) return undefined;
    throw error;
  }
}

async function takeOver(file: string, leftBehind: Turn | undefined): Promise<void> {
  const aside = `${file}.${randomUUID()}`;
  try {
    await rename(file, aside);
  } catch (error) {
    if (hasCode(error, "ENOENT")) return;
    throw error;
  }
  const moved = await heldTurn(aside);
  if (moved !== undefined && moved.turn?.token !== leftBehind?.token) {
    try {
      await link(aside, file);
    } catch (error) {
      if (!hasCode(error, "EEXIST")) throw error;
    }
  }
  await rm(aside, { force: true });
}

async function touchTurn(file: string, token: string): Promise<void> {
  const held = await heldTurn(file).catch(() => undefined);
  if (held?.turn?.token !== token) return;
  const now = new Date();
  await utimes(file, now, now).catch(() => undefined);
}

async function releaseTurn(file: string, token: string): Promise<void> {
  const held = await heldTurn(file);
  if (held?.turn?.token === token) await rm(file, { force: true });
}

function turnOf(text: string): Turn | undefined {
  try {
    const turn: unknown = JSON.parse(text);
    return typeof turn === "object" &&
      turn !== null &&
      "token" in turn &&
      typeof turn.token === "string" &&
      "pid" in turn &&
      typeof turn.pid === "number"
      ? { token: turn.token, pid: turn.pid }
      : undefined;
  } catch {
    return undefined;
  }
}

function hasCode(error: unknown, code: string): boolean {
  return error instanceof Error && "code" in error && error.code === code;
}
