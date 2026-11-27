import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  approvalKind,
  approvalKindSettingApi,
  approvalKindSettingsApi,
  approvalRequestsApi,
  askForApproval,
  decideApproval,
  readApprovalKind,
  readApprovalRequest,
  reportTo,
  signInAdministrator,
  signInColleague,
  untouchedApprovalKind,
  withdrawApproval,
  withdrawWaiting,
} from "../../../fixtures/records/approval-records";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, forEachTheme, signedInApi, test, themes } from "../../../fixtures/test";
import { ApprovalKindsPage, kindSnapshot } from "../../../pages/platform/approvals/approval-kinds.page";
import {
  ApprovalRequestPage,
  detailsSnapshot,
  dialogSnapshot,
  headerSnapshot,
  isRequestAction,
  stepsSnapshot,
} from "../../../pages/platform/approvals/approval-request.page";
import {
  approvalPagePaths,
  approvalRequestPath,
  ApprovalsPage,
  approvalsPath,
  dateTimeText,
  pageLinksSnapshot,
  requestRowSnapshot,
  triesToCloseWhileSaving,
  waitingText,
  watchRequests,
} from "../../../pages/platform/approvals/approvals.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const noAccess = "You do not have access to this page";

const ownPages = ["waiting", "mine", "decided", "kinds"] as const;

test.describe("approvals for a manager", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds Approvals in the navigation and lists the requests waiting for them, the late one first and marked", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "manager");
        const other = approvalKind(testInfo, theme, "refused");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          asked.push(await askForApproval(asker.request, kind, { submittedWorkingDaysAgo: 5 }));
          asked.push(await askForApproval(asker.request, kind));
          const late = await readApprovalRequest(asker.api, asked[0] ?? "");
          const fresh = await readApprovalRequest(asker.api, asked[1] ?? "");
          expect(late.late, "the request asked for five working days ago").toBe(true);
          expect(fresh.late, "the request asked for today").toBe(false);
          const byDue = [late.id, fresh.id];
          const bySubmission = [fresh.id, late.id];

          await page.goto("/");
          await shell.waitUntilInteractive();
          await shell.openNavigation();
          await shell.navigationLink("Approvals").click();
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath);
          await expect(page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
          await expect(approvals.heading).toHaveText("Waiting for me");
          await expect(page).toHaveTitle("Waiting for me · ERP-AI-Pro");
          await approvals.waitUntilInteractive();
          await expect(approvals.pageLinks).toMatchAriaSnapshot(pageLinksSnapshot(ownPages));
          await expect(approvals.pageLink("all")).toHaveCount(0);
          await expect(approvals.pageLink("waiting")).toHaveAttribute("aria-current", "page");
          await expect(approvals.pageLinks.locator("[aria-current]")).toHaveCount(1);
          await expect(approvals.counts).toHaveText("2 requests wait for your decision, 1 of them late.");
          await expect(approvals.listHeading).toHaveText("Requests waiting for you");
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Due, earliest first.",
          );
          expect(await approvals.requestIds(), "the late request first").toEqual(byDue);
          const cards = await approvals.list.showsCards();
          if (!cards)
            await expect(approvals.list.columnHeader("Due")).toHaveAttribute("aria-sort", "ascending");
          for (const shown of [late, fresh]) {
            await expect(await approvals.request(shown.id)).toMatchAriaSnapshot(
              requestRowSnapshot(shown, viewer, "waiting", cards),
            );
          }
          await capture("approvals-waiting", approvals.card);

          await approvals.list.sortBy("Asked on", "Asked on, latest first");
          await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "submittedAt:desc");
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Asked on, latest first.",
          );
          await expect.poll(() => approvals.requestIds()).toEqual(bySubmission);
          await approvals.list.sortBy("Due", "Due, latest first");
          await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "dueAt:desc");
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Due, latest first.",
          );
          await expect.poll(() => approvals.requestIds()).toEqual(bySubmission);
          await approvals.list.sortBy("Due", "Due, earliest first");
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath && url.search === "");
          await expect.poll(() => approvals.requestIds()).toEqual(byDue);

          await approvals.list.chooseOptions("Kind of request", [other.title]);
          await approvals.list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.searchParams.getAll("kind").join() === other.key);
          await expect(approvals.list.noMatches).toContainText("No requests match these filters.");
          await expect(approvals.list.status).toHaveText("No requests to show.");
          await capture("approvals-waiting-no-matches", approvals.card);
          await approvals.list.noMatches.getByRole("link", { name: "Clear filters" }).click();
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath && url.search === "");
          await expect(approvals.list.optionsFilter("Kind of request")).toBeFocused();
          await approvals.list.chooseOptions("Kind of request", [kind.title]);
          await approvals.list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.searchParams.getAll("kind").join() === kind.key);
          await expect(approvals.list.optionsFilter("Kind of request")).toHaveText(kind.title);
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Due, earliest first.",
          );
          await approvals.list.optionsFilter("Kind of request").click();
          await page.getByRole("menuitem", { name: "Clear choices" }).click();
          await expect(page.getByRole("menu")).toHaveCount(0);
          await approvals.list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath && url.search === "");

          await approvals.list.setColumnVisible("Step", false);
          await expect(page).toHaveURL((url) => url.searchParams.get("hide") === "step");
          if (!cards) await expect(approvals.list.columnHeader("Step")).toHaveCount(0);
          await expect(await approvals.request(late.id)).not.toContainText(waitingText(late, viewer));
          await approvals.list.setColumnVisible("Step", true);
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath && url.search === "");
          await expect(await approvals.request(late.id)).toContainText(waitingText(late, viewer));

          await approvals.list.choosePageSize(20);
          await expect(page).toHaveURL((url) => url.searchParams.get("size") === "20");
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Due, earliest first.",
          );
          await approvals.list.choosePageSize(10);
          await expect(page).toHaveURL((url) => url.pathname === approvalsPath && url.search === "");

          await (await approvals.requestLink(late.id)).click();
          await expect(page).toHaveURL((url) => url.pathname === approvalRequestPath(late.id));
          await expect(requestPage.heading).toHaveText(kind.title);
          await expect(page).toHaveTitle(`${kind.title} · ERP-AI-Pro`);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(late, viewer, ["approve", "return", "reject"]),
          );
          await expect(requestPage.withdraw).toHaveCount(0);
          await expect(requestPage.inPlace).toHaveCount(0);
          await expect(requestPage.details).toMatchAriaSnapshot(detailsSnapshot(late, viewer));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(late, viewer));
          await expect(requestPage.earlier).toHaveCount(0);
          await capture("approval-request-late");
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test("sends a request back only with what needs to change, and keeps the dialog open while it is sent", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "manager");
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          const id = await askForApproval(asker.request, kind);
          asked.push(id);
          const sent = watchRequests(page, (candidate) => isRequestAction(candidate, id));

          await requestPage.goto(id);
          await requestPage.waitUntilInteractive();
          await requestPage.sendBack.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot(
              "return",
              `Sending it back closes the request. ${asker.person.name} can change the record and ask again.`,
            ),
          );
          await expect(requestPage.comment).toBeFocused();
          await capture("approval-send-back", requestPage.dialog);

          await requestPage.cancel.click();
          await requestPage.expectDialogClosed();
          await expect(requestPage.sendBack).toBeFocused();
          await requestPage.sendBack.click();
          await expect(requestPage.dialog).toBeVisible();
          await page.keyboard.press("Escape");
          await requestPage.expectDialogClosed();
          await expect(requestPage.sendBack).toBeFocused();
          await requestPage.sendBack.press("Enter");
          await expect(requestPage.dialog).toBeVisible();
          await requestPage.close.click();
          await requestPage.expectDialogClosed();
          await expect(requestPage.sendBack).toBeFocused();

          await requestPage.sendBack.click();
          await requestPage.submit.click();
          await expect(requestPage.comment).toBeFocused();
          await expect(requestPage.comment).toHaveAttribute("aria-invalid", "true");
          await expect(requestPage.fieldOf(requestPage.comment)).toContainText("Say what needs to change.");
          expect(sent.count(), "decisions sent").toBe(0);
          await capture("approval-send-back-refused", requestPage.dialog);

          await requestPage.comment.fill("x".repeat(1001));
          await requestPage.submit.click();
          await expect(requestPage.comment).toBeFocused();
          await expect(requestPage.fieldOf(requestPage.comment)).toContainText(
            "Use 1,000 characters or fewer.",
          );
          expect(sent.count(), "decisions sent").toBe(0);

          const reason = "Attach the receipt for the taxi fare.";
          await requestPage.comment.fill(reason);
          await expect(requestPage.comment).not.toHaveAttribute("aria-invalid", "true");
          const release = await holdServerFunctionCalls(page, approvalRequestPath(id));
          await requestPage.submit.click();
          await expect(requestPage.submit).toHaveAttribute("aria-busy", "true");
          await triesToCloseWhileSaving(page, requestPage.dialog, [requestPage.close, requestPage.cancel]);
          release();

          await requestPage.expectDialogClosed();
          await expect(shell.toast("You sent the request back.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          await expect(requestPage.actions).toHaveCount(0);
          expect(sent.count(), "decisions sent").toBe(1);
          const returned = await readApprovalRequest(signedInApi(page.context()), id);
          expect(returned.state, "the request").toBe("returned");
          expect(returned.steps[0], "its step").toMatchObject({
            state: "returned",
            decidedById: viewer,
            comment: reason,
          });
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(returned, viewer, []));
          await expect(requestPage.details).toMatchAriaSnapshot(detailsSnapshot(returned, viewer));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(returned, viewer));
          await capture("approval-sent-back");
          expect(
            (await readApprovalRequest(asker.api, id)).steps[0]?.comment,
            "what the person who asked reads",
          ).toBe(reason);
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test("approves with an optional note, rejects only with the reason why, and lists both as decided", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "manager");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(page.context());
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          asked.push(await askForApproval(asker.request, kind));
          asked.push(await askForApproval(asker.request, kind));
          const [approvedId, rejectedId] = [asked[0] ?? "", asked[1] ?? ""];

          await requestPage.goto(approvedId);
          await requestPage.waitUntilInteractive();
          await requestPage.approve.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot("approve", "Approving the Manager step approves the request."),
          );
          await capture("approval-approve", requestPage.dialog);
          const note = "Fine to pay from the travel budget.";
          await requestPage.comment.fill(note);
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You approved the request.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          const approved = await readApprovalRequest(own, approvedId);
          expect(approved.steps[0], "the approved step").toMatchObject({ state: "approved", comment: note });
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(approved, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(approved, viewer));
          await capture("approval-approved");

          await requestPage.goto(rejectedId);
          await requestPage.waitUntilInteractive();
          await requestPage.reject.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot("reject", "Rejecting it closes the request without approving it."),
          );
          await requestPage.submit.click();
          await expect(requestPage.comment).toBeFocused();
          await expect(requestPage.fieldOf(requestPage.comment)).toContainText("Say why it is rejected.");
          await capture("approval-reject-refused", requestPage.dialog);
          const reason = "This trip was not agreed in advance.";
          await requestPage.comment.fill(reason);
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You rejected the request.")).toBeVisible();
          const rejected = await readApprovalRequest(own, rejectedId);
          expect(rejected.steps[0], "the rejected step").toMatchObject({
            state: "rejected",
            comment: reason,
          });
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(rejected, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(rejected, viewer));

          await approvals.goto("decided");
          await approvals.waitUntilInteractive();
          await expect(approvals.pageLink("decided")).toHaveAttribute("aria-current", "page");
          await expect(page).toHaveTitle("Decided by me · ERP-AI-Pro");
          await expect(approvals.counts).toHaveCount(0);
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Asked on, latest first.",
          );
          expect(await approvals.requestIds()).toEqual([rejectedId, approvedId]);
          const cards = await approvals.list.showsCards();
          for (const shown of [rejected, approved]) {
            await expect(await approvals.request(shown.id)).toMatchAriaSnapshot(
              requestRowSnapshot(shown, viewer, "answered", cards),
            );
          }
          await capture("approvals-decided", approvals.card);

          await approvals.list.chooseOptions("Status", ["Approved"]);
          await approvals.list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.searchParams.getAll("state").join() === "approved");
          await expect(approvals.list.status).toHaveText("Showing 1 request.");
          await expect.poll(() => approvals.requestIds()).toEqual([approvedId]);
          await approvals.list.filters.getByRole("link", { name: "Clear filters" }).click();
          await expect(page).toHaveURL(
            (url) => url.pathname === approvalPagePaths.decided && url.search === "",
          );
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Asked on, latest first.",
          );
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test.describe("once the request is withdrawn while its page is open", () => {
        test.use({ expectedConsoleError: /the server responded with a status of 404/ });

        test("is told the request is gone, and the page then answers not found", async ({
          page,
          person,
          request,
          playwright,
          baseURL,
          capture,
        }, testInfo) => {
          const kind = approvalKind(testInfo, theme, "manager");
          const requestPage = new ApprovalRequestPage(page);
          const asker = await signInColleague(playwright.request, baseURL, "accountant");
          const asked: string[] = [];
          try {
            await reportTo(await signInAdministrator(request), asker.person, person);
            const id = await askForApproval(asker.request, kind);
            asked.push(id);

            await requestPage.goto(id);
            await requestPage.waitUntilInteractive();
            await withdrawApproval(asker.api, id, "Asked for the wrong trip.");
            await requestPage.approve.click();
            await requestPage.submit.click();
            await expect(requestPage.summary).toBeFocused();
            await expect(requestPage.summary).toContainText(
              "This request no longer exists, or you can no longer see it.",
            );
            await expect(requestPage.reload).toHaveCount(0);
            await expect(requestPage.dialog).toBeVisible();
            await capture("approval-request-gone", requestPage.dialog);
            await requestPage.close.click();
            await requestPage.expectDialogClosed();

            const response = await page.reload();
            expect(response?.status(), "the request page of a request the manager may no longer see").toBe(
              404,
            );
            await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
          } finally {
            await withdrawWaiting(asker.api, asked);
            await asker.request.dispose();
          }
        });
      });
    });
  }
});

test.describe("approvals for the person who asked", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("follows their requests, is offered only Withdraw on their own, withdraws one and asks again", async ({
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
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const manager = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), person, manager.person);
          asked.push(await askForApproval(context.request, kind));
          asked.push(await askForApproval(context.request, kind));
          const [decidedId, waitingId] = [asked[0] ?? "", asked[1] ?? ""];
          const note = "Approved for the client visit.";
          await decideApproval(manager.api, decidedId, "approve", note);
          const decided = await readApprovalRequest(own, decidedId);
          const waiting = await readApprovalRequest(own, waitingId);

          await approvals.goto("waiting");
          await expect(approvals.empty).toBeVisible();
          await approvals.pageLink("mine").click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.mine);
          await expect(approvals.heading).toHaveText("My requests");
          await expect(page).toHaveTitle("My requests · ERP-AI-Pro");
          await approvals.waitUntilInteractive();
          await expect(approvals.pageLink("mine")).toHaveAttribute("aria-current", "page");
          await expect(approvals.counts).toHaveText("1 of your requests still waits for a decision.");
          await expect(approvals.listHeading).toHaveText("Requests you asked for");
          await expect(approvals.list.status).toHaveText(
            "Showing all 2 requests. Sorted by Asked on, latest first.",
          );
          expect(await approvals.requestIds()).toEqual([waitingId, decidedId]);
          const cards = await approvals.list.showsCards();
          for (const shown of [waiting, decided]) {
            await expect(await approvals.request(shown.id)).toMatchAriaSnapshot(
              requestRowSnapshot(shown, viewer, "answered", cards),
            );
          }
          await capture("approvals-mine", approvals.card);

          await (await approvals.requestLink(decidedId)).click();
          await expect(requestPage.heading).toHaveText(kind.title);
          await expect(requestPage.actions).toHaveCount(0);
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(decided, viewer));

          await requestPage.goto(waitingId);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(waiting, viewer, ["withdraw"]));
          for (const decision of [requestPage.approve, requestPage.sendBack, requestPage.reject]) {
            await expect(decision, "a decision on their own request").toHaveCount(0);
          }
          await expect(requestPage.details).toMatchAriaSnapshot(detailsSnapshot(waiting, viewer));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(waiting, viewer));
          await capture("approval-request-own");

          await requestPage.withdraw.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot("withdraw", "Withdrawing it closes the request, and nobody decides it."),
          );
          await capture("approval-withdraw", requestPage.dialog);
          const reason = "Asked for the wrong trip.";
          await requestPage.comment.fill(reason);
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You withdrew the request.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          await expect(requestPage.actions).toHaveCount(0);
          const withdrawn = await readApprovalRequest(own, waitingId);
          expect(withdrawn.state, "the request").toBe("withdrawn");
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(withdrawn, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(withdrawn, viewer));
          await capture("approval-withdrawn");

          const againId = await askForApproval(context.request, kind, { subjectId: withdrawn.subjectId });
          asked.push(againId);
          await requestPage.goto(againId);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.earlier).toMatchAriaSnapshot(`
            - heading "Earlier requests for this record" [level=2]
            - text: The requests of this kind asked for this record before this one, the newest first.
            - list "Earlier requests for this record":
              - listitem:
                - text: ${JSON.stringify(`Withdrawn Asked on ${dateTimeText(withdrawn.submittedAt)} closed on ${dateTimeText(withdrawn.closedAt ?? "")}`)}
          `);
          await expect(requestPage.earlier.getByRole("link"), "an earlier request's page").toHaveCount(0);
          await capture("approval-earlier-requests", requestPage.earlier);
        } finally {
          await withdrawWaiting(own, asked);
          await manager.request.dispose();
        }
      });

      test("asks for approval without a manager, which then waits for the administrators", async ({
        page,
        person,
        context,
        capture,
      }, testInfo) => {
        const kind = approvalKind(testInfo, theme, "nomanager");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const asked: string[] = [];
        try {
          asked.push(await askForApproval(context.request, kind));
          const waiting = await readApprovalRequest(own, asked[0] ?? "");
          expect(waiting.steps[0], "the manager step").toMatchObject({
            approverKind: "role",
            approverRole: "administrator",
          });

          await approvals.goto("mine");
          await approvals.waitUntilInteractive();
          await expect(await approvals.request(waiting.id)).toMatchAriaSnapshot(
            requestRowSnapshot(waiting, viewer, "answered", await approvals.list.showsCards()),
          );
          await (await approvals.requestLink(waiting.id)).click();
          await expect(requestPage.heading).toHaveText(kind.title);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(waiting, viewer, ["withdraw"]));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(waiting, viewer));
          await capture("approval-request-no-manager");
        } finally {
          await withdrawWaiting(own, asked);
        }
      });
    });
  }
});

test.describe("approvals for an administrator", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds a request that waits for the administrators by its kind and approves it, but never their own", async ({
        page,
        person,
        context,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "nomanager");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        const ownAsked: string[] = [];
        try {
          asked.push(await askForApproval(asker.request, kind));
          ownAsked.push(await askForApproval(context.request, kind));
          const theirs = await readApprovalRequest(own, asked[0] ?? "");
          const mine = await readApprovalRequest(own, ownAsked[0] ?? "");

          await approvals.goto("waiting", `?kind=${encodeURIComponent(kind.key)}`);
          await approvals.waitUntilInteractive();
          await expect(approvals.pageLinks).toMatchAriaSnapshot(pageLinksSnapshot([...ownPages, "all"]));
          await expect(approvals.list.optionsFilter("Kind of request")).toHaveText(kind.title);
          const row = await approvals.request(theirs.id);
          await expect(row).toMatchAriaSnapshot(
            requestRowSnapshot(theirs, viewer, "waiting", await approvals.list.showsCards()),
          );
          await expect(await approvals.request(mine.id), "their own request").toHaveCount(0);
          await capture("approvals-waiting-for-administrators", row);

          await (await approvals.requestLink(theirs.id)).click();
          await expect(requestPage.heading).toHaveText(kind.title);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(theirs, viewer, ["approve", "return", "reject"]),
          );
          await expect(requestPage.inPlace, "the administrators' own step").toHaveCount(0);
          await requestPage.approve.click();
          await expect(requestPage.dialogInPlace).toHaveCount(0);
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You approved the request.")).toBeVisible();
          const approved = await readApprovalRequest(own, theirs.id);
          expect(approved.steps[0], "the step the administrators decided").toMatchObject({
            state: "approved",
            decidedById: viewer,
            inPlaceOfApprover: false,
          });
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(approved, viewer));

          await requestPage.goto(mine.id);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(mine, viewer, ["withdraw"]));
          await expect(requestPage.inPlace).toHaveCount(0);
          await capture("approval-request-own-administrator");
          const refused = await own.post(`${approvalRequestsApi}/${mine.id}/approve`, {
            data: { version: mine.version, comment: null },
          });
          expect(refused.status(), "an approval of their own request").toBe(403);
          expect(await refused.json()).toMatchObject({ code: "approval.own-request" });
        } finally {
          await withdrawWaiting(asker.api, asked);
          await withdrawWaiting(own, ownAsked);
          await asker.request.dispose();
        }
      });
    });
  }
});

test.describe("an approval request before the page is interactive", () => {
  test("keeps every decision button inert so no click is lost", async ({
    page,
    person,
    request,
    playwright,
    baseURL,
  }, testInfo) => {
    const kind = approvalKind(testInfo, "light", "manager");
    const requestPage = new ApprovalRequestPage(page);
    const asker = await signInColleague(playwright.request, baseURL, "accountant");
    const asked: string[] = [];
    try {
      await reportTo(await signInAdministrator(request), asker.person, person);
      asked.push(await askForApproval(asker.request, kind));
      await withholdClientBundles(page);

      await requestPage.goto(asked[0] ?? "");
      for (const button of [requestPage.approve, requestPage.sendBack, requestPage.reject]) {
        await expect(button).toBeDisabled();
        await expect(button).toHaveAttribute("data-hydrating", "");
      }
    } finally {
      await withdrawWaiting(asker.api, asked);
      await asker.request.dispose();
    }
  });
});

test.describe("approvals for an employee", () => {
  forEachTheme(
    "shows them their own pages and the kinds without every request, and nothing waits for them yet",
    async ({ page, capture }) => {
      const approvals = new ApprovalsPage(page);
      const kinds = new ApprovalKindsPage(page);
      const untouched = await readApprovalKind(signedInApi(page.context()), untouchedApprovalKind);

      await approvals.goto("waiting");
      await expect(approvals.pageLinks).toMatchAriaSnapshot(pageLinksSnapshot(ownPages));
      await expect(approvals.pageLink("all")).toHaveCount(0);
      await expect(approvals.counts).toHaveCount(0);
      await expect(approvals.empty).toMatchAriaSnapshot(`
        - text: Nothing waits for your decision. A request that waits for you, or for a role you hold, appears here.
      `);
      await capture("approvals-employee-waiting");

      await approvals.pageLink("mine").click();
      await expect(approvals.heading).toHaveText("My requests");
      await expect(approvals.counts).toHaveCount(0);
      await expect(approvals.empty).toContainText("You have not asked for approval yet.");
      await expect(approvals.empty).toContainText(
        "A request you ask for, or submit for someone else, appears here.",
      );
      await approvals.pageLink("decided").click();
      await expect(approvals.heading).toHaveText("Decided by me");
      await expect(approvals.empty).toContainText("You have not decided a request yet.");
      await expect(approvals.empty).toContainText("A request you approve, send back or reject appears here.");
      await capture("approvals-employee-decided");

      await page.goto(approvalPagePaths.all);
      await expect(page.getByRole("heading", { name: noAccess, level: 1 })).toBeVisible();
      await expect(page).toHaveTitle("No access · ERP-AI-Pro");
      await expect(approvals.card).toHaveCount(0);
      await capture("approvals-employee-all");

      await approvals.pageLink("kinds").click();
      await expect(kinds.heading).toHaveText("Kinds of request");
      await kinds.waitUntilInteractive();
      await expect(kinds.list.getByTestId("approval-kind-change"), "a change of a kind").toHaveCount(0);
      await expect(kinds.kind(untouched.title)).toMatchAriaSnapshot(kindSnapshot(untouched, false));
      await capture("approval-kind-employee", kinds.kind(untouched.title));
    },
  );

  test("the API refuses them every request and the kinds' settings", async ({ api }) => {
    const answers = [
      { request: "every request", response: await api.get(`${approvalRequestsApi}?box=all`) },
      {
        request: "a set-up of a kind",
        response: await api.post(approvalKindSettingsApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { kind: untouchedApprovalKind.key, required: false, workingDaysToDecide: 5 },
        }),
      },
      {
        request: "a change of a kind",
        response: await api.put(approvalKindSettingApi(randomUUID()), {
          data: { required: false, workingDaysToDecide: 5, version: "AAAAAAAAB9E=" },
        }),
      },
    ];

    for (const { request, response } of answers) {
      expect(response.status(), request).toBe(403);
      expect(await response.json(), request).toMatchObject({ code: "request.forbidden" });
    }
  });
});
