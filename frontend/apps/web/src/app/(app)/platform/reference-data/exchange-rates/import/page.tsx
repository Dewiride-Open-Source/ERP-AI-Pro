import type { Metadata } from "next";

import { ImportExchangeRates, importExchangeRatesTitle } from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(importExchangeRatesTitle, "platform.reference-data.exchange-rates.record");
}

export default function ImportExchangeRatesPage() {
  return (
    <RequirePermission permission="platform.reference-data.exchange-rates.record">
      <ImportExchangeRates />
    </RequirePermission>
  );
}
