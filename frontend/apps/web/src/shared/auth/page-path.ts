import "server-only";

import { headers } from "next/headers";

import { localReturnPath, pagePathHeaderName } from "./sign-in-addresses";

// proxy.ts sets this header on every page request and Server Function call it serves, replacing whatever the browser sent;
// a request it does not serve (a router prefetch) may carry anything, so the value is checked like any return address.
export async function currentPagePath(): Promise<string | undefined> {
  return localReturnPath((await headers()).get(pagePathHeaderName));
}
