import { GlobeIcon } from "lucide-react";
import { redirect } from "next/navigation";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { countriesPage } from "../../list-pages/components/reference-list-pages";
import {
  readReferenceListView,
  referenceListHref,
  type ReferenceListColumn,
  type SearchParameters,
} from "../../list-pages/components/reference-list-view";
import { SearchedReferenceList } from "../../list-pages/components/searched-reference-list";
import { readCountries } from "../../reference-lists/server/queries";

export const countriesTitle = countriesPage.title;

const countriesPerPage = 20;

const listHeadingId = "countries-heading";

const countryColumns: readonly ReferenceListColumn[] = [
  { id: "code", header: "Code" },
  { id: "name", header: "Name", title: true },
];

export async function CountriesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { view, canonical } = readReferenceListView(searchParameters);
  if (!canonical) redirect(referenceListHref(countriesPage.path, view));
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
        <SearchedReferenceList
          items={list.items}
          pageSize={countriesPerPage}
          keysOf={(country) => country}
          rowOf={(country) => ({ id: country.code, cells: { code: country.code, name: country.name } })}
          path={countriesPage.path}
          view={view}
          label={countriesTitle}
          labelledBy={listHeadingId}
          noun={{ one: "country", other: "countries" }}
          searchExample="India or IN"
          columns={countryColumns}
        />
      ) : (
        <ReferenceListUnavailable list={list} listName="countries" />
      )}
    </ReferenceListFrame>
  );
}
