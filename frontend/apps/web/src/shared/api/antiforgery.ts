import { apiBasePath } from "./base-path.ts";

export const antiforgeryHeaderName = "X-XSRF-TOKEN";

export const requestTokenCookieName = "__Host-erp-xsrf";

export const requestTokenFieldName = "__RequestVerificationToken";

export const antiforgeryRenewalPath = `${apiBasePath}/auth/antiforgery`;

export const antiforgeryCodePrefix = "antiforgery.";

export const antiforgeryRefusalMessage = "This could not be sent. Try again.";

export type SetCookie = {
  readonly name: string;
  readonly value: string;
  readonly httpOnly: boolean;
  readonly secure: boolean;
  readonly sameSite: "strict" | "lax" | "none" | undefined;
  readonly path: string | undefined;
  readonly maxAge: number | undefined;
  readonly expires: Date | undefined;
};

export type AntiforgeryAttempt = { readonly cookies: string; readonly token: string | undefined };

export type AntiforgeryRenewal<TAnswer> =
  | { readonly outcome: "renewed"; readonly cookies: string }
  | { readonly outcome: "unauthenticated"; readonly answer: TAnswer }
  | { readonly outcome: "failed" };

export type AntiforgeryExchange<TAnswer> = {
  readonly method: string | undefined;
  readonly cookies: string;
  readonly send: (attempt: AntiforgeryAttempt) => Promise<TAnswer>;
  readonly isRefusal: (answer: TAnswer) => boolean | Promise<boolean>;
  readonly renew: (() => Promise<AntiforgeryRenewal<TAnswer>>) | undefined;
};

export type ApiFetch = (url: string, init: RequestInit) => Promise<Response>;

export type AntiforgeryFetchOptions = {
  readonly fetch: ApiFetch;
  readonly renewalUrl: string;
  readonly renewalHeaders: () => Promise<Readonly<Record<string, string>>>;
  readonly relay: (renewed: readonly SetCookie[]) => Promise<void>;
};

type CookiePair = { readonly name: string; readonly value: string; readonly text: string };

const unsafeMethods: ReadonlySet<string> = new Set(["POST", "PUT", "PATCH", "DELETE"]);

const deltaSeconds = /^-?[0-9]+$/;

export function isUnsafeMethod(method: string | undefined): boolean {
  return method !== undefined && unsafeMethods.has(method.toUpperCase());
}

export function readCookie(cookies: string | null | undefined, name: string): string | undefined {
  const pair = cookiePairs(cookies).find((candidate) => candidate.name === name);
  const value = pair === undefined ? undefined : decodeCookieValue(pair.value);
  return value === "" ? undefined : value;
}

export function isAntiforgeryRefusal(status: number, body: unknown): boolean {
  return (
    status === 400 &&
    typeof body === "object" &&
    body !== null &&
    "code" in body &&
    typeof body.code === "string" &&
    body.code.startsWith(antiforgeryCodePrefix)
  );
}

// RFC 6265, section 5.2: attribute names are case-insensitive, the last occurrence of an attribute wins, a Max-Age or
// Expires value that does not parse is ignored, and a Path that does not start with "/" means the default path.
export function parseSetCookie(header: string): SetCookie | undefined {
  const [pair = "", ...attributes] = header.split(";");
  const separator = pair.indexOf("=");
  if (separator === -1) return undefined;
  const name = pair.slice(0, separator).trim();
  const value = decodeCookieValue(pair.slice(separator + 1).trim());
  if (name === "" || value === undefined) return undefined;

  let httpOnly = false;
  let secure = false;
  let sameSite: SetCookie["sameSite"];
  let path: string | undefined;
  let maxAge: number | undefined;
  let expires: Date | undefined;
  for (const attribute of attributes) {
    const equals = attribute.indexOf("=");
    const key = (equals === -1 ? attribute : attribute.slice(0, equals)).trim().toLowerCase();
    const text = equals === -1 ? "" : attribute.slice(equals + 1).trim();
    switch (key) {
      case "httponly":
        httpOnly = true;
        break;
      case "secure":
        secure = true;
        break;
      case "samesite":
        sameSite = sameSiteOf(text);
        break;
      case "path":
        path = text.startsWith("/") ? text : undefined;
        break;
      case "max-age":
        if (deltaSeconds.test(text)) maxAge = Number(text);
        break;
      case "expires": {
        const date = new Date(text);
        if (!Number.isNaN(date.getTime())) expires = date;
        break;
      }
    }
  }
  return { name, value, httpOnly, secure, sameSite, path, maxAge, expires };
}

// ASP.NET Core and Next.js both escape a cookie value they set, so a value is read unescaped and merged escaped, as the
// browser sends it back once Next.js has set it.
export function mergeCookies(
  cookies: string | null | undefined,
  renewed: readonly { readonly name: string; readonly value: string }[],
): string {
  const replacements = new Map(
    renewed.map(({ name, value }) => [name, `${name}=${encodeURIComponent(value)}`] as const),
  );
  const merged: string[] = [];
  const replaced = new Set<string>();
  for (const pair of cookiePairs(cookies)) {
    const replacement = replacements.get(pair.name);
    if (replacement === undefined) {
      merged.push(pair.text);
    } else if (!replaced.has(pair.name)) {
      merged.push(replacement);
      replaced.add(pair.name);
    }
  }
  for (const [name, replacement] of replacements) {
    if (!replaced.has(name)) merged.push(replacement);
  }
  return merged.join("; ");
}

// The API checks the token before the feature gate, idempotency and the endpoint, so a refused request changed nothing and
// may be sent once more; one renewal and one resend keep a token that keeps failing from looping.
export async function sendWithAntiforgery<TAnswer>({
  method,
  cookies,
  send,
  isRefusal,
  renew,
}: AntiforgeryExchange<TAnswer>): Promise<TAnswer> {
  if (!isUnsafeMethod(method)) return send({ cookies, token: undefined });

  const answer = await send({ cookies, token: readCookie(cookies, requestTokenCookieName) });
  if (renew === undefined || !(await isRefusal(answer))) return answer;

  const renewal = await renew();
  switch (renewal.outcome) {
    case "renewed":
      return send({ cookies: renewal.cookies, token: readCookie(renewal.cookies, requestTokenCookieName) });
    case "unauthenticated":
      return renewal.answer;
    case "failed":
      return answer;
  }
}

// A renewed pair reaches the browser only with the answer of the Server Function that is still running, so the request sent
// once more carries it in its own cookie header as well.
export function antiforgeryFetch({
  fetch,
  renewalUrl,
  renewalHeaders,
  relay,
}: AntiforgeryFetchOptions): ApiFetch {
  const renew = async (cookieHeader: string): Promise<AntiforgeryRenewal<Response>> => {
    const response = await fetch(renewalUrl, {
      headers: { ...(await renewalHeaders()), accept: "application/json" },
    }).catch(() => undefined);
    if (response?.status === 204) {
      const renewed = response.headers.getSetCookie().flatMap((header) => parseSetCookie(header) ?? []);
      await relay(renewed);
      return { outcome: "renewed", cookies: mergeCookies(cookieHeader, renewed) };
    }
    if (response?.status === 401) return { outcome: "unauthenticated", answer: response };
    await response?.body?.cancel();
    return { outcome: "failed" };
  };

  return (url, init) => {
    const requestHeaders = new Headers(init.headers);
    const cookieHeader = requestHeaders.get("cookie") ?? "";
    return sendWithAntiforgery({
      method: init.method,
      cookies: cookieHeader,
      send: (attempt) => fetch(url, { ...init, headers: attemptHeaders(requestHeaders, attempt) }),
      isRefusal: isAntiforgeryRefusalResponse,
      // A streamed body is read by the first send, so that request cannot be sent again.
      renew: init.body instanceof ReadableStream ? undefined : () => renew(cookieHeader),
    });
  };
}

function attemptHeaders(
  requestHeaders: Headers,
  { cookies: cookieHeader, token }: AntiforgeryAttempt,
): Headers {
  const attempt = new Headers(requestHeaders);
  if (cookieHeader === "") attempt.delete("cookie");
  else attempt.set("cookie", cookieHeader);
  if (token !== undefined) attempt.set(antiforgeryHeaderName, token);
  return attempt;
}

async function isAntiforgeryRefusalResponse(response: Response): Promise<boolean> {
  if (response.status !== 400) return false;
  const body: unknown = await response
    .clone()
    .json()
    .catch(() => undefined);
  return isAntiforgeryRefusal(response.status, body);
}

function cookiePairs(cookies: string | null | undefined): readonly CookiePair[] {
  return (cookies ?? "")
    .split(";")
    .map((text) => text.trim())
    .filter((text) => text !== "")
    .map((text) => {
      const separator = text.indexOf("=");
      return separator === -1
        ? { name: "", value: text, text }
        : { name: text.slice(0, separator).trim(), value: text.slice(separator + 1).trim(), text };
    });
}

function decodeCookieValue(value: string): string | undefined {
  try {
    return decodeURIComponent(value);
  } catch {
    return undefined;
  }
}

function sameSiteOf(value: string): SetCookie["sameSite"] {
  const lowered = value.toLowerCase();
  return lowered === "strict" || lowered === "lax" || lowered === "none" ? lowered : undefined;
}
