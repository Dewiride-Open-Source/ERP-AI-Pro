import "server-only";

import { connect, type ErpApiClient } from "@dewiride/erp-api-client";
import { cookies, headers } from "next/headers";
import { cache } from "react";

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
  let value: T | undefined;
  try {
    value = await request(await apiClient());
  } catch (error) {
    throw toApiError(error);
  }

  if (value === undefined) throw new ApiError({ status: 502, title: "The API answered without a body." });
  return value;
}

export async function sendApi(request: (client: ErpApiClient) => Promise<unknown>): Promise<void> {
  try {
    await request(await apiClient());
  } catch (error) {
    throw toApiError(error);
  }
}

async function relayToBrowser(renewed: readonly SetCookie[]): Promise<void> {
  const browserCookies = await cookies();
  for (const { name, value, ...attributes } of renewed) browserCookies.set(name, value, attributes);
}
