import assert from "node:assert/strict";
import { test } from "node:test";

import type { JobRunResponse } from "@dewiride/erp-api-client";

import { runRow, runRows } from "./run-rows.ts";

const createdAt = new Date("2026-11-01T19:00:00Z");

const dueAt = new Date("2026-11-01T20:00:00Z");

function run(values: Partial<JobRunResponse>): JobRunResponse {
  return {
    id: "019a4b2c-3d4e-7f80-9a1b-2c3d4e5f6a7b",
    jobName: "identity.users.directory-account-check",
    jobTitle: "Check Microsoft accounts",
    idempotencyKey: "occurrence:2026-11-02T00:30+05:30",
    status: "failed",
    dueAt,
    createdAt,
    requestedBy: { kind: "system", id: "00000000-0000-0000-0000-000000000001", name: null },
    attemptCount: 2,
    failureCount: 2,
    maxAttempts: 6,
    startedAt: createdAt,
    finishedAt: null,
    cancelRequestedAt: null,
    lastErrorCode: "directory.unavailable",
    lastErrorMessage: "Microsoft Graph did not answer.",
    ...values,
  };
}

test("runRow_ARunOfAJobTheErpHas_KeepsEachDetailAsAPlainValue", () => {
  assert.deepEqual(runRow(run({})), {
    id: "019a4b2c-3d4e-7f80-9a1b-2c3d4e5f6a7b",
    jobName: "identity.users.directory-account-check",
    jobLabel: "Check Microsoft accounts",
    jobDefined: true,
    key: "occurrence:2026-11-02T00:30+05:30",
    status: "failed",
    createdAt,
    dueAt,
    startedAt: createdAt,
    finishedAt: undefined,
    requestedBy: "The ERP",
    attemptCount: 2,
    failureCount: 2,
    maxAttempts: 6,
    stopRequested: false,
    lastErrorCode: "directory.unavailable",
    lastError: "Microsoft Graph did not answer.",
  });
});

test("runRow_ARunOfAJobThisVersionDoesNotHave_IsLabelledByTheJobName", () => {
  const row = runRow(run({ jobName: "test.jobs.retired", jobTitle: null, maxAttempts: null }));

  assert.equal(row?.jobLabel, "test.jobs.retired");
  assert.equal(row?.jobDefined, false);
  assert.equal(row?.maxAttempts, undefined);
});

test("runRow_ARunningRunAskedToStop_IsMarkedAsStopRequested", () => {
  const askedAt = new Date("2026-11-01T19:05:00Z");

  assert.equal(runRow(run({ status: "running", cancelRequestedAt: askedAt }))?.stopRequested, true);
  assert.equal(runRow(run({ status: "running" }))?.stopRequested, false);
  assert.equal(runRow(run({ status: "cancelled", cancelRequestedAt: askedAt }))?.stopRequested, false);
});

test("runRow_APersonWhoAskedForTheRun_IsNamed", () => {
  const requestedBy = {
    kind: "person",
    id: "4f1c2d3e-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
    name: "Asha Rao",
  } as const;

  assert.equal(runRow(run({ requestedBy }))?.requestedBy, "Asha Rao");
});

test("runRows_ARunWithoutAnIdANameOrAStatus_IsLeftOut", () => {
  assert.deepEqual(
    runRows([run({ id: null }), run({ jobName: "" }), run({ status: null }), run({})]).map((row) => row.id),
    ["019a4b2c-3d4e-7f80-9a1b-2c3d4e5f6a7b"],
  );
});

test("runRow_DetailsTheApiSentAsNull_AreUndefined", () => {
  const row = runRow(
    run({
      lastErrorCode: null,
      lastErrorMessage: null,
      startedAt: null,
      attemptCount: null,
      failureCount: null,
    }),
  );

  assert.equal(row?.lastErrorCode, undefined);
  assert.equal(row?.lastError, undefined);
  assert.equal(row?.startedAt, undefined);
  assert.equal(row?.attemptCount, 0);
  assert.equal(row?.failureCount, 0);
});
