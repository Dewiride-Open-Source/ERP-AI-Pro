"use client";

import {
  DataTable,
  type DataTableColumn,
  type DataTableLinkProps,
  type DataTableSort,
} from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { createContext, use, useState, type MouseEvent } from "react";

import { formatRupees } from "@/shared/format/money";

import { Specimen } from "../specimen";

interface Vendor {
  readonly id: string;
  readonly name: string;
  readonly city: string;
  readonly outstanding: number;
}

const vendors: readonly Vendor[] = [
  { id: "v-1", name: "Kaveri Traders", city: "Mysuru", outstanding: 1_24_500 },
  { id: "v-2", name: "Deccan Logistics", city: "Hyderabad", outstanding: 8_40_000.5 },
  { id: "v-3", name: "Konark Electricals", city: "Bhubaneswar", outstanding: 36_250 },
  { id: "v-4", name: "Nilgiri Print House", city: "Ooty", outstanding: 2_15_990 },
  { id: "v-5", name: "Thar Solar Systems", city: "Jodhpur", outstanding: 0 },
];

const columns: readonly DataTableColumn<Vendor>[] = [
  {
    id: "name",
    header: "Vendor",
    cell: (vendor) => vendor.name,
    sort: "text",
    hideable: false,
    role: "title",
  },
  { id: "city", header: "City", cell: (vendor) => vendor.city, sort: "text" },
  {
    id: "outstanding",
    header: "Outstanding",
    cell: (vendor) => <span className="tabular-nums">{formatRupees(vendor.outstanding)}</span>,
    sort: "number",
    align: "end",
  },
];

const pageSize = 3;

const specimenPrefix = "#vendors-page-";

const PageChange = createContext<(page: number) => void>(() => undefined);

function SpecimenPageLink({ href, disabled, children, className, "aria-label": label }: DataTableLinkProps) {
  const changePage = use(PageChange);
  const follow = (event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    if (!disabled) changePage(Number(href.slice(specimenPrefix.length)));
  };
  return (
    <a
      href={href}
      aria-disabled={disabled || undefined}
      aria-label={label}
      className={className}
      onClick={follow}
    >
      {children}
    </a>
  );
}

function sorted(sort: DataTableSort): readonly Vendor[] {
  const direction = sort.direction === "desc" ? -1 : 1;
  return [...vendors].sort((left, right) => {
    if (sort.columnId === "outstanding") return direction * (left.outstanding - right.outstanding);
    const key = sort.columnId === "city" ? "city" : "name";
    return direction * left[key].localeCompare(right[key], "en-IN");
  });
}

export function DataTableSpecimens() {
  const [sort, setSort] = useState<DataTableSort>({ columnId: "name", direction: "asc" });
  const [page, setPage] = useState(1);
  const [hidden, setHidden] = useState<readonly string[]>([]);
  const [selected, setSelected] = useState<readonly string[]>([]);
  const rows = sorted(sort).slice((page - 1) * pageSize, page * pageSize);

  return (
    <>
      <Specimen
        title="Data table"
        description="Sortable headers, a column menu, row selection and page links; below its container breakpoint the same rows become cards. The list pages on the server."
        wide
      >
        <PageChange value={setPage}>
          <DataTable
            label="Vendors"
            noun={{ one: "vendor", other: "vendors" }}
            columns={columns}
            rows={rows}
            getRowId={(vendor) => vendor.id}
            paging={{
              page: {
                page,
                pageSize,
                totalCount: vendors.length,
                pageCount: Math.ceil(vendors.length / pageSize),
              },
              pageSizes: [pageSize],
              pageHref: (target) => `${specimenPrefix}${target}`,
              onPageSizeChange: () => setPage(1),
              linkComponent: SpecimenPageLink,
            }}
            sorting={{
              sort,
              onSortChange: (next) => {
                setSort(next);
                setPage(1);
              },
            }}
            columnVisibility={{ hiddenColumnIds: hidden, onHiddenColumnIdsChange: setHidden }}
            selection={{
              selectedIds: selected,
              onSelectedIdsChange: setSelected,
              rowLabel: (vendor) => vendor.name,
            }}
            empty={null}
          />
        </PageChange>
      </Specimen>
      <Specimen
        title="Data table while loading"
        description="The skeleton a list page shows before its first rows."
      >
        <DataTableSkeleton columns={3} rows={3} />
      </Specimen>
      <Specimen
        title="Data table without rows"
        description="A list with nothing to show keeps its filters and says so."
      >
        <DataTable
          label="Vendors without dues"
          noun={{ one: "vendor", other: "vendors" }}
          columns={columns}
          rows={[]}
          getRowId={(vendor) => vendor.id}
          paging={{
            page: { page: 1, pageSize, totalCount: 0, pageCount: 0 },
            pageSizes: [pageSize],
            pageHref: (target) => `${specimenPrefix}${target}`,
            onPageSizeChange: () => undefined,
            linkComponent: SpecimenPageLink,
          }}
          empty={
            <Empty className="border">
              <EmptyHeader>
                <EmptyTitle>No vendors owe anything.</EmptyTitle>
                <EmptyDescription>Every bill is settled.</EmptyDescription>
              </EmptyHeader>
            </Empty>
          }
        />
      </Specimen>
    </>
  );
}
