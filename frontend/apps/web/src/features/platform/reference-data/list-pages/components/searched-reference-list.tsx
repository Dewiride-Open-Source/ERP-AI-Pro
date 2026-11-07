import { redirect } from "next/navigation";

import { ReferenceListBrowser, type ReferenceListBrowserProps } from "./reference-list-browser";
import {
  referenceListHref,
  shownReferenceList,
  type ReferenceListRow,
  type SearchKeys,
} from "./reference-list-view";

// The list arrives whole from the API and is searched and paged here, on the web server, so the browser receives one page
// of plain rows; a page past the last answers with the last one.
export function SearchedReferenceList<T>({
  items,
  pageSize,
  keysOf,
  rowOf,
  ...browser
}: Omit<ReferenceListBrowserProps, "page" | "rows"> & {
  items: readonly T[];
  pageSize: number;
  keysOf: (item: T) => SearchKeys;
  rowOf: (item: T) => ReferenceListRow;
}) {
  const shown = shownReferenceList(items, browser.view, pageSize, keysOf);
  if (shown.page.page !== browser.view.page) {
    redirect(referenceListHref(browser.path, { ...browser.view, page: shown.page.page }));
  }

  return (
    <ReferenceListBrowser {...browser} page={shown.page} rows={shown.items.map((item) => rowOf(item))} />
  );
}
