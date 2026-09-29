"use client";

import { ChevronLeftIcon, ChevronRightIcon, ChevronsLeftIcon, ChevronsRightIcon } from "lucide-react";
import { useId, type ComponentType, type ReactNode } from "react";

import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@dewiride/erp-ui/components/ui/select";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { formatCount, type DataTablePage } from "./data-table-model";

export interface DataTableLinkProps {
  href: string;
  disabled: boolean;
  children: ReactNode;
  className: string;
  "aria-label": string;
}

export type DataTableLinkComponent = ComponentType<DataTableLinkProps>;

const pageLinks = [
  { key: "first", label: "First page", Icon: ChevronsLeftIcon },
  { key: "previous", label: "Previous page", Icon: ChevronLeftIcon },
  { key: "next", label: "Next page", Icon: ChevronRightIcon },
  { key: "last", label: "Last page", Icon: ChevronsRightIcon },
] as const;

type PageLinkKey = (typeof pageLinks)[number]["key"];

const pageLinkClass = cn(
  buttonVariants({ variant: "outline", size: "icon-sm" }),
  "aria-disabled:pointer-events-none aria-disabled:opacity-50",
);

function targetPage(key: PageLinkKey, page: DataTablePage): number {
  switch (key) {
    case "first":
      return 1;
    case "previous":
      return Math.max(1, page.page - 1);
    case "next":
      return Math.min(page.pageCount, page.page + 1);
    case "last":
      return page.pageCount;
  }
}

function isUnavailable(key: PageLinkKey, page: DataTablePage): boolean {
  return key === "first" || key === "previous" ? page.page <= 1 : page.page >= page.pageCount;
}

export function DataTablePagination({
  label,
  page,
  pageSizes,
  status,
  pageHref,
  onPageSizeChange,
  interactive,
  linkComponent: Link,
}: {
  label: string;
  page: DataTablePage;
  pageSizes: readonly number[];
  status: ReactNode;
  pageHref: (page: number) => string;
  onPageSizeChange: (pageSize: number) => void;
  interactive: boolean;
  linkComponent: DataTableLinkComponent;
}) {
  const sizeLabelId = useId();

  return (
    <div
      data-testid="data-table-pagination"
      className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 text-sm"
    >
      {status}
      {page.totalCount > 0 ? (
        <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
          <div className="flex items-center gap-2">
            <span id={sizeLabelId} className="text-muted-foreground">
              Rows per page
            </span>
            <Select
              value={String(page.pageSize)}
              disabled={!interactive}
              onValueChange={(value) => onPageSizeChange(Number(value))}
            >
              <SelectTrigger size="sm" aria-labelledby={sizeLabelId} data-testid="data-table-page-size">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {pageSizes.map((size) => (
                  <SelectItem key={size} value={String(size)}>
                    {size}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {page.pageCount > 1 ? (
            <nav aria-label={`${label}, pages`} className="flex items-center gap-1">
              {pageLinks.slice(0, 2).map(({ key, label: linkLabel, Icon }) => (
                <Link
                  key={key}
                  href={pageHref(targetPage(key, page))}
                  disabled={isUnavailable(key, page)}
                  aria-label={linkLabel}
                  className={pageLinkClass}
                >
                  <Icon aria-hidden />
                </Link>
              ))}
              <p className="px-2 whitespace-nowrap" data-testid="data-table-page">
                Page {formatCount(page.page)} of {formatCount(page.pageCount)}
              </p>
              {pageLinks.slice(2).map(({ key, label: linkLabel, Icon }) => (
                <Link
                  key={key}
                  href={pageHref(targetPage(key, page))}
                  disabled={isUnavailable(key, page)}
                  aria-label={linkLabel}
                  className={pageLinkClass}
                >
                  <Icon aria-hidden />
                </Link>
              ))}
            </nav>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
