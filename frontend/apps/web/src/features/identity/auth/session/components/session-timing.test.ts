import assert from "node:assert/strict";
import { test } from "node:test";

import {
  idleWarningLead,
  lifetimeWarningLead,
  planSession,
  renewalDue,
  renewalInterval,
} from "./session-timing.ts";

const expiresAt = Date.parse("2026-10-08T09:30:00Z");

test("planSession_SessionThatCanBeRenewed_WarnsTwoMinutesBeforeItEnds", () => {
  assert.deepEqual(
    planSession({ expiresAt, lifetimeEndsAt: Date.parse("2026-10-08T21:00:00Z"), clockOffset: 0 }),
    {
      endsAt: expiresAt,
      extendable: true,
      warnAt: expiresAt - idleWarningLead,
    },
  );
});

test("planSession_SessionAtTheEndOfItsLifetime_WarnsFiveMinutesBeforeItEnds", () => {
  assert.deepEqual(planSession({ expiresAt, lifetimeEndsAt: expiresAt, clockOffset: 0 }), {
    endsAt: expiresAt,
    extendable: false,
    warnAt: expiresAt - lifetimeWarningLead,
  });
});

test("planSession_BrowserClockBehindTheApi_MovesTheTimesOntoTheBrowserClock", () => {
  const plan = planSession({ expiresAt, lifetimeEndsAt: expiresAt + 1, clockOffset: 7000 });

  assert.equal(plan.endsAt, expiresAt - 7000);
  assert.equal(plan.warnAt, expiresAt - 7000 - idleWarningLead);
});

test("renewalDue_NoRenewalYet_IsDue", () => {
  assert.equal(renewalDue(undefined, expiresAt), true);
});

test("renewalDue_WithinAMinuteOfTheLastRenewal_IsNotDue", () => {
  assert.equal(renewalDue(expiresAt, expiresAt + renewalInterval - 1), false);
});

test("renewalDue_AMinuteAfterTheLastRenewal_IsDue", () => {
  assert.equal(renewalDue(expiresAt, expiresAt + renewalInterval), true);
});
