import type { SessionAnswer } from "@/shared/api/session/session-watch";

import {
  endCheckDelay,
  planSession,
  renewalDue,
  unknownRetryDelay,
  type SessionPlan,
} from "./session-timing.ts";

export type ExchangeKind = "check" | "renew";

export type KeeperNotice =
  | { readonly kind: "none" }
  | { readonly kind: "idle"; readonly endsAt: number }
  | { readonly kind: "lifetime"; readonly endsAt: number }
  | { readonly kind: "ended" };

export type KeeperState = {
  readonly inFlight: ExchangeKind | undefined;
  readonly queued: ExchangeKind | undefined;
  readonly lastRenewedAt: number | undefined;
  readonly nextCheckAt: number | undefined;
  readonly notice: KeeperNotice;
  readonly lifetimeNoticeSeen: boolean;
  readonly disposed: boolean;
};

export type KeeperStep = {
  readonly state: KeeperState;
  readonly send: ExchangeKind | undefined;
};

const quiet: KeeperNotice = { kind: "none" };

const ended: KeeperNotice = { kind: "ended" };

export function initialKeeperState(): KeeperState {
  return {
    inFlight: undefined,
    queued: undefined,
    lastRenewedAt: undefined,
    nextCheckAt: undefined,
    notice: quiet,
    lifetimeNoticeSeen: false,
    disposed: false,
  };
}

// One exchange runs at a time, so two answers never overtake each other. A request made meanwhile waits for it, and a
// renewal takes the place of a waiting check, because its answer tells the same and also moves the end.
export function requestExchange(state: KeeperState, kind: ExchangeKind, now: number): KeeperStep {
  if (isFinished(state)) return { state, send: undefined };
  if (state.inFlight !== undefined) {
    return { state: { ...state, queued: state.queued === "renew" ? "renew" : kind }, send: undefined };
  }
  return sent(state, kind, now);
}

// A renewal that waited behind a check makes the check's answer stale, so that answer is dropped and the renewal goes out
// at once. The API counts the end to the second, so a check that still finds the session waits a second before the next one,
// and an answer that says nothing about the session keeps the next check at most thirty seconds away.
export function settleExchange(state: KeeperState, answer: SessionAnswer, now: number): KeeperStep {
  if (isFinished(state)) {
    return {
      state: { ...state, inFlight: undefined, queued: undefined, nextCheckAt: undefined },
      send: undefined,
    };
  }
  const settled: KeeperState = { ...state, inFlight: undefined };
  if (state.inFlight === "check" && state.queued === "renew") return sent(settled, "renew", now);

  const next = answered(settled, answer, now);
  if (next.queued === undefined || next.notice.kind === "ended") {
    return { state: { ...next, queued: undefined }, send: undefined };
  }
  return sent(next, next.queued, now);
}

// The API never renews a session past its lifetime, so near that end the person is told once instead of asked to stay.
export function noticeFor(plan: SessionPlan, now: number, lifetimeNoticeSeen: boolean): KeeperNotice {
  if (now < plan.warnAt) return quiet;
  if (plan.extendable) return { kind: "idle", endsAt: plan.endsAt };
  return lifetimeNoticeSeen ? quiet : { kind: "lifetime", endsAt: plan.endsAt };
}

// While a notice waits for the person, activity renews nothing, so a key press can never answer the notice in their place.
export function activityRenews(state: KeeperState, now: number): boolean {
  return !isFinished(state) && state.notice.kind === "none" && renewalDue(state.lastRenewedAt, now);
}

export function checkDue(state: KeeperState, now: number): KeeperStep {
  return requestExchange({ ...state, nextCheckAt: undefined }, "check", now);
}

export function dismissNotice(state: KeeperState, kind: "idle" | "lifetime"): KeeperState {
  if (state.notice.kind !== kind) return state;
  return { ...state, notice: quiet, lifetimeNoticeSeen: state.lifetimeNoticeSeen || kind === "lifetime" };
}

export function disposeKeeper(state: KeeperState): KeeperState {
  return { ...state, disposed: true, queued: undefined, nextCheckAt: undefined };
}

function isFinished(state: KeeperState): boolean {
  return state.disposed || state.notice.kind === "ended";
}

function sent(state: KeeperState, kind: ExchangeKind, now: number): KeeperStep {
  return {
    state: {
      ...state,
      inFlight: kind,
      queued: undefined,
      lastRenewedAt: kind === "renew" ? now : state.lastRenewedAt,
    },
    send: kind,
  };
}

function answered(state: KeeperState, answer: SessionAnswer, now: number): KeeperState {
  switch (answer.state) {
    case "unknown":
      return { ...state, nextCheckAt: Math.min(pendingCheck(state, now), now + unknownRetryDelay) };
    case "ended":
      return { ...state, queued: undefined, nextCheckAt: undefined, notice: ended };
    case "active": {
      const plan = planSession(answer.times);
      return {
        ...state,
        notice: keptNotice(state.notice, noticeFor(plan, now, state.lifetimeNoticeSeen)),
        nextCheckAt: now < plan.warnAt ? plan.warnAt : Math.max(plan.endsAt, now) + endCheckDelay,
      };
    }
  }
}

function pendingCheck(state: KeeperState, now: number): number {
  return state.nextCheckAt !== undefined && state.nextCheckAt > now
    ? state.nextCheckAt
    : Number.POSITIVE_INFINITY;
}

function keptNotice(current: KeeperNotice, next: KeeperNotice): KeeperNotice {
  return current.kind === next.kind && endOf(current) === endOf(next) ? current : next;
}

function endOf(notice: KeeperNotice): number | undefined {
  return notice.kind === "idle" || notice.kind === "lifetime" ? notice.endsAt : undefined;
}
