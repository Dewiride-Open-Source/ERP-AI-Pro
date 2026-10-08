import type { SessionTimes } from "@/shared/api/session/session-watch";

export type SessionPlan = {
  readonly endsAt: number;
  readonly warnAt: number;
  readonly extendable: boolean;
};

export const idleWarningLead = 2 * 60 * 1000;

export const lifetimeWarningLead = 5 * 60 * 1000;

export const renewalInterval = 60 * 1000;

export const endCheckDelay = 1000;

// The API never renews a session past its lifetime, so once the idle expiry is the lifetime's end, staying signed in cannot
// extend it and the warning comes earlier, with time to save work before signing in again. WCAG 2.2.1 asks for at least twenty
// seconds to extend a time limit; both leads are minutes. Times are turned from the API's clock into this browser's.
export function planSession({ expiresAt, lifetimeEndsAt, clockOffset }: SessionTimes): SessionPlan {
  const endsAt = expiresAt - clockOffset;
  const extendable = expiresAt < lifetimeEndsAt;
  return { endsAt, extendable, warnAt: endsAt - (extendable ? idleWarningLead : lifetimeWarningLead) };
}

// Each renewal restarts the idle timeout, so renewing more than once a minute while the person works gains less than a minute
// of session for every extra request.
export function renewalDue(lastRenewedAt: number | undefined, now: number): boolean {
  return lastRenewedAt === undefined || now - lastRenewedAt >= renewalInterval;
}
