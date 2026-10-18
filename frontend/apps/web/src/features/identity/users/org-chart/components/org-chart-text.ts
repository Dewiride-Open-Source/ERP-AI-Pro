import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import { countLabel } from "../../organisation/components/disclosure-tree.ts";

import type { ChartNode } from "./org-chart.ts";

export const offChartExplanation =
  "A person is left off the chart when their record is deactivated or deleted, or when they had not joined by the chart's date.";

export const otherDateRefusal = {
  title: "You can see only today's chart.",
  description:
    "The chart of another date shows where people were or will be, which only administrators can see.",
  action: "Show today's chart",
} as const;

export const departmentsUnreadable =
  "Departments could not be read, so the chart cannot be filtered by department now.";

export const departmentsUnreadableWhileChosen =
  "Departments could not be read, so the chart shows only the people directly in the chosen department, and no other department can be chosen now.";

export function chartHeading(on: string | undefined): string {
  return on === undefined ? "Today's chart" : `Chart on ${formatDisplayDate(on)}`;
}

export function peopleOnChartLabel(count: number): string {
  return count === 0
    ? "Nobody is on this chart."
    : countLabel(count, "person is on this chart.", "people are on this chart.");
}

export function emptyChartMessage(departmentName: string | undefined, chosen: boolean): string {
  if (!chosen) return "Nobody is on the chart for this date.";
  return departmentName === undefined
    ? "Nobody on this chart is in the chosen department."
    : `Nobody on this chart is in ${departmentName} or the departments under it.`;
}

export function showReportsLabel(name: string): string {
  return `Show the people who report to ${name}`;
}

export function reportsListLabel(name: string): string {
  return `People who report to ${name}`;
}

export function reportCountLabel(count: number): string {
  return countLabel(count, "person reports to them", "people report to them");
}

export function positionText(person: Pick<ChartNode, "designationName" | "departmentName">): string {
  return `${person.designationName ?? "No designation"} · ${person.departmentName ?? "No department"}`;
}

export function managerOutsideNote(
  person: Pick<ChartNode, "managerName" | "managerOutside">,
  departmentName: string | undefined,
): string | undefined {
  const manager = person.managerName ?? "their manager";
  switch (person.managerOutside) {
    case "off-chart":
      return `Reports to ${manager}, who is not on this chart.`;
    case "other-department":
      return departmentName === undefined
        ? `Reports to ${manager}, who is outside the chosen department.`
        : `Reports to ${manager}, who is outside ${departmentName}.`;
    case undefined:
      return undefined;
  }
}

export function pageLabel(page: number, pageCount: number): string {
  return `Page ${page} of ${pageCount}`;
}
