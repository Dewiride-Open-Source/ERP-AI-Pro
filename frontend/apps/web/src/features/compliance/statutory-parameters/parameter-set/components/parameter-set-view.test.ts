import assert from "node:assert/strict";
import { test } from "node:test";

import {
  isParameterSetKey,
  keptVersion,
  parameterSetAddress,
  parameterSetHref,
  readParameterSetView,
  versionHref,
  versionStanding,
} from "./parameter-set-view.ts";

const versions = [{ from: "2027-04-01" }, { from: "2025-04-01" }, { from: "2023-04-01" }];

test("readParameterSetView_NoParameters_IsTheVersionInForceTodayOnItsFirstPage", () => {
  assert.deepEqual(readParameterSetView({}), {
    view: { version: undefined, list: { search: undefined, page: 1 } },
    canonical: true,
  });
});

test("readParameterSetView_VersionSearchAndPage_KeepsEachAsTheCanonicalAddress", () => {
  assert.deepEqual(readParameterSetView({ version: "2023-04-01", q: "transport", page: "2" }), {
    view: { version: "2023-04-01", list: { search: "transport", page: 2 } },
    canonical: true,
  });
});

test("readParameterSetView_MalformedOrRepeatedVersion_DropsIt", () => {
  for (const version of [
    "yesterday",
    "01-04-2023",
    "2023-02-30",
    "0000-01-01",
    ["2023-04-01", "2025-04-01"],
  ]) {
    assert.deepEqual(
      readParameterSetView({ version, q: "transport" }),
      { view: { version: undefined, list: { search: "transport", page: 1 } }, canonical: false },
      JSON.stringify(version),
    );
  }
});

test("readParameterSetView_SearchOrPageWrittenAnotherWay_IsNotCanonical", () => {
  assert.deepEqual(readParameterSetView({ version: "2023-04-01", q: " transport ", page: "1" }), {
    view: { version: "2023-04-01", list: { search: "transport", page: 1 } },
    canonical: false,
  });
});

test("readParameterSetView_ParameterThePageDoesNotKnow_IsNotCanonical", () => {
  assert.equal(readParameterSetView({ version: "2023-04-01", on: "2023-04-01" }).canonical, false);
});

test("isParameterSetKey_KeyOfDottedLowerCaseWords_IsOne", () => {
  for (const key of ["gst.central-tax-rates", "a.b", "tds.section-194j.rates", "x1.y2-z3"]) {
    assert.equal(isParameterSetKey(key), true, key);
  }
});

test("isParameterSetKey_AnythingElse_IsNotOne", () => {
  for (const key of [
    "",
    "gst",
    "GST.rates",
    "gst..rates",
    ".rates",
    "rates.",
    "gst.-rates",
    "gst.rates-",
    "gst.rates rates",
    `gst.${"a".repeat(61)}`,
  ]) {
    assert.equal(isParameterSetKey(key), false, key);
  }
  assert.equal(isParameterSetKey(`gst.${"a".repeat(60)}`), true);
});

test("keptVersion_NoVersionOrTheOneInForceToday_IsLeftOut", () => {
  assert.equal(keptVersion(undefined, versions, "2025-04-01"), undefined);
  assert.equal(keptVersion("2025-04-01", versions, "2025-04-01"), undefined);
});

test("keptVersion_DayThatStartsNoVersion_IsLeftOut", () => {
  assert.equal(keptVersion("2024-01-01", versions, "2025-04-01"), undefined);
  assert.equal(keptVersion("2020-04-01", versions, "2025-04-01"), undefined);
});

test("keptVersion_EarlierOrUpcomingVersion_IsKept", () => {
  assert.equal(keptVersion("2023-04-01", versions, "2025-04-01"), "2023-04-01");
  assert.equal(keptVersion("2027-04-01", versions, "2025-04-01"), "2027-04-01");
  assert.equal(keptVersion("2023-04-01", versions, null), "2023-04-01");
});

test("parameterSetAddress_EarlierVersion_KeepsItInEveryAddressTheEntriesBuild", () => {
  assert.deepEqual(parameterSetAddress("gst.sample", "2023-04-01"), {
    path: "/compliance/statutory-parameters/parameter-sets/gst.sample",
    keep: { version: "2023-04-01" },
  });
  assert.deepEqual(parameterSetAddress("gst.sample", undefined), {
    path: "/compliance/statutory-parameters/parameter-sets/gst.sample",
    keep: undefined,
  });
});

test("parameterSetHref_VersionSearchAndPage_NamesTheVersionFirst", () => {
  assert.equal(
    parameterSetHref("gst.sample", { version: "2023-04-01", list: { search: "transport", page: 2 } }),
    "/compliance/statutory-parameters/parameter-sets/gst.sample?version=2023-04-01&q=transport&page=2",
  );
  assert.equal(
    parameterSetHref("gst.sample", { version: undefined, list: { search: undefined, page: 1 } }),
    "/compliance/statutory-parameters/parameter-sets/gst.sample",
  );
});

test("parameterSetHref_CanonicalAddress_ReadsBackAsTheSameView", () => {
  const view = { version: "2027-04-01", list: { search: "goods transport", page: 3 } };
  const query = new URL(parameterSetHref("gst.sample", view), "https://erp.example").searchParams;

  assert.deepEqual(readParameterSetView(Object.fromEntries(query)), { view, canonical: true });
});

test("versionHref_EachVersion_OpensItsFirstPageWithoutASearch", () => {
  assert.equal(
    versionHref("gst.sample", "2023-04-01", "2025-04-01"),
    "/compliance/statutory-parameters/parameter-sets/gst.sample?version=2023-04-01",
  );
  assert.equal(
    versionHref("gst.sample", "2025-04-01", "2025-04-01"),
    "/compliance/statutory-parameters/parameter-sets/gst.sample",
  );
});

test("versionStanding_VersionAgainstTheOneInForceToday_IsInForceUpcomingOrPast", () => {
  assert.equal(versionStanding("2025-04-01", "2025-04-01"), "inForceToday");
  assert.equal(versionStanding("2027-04-01", "2025-04-01"), "upcoming");
  assert.equal(versionStanding("2023-04-01", "2025-04-01"), "past");
});

test("versionStanding_SetWithNothingInForceToday_HasOnlyUpcomingVersions", () => {
  assert.equal(versionStanding("2027-04-01", null), "upcoming");
});
