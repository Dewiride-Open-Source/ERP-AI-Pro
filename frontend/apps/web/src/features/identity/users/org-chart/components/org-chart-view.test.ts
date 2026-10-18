import assert from "node:assert/strict";
import { test } from "node:test";

import { filterSignature, isChartDate, orgChartHref, readOrgChartView } from "./org-chart-view.ts";

const today = "2026-10-18";

const sales = "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e";

test("readOrgChartView_NoParameters_IsTodaysWholeChartOnItsFirstPage", () => {
  assert.deepEqual(readOrgChartView({}, today), {
    view: { department: undefined, on: undefined, page: 1 },
    canonical: true,
  });
});

test("readOrgChartView_DepartmentDateAndPage_KeepsEachAsTheCanonicalAddress", () => {
  assert.deepEqual(readOrgChartView({ department: sales, on: "2026-04-01", page: "3" }, today), {
    view: { department: sales, on: "2026-04-01", page: 3 },
    canonical: true,
  });
});

test("readOrgChartView_TodaysDateOrTheFirstPageNamed_LeavesThemOutOfTheCanonicalAddress", () => {
  assert.deepEqual(readOrgChartView({ on: today }, today), {
    view: { department: undefined, on: undefined, page: 1 },
    canonical: false,
  });
  assert.deepEqual(readOrgChartView({ page: "1" }, today).canonical, false);
});

test("readOrgChartView_MalformedOrRepeatedValues_DropsThem", () => {
  for (const parameters of [
    { department: "sales" },
    { department: [sales, sales] },
    { on: "18-10-2026" },
    { on: "2026-02-30" },
    { on: "0000-01-01" },
    { page: "0" },
    { page: "01" },
    { page: "two" },
    { page: "1000000" },
    { page: ["2", "3"] },
  ]) {
    assert.deepEqual(
      readOrgChartView(parameters, today),
      { view: { department: undefined, on: undefined, page: 1 }, canonical: false },
      JSON.stringify(parameters),
    );
  }
});

test("readOrgChartView_UpperCaseDepartment_IsWrittenInLowerCase", () => {
  assert.deepEqual(readOrgChartView({ department: sales.toUpperCase() }, today), {
    view: { department: sales, on: undefined, page: 1 },
    canonical: false,
  });
});

test("readOrgChartView_ParameterTheChartDoesNotKnow_IsNotCanonical", () => {
  assert.deepEqual(readOrgChartView({ department: sales, sort: "name" }, today), {
    view: { department: sales, on: undefined, page: 1 },
    canonical: false,
  });
});

test("orgChartHref_View_NamesOnlyWhatDiffersFromTheDefaultInAFixedOrder", () => {
  assert.equal(orgChartHref({ department: undefined, on: undefined, page: 1 }), "/identity/users/org-chart");
  assert.equal(
    orgChartHref({ department: sales, on: "2026-04-01", page: 2 }),
    `/identity/users/org-chart?department=${sales}&on=2026-04-01&page=2`,
  );
  assert.equal(
    orgChartHref({ department: undefined, on: "2026-04-01", page: 1 }),
    "/identity/users/org-chart?on=2026-04-01",
  );
});

test("orgChartHref_CanonicalAddress_ReadsBackAsTheSameView", () => {
  const view = { department: sales, on: "2027-01-31", page: 4 };
  const address = new URL(orgChartHref(view), "https://erp.example");

  assert.deepEqual(readOrgChartView(Object.fromEntries(address.searchParams), today), {
    view,
    canonical: true,
  });
});

test("isChartDate_Dates_AcceptsRealDatesTheApiCanRead", () => {
  assert.equal(isChartDate("2026-10-18"), true);
  assert.equal(isChartDate("2028-02-29"), true);
  assert.equal(isChartDate("2026-02-29"), false);
  assert.equal(isChartDate("0000-12-31"), false);
  assert.equal(isChartDate(""), false);
});

test("filterSignature_Views_DiffersOnlyWhenTheDepartmentOrTheDateDiffers", () => {
  assert.equal(
    filterSignature({ department: sales, on: undefined }),
    filterSignature({ department: sales, on: undefined }),
  );
  assert.notEqual(
    filterSignature({ department: sales, on: undefined }),
    filterSignature({ department: undefined, on: undefined }),
  );
  assert.notEqual(
    filterSignature({ department: undefined, on: "2026-04-01" }),
    filterSignature({ department: undefined, on: undefined }),
  );
});
