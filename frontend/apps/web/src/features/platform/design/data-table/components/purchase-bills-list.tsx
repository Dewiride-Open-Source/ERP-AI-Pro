"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import { DataTableOptionsFilter } from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import { formatRupees } from "@/shared/format/money";
import { isDateRange, type ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import {
  purchaseBillsList,
  purchaseBillStatuses,
  type PurchaseBillStatus,
} from "../lists/purchase-bills.list";
import type { PurchaseBill } from "../server/queries";

const statusLabels: Readonly<Record<PurchaseBillStatus, string>> = {
  draft: "Draft",
  approved: "Approved",
  paid: "Paid",
  overdue: "Overdue",
};

const statusOptions = purchaseBillStatuses.map((status) => ({ value: status, label: statusLabels[status] }));

function StatusBadge({ status }: { status: PurchaseBillStatus }) {
  switch (status) {
    case "paid":
      return (
        <Badge variant="outline" className="border-transparent bg-success/10 text-success dark:bg-success/20">
          {statusLabels.paid}
        </Badge>
      );
    case "overdue":
      return <Badge variant="destructive">{statusLabels.overdue}</Badge>;
    case "approved":
      return <Badge variant="secondary">{statusLabels.approved}</Badge>;
    case "draft":
      return <Badge variant="outline">{statusLabels.draft}</Badge>;
  }
}

const columns: readonly DataTableColumn<PurchaseBill>[] = [
  {
    id: "number",
    header: "Bill",
    cell: (bill) => <span className="font-medium tabular-nums">{bill.number}</span>,
    sort: "text",
    hideable: false,
    role: "title",
  },
  { id: "supplier", header: "Supplier", cell: (bill) => bill.supplier, sort: "text" },
  { id: "state", header: "State", cell: (bill) => bill.state },
  {
    id: "billDate",
    header: "Bill date",
    cell: (bill) => <span className="tabular-nums">{formatDisplayDate(bill.billDate)}</span>,
    sort: "date",
  },
  {
    id: "dueDate",
    header: "Due date",
    cell: (bill) => <span className="tabular-nums">{formatDisplayDate(bill.dueDate)}</span>,
    sort: "date",
  },
  {
    id: "amount",
    header: "Amount",
    cell: (bill) => <span className="tabular-nums">{formatRupees(bill.amount)}</span>,
    sort: "number",
    align: "end",
  },
  { id: "status", header: "Status", cell: (bill) => <StatusBadge status={bill.status} /> },
];

const noValues: readonly string[] = [];

export function PurchaseBillsList({
  query,
  page,
  bills,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  bills: readonly PurchaseBill[];
  labelledBy: string;
}) {
  const supplier = query.filters.supplier;
  const status = query.filters.status;
  const due = query.filters.due;

  return (
    <ListTable
      basePath="/design/data-table"
      definition={purchaseBillsList}
      query={query}
      page={page}
      label="Purchase bills"
      labelledBy={labelledBy}
      noun={{ one: "bill", other: "bills" }}
      columns={columns}
      rows={bills}
      getRowId={(bill) => bill.id}
      selection={{ rowLabel: (bill) => bill.number }}
      noMatches="No bills match these filters."
      empty={
        <Empty className="border">
          <EmptyHeader>
            <EmptyTitle>No purchase bills yet.</EmptyTitle>
            <EmptyDescription>Bills appear here once they are recorded.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        <>
          <DataTableTextFilter
            name="supplier"
            label="Supplier"
            defaultValue={typeof supplier === "string" ? supplier : ""}
            placeholder="Search by name"
            maxLength={100}
          />
          <DataTableOptionsFilter
            name="status"
            label="Status"
            options={statusOptions}
            defaultValues={Array.isArray(status) ? status : noValues}
          />
          <DataTableDateRangeFilter
            name="due"
            label="Due between"
            defaultValue={due !== undefined && isDateRange(due) ? due : { from: "", to: "" }}
          />
        </>
      }
    />
  );
}
