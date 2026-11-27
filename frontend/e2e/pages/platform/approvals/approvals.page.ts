import { expect, type Locator, type Page, type Request } from "@playwright/test";

import type {
  ApprovalRequestSummary,
  ApprovalRole,
  ApprovalState,
} from "../../../fixtures/records/approval-records";
import { DataTableRegion } from "../../shared/lists/data-table.page";

export type ApprovalPageKey = "waiting" | "mine" | "decided" | "kinds" | "all";

export const approvalsPath = "/platform/approvals";

export const approvalPagePaths: Readonly<Record<ApprovalPageKey, string>> = {
  waiting: approvalsPath,
  mine: `${approvalsPath}/mine`,
  decided: `${approvalsPath}/decided`,
  kinds: `${approvalsPath}/kinds`,
  all: `${approvalsPath}/all`,
};

export const approvalPageTitles: Readonly<Record<ApprovalPageKey, string>> = {
  waiting: "Waiting for me",
  mine: "My requests",
  decided: "Decided by me",
  kinds: "Kinds of request",
  all: "All requests",
};

export const approvalStateLabels: Readonly<Record<ApprovalState, string>> = {
  submitted: "Waiting",
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
  withdrawn: "Withdrawn",
};

export const lateLabel = "Late";

export const notRecorded = "—";

const roleGroups: Readonly<Record<ApprovalRole, string>> = {
  employee: "the employees",
  finance: "Finance",
  hr: "HR",
  manager: "the managers",
  administrator: "the administrators",
};

// The page's own reading of the times it shows, written apart from the product's: India Standard Time, as people in India
// write it, and a step due by the end of the day before the midnight the API names as its due time.
const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

const indiaDate = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", dateStyle: "medium" });

const indiaDay = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});

export function approvalRequestPath(id: string): string {
  return `${approvalsPath}/requests/${id}`;
}

function lastDayToDecide(dueAt: string): Date {
  return new Date(new Date(dueAt).getTime() - 1);
}

export function dateTimeText(instant: string): string {
  return indiaDateTime.format(new Date(instant));
}

export function dueDateText(dueAt: string): string {
  return indiaDate.format(lastDayToDecide(dueAt));
}

export function dueDayText(dueAt: string): string {
  return indiaDay.format(lastDayToDecide(dueAt));
}

export function roleGroupText(role: ApprovalRole): string {
  return roleGroups[role];
}

// A page names the person reading it "you" wherever a request names them.
export function personText(id: string, name: string | null, viewer: string, sentenceStart = false): string {
  if (id.toLowerCase() === viewer.toLowerCase()) return sentenceStart ? "You" : "you";
  return name ?? (sentenceStart ? "An unknown person" : "an unknown person");
}

export function waitingForText(request: ApprovalRequestSummary, viewer: string): string {
  if (request.waitingForKind === "role" && request.waitingForRole !== null) {
    return roleGroupText(request.waitingForRole);
  }
  return personText(request.waitingForId ?? "", request.waitingForName, viewer);
}

export function waitingText(request: ApprovalRequestSummary, viewer: string): string {
  return `${request.waitingStepTitle ?? ""} step, waiting for ${waitingForText(request, viewer)}`;
}

// Requests of one kind read alike in a list, so each request's link names who asked and when beside its kind.
export function requestLinkName(request: ApprovalRequestSummary, viewer: string): string {
  const askedBy = personText(request.requestedForId, request.requestedForName, viewer);
  return `${request.kindTitle}, asked by ${askedBy} on ${dateTimeText(request.submittedAt)}`;
}

export type RequestRowColumns = "waiting" | "answered";

// A row's cells in the order the list shows them, with the header a card writes beside each one on a narrow screen.
export function requestRowCells(
  request: ApprovalRequestSummary,
  viewer: string,
  columns: RequestRowColumns,
): readonly (readonly [header: string, text: string])[] {
  const asked: (readonly [string, string])[] = [
    ["Asked by", personText(request.requestedForId, request.requestedForName, viewer, true)],
    ["Asked on", dateTimeText(request.submittedAt)],
  ];
  const waiting = request.state === "submitted";
  const due = request.dueAt === null || !waiting ? notRecorded : dueDateText(request.dueAt);
  if (columns === "waiting") {
    return [
      ...asked,
      ["Step", waitingText(request, viewer)],
      ["Due", request.late ? `${due} ${lateLabel}` : due],
    ];
  }
  const status = [
    approvalStateLabels[request.state],
    ...(waiting && request.late ? [lateLabel] : []),
    ...(waiting ? [waitingText(request, viewer)] : []),
  ].join(" ");
  return [
    ...asked,
    ["Status", status],
    ["Due", due],
    ["Closed", request.closedAt === null ? notRecorded : dateTimeText(request.closedAt)],
  ];
}

// A row reads as a table row on a wide screen and as a card of terms on a narrow one.
export function requestRowSnapshot(
  request: ApprovalRequestSummary,
  viewer: string,
  columns: RequestRowColumns,
  cards: boolean,
): string {
  const name = requestLinkName(request, viewer);
  const link = [
    `- link ${JSON.stringify(name)}:`,
    `  - /url: ${approvalRequestPath(request.id)}`,
    `  - text: ${JSON.stringify(request.kindTitle)}`,
  ];
  const cells = requestRowCells(request, viewer, columns);
  if (cards) {
    return [
      "- listitem:",
      ...link.map((line) => `  ${line}`),
      ...cells.flatMap(([header, text]) => [
        `  - term: ${JSON.stringify(header)}`,
        `  - definition: ${JSON.stringify(text)}`,
      ]),
    ].join("\n");
  }
  return [
    "- row:",
    `  - rowheader ${JSON.stringify(name)}:`,
    ...link.map((line) => `    ${line}`),
    ...cells.map(([, text]) => `  - cell ${JSON.stringify(text)}`),
  ].join("\n");
}

export function watchRequests(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

// A dialog that starts to close turns its data-state to "closed" at once but stays on the page through its exit animation, so
// every attempt to close it while it saves is judged by that state, never by visibility.
export async function triesToCloseWhileSaving(
  page: Page,
  dialog: Locator,
  closers: readonly Locator[],
): Promise<void> {
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveAttribute("data-state", "open");
  await page.mouse.click(4, 4);
  await expect(dialog).toHaveAttribute("data-state", "open");
  for (const closer of closers) {
    await expect(closer).toHaveAttribute("aria-disabled", "true");
    await closer.focus();
    await page.keyboard.press("Enter");
    await expect(dialog).toHaveAttribute("data-state", "open");
  }
}

export function pageLinksSnapshot(pages: readonly ApprovalPageKey[]): string {
  return [
    '- navigation "Approvals":',
    "  - list:",
    ...pages.flatMap((key) => [
      "    - listitem:",
      `      - link ${JSON.stringify(approvalPageTitles[key])}:`,
      `        - /url: ${approvalPagePaths[key]}`,
    ]),
  ].join("\n");
}

export class ApprovalsPage {
  readonly main: Locator;
  readonly pageLinks: Locator;
  readonly heading: Locator;
  readonly counts: Locator;
  readonly card: Locator;
  readonly listHeading: Locator;
  readonly empty: Locator;
  readonly unavailable: Locator;
  readonly list: DataTableRegion;

  constructor(private readonly page: Page) {
    this.main = page.getByRole("main");
    this.pageLinks = this.main.getByRole("navigation", { name: "Approvals", exact: true });
    this.heading = this.main.getByRole("heading", { level: 1 });
    this.counts = page.getByTestId("approval-counts");
    this.card = page.getByTestId("approval-requests-card");
    this.listHeading = this.card.getByRole("heading", { level: 2 });
    this.empty = page.getByTestId("approval-requests-empty");
    this.unavailable = page.getByTestId("approval-requests-unavailable");
    this.list = new DataTableRegion(page, this.card);
  }

  async goto(key: Exclude<ApprovalPageKey, "kinds">, query = ""): Promise<void> {
    await this.page.goto(`${approvalPagePaths[key]}${query}`);
    await expect(this.page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
    await expect(this.heading).toBeVisible();
  }

  pageLink(key: ApprovalPageKey): Locator {
    return this.pageLinks.getByRole("link", { name: approvalPageTitles[key], exact: true });
  }

  // The list's controls work only through React, so they are disabled in the server's HTML until the page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await this.list.waitUntilInteractive();
  }

  // A request's link carries its id, and the rows show as a table on a wide screen and as cards on a narrow one.
  async request(id: string): Promise<Locator> {
    return (await this.list.rows()).filter({ has: this.page.locator(`[data-request-link="${id}"]`) });
  }

  async requestLink(id: string): Promise<Locator> {
    return (await this.request(id)).getByTestId("approval-request-link");
  }

  async requestIds(): Promise<string[]> {
    return (await this.list.rows())
      .getByTestId("approval-request-link")
      .evaluateAll((links) => links.map((link) => link.getAttribute("data-request-link") ?? ""));
  }

  // The shown rows' list, the table of a wide screen or the cards of a narrow one, for an ARIA snapshot of its rows.
  async shownList(): Promise<Locator> {
    return (await this.list.showsCards()) ? this.list.cardList : this.list.table;
  }
}
