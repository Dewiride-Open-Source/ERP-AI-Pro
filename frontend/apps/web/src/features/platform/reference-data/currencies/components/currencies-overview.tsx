import { CoinsIcon } from "lucide-react";
import { redirect } from "next/navigation";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { currenciesPage } from "../../list-pages/components/reference-list-pages";
import {
  readReferenceListView,
  referenceListHref,
  type ReferenceListColumn,
  type SearchParameters,
} from "../../list-pages/components/reference-list-view";
import { SearchedReferenceList } from "../../list-pages/components/searched-reference-list";
import { readCurrencies } from "../../reference-lists/server/queries";

export const currenciesTitle = currenciesPage.title;

const currenciesPerPage = 20;

const listHeadingId = "currencies-heading";

const currencyColumns: readonly ReferenceListColumn[] = [
  { id: "name", header: "Name", title: true },
  { id: "code", header: "Code" },
  { id: "decimals", header: "Decimal places" },
];

export async function CurrenciesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { view, canonical } = readReferenceListView(searchParameters);
  if (!canonical) redirect(referenceListHref(currenciesPage.path, view));
  const list = await readCurrencies();

  return (
    <ReferenceListFrame
      title={currenciesTitle}
      description="Every currency an amount in the ERP can be in, with the number of decimal places it is counted in."
      listHeading="All currencies"
      listDescription="By code. Search by a currency's name or by its code."
      headingId={listHeadingId}
      icon={CoinsIcon}
      testId="currencies-card"
    >
      {list.status === "read" ? (
        <SearchedReferenceList
          items={list.items}
          pageSize={currenciesPerPage}
          keysOf={(currency) => currency}
          rowOf={(currency) => ({
            id: currency.code,
            cells: { code: currency.code, name: currency.name, decimals: String(currency.minorUnits) },
          })}
          path={currenciesPage.path}
          view={view}
          label={currenciesTitle}
          labelledBy={listHeadingId}
          noun={{ one: "currency", other: "currencies" }}
          searchExample="US Dollar or USD"
          columns={currencyColumns}
        />
      ) : (
        <ReferenceListUnavailable list={list} listName="currencies" />
      )}
    </ReferenceListFrame>
  );
}
