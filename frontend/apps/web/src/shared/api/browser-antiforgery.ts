import {
  antiforgeryRenewalPath,
  readCookie,
  requestTokenCookieName,
  type AntiforgeryRenewal,
} from "./antiforgery.ts";

export function browserRequestToken(): string | undefined {
  return readCookie(document.cookie, requestTokenCookieName);
}

// The browser stores the cookies a response sets before fetch resolves, so document.cookie read afterwards holds the renewed
// request token. A 401 means the session has ended, which the caller words as its own answer.
export async function renewBrowserAntiforgeryTokens<TAnswer>(
  signedOut: (body: unknown) => TAnswer,
): Promise<AntiforgeryRenewal<TAnswer>> {
  const response = await fetch(antiforgeryRenewalPath, {
    credentials: "same-origin",
    cache: "no-store",
    headers: { Accept: "application/json" },
  }).catch(() => undefined);
  if (response?.status === 204) return { outcome: "renewed", cookies: document.cookie };
  if (response?.status === 401)
    return { outcome: "unauthenticated", answer: signedOut(await readJson(response)) };
  await response?.body?.cancel();
  return { outcome: "failed" };
}

export async function readJson(response: Response): Promise<unknown> {
  return parseJson(await response.text().catch(() => ""));
}

export function parseJson(text: string): unknown {
  if (text.length === 0) return undefined;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}
