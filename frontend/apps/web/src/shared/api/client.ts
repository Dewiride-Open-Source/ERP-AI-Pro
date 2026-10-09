import "server-only";

import { connect, type ErpApiClient } from "@dewiride/erp-api-client";
import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { cache } from "react";

import { currentPagePath } from "@/shared/auth/page-path";
import { loginHref } from "@/shared/auth/sign-in-addresses";
import { serverEnv } from "@/shared/config/env";

import { antiforgeryFetch, antiforgeryRenewalPath, type SetCookie } from "./antiforgery";
import { forwardedHeaders } from "./forwarded-headers";
import { ApiError, toApiError } from "./problem-details";

// The API never redirects, so a redirect means a misrouted request and must not carry the forwarded cookie anywhere.
const fetchFromApi = (url: string, init: RequestInit) =>
  fetch(url, { ...init, cache: "no-store", redirect: "error" });

export const apiClient = cache(async (): Promise<ErpApiClient> => {
  const { apiInternalUrl } = serverEnv();
  return connect({
    baseUrl: apiInternalUrl,
    headers: forwardedHeaders(await headers()),
    fetch: antiforgeryFetch({
      fetch: fetchFromApi,
      renewalUrl: `${apiInternalUrl}${antiforgeryRenewalPath}`,
      renewalHeaders: async () => forwardedHeaders(await headers()),
      relay: relayToBrowser,
    }),
  });
});

export async function callApi<T>(request: (client: ErpApiClient) => Promise<T | undefined>): Promise<T> {
  return signedInOnly(() => callApiAllowingSignedOut(request));
}

export async function sendApi(request: (client: ErpApiClient) => Promise<unknown>): Promise<void> {
  return signedInOnly(async () => {
    try {
      await request(await apiClient());
    } catch (error) {
      throw toApiError(error);
    }
  });
}

// Reading who is signed in is the one call whose 401 answers the question rather than ending the page: the session check
// and the sign-in page decide what a signed-out visitor sees.
export async function callApiAllowingSignedOut<T>(
  request: (client: ErpApiClient) => Promise<T | undefined>,
): Promise<T> {
  let value: T | undefined;
  try {
    value = await request(await apiClient());
  } catch (error) {
    throw toApiError(error);
  }

  if (value === undefined) throw new ApiError({ status: 502, title: "The API answered without a body." });
  return value;
}

// The API answers 401 only when the session the forwarded cookie names has ended, so the person signs in again and comes
// back to this page; redirect() throws, so a caller that catches errors passes it on with unstable_rethrow.
async function signedInOnly<T>(call: () => Promise<T>): Promise<T> {
  try {
    return await call();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401)
      redirect(loginHref(await currentPagePath(), "session-ended"));
    throw error;
  }
}

async function relayToBrowser(renewed: readonly SetCookie[]): Promise<void> {
  const browserCookies = await cookies();
  for (const { name, value, ...attributes } of renewed) browserCookies.set(name, value, attributes);
}
