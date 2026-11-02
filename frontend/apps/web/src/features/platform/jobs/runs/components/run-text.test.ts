import assert from "node:assert/strict";
import { test } from "node:test";

import type { JobRunEventKind, JobRunEventResponse, JobRunStatus } from "@dewiride/erp-api-client";

import { formFailed, formInvalid } from "@/shared/forms/state/form-state";

import {
  actorText,
  canCancel,
  canRerun,
  cancelDone,
  cancelOutcome,
  cancelProblemMessages,
  cancelQuestion,
  eventErrorText,
  eventSentence,
  givesUpText,
  lastErrorText,
  noError,
  refusedOutcome,
  rerunDone,
  rerunProblemMessages,
  rerunQuestion,
  retryText,
  runActionName,
  runEventDays,
  runGone,
  runLinkName,
  runName,
  runOutOfDate,
  runStatusDetail,
  runStatusLabel,
  runStatusText,
  showsReference,
  stillRunning,
  triesText,
  type RunSubject,
} from "./run-text.ts";

const retryAt = new Date("2026-11-01T20:00:00Z");

const createdAt = new Date("2026-11-01T19:00:00Z");

const system = { kind: "system", id: "00000000-0000-0000-0000-000000000001", name: null } as const;

const administrator = {
  kind: "person",
  id: "4f1c2d3e-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
  name: "Asha Rao",
} as const;

const unexpected =
  "Something unexpected went wrong; the server's log holds the details under the try's reference.";

function event(kind: JobRunEventKind, values: Partial<JobRunEventResponse> = {}): JobRunEventResponse {
  return { id: `event-${kind}`, kind, occurredAt: retryAt, actor: system, ...values };
}

function subject(values: Partial<RunSubject> = {}): RunSubject {
  return {
    jobLabel: "Check Microsoft accounts",
    createdAt,
    key: "occurrence:2026-11-02T00:30+05:30",
    status: "failed",
    dueAt: retryAt,
    stopRequested: false,
    jobDefined: true,
    maxAttempts: 6,
    ...values,
  };
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

test("canCancel_AnUnfinishedRunOfAJobTheErpHas_IsCancelledOrAskedToStopOnce", () => {
  const offered = (status: JobRunStatus, stopRequested = false) =>
    canCancel({ status, dueAt: undefined, stopRequested, jobDefined: true });

  assert.deepEqual(
    (["pending", "running", "failed", "succeeded", "deadLettered", "cancelled"] as const).filter((status) =>
      offered(status),
    ),
    ["pending", "running", "failed"],
  );
  assert.equal(offered("running", true), false);
});

test("canCancel_ARunningRunOfAJobThisVersionDoesNotHave_IsOfferedAgainAfterAStopRequest", () => {
  const offered = (status: JobRunStatus, stopRequested = false) =>
    canCancel({ status, dueAt: undefined, stopRequested, jobDefined: false });

  assert.deepEqual(
    (["pending", "running", "failed", "succeeded", "deadLettered", "cancelled"] as const).filter((status) =>
      offered(status),
    ),
    ["pending", "running", "failed"],
  );
  assert.equal(offered("running", true), true);
});

test("runName_ARunWithItsCreationTime_IsNamedByWhenItWasCreatedInIndiaTime", () => {
  assert.equal(
    runName({ createdAt, key: "occurrence:2026-11-02T00:30+05:30" }),
    "created 2 Nov 2026, 12:30 am",
  );
  assert.equal(
    runName({ createdAt: undefined, key: "occurrence:2026-11-02T00:30+05:30" }),
    "key occurrence:2026-11-02T00:30+05:30",
  );
});

test("runLinkName_ARun_IsNamedByItsJobThenWhatItsRowShowsOfIt", () => {
  assert.equal(runLinkName(subject()), "Check Microsoft accounts, created 2 Nov 2026, 12:30 am");
  assert.equal(
    runLinkName(subject({ jobLabel: "e2e.jobs.retired", createdAt: undefined, key: "nightly" })),
    "e2e.jobs.retired, key nightly",
  );
});

test("runActionName_EachAction_NamesTheJobAndTheRun", () => {
  assert.equal(
    runActionName("Run again", subject()),
    "Run again: Check Microsoft accounts, created 2 Nov 2026, 12:30 am",
  );
  assert.equal(
    runActionName("Cancel", subject({ jobLabel: "e2e.jobs.retired", createdAt: undefined, key: "nightly" })),
    "Cancel: e2e.jobs.retired, key nightly",
  );
});

test("rerunQuestion_AJobWithItsLimit_NamesTheRunAndSaysHowManyTriesItGetsAgain", () => {
  assert.deepEqual(rerunQuestion(subject()), {
    title: "Run Check Microsoft accounts again?",
    description:
      "This run, created 2 Nov 2026, 12:30 am, starts again in a moment, with all 6 of its tries available again. Its earlier tries stay in its history.",
    confirm: "Run again",
    keep: "Keep it as it is",
  });
  assert.match(
    rerunQuestion(subject({ maxAttempts: undefined })).description,
    /with all its tries available/,
  );
});

test("cancelQuestion_EachUnfinishedRunOfAJobTheErpHas_AsksTheMatchingQuestion", () => {
  assert.deepEqual(cancelQuestion(subject({ status: "running" })), {
    title: "Ask this run of Check Microsoft accounts to stop?",
    description:
      "This run, created 2 Nov 2026, 12:30 am, stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.",
    confirm: "Ask it to stop",
    keep: "Keep it running",
  });
  assert.deepEqual(cancelQuestion(subject({ status: "pending" })), {
    title: "Cancel this run of Check Microsoft accounts?",
    description: "This run, created 2 Nov 2026, 12:30 am, does not start. You can run it again later.",
    confirm: "Cancel the run",
    keep: "Keep the run",
  });
  assert.equal(
    cancelQuestion(subject({ status: "failed" })).description,
    "This run, created 2 Nov 2026, 12:30 am, does not try again. You can run it again later.",
  );
});

test("cancelQuestion_ARunOfAJobThisVersionDoesNotHave_PromisesNoRunAgainAndNoStopWithinAMinute", () => {
  const retired = { jobLabel: "e2e.jobs.retired", jobDefined: false } as const;
  const notHere =
    "If it has stopped answering, it is cancelled at once; otherwise it stops only once the version of the ERP that started it notices the request.";

  assert.equal(
    cancelQuestion(subject({ ...retired, status: "pending" })).description,
    "This run, created 2 Nov 2026, 12:30 am, does not start. This version of the ERP does not have its job, so it cannot run it again.",
  );
  assert.equal(
    cancelQuestion(subject({ ...retired, status: "failed" })).description,
    "This run, created 2 Nov 2026, 12:30 am, does not try again. This version of the ERP does not have its job, so it cannot run it again.",
  );
  assert.deepEqual(cancelQuestion(subject({ ...retired, status: "running" })), {
    title: "Ask this run of e2e.jobs.retired to stop?",
    description: `This run, created 2 Nov 2026, 12:30 am, belongs to a job this version of the ERP does not have. ${notHere}`,
    confirm: "Ask it to stop",
    keep: "Keep it running",
  });
  assert.deepEqual(cancelQuestion(subject({ ...retired, status: "running", stopRequested: true })), {
    title: "Cancel this run of e2e.jobs.retired?",
    description: `This run, created 2 Nov 2026, 12:30 am, was asked to stop, but it belongs to a job this version of the ERP does not have. ${notHere}`,
    confirm: "Cancel the run",
    keep: "Keep it running",
  });
});

test("cancelOutcome_ARunAskedToStopAgainThatIsStillRunning_IsShownAsNotCancelledYet", () => {
  assert.deepEqual(cancelOutcome({ done: true, stopRequested: true }, true), {
    done: false,
    message: stillRunning,
  });
  assert.deepEqual(cancelOutcome({ done: true, stopRequested: true }, false), {
    done: true,
    stopRequested: true,
  });
  assert.deepEqual(cancelOutcome({ done: true, stopRequested: false }, true), {
    done: true,
    stopRequested: false,
  });
  assert.deepEqual(cancelOutcome({ done: false, message: runGone }, true), { done: false, message: runGone });
});

test("rerunProblemMessages_RefusalsOfTheApi_AreWordedForThePerson", () => {
  assert.deepEqual(Object.keys(rerunProblemMessages).sort(), [
    "job.not-defined",
    "job.run-not-found",
    "job.run-not-rerunnable",
  ]);
  assert.equal(
    rerunProblemMessages["job.run-not-rerunnable"],
    "This run is waiting, running or done by now, so it cannot run again. Reload the page to see where it stands.",
  );
  assert.equal(
    rerunProblemMessages["job.not-defined"],
    "This version of the ERP no longer has the job of this run, so the run cannot run again.",
  );
  assert.equal(rerunProblemMessages["job.run-not-found"], runGone);
});

test("cancelProblemMessages_RefusalsOfTheApi_AreWordedForThePerson", () => {
  assert.deepEqual(Object.keys(cancelProblemMessages).sort(), [
    "concurrency.conflict",
    "job.run-finished",
    "job.run-not-found",
  ]);
  assert.equal(
    cancelProblemMessages["job.run-finished"],
    "This run has finished meanwhile, so there is nothing to cancel. Reload the page to see how it ended.",
  );
  assert.equal(
    cancelProblemMessages["concurrency.conflict"],
    "This run kept changing while it was being cancelled. Reload the page, then try again.",
  );
  assert.equal(cancelProblemMessages["job.run-not-found"], runGone);
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

test("eventErrorText_ATryThatStoppedAnswering_IsSaidOnceInPlainWords", () => {
  const leaseExpired = {
    errorCode: "job.lease-expired",
    errorMessage: "The runner of the attempt stopped renewing its lease, so the attempt counts as failed.",
  };

  assert.equal(eventErrorText(event("leaseExpired", leaseExpired)), undefined);
  assert.equal(eventErrorText(event("cancelled", leaseExpired)), "The try had stopped answering.");
  assert.equal(eventErrorText(event("deadLettered", leaseExpired)), "The try stopped answering.");
});

test("eventErrorText_TheErpsOwnReasonsATryFailed_AreWordedWithoutItsTechnicalMessage", () => {
  assert.equal(
    eventErrorText(
      event("failed", {
        errorCode: "job.timed-out",
        errorMessage: "The attempt ran longer than its limit of 00:10:00 and was told to stop.",
      }),
    ),
    "The try ran longer than its time limit, so the ERP stopped it.",
  );
  assert.equal(
    eventErrorText(
      event("deadLettered", {
        errorCode: "job.exception",
        errorMessage: "The attempt threw HttpRequestException; the log of the attempt holds the details.",
      }),
    ),
    unexpected,
  );
});

test("eventErrorText_AnErrorTheJobItselfRecorded_IsItsStoredMessage", () => {
  assert.equal(
    eventErrorText(
      event("failed", {
        errorCode: "directory.unavailable",
        errorMessage: "Microsoft Graph did not answer.",
      }),
    ),
    "Microsoft Graph did not answer.",
  );
  assert.equal(
    eventErrorText(event("failed", { errorCode: "directory.unavailable", errorMessage: " " })),
    undefined,
  );
  assert.equal(eventErrorText(event("succeeded")), undefined);
});

test("lastErrorText_TheErpsOwnReasonsATryFailed_AreWordedWithoutItsTechnicalMessage", () => {
  const stored = "A technical message the page does not show.";

  assert.equal(
    lastErrorText({ lastErrorCode: "job.lease-expired", lastError: stored }),
    "A try stopped answering, so it counted as a failed try.",
  );
  assert.equal(
    lastErrorText({ lastErrorCode: "job.timed-out", lastError: stored }),
    "A try ran longer than its time limit, so the ERP stopped it.",
  );
  assert.equal(lastErrorText({ lastErrorCode: "job.exception", lastError: stored }), unexpected);
});

test("lastErrorText_AnErrorTheJobItselfRecordedOrNone_IsItsStoredMessageOrNone", () => {
  assert.equal(
    lastErrorText({ lastErrorCode: "directory.unavailable", lastError: "Microsoft Graph did not answer." }),
    "Microsoft Graph did not answer.",
  );
  assert.equal(lastErrorText({ lastErrorCode: undefined, lastError: undefined }), noError);
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
