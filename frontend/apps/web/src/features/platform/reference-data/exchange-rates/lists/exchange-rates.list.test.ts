import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery } from "@/shared/lists/list-query";

import { exchangeRatesList } from "./exchange-rates.list.ts";

const basePath = "/platform/reference-data/exchange-rates";

test("exchangeRatesList_NoParameters_ListsTenWithTheNewestDayFirst", () => {
  const { query, canonical } = readListQuery({}, exchangeRatesList);

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, exchangeRatesList), {
    page: 1,
    pageSize: 10,
    sort: "rateDate:desc",
    filter: undefined,
  });
  assert.equal(listHref(basePath, query, exchangeRatesList), basePath);
});

test("exchangeRatesList_CurrenciesAndDays_ReachTheApiAsTheAddressHoldsThem", () => {
  const { query, canonical } = readListQuery(
    {
      currency: ["CHF", "SEK"],
      dayFrom: "1901-01-01",
      dayTo: "1901-01-05",
      sort: "currency:asc",
      size: "20",
      page: "2",
    },
    exchangeRatesList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, exchangeRatesList), {
    page: 2,
    pageSize: 20,
    sort: "currency:asc",
    filter: "currency:in:CHF|SEK;rateDate:gte:1901-01-01;rateDate:lte:1901-01-05",
  });
  assert.equal(
    listHref(basePath, query, exchangeRatesList),
    `${basePath}?currency=CHF&currency=SEK&dayFrom=1901-01-01&dayTo=1901-01-05&sort=currency%3Aasc&size=20&page=2`,
  );
});

test("exchangeRatesList_OneEndOfTheDays_FiltersFromOrUntilThatDay", () => {
  const { query } = readListQuery({ dayTo: "2026-03-31" }, exchangeRatesList);

  assert.equal(listApiParameters(query, exchangeRatesList).filter, "rateDate:lte:2026-03-31");
});

test("exchangeRatesList_CurrenciesInAnotherOrder_ReachTheApiInCodeOrder", () => {
  const { query } = readListQuery({ currency: ["SEK", "CHF"] }, exchangeRatesList);

  assert.equal(listApiParameters(query, exchangeRatesList).filter, "currency:in:CHF|SEK");
  assert.equal(listHref(basePath, query, exchangeRatesList), `${basePath}?currency=CHF&currency=SEK`);
});

test("exchangeRatesList_ReversedDaysOrAPageSizeItDoesNotOffer_FallBackToTheDefaults", () => {
  const { query, canonical } = readListQuery(
    { dayFrom: "2026-04-01", dayTo: "2026-03-31", size: "25" },
    exchangeRatesList,
  );

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, {});
  assert.equal(query.pageSize, 10);
});

test("exchangeRatesList_ASortFieldItDoesNotOffer_FallsBackToTheNewestDayFirst", () => {
  const { query, canonical } = readListQuery({ sort: "rate:desc" }, exchangeRatesList);

  assert.equal(canonical, false);
  assert.deepEqual(query.sort, { field: "rateDate", direction: "desc" });
});
