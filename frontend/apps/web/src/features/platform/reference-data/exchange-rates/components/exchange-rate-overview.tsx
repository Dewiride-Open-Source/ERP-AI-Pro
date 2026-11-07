import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { InfoIcon } from "lucide-react";
import { notFound } from "next/navigation";
import { Suspense, type ReactNode } from "react";

import { auditNavigation, RecordHistory, RecordHistorySkeleton } from "@/features/platform/audit";
import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { exchangeRatesNavigation } from "../../nav";
import { getExchangeRate } from "../server/queries";

import { ExchangeRateActions } from "./exchange-rate-actions";
import {
  exchangeRateName,
  exchangeRateRecord,
  rateForUnitText,
  rateInputValue,
  sourceText,
  unitText,
} from "./exchange-rate-display";
import { exchangeRateTitle } from "./exchange-rate-page-title";

export async function ExchangeRateOverview({ exchangeRateId }: { exchangeRateId: string }) {
  const read = await getExchangeRate(exchangeRateId);
  if (read.status === "missing") notFound();
  if (read.status === "failed") return <Unavailable reason={describeFailure(read.reason)} />;
  const recorded = exchangeRateRecord(read.record);
  if (recorded === undefined) return <Unavailable reason="The API sent this rate without its details." />;

  const { id, currency, rateDate, rate, unit, source, version } = recorded;
  const name = exchangeRateName(currency, rateDate);
  const day = formatDisplayDate(rateDate);
  const [session, flags] = await Promise.all([requireSession(), getFeatureFlags()]);
  const canCorrect = mayShow(session, "platform.reference-data.exchange-rates.correct");
  const showsHistory =
    mayShow(session, auditNavigation.permission) && isFeatureEnabled(flags, auditNavigation.featureFlag);

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-4">
        <div className="grid gap-1">
          <p className="text-eyebrow text-primary uppercase">{exchangeRatesNavigation.title}</p>
          <h1 className="text-title wrap-anywhere">{name}</h1>
        </div>
        {canCorrect ? (
          <ExchangeRateActions
            rate={{ id, currency, rateDate, rate: rateInputValue(rate), unit, source, version }}
          />
        ) : null}
      </header>

      <Card data-testid="exchange-rate-details">
        <CardHeader>
          <CardTitle>
            <h2 className="flex items-center gap-2">
              <InfoIcon className="size-4 text-muted-foreground" aria-hidden />
              Details
            </h2>
          </CardTitle>
          <CardDescription>
            The rate is in rupees for {unitText(unit)} {currency} on {day}, as its source published it.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2">
            <Detail label="Currency">{currency}</Detail>
            <Detail label="Day">{day}</Detail>
            <Detail label="Rate">{rateForUnitText(rate, unit, currency)}</Detail>
            <Detail label="Source">{sourceText(source)}</Detail>
          </dl>
        </CardContent>
      </Card>

      {showsHistory ? (
        <Suspense fallback={<RecordHistorySkeleton />}>
          <RecordHistory recordId={id} recordName={name} />
        </Suspense>
      ) : null}
    </div>
  );
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0 rounded-lg border bg-muted/40 p-4">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium wrap-anywhere">{children}</dd>
    </div>
  );
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div className="grid gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">{exchangeRatesNavigation.title}</p>
        <h1 className="text-title">{exchangeRateTitle}</h1>
      </header>
      <div
        role="status"
        data-testid="exchange-rate-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">This exchange rate&apos;s details are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
      </div>
    </div>
  );
}
