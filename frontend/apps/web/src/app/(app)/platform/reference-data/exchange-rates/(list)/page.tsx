import type { Metadata } from "next";

import { ExchangeRatesOverview, exchangeRatesNavigation } from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(exchangeRatesNavigation.title, exchangeRatesNavigation.permission);
}

export default async function ExchangeRatesPage({
  searchParams,
}: Readonly<PageProps<"/platform/reference-data/exchange-rates">>) {
  return (
    <RequirePermission permission={exchangeRatesNavigation.permission}>
      <ExchangeRatesOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
