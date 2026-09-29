import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { redirect } from "next/navigation";

import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { purchaseBillsList } from "../lists/purchase-bills.list";
import { getPurchaseBills } from "../server/queries";

import { PurchaseBillsList } from "./purchase-bills-list";

const basePath = "/design/data-table";

const listHeadingId = "purchase-bills-heading";

export function DataTableDemo({ searchParameters }: { searchParameters: SearchParameters }) {
  const { query, canonical } = readListQuery(searchParameters, purchaseBillsList);
  if (!canonical) redirect(listHref(basePath, query, purchaseBillsList));

  const bills = getPurchaseBills(query);
  if (query.page > 1 && query.page > bills.totalPages) {
    redirect(listHref(basePath, withPage(query, Math.max(1, bills.totalPages)), purchaseBillsList));
  }

  return (
    <div className="grid min-w-0 animate-fade-up grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Data table</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          One list built the way every ERP list is built: the server sorts, filters and pages it, and the page
          address keeps the view, so a link or the Back button brings it back. The bills are examples.
        </p>
      </header>

      <Card data-testid="purchase-bills-card">
        <CardHeader>
          <CardTitle>
            <h2 id={listHeadingId}>Purchase bills (example)</h2>
          </CardTitle>
          <CardDescription>
            Sort by a column, filter, page through the bills and choose the columns to show.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <PurchaseBillsList
            query={query}
            page={{
              page: bills.page,
              pageSize: bills.pageSize,
              totalCount: bills.totalCount,
              pageCount: bills.totalPages,
            }}
            bills={bills.items}
            labelledBy={listHeadingId}
          />
        </CardContent>
      </Card>
    </div>
  );
}
