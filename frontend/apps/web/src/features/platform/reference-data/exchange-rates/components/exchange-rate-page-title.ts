import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";

import { exchangeRatesNavigation } from "../../nav";
import { getExchangeRate } from "../server/queries";

import { exchangeRateName, exchangeRateRecord } from "./exchange-rate-display";

export const exchangeRateTitle = "Exchange rate";

// The title names the rate as the audit trail does, read through the same cached request as the page, so it is ready when the
// page is; a person without access is never read for, and an unknown rate answers the page's not-found.
export async function exchangeRatePageTitle(exchangeRateId: string): Promise<Metadata> {
  const { permission } = exchangeRatesNavigation;
  if (!mayShow(await requireSession(), permission)) return pageMetadata(exchangeRateTitle, permission);
  const read = await getExchangeRate(exchangeRateId);
  if (read.status === "missing") notFound();
  const recorded = read.status === "read" ? exchangeRateRecord(read.record) : undefined;
  const title =
    recorded === undefined ? exchangeRateTitle : exchangeRateName(recorded.currency, recorded.rateDate);
  return pageMetadata(title, permission);
}
