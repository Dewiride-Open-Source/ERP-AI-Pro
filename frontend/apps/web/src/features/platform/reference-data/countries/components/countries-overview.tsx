import { GlobeIcon } from "lucide-react";
import { redirect } from "next/navigation";

import type { SearchParameters } from "@/shared/lists/list-query";
import { SearchedList } from "@/shared/lists/searched/searched-list";
import {
  readSearchedListView,
  searchedListHref,
  type SearchedListAddress,
  type SearchedListColumn,
} from "@/shared/lists/searched/searched-list-view";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { countriesPage, referenceListSearch } from "../../list-pages/components/reference-list-pages";
import { readCountries } from "../../reference-lists/server/queries";

export const countriesTitle = countriesPage.title;

const countriesPerPage = 20;

const listHeadingId = "countries-heading";

const countriesAddress: SearchedListAddress = { path: countriesPage.path };

const countryColumns: readonly SearchedListColumn[] = [
  { id: "name", header: "Name", title: true },
  { id: "code", header: "Code" },
];

export async function CountriesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { view, canonical } = readSearchedListView(searchParameters);
  if (!canonical) redirect(searchedListHref(countriesAddress, view));
  const list = await readCountries();

  return (
    <ReferenceListFrame
      title={countriesTitle}
      description="Every country the ERP knows, with the two-letter code that GST e-invoices use for it."
      listHeading="All countries"
      listDescription="By name. Search by a country's name or by its code."
      headingId={listHeadingId}
      icon={GlobeIcon}
      testId="countries-card"
    >
      {list.status === "read" ? (
        <SearchedList
          items={list.items}
          pageSize={countriesPerPage}
          keysOf={(country) => country}
          rowOf={(country) => ({ id: country.code, cells: { code: country.code, name: country.name } })}
          address={countriesAddress}
          view={view}
          label={countriesTitle}
          labelledBy={listHeadingId}
          noun={{ one: "country", other: "countries" }}
          search={{ ...referenceListSearch, example: "India or IN" }}
          columns={countryColumns}
        />
      ) : (
        <ReferenceListUnavailable list={list} listName="countries" />
      )}
    </ReferenceListFrame>
  );
}
