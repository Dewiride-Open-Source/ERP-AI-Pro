import type { Route } from "next";

import { apiBasePath } from "../api/base-path.ts";

export type SignInReason = "session-ended" | "signed-out";

export type QueryValue = string | readonly string[] | undefined;

export const loginPath = "/login";

export const signOutPath = `${apiBasePath}/auth/logout`;

export const returnUrlParameter = "returnUrl";

export const reasonParameter = "reason";

export const maxReturnPathLength = 2048;

export const pagePathHeaderName = "x-erp-page-path";

const signInReasons: ReadonlySet<string> = new Set<SignInReason>(["session-ended", "signed-out"]);

const firstVisibleCharacter = 0x21;

const lastVisibleCharacter = 0x7e;

// The API's LoginRequest refuses every other return address (RedirectHttpResult.IsLocalUrl without its ~/ form, visible
// ASCII only, at most LoginRequest.MaxReturnUrlLength characters), so the web app never hands it one: a value that fails
// here is dropped, never repaired.
export function localReturnPath(value: QueryValue | null): string | undefined {
  const path = singleValue(value);
  if (path === undefined || path.length === 0 || path.length > maxReturnPathLength) return undefined;
  if (!path.startsWith("/") || path[1] === "/" || path[1] === "\\") return undefined;
  for (const character of path) {
    const code = character.codePointAt(0) ?? 0;
    if (code < firstVisibleCharacter || code > lastVisibleCharacter) return undefined;
  }
  return path;
}

export function signInReason(value: QueryValue): SignInReason | undefined {
  const reason = singleValue(value);
  return reason !== undefined && signInReasons.has(reason) ? (reason as SignInReason) : undefined;
}

export function signInHref(returnPath: string | undefined): Route {
  const query = new URLSearchParams({ [returnUrlParameter]: localReturnPath(returnPath) ?? "/" });
  return `${apiBasePath}/auth/login?${query.toString()}` as Route;
}

export function loginHref(returnPath: string | undefined, reason?: SignInReason): Route {
  const query = new URLSearchParams();
  const path = localReturnPath(returnPath);
  if (path !== undefined && path !== "/") query.set(returnUrlParameter, path);
  if (reason !== undefined) query.set(reasonParameter, reason);
  const search = query.toString();
  return (search === "" ? loginPath : `${loginPath}?${search}`) as Route;
}

// A query that names a parameter twice is ambiguous, and the API would read the two values joined, so it counts as absent.
function singleValue(value: QueryValue | null): string | undefined {
  if (value === null || value === undefined) return undefined;
  if (typeof value === "string") return value;
  return value.length === 1 ? value[0] : undefined;
}
