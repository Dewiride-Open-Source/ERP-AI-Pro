import type { Metadata } from "next";

import {
  ExchangeRateOverview,
  exchangeRatesNavigation,
  exchangeRatePageTitle,
} from "@/features/platform/reference-data";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({
  params,
}: PageProps<"/platform/reference-data/exchange-rates/[exchangeRateId]">): Promise<Metadata> {
  return exchangeRatePageTitle((await params).exchangeRateId);
}

export default async function ExchangeRatePage({
  params,
}: Readonly<PageProps<"/platform/reference-data/exchange-rates/[exchangeRateId]">>) {
  const { exchangeRateId } = await params;
  return (
    <RequirePermission permission={exchangeRatesNavigation.permission}>
      <ExchangeRateOverview exchangeRateId={exchangeRateId} />
    </RequirePermission>
  );
}
