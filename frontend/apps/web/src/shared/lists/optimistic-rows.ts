import type { DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";

export type RowRemovalOutcome =
  | { readonly removed: true; readonly message?: string | undefined }
  | { readonly removed: false; readonly message: string; readonly reference?: string | undefined };

export interface ShownRows<TRow> {
  readonly page: DataTablePage;
  readonly rows: readonly TRow[];
}

// The last row of a later page stays until the server answers: hiding it would show a page past the last one, with the
// list's empty state, while other pages still hold rows.
export function withoutRow<TRow>(
  shown: ShownRows<TRow>,
  id: string,
  getRowId: (row: TRow) => string,
): ShownRows<TRow> {
  const rows = shown.rows.filter((row) => getRowId(row) !== id);
  if (rows.length === shown.rows.length) return shown;
  if (rows.length === 0 && shown.page.page > 1) return shown;
  const totalCount = Math.max(0, shown.page.totalCount - 1);
  return {
    rows,
    page: { ...shown.page, totalCount, pageCount: Math.ceil(totalCount / shown.page.pageSize) },
  };
}
