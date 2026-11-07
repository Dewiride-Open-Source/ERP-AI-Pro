import { CalendarRangeIcon } from "lucide-react";
import { redirect } from "next/navigation";

import { istDateKey } from "@/shared/format/dates";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { financialYearsPage } from "../../list-pages/components/reference-list-pages";
import { ReferenceListTable } from "../../list-pages/components/reference-list-table";
import type { SearchParameters } from "../../list-pages/components/reference-list-view";
import { ReferenceListsProvider } from "../../reference-lists/components/reference-lists-provider";
import { readFinancialYears } from "../../reference-lists/server/queries";

import { FinancialYearBrowser } from "./financial-year-browser";
import { periodColumns, periodRows, quarterColumns, quarterRows, yearDays } from "./financial-year-rows";
import { financialYearChoice, financialYearsHref, readFinancialYearView } from "./financial-year-view";

export const financialYearsTitle = financialYearsPage.title;

const yearHeadingId = "financial-year-heading";

const quartersHeadingId = "financial-year-quarters-heading";

const periodsHeadingId = "financial-year-periods-heading";

export async function FinancialYearsOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const choice = financialYearChoice(istDateKey(new Date()));
  const { view, canonical } = readFinancialYearView(searchParameters, choice);
  if (!canonical) redirect(financialYearsHref(view));

  const shown = view.year ?? choice.current;
  const years = { from: choice.from, to: choice.to };
  const list = await readFinancialYears(years.from, years.to);
  const year = list.status === "read" ? list.items.find((item) => item.label === shown) : undefined;

  return (
    <ReferenceListFrame
      title={financialYearsTitle}
      description="Choose a financial year to see its quarters and its monthly periods, each with its first and last day."
      listHeading={`Financial year ${shown}`}
      listDescription={year === undefined ? "Its quarters and monthly periods." : yearDays(year)}
      headingId={yearHeadingId}
      icon={CalendarRangeIcon}
      testId="financial-years-card"
    >
      {year === undefined ? (
        <ReferenceListUnavailable
          list={list.status === "read" ? { status: "failed" } : list}
          listName="financial years"
        />
      ) : (
        <ReferenceListsProvider needs={{ financialYears: years }}>
          <FinancialYearBrowser year={shown} current={choice.current}>
            <div className="grid min-w-0 gap-3" data-testid="financial-year-quarters">
              <h3 id={quartersHeadingId} className="font-medium">
                Quarters
              </h3>
              <ReferenceListTable
                labelledBy={quartersHeadingId}
                columns={quarterColumns}
                rows={quarterRows(year)}
              />
            </div>
            <div className="grid min-w-0 gap-3" data-testid="financial-year-periods">
              <h3 id={periodsHeadingId} className="font-medium">
                Monthly periods
              </h3>
              <ReferenceListTable
                labelledBy={periodsHeadingId}
                columns={periodColumns}
                rows={periodRows(year)}
              />
            </div>
          </FinancialYearBrowser>
        </ReferenceListsProvider>
      )}
    </ReferenceListFrame>
  );
}
