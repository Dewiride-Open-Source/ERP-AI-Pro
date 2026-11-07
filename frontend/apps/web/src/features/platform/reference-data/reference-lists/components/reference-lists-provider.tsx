import type { ReactNode } from "react";

import { readCountries, readCurrencies, readFinancialYears, readIndianStates } from "../server/queries";
import { ReferenceListsContextProvider } from "./reference-lists-context";
import type { ReferenceLists } from "./reference-options";

export interface ReferenceListNeeds {
  readonly countries?: boolean;
  readonly indianStates?: boolean;
  readonly currencies?: boolean;
  readonly financialYears?: { readonly from: string; readonly to: string };
}

// Another module's page wraps the form that holds pickers in this provider, naming the lists it needs, so the lists are
// read on the server with the page and the module's queries stay its own.
export async function ReferenceListsProvider({
  needs,
  children,
}: Readonly<{ needs: ReferenceListNeeds; children: ReactNode }>) {
  const [countries, indianStates, currencies, financialYears] = await Promise.all([
    needs.countries ? readCountries() : undefined,
    needs.indianStates ? readIndianStates() : undefined,
    needs.currencies ? readCurrencies() : undefined,
    needs.financialYears ? readFinancialYears(needs.financialYears.from, needs.financialYears.to) : undefined,
  ]);
  const lists: ReferenceLists = {
    ...(countries === undefined ? {} : { countries }),
    ...(indianStates === undefined ? {} : { indianStates }),
    ...(currencies === undefined ? {} : { currencies }),
    ...(financialYears === undefined ? {} : { financialYears }),
  };

  return <ReferenceListsContextProvider lists={lists}>{children}</ReferenceListsContextProvider>;
}
