import type { Page } from "@playwright/test";

import { AppShell } from "../pages/shared/layout/app-shell.page";

export interface NavigationEntry {
  readonly name: string;
  readonly href: string;
}

export interface RegisteredNavigation {
  readonly area: string;
  readonly title: string;
  readonly href: string;
  readonly description: string;
  readonly employees: boolean;
}

type PageSearchOption = Pick<RegisteredNavigation, "title" | "href"> &
  Partial<Pick<RegisteredNavigation, "area" | "description">>;

type ArrowKey = "ArrowDown" | "ArrowUp";

export const registeredNavigation: readonly RegisteredNavigation[] = [
  {
    area: "Platform",
    title: "System",
    href: "/platform/system-info",
    description: "See which version of the ERP is running, since when, and its recent starts.",
    employees: false,
  },
  {
    area: "Platform",
    title: "Attachments",
    href: "/platform/attachments",
    description: "Upload and download the files kept with the ERP.",
    employees: true,
  },
  {
    area: "Platform",
    title: "Audit trail",
    href: "/platform/audit",
    description: "See who changed what in the ERP, and when.",
    employees: false,
  },
  {
    area: "Platform",
    title: "Background jobs",
    href: "/platform/jobs",
    description: "See the work the ERP does on its own, when it runs next, and how each run went.",
    employees: false,
  },
  {
    area: "Organisation",
    title: "People",
    href: "/identity/users",
    description: "Register people, assign their roles and record their moves.",
    employees: false,
  },
  {
    area: "Organisation",
    title: "Organisation chart",
    href: "/identity/users/org-chart",
    description: "See who works in each department and who reports to whom.",
    employees: true,
  },
  {
    area: "Organisation",
    title: "Departments",
    href: "/identity/users/departments",
    description: "See the company's departments and which department each one sits under.",
    employees: true,
  },
  {
    area: "Organisation",
    title: "Designations",
    href: "/identity/users/designations",
    description: "See the designations people can hold in the company.",
    employees: true,
  },
];

export const employeeNavigation: readonly RegisteredNavigation[] = registeredNavigation.filter(
  (entry) => entry.employees,
);

const home: PageSearchOption = { title: "Home", href: "/" };

const appName = "ERP-AI-Pro";

// The shell lists the module registry's enabled entries in the primary navigation under the title of their area, after the
// Home link that every person has. Below the md breakpoint that navigation exists only while the drawer is open, so the
// drawer is opened for the reading and closed again.
export async function registeredNavigationEntries(page: Page): Promise<NavigationEntry[]> {
  const shell = new AppShell(page);
  await shell.openNavigation();
  const entries = await shell.primaryNavigation
    .getByRole("list", { name: /\S/ })
    .getByRole("link")
    .evaluateAll((links) =>
      links.map((link) => ({
        name: link.textContent?.trim() ?? "",
        href: link.getAttribute("href") ?? "",
      })),
    );
  await shell.closeDrawer();
  return entries;
}

export function primaryNavigationSnapshot(entries: readonly RegisteredNavigation[]): string {
  return primaryNavigation(entries).join("\n");
}

export function sidebarSnapshot(entries: readonly RegisteredNavigation[]): string {
  return [...wordmark(), ...primaryNavigation(entries)].join("\n");
}

export function drawerSnapshot(entries: readonly RegisteredNavigation[]): string {
  return node(key("dialog", "Navigation"), [
    ...node(key("heading", "Navigation", "level=2")),
    text("paragraph", "The pages you can open."),
    ...wordmark(),
    ...node(key("button", "Close navigation")),
    ...primaryNavigation(entries),
  ]).join("\n");
}

export function pageSearchMatches(
  query: string,
  entries: readonly RegisteredNavigation[] = registeredNavigation,
): readonly PageSearchOption[] {
  const terms = folded(query)
    .split(/\s+/u)
    .filter((term) => term !== "");
  return [home, ...entries].filter((option) =>
    terms.every((term) =>
      [option.title, option.description, option.area].some(
        (field) => field !== undefined && folded(field).includes(term),
      ),
    ),
  );
}

export function pageSearchStatus(
  query: string,
  entries: readonly RegisteredNavigation[] = registeredNavigation,
): string {
  if (folded(query).trim() === "") return "";
  const count = pageSearchMatches(query, entries).length;
  if (count === 0) return "No page matches your search.";
  return count === 1 ? "1 page" : `${count} pages`;
}

export function pageSearchResultsSnapshot(matches: readonly PageSearchOption[]): string {
  return pageSearchResults(matches).join("\n");
}

export function pageSearchDialogSnapshot(matches: readonly PageSearchOption[]): string {
  return node(key("dialog", "Go to a page"), [
    ...node(key("heading", "Go to a page", "level=2")),
    ...node(key("combobox", "Search pages", "expanded")),
    ...node(key("button", "Close Esc")),
    ...node(key("status")),
    ...pageSearchResults(matches),
  ]).join("\n");
}

export function pageSearchArrowCycle(
  entries: readonly RegisteredNavigation[],
): readonly (readonly [ArrowKey, string])[] {
  const titles = entries.map((entry) => entry.title);
  return [
    ...titles.map((title): [ArrowKey, string] => ["ArrowDown", title]),
    ["ArrowDown", home.title],
    ...titles.toReversed().map((title): [ArrowKey, string] => ["ArrowUp", title]),
  ];
}

export function startPageSnapshot(
  entries: readonly RegisteredNavigation[],
  welcome: string | RegExp,
): string {
  return [
    text("paragraph", "Home"),
    ...node(key("heading", welcome, "level=1")),
    text("paragraph", "Choose where to start."),
    ...node(key("region", "Areas you can open"), [
      ...node(key("heading", "Areas you can open", "level=2")),
      ...node(
        key("list"),
        entries.flatMap((entry) =>
          node(key("listitem"), [
            ...node(key("heading", entry.title, "level=3")),
            text("text", entry.description),
            ...node(key("link", `Open ${entry.title}`)),
          ]),
        ),
      ),
    ]),
  ].join("\n");
}

function primaryNavigation(entries: readonly RegisteredNavigation[]): string[] {
  return node(key("navigation", "Primary"), [
    ...node(key("list"), node(key("listitem"), link(home))),
    ...runsOf(entries, (entry) => entry.area).flatMap(({ area, members }) => [
      text("text", area),
      ...node(
        key("list", area),
        members.flatMap((entry) => node(key("listitem"), link(entry))),
      ),
    ]),
  ]);
}

function pageSearchResults(matches: readonly PageSearchOption[]): string[] {
  const options = matches.map((match, index) => ({
    area: match.area,
    lines: node(
      key(
        "option",
        [match.title, match.description].filter(Boolean).join(" "),
        ...(index === 0 ? ["selected"] : []),
      ),
    ),
  }));
  return node(
    key("listbox", "Pages"),
    runsOf(options, (option) => option.area).flatMap(({ area, members }) => {
      const lines = members.flatMap((option) => option.lines);
      return area === undefined ? lines : node(key("group", area), [text("text", area), ...lines]);
    }),
  );
}

function runsOf<T, TArea extends string | undefined>(
  items: readonly T[],
  areaOf: (item: T) => TArea,
): { readonly area: TArea; readonly members: readonly T[] }[] {
  const runs: { area: TArea; members: T[] }[] = [];
  for (const item of items) {
    const area = areaOf(item);
    const last = runs.at(-1);
    if (last !== undefined && last.area === area) last.members.push(item);
    else runs.push({ area, members: [item] });
  }
  return runs;
}

function wordmark(): string[] {
  return node(key("list"), node(key("listitem"), link({ title: appName, href: "/" })));
}

function link({ title, href }: PageSearchOption): string[] {
  return node(key("link", title), [`- /url: ${yamlValue(href)}`]);
}

function key(role: string, name?: string | RegExp, ...attributes: string[]): string {
  const accessibleName =
    name === undefined ? [] : [typeof name === "string" ? JSON.stringify(name) : String(name)];
  return yamlKey([role, ...accessibleName, ...attributes.map((attribute) => `[${attribute}]`)].join(" "));
}

function text(role: string, value: string): string {
  return `- ${role}: ${yamlValue(value)}`;
}

function node(nodeKey: string, children: readonly string[] = []): string[] {
  return children.length === 0 ? [`- ${nodeKey}`] : [`- ${nodeKey}:`, ...children.map((line) => `  ${line}`)];
}

function folded(value: string): string {
  return value.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
}

function needsQuotes(value: string): boolean {
  return (
    value === "" ||
    /^\s|\s$/.test(value) ||
    value.startsWith("-") ||
    /:(\s|$)/.test(value) ||
    /\s#/.test(value) ||
    /^[&*\],?!>|@"'#%[]/.test(value) ||
    /[{}`]/.test(value) ||
    !Number.isNaN(Number(value)) ||
    ["y", "n", "yes", "no", "true", "false", "on", "off", "null"].includes(value.toLowerCase())
  );
}

function yamlKey(value: string): string {
  return needsQuotes(value) ? `'${value.replaceAll("'", "''")}'` : value;
}

function yamlValue(value: string): string {
  return needsQuotes(value) ? JSON.stringify(value) : value;
}
