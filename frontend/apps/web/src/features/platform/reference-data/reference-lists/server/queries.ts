import "server-only";

import { unstable_rethrow } from "next/navigation";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { isoDateOf } from "@/shared/api/dates/date-only";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { referenceListsNavigation } from "../../nav";
import type {
  CountryItem,
  CurrencyItem,
  FinancialYearItem,
  IndianStateItem,
  ListRead,
} from "../components/reference-options";

// A list that cannot be read, or whose module is switched off, is answered as such instead of thrown, so the page around a
// picker still renders and the other lists stay usable.
async function read<T>(load: () => Promise<readonly T[]>): Promise<ListRead<T>> {
  const flags = await getFeatureFlags();
  if (flags.get(referenceListsNavigation.featureFlag) === false) return { status: "disabled" };
  try {
    return { status: "read", items: await load() };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed" };
  }
}

function required(value: string | undefined): string {
  if (value === undefined) throw new TypeError("The API answered a date that is not one.");
  return value;
}

export const readCountries = cache((): Promise<ListRead<CountryItem>> =>
  read(async () => {
    const response = await callApi((client) => client.api.platform.referenceData.countries.get());
    return (response.countries ?? []).flatMap(({ code, name }) => (code && name ? [{ code, name }] : []));
  }),
);

export const readIndianStates = cache((): Promise<ListRead<IndianStateItem>> =>
  read(async () => {
    const response = await callApi((client) => client.api.platform.referenceData.indianStates.get());
    return (response.states ?? []).flatMap(({ gstCode, name, kind, validFrom, validUntil }) =>
      gstCode && name && kind && validFrom
        ? [
            {
              gstCode,
              name,
              kind,
              validFrom: required(isoDateOf(validFrom)),
              validUntil: isoDateOf(validUntil) ?? null,
            },
          ]
        : [],
    );
  }),
);

export const readCurrencies = cache((): Promise<ListRead<CurrencyItem>> =>
  read(async () => {
    const response = await callApi((client) => client.api.platform.referenceData.currencies.get());
    return (response.currencies ?? []).flatMap(({ code, name, minorUnits }) =>
      code && name && typeof minorUnits === "number" ? [{ code, name, minorUnits }] : [],
    );
  }),
);

export const readFinancialYears = cache((from: string, to: string): Promise<ListRead<FinancialYearItem>> =>
  read(async () => {
    const response = await callApi((client) =>
      client.api.platform.referenceData.financialYears.get({ queryParameters: { from, to } }),
    );
    return (response.years ?? []).flatMap(({ label, start, end, quarters, periods }) =>
      label && start && end
        ? [
            {
              label,
              start: required(isoDateOf(start)),
              end: required(isoDateOf(end)),
              quarters: (quarters ?? []).map((quarter) => ({
                number: quarter.number ?? 0,
                label: quarter.label ?? "",
                start: required(isoDateOf(quarter.start)),
                end: required(isoDateOf(quarter.end)),
              })),
              periods: (periods ?? []).map((period) => ({
                number: period.number ?? 0,
                label: period.label ?? "",
                quarter: period.quarter ?? 0,
                start: required(isoDateOf(period.start)),
                end: required(isoDateOf(period.end)),
              })),
            },
          ]
        : [],
    );
  }),
);
