import "server-only";

import type { PersonResponse, PositionResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

export type PersonRead =
  | { readonly status: "read"; readonly person: PersonResponse }
  | { readonly status: "missing" }
  | { readonly status: "failed"; readonly reason: unknown };

const personId = z.uuid();

const positionsPageSize = 200;

export const getPerson = cache(async (userId: string): Promise<PersonRead> => {
  if (!personId.safeParse(userId).success) return { status: "missing" };
  try {
    return {
      status: "read",
      person: await callApi((client) => client.api.identity.users.byId(userId).get()),
    };
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof ApiError && error.status === 404
      ? { status: "missing" }
      : { status: "failed", reason: error };
  }
});

export const getPositions = cache(async (userId: string): Promise<PositionResponse[]> => {
  const page = (number: number) =>
    callApi((client) =>
      client.api.identity.users.byId(userId).positions.get({
        queryParameters: { page: number, pageSize: positionsPageSize, sort: "effectiveFrom:desc" },
      }),
    );
  const first = await page(1);
  const rest = await Promise.all(
    Array.from({ length: Math.max((first.totalPages ?? 1) - 1, 0) }, (_, index) => page(index + 2)),
  );
  return [first, ...rest].flatMap((read) => read.items ?? []);
});
