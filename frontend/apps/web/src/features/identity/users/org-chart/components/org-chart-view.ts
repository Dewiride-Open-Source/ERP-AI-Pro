import { isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";

export type SearchParameters = Readonly<Record<string, string | readonly string[] | undefined>>;

export interface OrgChartView {
  readonly department: string | undefined;
  readonly on: string | undefined;
  readonly page: number;
}

export interface ReadOrgChartView {
  readonly view: OrgChartView;
  readonly canonical: boolean;
}

export const orgChartPath = "/identity/users/org-chart";

export const orgChartParameters = { department: "department", on: "on", page: "page" } as const;

const departmentId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const pageNumber = /^[1-9][0-9]{0,5}$/;

const knownParameters: ReadonlySet<string> = new Set(Object.values(orgChartParameters));

// The API reads a date as .NET's DateOnly, which has no year 0.
export function isChartDate(text: string): boolean {
  return isIsoDate(text) && !text.startsWith("0000");
}

function single(value: string | readonly string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

// The address holds the view, and an address that says it another way, or names what the chart cannot show, is answered
// with the one canonical address: a malformed or repeated value is dropped, an id is written in lower case, today's date is
// left out because it is the default, and the first page is never named.
export function readOrgChartView(parameters: SearchParameters, today: string): ReadOrgChartView {
  const department = single(parameters[orgChartParameters.department]);
  const on = single(parameters[orgChartParameters.on]);
  const page = single(parameters[orgChartParameters.page]);
  const view: OrgChartView = {
    department:
      department !== undefined && departmentId.test(department) ? department.toLowerCase() : undefined,
    on: on !== undefined && isChartDate(on) && on !== today ? on : undefined,
    page: page !== undefined && pageNumber.test(page) ? Number(page) : 1,
  };
  const canonical =
    Object.entries(parameters).every(
      ([name, value]) => value === undefined || (knownParameters.has(name) && typeof value === "string"),
    ) &&
    department === view.department &&
    on === view.on &&
    (page === undefined || (page === String(view.page) && view.page > 1));
  return { view, canonical };
}

export function orgChartHref(
  view: OrgChartView,
): "/identity/users/org-chart" | `/identity/users/org-chart?${string}` {
  const search = new URLSearchParams();
  if (view.department !== undefined) search.set(orgChartParameters.department, view.department);
  if (view.on !== undefined) search.set(orgChartParameters.on, view.on);
  if (view.page > 1) search.set(orgChartParameters.page, String(view.page));
  const query = search.toString();
  return query === "" ? orgChartPath : `${orgChartPath}?${query}`;
}

export function filterSignature(view: Pick<OrgChartView, "department" | "on">): string {
  return `${view.department ?? ""}|${view.on ?? ""}`;
}
