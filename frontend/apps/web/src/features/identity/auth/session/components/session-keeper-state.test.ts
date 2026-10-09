import assert from "node:assert/strict";
import { test } from "node:test";

import type { SessionAnswer } from "@/shared/api/session/session-watch";

import {
  activityRenews,
  checkDue,
  dismissNotice,
  disposeKeeper,
  initialKeeperState,
  noticeFor,
  requestExchange,
  settleExchange,
  type ExchangeKind,
  type KeeperState,
} from "./session-keeper-state.ts";
import { planSession } from "./session-timing.ts";

const now = Date.parse("2026-10-08T09:00:00Z");

const lifetimeEnd = now + 12 * 60 * 60 * 1000;

const unknown: SessionAnswer = { state: "unknown" };

const sessionEnded: SessionAnswer = { state: "ended" };

function active(expiresAt: number, lifetimeEndsAt = lifetimeEnd, clockOffset = 0): SessionAnswer {
  return { state: "active", times: { expiresAt, lifetimeEndsAt, clockOffset } };
}

function inFlight(kind: ExchangeKind, state = initialKeeperState()): KeeperState {
  const step = requestExchange(state, kind, now);
  assert.equal(step.send, kind);
  return step.state;
}

function answeredState(answer: SessionAnswer, at = now): KeeperState {
  return settleExchange(inFlight("renew"), answer, at).state;
}

test("requestExchange_NothingInFlight_SendsItAndARenewalMovesTheLastRenewal", () => {
  const renewal = requestExchange(initialKeeperState(), "renew", now);
  const check = requestExchange(initialKeeperState(), "check", now);

  assert.equal(renewal.send, "renew");
  assert.equal(renewal.state.lastRenewedAt, now);
  assert.equal(check.send, "check");
  assert.equal(check.state.lastRenewedAt, undefined);
});

test("requestExchange_ExchangeInFlight_QueuesTheRequestWithoutSendingOrMovingTheLastRenewal", () => {
  const step = requestExchange(inFlight("check"), "renew", now + 10);

  assert.equal(step.send, undefined);
  assert.equal(step.state.queued, "renew");
  assert.equal(step.state.lastRenewedAt, undefined);
});

test("requestExchange_RenewalAfterAQueuedCheck_TakesTheCheckPlace", () => {
  let state = requestExchange(inFlight("renew"), "check", now).state;
  state = requestExchange(state, "renew", now).state;

  assert.equal(state.queued, "renew");
});

test("requestExchange_CheckAfterAQueuedRenewal_KeepsTheRenewal", () => {
  let state = requestExchange(inFlight("check"), "renew", now).state;
  state = requestExchange(state, "check", now).state;

  assert.equal(state.queued, "renew");
});

test("settleExchange_RenewalRequestedWhileACheckRuns_DropsTheCheckAnswerAndSendsTheRenewal", () => {
  const warning = settleExchange(inFlight("check"), active(now + 60_000), now).state;
  assert.equal(warning.notice.kind, "idle");
  const staying = dismissNotice(warning, "idle");
  const checking = inFlight("check", staying);
  const queued = requestExchange(checking, "renew", now + 5).state;

  const step = settleExchange(queued, active(now + 60_000), now + 10);

  assert.equal(step.send, "renew");
  assert.deepEqual(step.state.notice, { kind: "none" });
  assert.equal(step.state.lastRenewedAt, now + 10);
  assert.equal(step.state.queued, undefined);
  assert.equal(step.state.inFlight, "renew");
});

test("settleExchange_QueuedCheckAfterARenewal_SendsTheCheckOnceTheRenewalIsAnswered", () => {
  const queued = requestExchange(inFlight("renew"), "check", now).state;

  const step = settleExchange(queued, active(now + 30 * 60_000), now + 10);

  assert.equal(step.send, "check");
  assert.equal(step.state.inFlight, "check");
  assert.equal(step.state.queued, undefined);
});

test("settleExchange_SessionBeforeTheWarning_ChecksAgainTwoMinutesBeforeTheIdleEnd", () => {
  const state = answeredState(active(now + 300_000));

  assert.deepEqual(state.notice, { kind: "none" });
  assert.equal(state.nextCheckAt, now + 180_000);
});

test("settleExchange_SessionAtTheEndOfItsLifetime_ChecksAgainFiveMinutesBeforeItEnds", () => {
  const state = answeredState(active(now + 600_000, now + 600_000));

  assert.deepEqual(state.notice, { kind: "none" });
  assert.equal(state.nextCheckAt, now + 300_000);
});

test("settleExchange_SessionInsideTheIdleWarning_WarnsAndChecksASecondAfterTheEnd", () => {
  const state = answeredState(active(now + 120_000));

  assert.deepEqual(state.notice, { kind: "idle", endsAt: now + 120_000 });
  assert.equal(state.nextCheckAt, now + 121_000);
});

test("settleExchange_SessionInsideTheLifetimeWarning_TellsAndChecksASecondAfterTheEnd", () => {
  const state = answeredState(active(now + 300_000, now + 300_000));

  assert.deepEqual(state.notice, { kind: "lifetime", endsAt: now + 300_000 });
  assert.equal(state.nextCheckAt, now + 301_000);
});

test("settleExchange_SessionStillFoundAfterItsEnd_ChecksAgainASecondLater", () => {
  const state = answeredState(active(now - 500));

  assert.equal(state.notice.kind, "idle");
  assert.equal(state.nextCheckAt, now + 1000);
});

test("settleExchange_SameWarningAgain_KeepsTheShownNotice", () => {
  const first = answeredState(active(now + 60_000));
  const second = settleExchange(inFlight("check", first), active(now + 60_000), now + 1000).state;

  assert.equal(second.notice, first.notice);
});

test("settleExchange_SessionRenewedElsewhere_ClosesTheWarning", () => {
  const warning = answeredState(active(now + 60_000));
  const state = settleExchange(inFlight("check", warning), active(now + 30 * 60_000), now + 1000).state;

  assert.deepEqual(state.notice, { kind: "none" });
  assert.equal(state.nextCheckAt, now + 30 * 60_000 - 120_000);
});

test("settleExchange_AnswerThatSaysNothing_ChecksAgainInThirtySecondsAndKeepsTheNotice", () => {
  const warning = answeredState(active(now + 90_000));
  const state = settleExchange(inFlight("check", warning), unknown, now).state;

  assert.equal(state.notice, warning.notice);
  assert.equal(state.nextCheckAt, now + 30_000);
});

test("settleExchange_AnswerThatSaysNothingBeforeAnEarlierCheck_KeepsTheEarlierCheck", () => {
  const warning = answeredState(active(now + 5000));
  assert.equal(warning.nextCheckAt, now + 6000);

  const state = settleExchange(inFlight("check", warning), unknown, now + 1000).state;

  assert.equal(state.nextCheckAt, now + 6000);
});

test("settleExchange_TwoAnswersThatSayNothing_LeaveOneCheckThirtySecondsAway", () => {
  const first = answeredState(unknown);
  const second = settleExchange(inFlight("check", first), unknown, now).state;

  assert.equal(first.nextCheckAt, now + 30_000);
  assert.equal(second.nextCheckAt, now + 30_000);
});

test("settleExchange_AnswerThatSaysNothingAfterTheCheckWasDue_ChecksAgainInThirtySeconds", () => {
  const first = answeredState(unknown);
  const later = now + 30_000;

  const state = settleExchange(inFlight("check", first), unknown, later).state;

  assert.equal(state.nextCheckAt, later + 30_000);
});

test("checkDue_AnswerThatSaysNothingBeforeTheBrowserClockReachesTheCheck_ChecksAgainInThirtySeconds", () => {
  const first = answeredState(active(now + 300_000));
  const dueAt = first.nextCheckAt ?? Number.NaN;

  const step = checkDue(first, dueAt - 5);
  const state = settleExchange(step.state, unknown, dueAt - 3).state;

  assert.equal(step.send, "check");
  assert.equal(step.state.nextCheckAt, undefined);
  assert.equal(state.nextCheckAt, dueAt + 29_997);
});

test("settleExchange_OnlyAnUnauthenticatedAnswer_EndsTheSessionAndStopsEverything", () => {
  const warning = answeredState(active(now + 60_000));
  const saidNothing = settleExchange(inFlight("check", warning), unknown, now).state;
  assert.equal(saidNothing.notice.kind, "idle");
  const queued = requestExchange(inFlight("renew", saidNothing), "check", now).state;

  const step = settleExchange(queued, sessionEnded, now);

  assert.equal(step.send, undefined);
  assert.deepEqual(step.state.notice, { kind: "ended" });
  assert.equal(step.state.nextCheckAt, undefined);
  assert.equal(step.state.queued, undefined);
  assert.equal(requestExchange(step.state, "check", now).send, undefined);
  assert.equal(requestExchange(step.state, "renew", now).send, undefined);
  assert.equal(activityRenews(step.state, now + 60 * 60_000), false);
});

test("settleExchange_AnswerAfterTheKeeperIsGone_SchedulesAndSendsNothing", () => {
  const queued = requestExchange(inFlight("renew"), "check", now).state;
  const gone = disposeKeeper(queued);

  const step = settleExchange(gone, active(now + 60_000), now);

  assert.equal(step.send, undefined);
  assert.equal(step.state.nextCheckAt, undefined);
  assert.deepEqual(step.state.notice, { kind: "none" });
  assert.equal(requestExchange(step.state, "renew", now).send, undefined);
  assert.equal(activityRenews(step.state, now), false);
});

test("noticeFor_ApiClockBehindTheBrowser_ShowsTheEndOnTheBrowserClock", () => {
  const expiresAt = now + 60_000;
  const plan = planSession({ expiresAt, lifetimeEndsAt: lifetimeEnd, clockOffset: -5000 });

  assert.deepEqual(noticeFor(plan, now, false), { kind: "idle", endsAt: expiresAt + 5000 });
});

test("noticeFor_BeforeTheWarning_ShowsNothing", () => {
  const plan = planSession({ expiresAt: now + 120_001, lifetimeEndsAt: lifetimeEnd, clockOffset: 0 });

  assert.deepEqual(noticeFor(plan, now, false), { kind: "none" });
  assert.deepEqual(noticeFor(plan, now + 1, false), { kind: "idle", endsAt: now + 120_001 });
});

test("noticeFor_LifetimeNoticeAlreadySeen_ShowsNothing", () => {
  const plan = planSession({ expiresAt: now + 60_000, lifetimeEndsAt: now + 60_000, clockOffset: 0 });

  assert.deepEqual(noticeFor(plan, now, false), { kind: "lifetime", endsAt: now + 60_000 });
  assert.deepEqual(noticeFor(plan, now, true), { kind: "none" });
});

test("dismissNotice_LifetimeNotice_WarnsOnlyOnce", () => {
  const told = answeredState(active(now + 240_000, now + 240_000));
  assert.equal(told.notice.kind, "lifetime");

  const continued = dismissNotice(told, "lifetime");
  const again = settleExchange(
    inFlight("check", continued),
    active(now + 240_000, now + 240_000),
    now + 1000,
  );

  assert.deepEqual(continued.notice, { kind: "none" });
  assert.deepEqual(again.state.notice, { kind: "none" });
  assert.equal(again.state.nextCheckAt, now + 241_000);
});

test("dismissNotice_AnotherNoticeShown_ChangesNothing", () => {
  const warning = answeredState(active(now + 60_000));

  assert.equal(dismissNotice(warning, "lifetime"), warning);
});

test("activityRenews_AMinuteAfterTheLastRenewal_RenewsOnlyWhileNoNoticeIsShown", () => {
  const quiet = answeredState(active(now + 30 * 60_000));
  const warning = answeredState(active(now + 60_000));

  assert.equal(activityRenews(quiet, now + 59_999), false);
  assert.equal(activityRenews(quiet, now + 60_000), true);
  assert.equal(activityRenews(warning, now + 60_000), false);
  assert.equal(activityRenews(initialKeeperState(), now), true);
});
