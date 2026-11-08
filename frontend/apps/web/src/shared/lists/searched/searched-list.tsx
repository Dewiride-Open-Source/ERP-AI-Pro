import { redirect } from "next/navigation";

import { SearchedListBrowser, type SearchedListBrowserProps } from "./searched-list-browser.tsx";
import {
  searchedListHref,
  shownSearchedList,
  type SearchedListRow,
  type SearchKeys,
} from "./searched-list-view.ts";

// The list arrives whole from the API and is searched and paged here, on the web server, so the browser receives one page
// of rows, each cell its text or the link or badge rendered here; a page past the last answers with the last one.
export function SearchedList<T>({
  items,
  pageSize,
  keysOf,
  rowOf,
  ...browser
}: Omit<SearchedListBrowserProps, "page" | "rows"> & {
  items: readonly T[];
  pageSize: number;
  keysOf: (item: T) => SearchKeys;
  rowOf: (item: T) => SearchedListRow;
}) {
  const shown = shownSearchedList(items, browser.view, pageSize, keysOf);
  if (shown.page.page !== browser.view.page) {
    redirect(searchedListHref(browser.address, { ...browser.view, page: shown.page.page }));
  }

  return <SearchedListBrowser {...browser} page={shown.page} rows={shown.items.map((item) => rowOf(item))} />;
}
