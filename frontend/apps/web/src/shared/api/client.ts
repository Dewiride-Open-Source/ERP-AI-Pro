import "server-only";

import { connect, type ErpApiClient } from "@dewiride/erp-api-client";
import { cookies, headers } from "next/headers";
import { cache } from "react";

import { serverEnv } from "@/shared/config/env";

import {
  antiforgeryHeaderName,
  antiforgeryRenewalPath,
  isAntiforgeryRefusal,
  mergeCookies,
  parseSetCookie,
  sendWithAntiforgery,
  type AntiforgeryAttempt,
  type AntiforgeryRenewal,
  type SetCookie,
} from "./antiforgery";
import { forwardedHeaders } from "./forwarded-headers";
import { ApiError, toApiError } from "./problem-details";

// The API never redirects, so a redirect means a misrouted request and must not carry the forwarded cookie anywhere.
const fetchFromApi = (url: string, init: RequestInit) =>
  fetch(url, { ...init, cache: "no-store", redirect: "error" });

const fetchWithAntiforgery = (url: string, init: RequestInit): Promise<Response> => {
  const requestHeaders = new Headers(init.headers);
  const cookieHeader = requestHeaders.get("cookie") ?? "";
  return sendWithAntiforgery({
    method: init.method,
    cookies: cookieHeader,
    send: (attempt) => fetchFromApi(url, { ...init, headers: attemptHeaders(requestHeaders, attempt) }),
    isRefusal: isAntiforgeryRefusalResponse,
    // A streamed body is read by the first send, so that request cannot be sent again.
    renew: init.body instanceof ReadableStream ? undefined : () => renewAntiforgeryTokens(cookieHeader),
  });
};

export const apiClient = cache(async (): Promise<ErpApiClient> =>
  connect({
    baseUrl: serverEnv().apiInternalUrl,
    headers: forwardedHeaders(await headers()),
    fetch: fetchWithAntiforgery,
  }),
);

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

async function renewAntiforgeryTokens(cookieHeader: string): Promise<AntiforgeryRenewal<Response>> {
  const response = await fetchFromApi(`${serverEnv().apiInternalUrl}${antiforgeryRenewalPath}`, {
    headers: { ...forwardedHeaders(await headers()), accept: "application/json" },
  }).catch(() => undefined);
  if (response?.status === 204) {
    return { outcome: "renewed", cookies: mergeCookies(cookieHeader, await relayToBrowser(response)) };
  }
  if (response?.status === 401) return { outcome: "unauthenticated", answer: response };
  await response?.body?.cancel();
  return { outcome: "failed" };
}

async function relayToBrowser(response: Response): Promise<readonly SetCookie[]> {
  const renewed = response.headers.getSetCookie().flatMap((header) => parseSetCookie(header) ?? []);
  const browserCookies = await cookies();
  for (const { name, value, ...attributes } of renewed) browserCookies.set(name, value, attributes);
  return renewed;
}
