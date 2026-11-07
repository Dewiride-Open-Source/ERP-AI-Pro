"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import {
  DataTableOptionsFilter,
  type DataTableFilterOption,
} from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import Link from "next/link";

import { isDateRange, type ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import { exchangeRatesNavigation } from "../../nav";
import { exchangeRatesList } from "../lists/exchange-rates.list";

import type { ExchangeRateListRow } from "./exchange-rate-display";

const noChoices: readonly string[] = [];

const exchangeRateColumns: readonly DataTableColumn<ExchangeRateListRow>[] = [
  {
    id: "rateDate",
    header: "Day",
    cell: (row) => (
      <Link
        href={`/platform/reference-data/exchange-rates/${row.id}`}
        prefetch={false}
        aria-label={row.name}
        className="rounded-sm font-medium whitespace-nowrap text-primary underline-offset-4 focus-ring hover:underline"
        data-testid="exchange-rate-link"
      >
        {row.day}
      </Link>
    ),
    sort: "date",
    hideable: false,
    role: "title",
  },
  { id: "currency", header: "Currency", cell: (row) => row.currency, sort: "text", hideable: false },
  {
    id: "rate",
    header: "Rate",
    cell: (row) => row.rate,
    align: "end",
    hideable: false,
    className: "tabular-nums",
  },
  {
    id: "unit",
    header: "Per",
    cell: (row) => row.unit,
    align: "end",
    hideable: false,
    className: "tabular-nums",
  },
  {
    id: "source",
    header: "Source",
    cell: (row) => row.source,
    className: "min-w-40 max-w-64 whitespace-normal break-words",
  },
];

export function ExchangeRatesList({
  query,
  page,
  rows,
  currencyOptions,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  rows: readonly ExchangeRateListRow[];
  currencyOptions: readonly DataTableFilterOption[];
  labelledBy: string;
}) {
  const { currency, day } = query.filters;

  return (
    <ListTable
      basePath={exchangeRatesNavigation.basePath}
      definition={exchangeRatesList}
      query={query}
      page={page}
      label="Exchange rates"
      labelledBy={labelledBy}
      noun={{ one: "exchange rate", other: "exchange rates" }}
      columns={exchangeRateColumns}
      rows={rows}
      getRowId={(row) => row.id}
      noMatches="No exchange rates match these filters."
      empty={
        <Empty className="border" data-testid="exchange-rates-empty">
          <EmptyHeader>
            <EmptyTitle>No exchange rates yet.</EmptyTitle>
            <EmptyDescription>
              Rates appear here once they are recorded by hand or imported from a file.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        <>
          {currencyOptions.length > 0 ? (
            <DataTableOptionsFilter
              name="currency"
              label="Currency"
              options={currencyOptions}
              defaultValues={Array.isArray(currency) ? currency : noChoices}
            />
          ) : null}
          <DataTableDateRangeFilter
            name="day"
            label="Days between"
            defaultValue={day !== undefined && isDateRange(day) ? day : { from: "", to: "" }}
          />
        </>
      }
    />
  );
}
