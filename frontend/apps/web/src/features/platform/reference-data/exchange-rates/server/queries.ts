import "server-only";

import type { ExchangeRateResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import type { ListApiParameters } from "@/shared/lists/list-api";

export type ExchangeRateRead =
  | { readonly status: "read"; readonly record: ExchangeRateResponse }
  | { readonly status: "missing" }
  | { readonly status: "failed"; readonly reason: unknown };

const rateId = z.guid();

const exchangeRatesPage = cache((page: number, pageSize: number, sort: string, filter: string | undefined) =>
  callApi((client) =>
    client.api.platform.referenceData.exchangeRates.get({
      queryParameters: filter === undefined ? { page, pageSize, sort } : { page, pageSize, sort, filter },
    }),
  ),
);

export function getExchangeRates({ page, pageSize, sort, filter }: ListApiParameters) {
  return exchangeRatesPage(page, pageSize, sort, filter);
}

// The API matches only a well-formed id, so any other is a rate that does not exist, as the API itself would answer.
export const getExchangeRate = cache(async (id: string): Promise<ExchangeRateRead> => {
  if (!rateId.safeParse(id).success) return { status: "missing" };
  try {
    return {
      status: "read",
      record: await callApi((client) => client.api.platform.referenceData.exchangeRates.byId(id).get()),
    };
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof ApiError && error.status === 404
      ? { status: "missing" }
      : { status: "failed", reason: error };
  }
});
