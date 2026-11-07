import type { PagedResponseOfExchangeRateResponse } from "@dewiride/erp-api-client";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { FileUpIcon, PlusIcon } from "lucide-react";
import Link from "next/link";
import { redirect, unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { listApiParameters, type ListApiParameters } from "@/shared/lists/list-api";
import {
  listHref,
  readListQuery,
  withFilters,
  withPage,
  type ListQuery,
  type SearchParameters,
} from "@/shared/lists/list-query";

import { exchangeRatesNavigation } from "../../nav";
import { readCurrencies } from "../../reference-lists/server/queries";
import { exchangeRatesList } from "../lists/exchange-rates.list";
import { getExchangeRates } from "../server/queries";

import { currencyFilterOptions, exchangeRateListRow, knownCurrencies } from "./exchange-rate-display";
import { ExchangeRatesList } from "./exchange-rates-list";

const listHeadingId = "exchange-rates-list-heading";

type ListRead =
  | { readonly status: "read"; readonly list: PagedResponseOfExchangeRateResponse }
  | { readonly status: "failed"; readonly reason: unknown };

export async function ExchangeRatesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { basePath, title, area, icon: Icon } = exchangeRatesNavigation;
  const { query, canonical } = readListQuery(searchParameters, exchangeRatesList);
  if (!canonical) redirect(listHref(basePath, query, exchangeRatesList));

  const [currencies, session, read] = await Promise.all([
    readCurrencies(),
    requireSession(),
    readList(listApiParameters(query, exchangeRatesList)),
  ]);
  const chosen = chosenCurrencies(query);
  const kept = knownCurrencies(currencies, chosen);
  if (kept.length !== chosen.length)
    redirect(listHref(basePath, withCurrencies(query, kept), exchangeRatesList));
  const canRecord = mayShow(session, "platform.reference-data.exchange-rates.record");
  if (read.status === "read") {
    const totalPages = read.list.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(basePath, withPage(query, Math.max(1, totalPages)), exchangeRatesList));
    }
  }

  return (
    <div className="grid gap-section">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-eyebrow text-primary uppercase">{area.title}</p>
          <h1 className="text-title">{title}</h1>
        </div>
        {canRecord ? (
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href="/platform/reference-data/exchange-rates/import">
                <FileUpIcon aria-hidden />
                Import rates
              </Link>
            </Button>
            <Button asChild>
              <Link href="/platform/reference-data/exchange-rates/new">
                <PlusIcon aria-hidden />
                Record a rate
              </Link>
            </Button>
          </div>
        ) : null}
      </header>

      <Card data-testid="exchange-rates-list-card">
        <CardHeader>
          <CardTitle>
            <h2 id={listHeadingId} className="flex items-center gap-2">
              <Icon className="size-4 text-muted-foreground" aria-hidden />
              Rates
            </h2>
          </CardTitle>
          <CardDescription>
            The rupees for foreign currencies, the newest day first. Open a rate to see its details and its
            changes.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <ExchangeRatesList
              query={query}
              page={{
                page: read.list.page ?? query.page,
                pageSize: read.list.pageSize ?? query.pageSize,
                totalCount: read.list.totalCount ?? 0,
                pageCount: read.list.totalPages ?? 0,
              }}
              rows={(read.list.items ?? []).flatMap((item) => exchangeRateListRow(item) ?? [])}
              currencyOptions={currencyFilterOptions(currencies, chosen)}
              labelledBy={listHeadingId}
            />
          ) : (
            <Unavailable reason={describeFailure(read.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function chosenCurrencies(query: ListQuery): readonly string[] {
  const chosen = query.filters.currency;
  return Array.isArray(chosen) ? chosen : [];
}

function withCurrencies(query: ListQuery, currencies: readonly string[]): ListQuery {
  const { currency: _dropped, ...others } = query.filters;
  return withFilters(query, currencies.length === 0 ? others : { ...others, currency: currencies });
}

async function readList(parameters: ListApiParameters): Promise<ListRead> {
  try {
    return { status: "read", list: await getExchangeRates(parameters) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: error };
  }
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="exchange-rates-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">Exchange rates are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
