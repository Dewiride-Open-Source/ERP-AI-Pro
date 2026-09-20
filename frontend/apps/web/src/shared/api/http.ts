import "server-only";

import { headers } from "next/headers";

import { serverEnv } from "@/shared/config/env";

import { apiBasePath } from "./base-path";
import { ApiError, type ProblemDetails } from "./problem-details";

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
};

export async function apiFetch<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  const incoming = await headers();
  const requestHeaders = new Headers({ Accept: "application/json" });
  const cookie = incoming.get("cookie");
  if (cookie) requestHeaders.set("cookie", cookie);
  const traceparent = incoming.get("traceparent");
  if (traceparent) requestHeaders.set("traceparent", traceparent);
  if (options.body !== undefined) requestHeaders.set("Content-Type", "application/json");

  const response = await fetch(`${serverEnv().apiInternalUrl}${apiBasePath}${path}`, {
    method: options.method ?? "GET",
    headers: requestHeaders,
    cache: "no-store",
    ...(options.body === undefined ? {} : { body: JSON.stringify(options.body) }),
    ...(options.signal ? { signal: options.signal } : {}),
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) return undefined as TResponse;
  return (await response.json()) as TResponse;
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("json")) {
    try {
      return (await response.json()) as ProblemDetails;
    } catch {
      return { status: response.status, title: response.statusText };
    }
  }
  return { status: response.status, title: response.statusText };
}
