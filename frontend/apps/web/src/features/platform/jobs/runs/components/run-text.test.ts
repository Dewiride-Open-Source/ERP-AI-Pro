import assert from "node:assert/strict";
import { test } from "node:test";

import type { JobRunEventKind, JobRunEventResponse, JobRunStatus } from "@dewiride/erp-api-client";

import { formFailed, formInvalid } from "@/shared/forms/state/form-state";

import {
  actorText,
  canCancel,
  canRerun,
  cancelDone,
  cancelQuestion,
  eventSentence,
  givesUpText,
  refusedOutcome,
  rerunDone,
  rerunQuestion,
  retryText,
  runEventDays,
  runOutOfDate,
  runStatusDetail,
  runStatusLabel,
  runStatusText,
  showsReference,
  triesText,
} from "./run-text.ts";

const retryAt = new Date("2026-11-01T20:00:00Z");

const system = { kind: "system", id: "00000000-0000-0000-0000-000000000001", name: null } as const;

const administrator = {
  kind: "person",
  id: "4f1c2d3e-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
  name: "Asha Rao",
} as const;

function event(kind: JobRunEventKind, values: Partial<JobRunEventResponse> = {}): JobRunEventResponse {
  return { id: `event-${kind}`, kind, occurredAt: retryAt, actor: system, ...values };
}

test("runStatusLabel_EachStatus_ReadsAsPlainWords", () => {
  const labels: Record<JobRunStatus, string> = {
    pending: "Waiting",
    running: "Running",
    failed: "Failed",
    succeeded: "Done",
    deadLettered: "Gave up",
    cancelled: "Cancelled",
  };

  for (const [status, label] of Object.entries(labels) as [JobRunStatus, string][]) {
    assert.equal(runStatusLabel(status), label, status);
  }
});

test("runStatusText_AFailedRun_SaysWhenItTriesAgainInIndiaTime", () => {
  const run = { status: "failed", dueAt: retryAt, stopRequested: false } as const;

  assert.equal(runStatusDetail(run), "Tries again at 2 Nov 2026, 1:30 am");
  assert.equal(runStatusText(run), "Failed, tries again at 2 Nov 2026, 1:30 am");
});

test("runStatusText_ARunningRunAskedToStop_SaysSo", () => {
  assert.equal(
    runStatusText({ status: "running", dueAt: retryAt, stopRequested: true }),
    "Running, stop requested",
  );
  assert.equal(runStatusText({ status: "running", dueAt: retryAt, stopRequested: false }), "Running");
});

test("runStatusText_EveryOtherStatus_IsItsLabelAlone", () => {
  for (const status of ["pending", "succeeded", "deadLettered", "cancelled"] as const) {
    assert.equal(runStatusDetail({ status, dueAt: retryAt, stopRequested: false }), undefined, status);
    assert.equal(
      runStatusText({ status, dueAt: retryAt, stopRequested: false }),
      runStatusLabel(status),
      status,
    );
  }
});

test("actorText_EachKindOfActor_IsNamedAsAPersonReadsIt", () => {
  assert.equal(actorText(system), "The ERP");
  assert.equal(
    actorText({ kind: "anonymous", id: "00000000-0000-0000-0000-000000000002" }),
    "Someone not signed in",
  );
  assert.equal(actorText(administrator), "Asha Rao");
  assert.equal(actorText({ ...administrator, name: "  " }), "Unknown person");
  assert.equal(actorText(undefined), "Unknown person");
});

test("triesText_TheTriesARunMade_CountTheFailedOnes", () => {
  assert.equal(triesText({ attemptCount: 0, failureCount: 0 }), "Not started yet");
  assert.equal(triesText({ attemptCount: 1, failureCount: 0 }), "1 try");
  assert.equal(triesText({ attemptCount: 3, failureCount: 2 }), "3 tries, 2 failed");
});

test("givesUpText_TheJobsLimit_SaysAfterHowManyFailedTries", () => {
  assert.equal(givesUpText(1), "After 1 failed try");
  assert.equal(givesUpText(6), "After 6 failed tries");
  assert.equal(givesUpText(undefined), "Not known: this version of the ERP does not have this job");
});

test("canRerun_OnlyAFailedGivenUpOrCancelledRunOfAJobTheErpHas_RunsAgain", () => {
  const offered = (status: JobRunStatus, jobDefined = true) =>
    canRerun({ status, dueAt: undefined, stopRequested: false, jobDefined });

  assert.deepEqual(
    (["pending", "running", "failed", "succeeded", "deadLettered", "cancelled"] as const).filter((status) =>
      offered(status),
    ),
    ["failed", "deadLettered", "cancelled"],
  );
  assert.equal(offered("deadLettered", false), false);
});

test("canCancel_AnUnfinishedRun_IsCancelledOrAskedToStopOnce", () => {
  const offered = (status: JobRunStatus, stopRequested = false) =>
    canCancel({ status, dueAt: undefined, stopRequested, jobDefined: false });

  assert.deepEqual(
    (["pending", "running", "failed", "succeeded", "deadLettered", "cancelled"] as const).filter((status) =>
      offered(status),
    ),
    ["pending", "running", "failed"],
  );
  assert.equal(offered("running", true), false);
});

test("rerunQuestion_AJobWithItsLimit_SaysHowManyTriesItGetsAgain", () => {
  assert.deepEqual(rerunQuestion("Check Microsoft accounts", 6), {
    title: "Run Check Microsoft accounts again?",
    description:
      "The ERP starts this run again in a moment, with all 6 of its tries available again. Its earlier tries stay in its history.",
    confirm: "Run again",
    keep: "Keep it as it is",
  });
  assert.match(
    rerunQuestion("Check Microsoft accounts", undefined).description,
    /with all its tries available/,
  );
});

test("cancelQuestion_EachUnfinishedStatus_AsksTheMatchingQuestion", () => {
  assert.deepEqual(cancelQuestion("Check Microsoft accounts", "running"), {
    title: "Ask this run of Check Microsoft accounts to stop?",
    description:
      "The run stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.",
    confirm: "Ask it to stop",
    keep: "Keep it running",
  });
  assert.deepEqual(cancelQuestion("Check Microsoft accounts", "pending"), {
    title: "Cancel this run of Check Microsoft accounts?",
    description: "The run does not start. You can run it again later.",
    confirm: "Cancel the run",
    keep: "Keep the run",
  });
  assert.equal(
    cancelQuestion("Check Microsoft accounts", "failed").description,
    "The run does not try again. You can run it again later.",
  );
});

test("cancelDone_TheOutcome_SaysWhetherTheRunStoppedOrWasAskedTo", () => {
  assert.equal(
    cancelDone("Check Microsoft accounts", false),
    "Cancelled the run of Check Microsoft accounts.",
  );
  assert.equal(
    cancelDone("Check Microsoft accounts", true),
    "Asked the run of Check Microsoft accounts to stop.",
  );
  assert.equal(rerunDone("Check Microsoft accounts"), "Check Microsoft accounts will run again in a moment.");
});

test("refusedOutcome_AFailureOrAMalformedRequest_IsShownBesideTheButton", () => {
  assert.deepEqual(refusedOutcome(formFailed("Refused.", { code: "job.run-finished", reference: "abc" })), {
    done: false,
    message: "Refused.",
    reference: "abc",
  });
  assert.deepEqual(refusedOutcome(formInvalid({ fieldErrors: { id: ["Not a GUID."] }, formErrors: [] })), {
    done: false,
    message: runOutOfDate,
  });
});

test("eventSentence_EachKindOfEvent_SaysWhatHappenedAndWho", () => {
  const sentences: Record<JobRunEventKind, string> = {
    enqueued: "The ERP asked for this run.",
    started: "Try 2 started.",
    succeeded: "Try 2 succeeded.",
    failed: "Try 2 failed.",
    deadLettered: "Try 2 failed, so the ERP gave up on this run.",
    interrupted: "Try 2 stopped because the ERP was shutting down, so it does not count as a failed try.",
    leaseExpired: "Try 2 stopped answering, so it counts as a failed try.",
    cancelRequested: "The ERP asked this run to stop.",
    cancelled: "The ERP cancelled this run.",
    rerun: "The ERP set this run to run again.",
  };

  for (const [kind, sentence] of Object.entries(sentences) as [JobRunEventKind, string][]) {
    assert.equal(eventSentence(event(kind, { attempt: 2 })), sentence, kind);
  }
});

test("eventSentence_AnAdministratorsRequest_NamesThem", () => {
  assert.equal(
    eventSentence(event("rerun", { actor: administrator })),
    "Asha Rao set this run to run again.",
  );
  assert.equal(
    eventSentence(event("cancelRequested", { actor: administrator })),
    "Asha Rao asked this run to stop.",
  );
  assert.equal(eventSentence(event("failed")), "A try failed.");
});

test("retryText_TheTimeOfTheNextTry_IsInIndiaTime", () => {
  assert.equal(retryText(retryAt), "It tries again at 2 Nov 2026, 1:30 am.");
});

test("showsReference_OnlyTheStartOfATry_ShowsItsReference", () => {
  assert.equal(showsReference("started"), true);
  for (const kind of ["failed", "succeeded", "enqueued"] as const)
    assert.equal(showsReference(kind), false, kind);
});

test("runEventDays_EventsOnTwoIndiaDays_AreGroupedByDayInOrder", () => {
  const days = runEventDays([
    event("enqueued", { id: "a", occurredAt: new Date("2026-11-01T18:29:59Z") }),
    event("started", { id: "b", occurredAt: new Date("2026-11-01T18:30:00Z") }),
    event("succeeded", { id: "c", occurredAt: new Date("2026-11-01T18:31:00Z") }),
    event("rerun", { id: "d", occurredAt: null }),
  ]);

  assert.deepEqual(
    days.map((day) => ({ key: day.key, heading: day.heading, ids: day.events.map((item) => item.id) })),
    [
      { key: "2026-11-01", heading: "Sunday, 1 November 2026", ids: ["a"] },
      { key: "2026-11-02", heading: "Monday, 2 November 2026", ids: ["b", "c"] },
    ],
  );
});
