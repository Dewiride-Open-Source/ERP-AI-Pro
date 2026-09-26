import "server-only";

import { connect, type ErpApiClient } from "@dewiride/erp-api-client";
import { headers } from "next/headers";
import { cache } from "react";

import { serverEnv } from "@/shared/config/env";

import { forwardedHeaders } from "./forwarded-headers";
import { ApiError, toApiError } from "./problem-details";

// The API never redirects, so a redirect means a misrouted request and must not carry the forwarded cookie anywhere.
const fetchFromApi = (url: string, init: RequestInit) =>
  fetch(url, { ...init, cache: "no-store", redirect: "error" });

export const apiClient = cache(async (): Promise<ErpApiClient> =>
  connect({
    baseUrl: serverEnv().apiInternalUrl,
    headers: forwardedHeaders(await headers()),
    fetch: fetchFromApi,
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
