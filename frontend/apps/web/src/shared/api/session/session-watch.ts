import type { SessionResponse } from "@dewiride/erp-api-client";

import { antiforgeryHeaderName, isAntiforgeryRefusal, sendWithAntiforgery } from "../antiforgery.ts";
import { apiBasePath } from "../base-path.ts";
import { readJson, renewBrowserAntiforgeryTokens } from "../browser-antiforgery.ts";

export type SessionTimes = {
  readonly expiresAt: number;
  readonly lifetimeEndsAt: number;
  readonly clockOffset: number;
};

export type SessionAnswer =
  | { readonly state: "active"; readonly times: SessionTimes }
  | { readonly state: "ended" }
  | { readonly state: "unknown" };

export type SessionExchange = {
  readonly status: number;
  readonly body: unknown;
  readonly date: string | null;
};

export const sessionPath = `${apiBasePath}/auth/session`;

const unreachable: SessionExchange = { status: 0, body: undefined, date: null };

// The page only reads the session here, so it still ends after the idle timeout when the person does nothing.
export async function readSessionTimes(): Promise<SessionAnswer> {
  return sessionAnswer(await send("GET", undefined), Date.now());
}

// The web server's calls renew the session on the API, but only a response the browser receives itself carries the renewed
// cookie, so the page renews it from here while the person uses it and when they choose to stay signed in.
export async function renewSessionTimes(): Promise<SessionAnswer> {
  const exchange = await sendWithAntiforgery<SessionExchange>({
    method: "POST",
    cookies: document.cookie,
    send: ({ token }) => send("POST", token),
    isRefusal: (answer) => isAntiforgeryRefusal(answer.status, answer.body),
    renew: () => renewBrowserAntiforgeryTokens((body) => ({ status: 401, body, date: null })),
  });
  return sessionAnswer(exchange, Date.now());
}

// Both times are the API's clock, so the offset to this browser's clock comes from the answer's Date header, to the second.
export function sessionAnswer({ status, body, date }: SessionExchange, now: number): SessionAnswer {
  if (status === 401) return { state: "ended" };
  if (status !== 200 || typeof body !== "object" || body === null) return { state: "unknown" };

  const { expiresAt, lifetimeEndsAt } = body as Partial<Record<keyof SessionResponse, unknown>>;
  const expires = instant(expiresAt);
  const lifetimeEnds = instant(lifetimeEndsAt);
  if (expires === undefined || lifetimeEnds === undefined) return { state: "unknown" };

  const answeredAt = date === null ? Number.NaN : Date.parse(date);
  return {
    state: "active",
    times: {
      expiresAt: expires,
      lifetimeEndsAt: lifetimeEnds,
      clockOffset: Number.isNaN(answeredAt) ? 0 : answeredAt - now,
    },
  };
}

async function send(method: "GET" | "POST", token: string | undefined): Promise<SessionExchange> {
  const response = await fetch(sessionPath, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    // A renewal the person's activity started still reaches the API when they leave the page before it is answered.
    keepalive: true,
    headers: {
      Accept: "application/json",
      ...(token === undefined ? {} : { [antiforgeryHeaderName]: token }),
    },
  }).catch(() => undefined);
  if (response === undefined) return unreachable;
  return { status: response.status, body: await readJson(response), date: response.headers.get("date") };
}

function instant(value: unknown): number | undefined {
  if (typeof value !== "string") return undefined;
  const time = Date.parse(value);
  return Number.isNaN(time) ? undefined : time;
}
