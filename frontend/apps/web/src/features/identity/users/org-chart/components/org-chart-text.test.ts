import assert from "node:assert/strict";
import { test } from "node:test";

import {
  chartHeading,
  emptyChartMessage,
  managerOutsideNote,
  pageLabel,
  peopleOnChartLabel,
  positionText,
  reportCountLabel,
  reportsListLabel,
  showReportsLabel,
} from "./org-chart-text.ts";

test("chartHeading_TodayOrAnotherDate_NamesTheDayShown", () => {
  assert.equal(chartHeading(undefined), "Today's chart");
  assert.equal(chartHeading("2026-04-01"), "Chart on 01-04-2026");
});

test("peopleOnChartLabel_Counts_SaysHowManyPeopleTheChartHolds", () => {
  assert.equal(peopleOnChartLabel(0), "Nobody is on this chart.");
  assert.equal(peopleOnChartLabel(1), "1 person is on this chart.");
  assert.equal(peopleOnChartLabel(1234), "1,234 people are on this chart.");
});

test("emptyChartMessage_WholeChartOrADepartment_SaysWhereNobodyIs", () => {
  assert.equal(emptyChartMessage(undefined, false), "Nobody is on the chart for this date.");
  assert.equal(
    emptyChartMessage("Sales", true),
    "Nobody on this chart is in Sales or the departments under it.",
  );
  assert.equal(emptyChartMessage(undefined, true), "Nobody on this chart is in the chosen department.");
});

test("reportLabels_APerson_NameThemInTheButtonTheListAndTheCount", () => {
  assert.equal(showReportsLabel("Asha Rao"), "Show the people who report to Asha Rao");
  assert.equal(reportsListLabel("Asha Rao"), "People who report to Asha Rao");
  assert.equal(reportCountLabel(1), "1 person reports to them");
  assert.equal(reportCountLabel(3), "3 people report to them");
});

test("positionText_DesignationAndDepartment_NamesBothOrSaysNone", () => {
  assert.equal(positionText({ designationName: "Analyst", departmentName: "Finance" }), "Analyst · Finance");
  assert.equal(
    positionText({ designationName: undefined, departmentName: undefined }),
    "No designation · No department",
  );
});

test("managerOutsideNote_ManagerNotShown_SaysWhyThePersonIsAtTheTop", () => {
  assert.equal(
    managerOutsideNote({ managerName: "Asha Rao", managerOutside: "off-chart" }, "Sales"),
    "Reports to Asha Rao, who is not on this chart.",
  );
  assert.equal(
    managerOutsideNote({ managerName: "Asha Rao", managerOutside: "other-department" }, "Sales"),
    "Reports to Asha Rao, who is outside Sales.",
  );
  assert.equal(
    managerOutsideNote({ managerName: undefined, managerOutside: "other-department" }, undefined),
    "Reports to their manager, who is outside the chosen department.",
  );
  assert.equal(
    managerOutsideNote({ managerName: "Asha Rao", managerOutside: undefined }, "Sales"),
    undefined,
  );
});

test("pageLabel_Pages_NamesThePageShownAndHowManyThereAre", () => {
  assert.equal(pageLabel(2, 3), "Page 2 of 3");
});
