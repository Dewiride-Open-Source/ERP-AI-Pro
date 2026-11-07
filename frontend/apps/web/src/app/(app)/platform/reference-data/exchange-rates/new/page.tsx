import type { Metadata } from "next";

import { NewExchangeRate, newExchangeRateTitle } from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(newExchangeRateTitle, "platform.reference-data.exchange-rates.record");
}

export default function NewExchangeRatePage() {
  return (
    <RequirePermission permission="platform.reference-data.exchange-rates.record">
      <NewExchangeRate />
    </RequirePermission>
  );
}
