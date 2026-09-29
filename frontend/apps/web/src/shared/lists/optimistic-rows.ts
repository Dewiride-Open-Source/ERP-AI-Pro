import type { DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";

export type RowRemovalOutcome =
  | { readonly removed: true }
  | { readonly removed: false; readonly message: string; readonly reference?: string | undefined };

export interface ShownRows<TRow> {
  readonly page: DataTablePage;
  readonly rows: readonly TRow[];
}

export function withoutRow<TRow>(
  shown: ShownRows<TRow>,
  id: string,
  getRowId: (row: TRow) => string,
): ShownRows<TRow> {
  const rows = shown.rows.filter((row) => getRowId(row) !== id);
  if (rows.length === shown.rows.length) return shown;
  const totalCount = Math.max(0, shown.page.totalCount - 1);
  return {
    rows,
    page: { ...shown.page, totalCount, pageCount: Math.ceil(totalCount / shown.page.pageSize) },
  };
}
