export const countriesPage = { path: "/platform/reference-data", title: "Countries" } as const;

export const indianStatesPage = {
  path: "/platform/reference-data/indian-states",
  title: "States and union territories",
} as const;

export const currenciesPage = { path: "/platform/reference-data/currencies", title: "Currencies" } as const;

export const financialYearsPage = {
  path: "/platform/reference-data/financial-years",
  title: "Financial years",
} as const;

export const referenceListPages = [
  countriesPage,
  indianStatesPage,
  currenciesPage,
  financialYearsPage,
] as const;

export const referenceListSearch = {
  label: "Name or code",
  advice: "Search by another name or code.",
} as const;
