import {
  approvalKind,
  askForApproval,
  decideApproval,
  listApprovalRequests,
  readApprovalRequest,
  reportTo,
  signInAdministrator,
  signInColleague,
  withdrawApproval,
  withdrawWaiting,
} from "../../../fixtures/records/approval-records";
import { expect, signedInApi, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  approvalPagePaths,
  ApprovalsPage,
  requestRowSnapshot,
} from "../../../pages/platform/approvals/approvals.page";

const pageSize = 10;

const newestFirst = "Sorted by Asked on, latest first.";

async function firstPageIds(
  api: SignedInApi,
  query: Readonly<Record<string, string>>,
): Promise<readonly string[]> {
  return (await listApprovalRequests(api, "mine", query)).slice(0, pageSize).map((request) => request.id);
}

// A person's own requests are theirs alone, so the list a new person sees holds exactly the requests the test asked for.
test.describe("approval lists", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("pages through a person's requests and sorts, filters by kind and status and hides their columns", async ({
        page,
        person,
        context,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "manager");
        const refused = approvalKind(testInfo, theme, "refused");
        const approvals = new ApprovalsPage(page);
        const { list } = approvals;
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const manager = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), person, manager.person);
          for (const scenario of [kind, kind, kind, kind, kind, kind, kind, kind, kind, refused, refused]) {
            asked.push(await askForApproval(context.request, scenario));
          }
          const [approvedFirst, approvedSecond, sentBack, rejected, withdrawn] = asked;
          await decideApproval(manager.api, approvedFirst ?? "", "approve");
          await decideApproval(manager.api, approvedSecond ?? "", "approve");
          await decideApproval(manager.api, sentBack ?? "", "return", "Add the hotel bill.");
          await decideApproval(manager.api, rejected ?? "", "reject", "Not a business trip.");
          await withdrawApproval(own, withdrawn ?? "");
          const newest = await firstPageIds(own, { sort: "submittedAt:desc" });
          const everyRequest = await listApprovalRequests(own, "mine");
          expect(everyRequest, "the requests the test asked for").toHaveLength(asked.length);

          await approvals.goto("mine");
          await approvals.waitUntilInteractive();
          await expect(approvals.counts).toHaveText("6 of your requests still wait for a decision.");
          await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 requests. ${newestFirst}`);
          await expect(list.pageLabel).toHaveText("Page 1 of 2");
          expect(await approvals.requestIds(), "the newest requests first").toEqual(newest);
          await expect(list.filters).toMatchAriaSnapshot(`
            - search "Filter requests you asked for":
              - group:
                - text: Kind of request
                - button "Kind of request Any"
              - group:
                - text: Status
                - button "Status Any"
              - button "Apply filters"
          `);
          const cards = await list.showsCards();
          if (!cards) {
            await expect(list.columnHeader("Asked on")).toHaveAttribute("aria-sort", "descending");
          }
          await capture("approvals-mine-pages", approvals.card);

          await list.nextPage.click();
          await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
          await expect(list.pageLabel).toHaveText("Page 2 of 2");
          await expect(list.status).toHaveText(`Showing 11–11 of 11 requests. ${newestFirst}`);
          await expect.poll(() => approvals.requestIds()).toEqual([everyRequest.at(-1)?.id]);
          await list.previousPage.click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");
          await expect(list.pageLabel).toHaveText("Page 1 of 2");

          await list.choosePageSize(20);
          await expect(page).toHaveURL((url) => url.searchParams.get("size") === "20");
          await expect(list.status).toHaveText(`Showing all 11 requests. ${newestFirst}`);
          await expect(list.nextPage).toHaveCount(0);
          await list.choosePageSize(pageSize);
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");

          for (const [header, choice, sort] of [
            ["Due", "Due, latest first", "dueAt:desc"],
            ["Due", "Due, earliest first", "dueAt:asc"],
            ["Closed", "Closed, latest first", "closedAt:desc"],
          ] as const) {
            await list.sortBy(header, choice);
            await expect(page).toHaveURL((url) => url.searchParams.get("sort") === sort);
            await expect(list.status).toHaveText(
              `Showing 1–${pageSize} of 11 requests. Sorted by ${choice}.`,
            );
            await expect.poll(() => approvals.requestIds()).toEqual(await firstPageIds(own, { sort }));
          }
          await list.sortBy("Asked on", "Asked on, latest first");
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");
          await expect.poll(() => approvals.requestIds()).toEqual(newest);

          await list.chooseOptions("Kind of request", [refused.title]);
          await list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.searchParams.getAll("kind").join() === refused.key);
          await expect(list.status).toHaveText(`Showing all 2 requests. ${newestFirst}`);
          await expect.poll(() => approvals.requestIds()).toEqual(asked.slice(-2).toReversed());
          await list.chooseOptions("Status", ["Approved"]);
          await list.applyFilters.click();
          await expect(page).toHaveURL(
            (url) =>
              url.searchParams.getAll("kind").join() === refused.key &&
              url.searchParams.getAll("state").join() === "approved",
          );
          await expect(list.noMatches).toContainText("No requests match these filters.");
          await expect(list.status).toHaveText("No requests to show.");
          await capture("approvals-mine-no-matches", approvals.card);
          await list.noMatches.getByRole("link", { name: "Clear filters" }).click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");
          await expect(list.optionsFilter("Kind of request")).toBeFocused();

          await list.chooseOptions("Status", ["Sent back", "Rejected"]);
          await list.applyFilters.click();
          await expect(page).toHaveURL(
            (url) => url.searchParams.getAll("state").toSorted().join() === "rejected,returned",
          );
          await expect(list.optionsFilter("Status")).toHaveText("2 selected");
          await expect(list.status).toHaveText(`Showing all 2 requests. ${newestFirst}`);
          await expect.poll(() => approvals.requestIds()).toEqual([rejected, sentBack]);
          for (const id of [rejected ?? "", sentBack ?? ""]) {
            await expect(await approvals.request(id)).toMatchAriaSnapshot(
              requestRowSnapshot(await readApprovalRequest(own, id), viewer, "answered", cards),
            );
          }
          await capture("approvals-mine-filtered", approvals.card);
          await list.optionsFilter("Status").click();
          await page.getByRole("menuitem", { name: "Clear choices" }).click();
          await expect(page.getByRole("menu")).toHaveCount(0);
          await list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");
          await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 requests. ${newestFirst}`);

          await list.setColumnVisible("Asked by", false);
          await expect(page).toHaveURL((url) => url.searchParams.get("hide") === "requestedFor");
          if (!cards) await expect(list.columnHeader("Asked by")).toHaveCount(0);
          else await expect(list.cards.getByRole("term").filter({ hasText: "Asked by" })).toHaveCount(0);
          await list.setColumnVisible("Asked by", true);
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine && url.search === "");
          expect(await approvals.requestIds()).toEqual(newest);
        } finally {
          await withdrawWaiting(own, asked);
          await manager.request.dispose();
        }
      });
    });
  }
});
