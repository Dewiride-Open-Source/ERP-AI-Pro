import type { Route } from "next";

import { localReturnPath, loginHref, loginPath, returnUrlParameter } from "./sign-in-addresses.ts";

export const sessionCookieName = "__Host-erp-session";

export type PageRequest = {
  readonly method: string;
  readonly pathname: string;
  readonly search: string;
  readonly hasSessionCookie: boolean;
};

const publicPagePaths: ReadonlySet<string> = new Set([loginPath]);

const navigationMethods: ReadonlySet<string> = new Set(["GET", "HEAD"]);

const frameworkPathPrefixes = ["/_next/", "/__nextjs"];

// The session cookie is HttpOnly and opaque to the web server, so its presence is all proxy.ts can check; the API decides
// whether the session it names still holds when the page reads it. A Server Function call is a POST and is never redirected
// here: the function's own session check sends the page to the sign-in page through the action protocol.
export function signInRedirectFor({
  method,
  pathname,
  search,
  hasSessionCookie,
}: PageRequest): Route | undefined {
  if (hasSessionCookie || publicPagePaths.has(pathname) || !navigationMethods.has(method)) return undefined;
  if (frameworkPathPrefixes.some((prefix) => pathname.startsWith(prefix))) return undefined;
  return loginHref(`${pathname}${search}`);
}

// A sign-in page address carrying a return address the API would refuse loses that parameter, so the page never offers it
// to the API; every other parameter stays.
export function loginAddressWithoutForeignReturn(searchParams: URLSearchParams): Route | undefined {
  const values = searchParams.getAll(returnUrlParameter);
  if (values.length === 0 || localReturnPath(values) !== undefined) return undefined;

  const kept = new URLSearchParams(searchParams);
  kept.delete(returnUrlParameter);
  const search = kept.toString();
  return (search === "" ? loginPath : `${loginPath}?${search}`) as Route;
}
