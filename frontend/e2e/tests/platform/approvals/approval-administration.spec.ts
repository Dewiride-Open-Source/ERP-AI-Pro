import {
  approvalKind,
  askForApproval,
  readApprovalRequest,
  signInColleague,
  withdrawApproval,
  withdrawWaiting,
} from "../../../fixtures/records/approval-records";
import { expect, signedInApi, test, themes } from "../../../fixtures/test";
import {
  ApprovalRequestPage,
  dialogSnapshot,
  headerSnapshot,
  inPlaceNotice,
  stepsSnapshot,
} from "../../../pages/platform/approvals/approval-request.page";
import {
  approvalPagePaths,
  approvalRequestPath,
  ApprovalsPage,
  dateTimeText,
  pageLinksSnapshot,
  requestRowSnapshot,
} from "../../../pages/platform/approvals/approvals.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// A request of a Finance kind waits for every holder of the Finance role and every request shows under All requests, so the
// tests take their project's and theme's kind, run one after another, and look only at the requests they asked for.
test.describe("approvals for an administrator", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.describe.configure({ mode: "default" });
      test.use({ theme });

      test("finds a request under All requests by its kind and decides it in place of Finance", async ({
        page,
        person,
        context,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "finance");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          asked.push(await askForApproval(asker.request, kind));
          const waiting = await readApprovalRequest(own, asked[0] ?? "");
          expect(waiting.decidesInPlace, "an administrator decides a Finance step in Finance's place").toBe(
            true,
          );

          await approvals.goto("waiting");
          await approvals.pageLink("all").click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.all);
          await expect(approvals.heading).toHaveText("All requests");
          await expect(page).toHaveTitle("All requests · ERP-AI-Pro");
          await approvals.waitUntilInteractive();
          await expect(approvals.pageLinks).toMatchAriaSnapshot(
            pageLinksSnapshot(["waiting", "mine", "decided", "kinds", "all"]),
          );
          await expect(approvals.pageLink("all")).toHaveAttribute("aria-current", "page");
          await expect(approvals.listHeading).toHaveText("Every request");
          await expect(approvals.counts).toHaveCount(0);

          await approvals.list.chooseOptions("Kind of request", [kind.title]);
          await approvals.list.chooseOptions("Status", ["Waiting"]);
          await approvals.list.applyFilters.click();
          await expect(page).toHaveURL(
            (url) =>
              url.searchParams.getAll("kind").join() === kind.key &&
              url.searchParams.getAll("state").join() === "submitted",
          );
          const row = await approvals.request(waiting.id);
          await expect(row).toMatchAriaSnapshot(
            requestRowSnapshot(waiting, viewer, "answered", await approvals.list.showsCards()),
          );
          await capture("approvals-all", approvals.card);

          await (await approvals.requestLink(waiting.id)).click();
          await expect(page).toHaveURL((url) => url.pathname === approvalRequestPath(waiting.id));
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(waiting, viewer, ["approve", "return", "reject"], "Finance"),
          );
          await expect(requestPage.inPlace).toHaveText(inPlaceNotice("Finance"));
          await capture("approval-request-in-place");

          await requestPage.approve.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot("approve", "Approving the Finance step approves the request.", "Finance"),
          );
          await capture("approval-approve-in-place", requestPage.dialog);
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You approved the request.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          await expect(requestPage.inPlace).toHaveCount(0);
          const approved = await readApprovalRequest(own, waiting.id);
          expect(approved.steps[0], "the step decided in Finance's place").toMatchObject({
            state: "approved",
            decidedById: viewer,
            inPlaceOfApprover: true,
          });
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(approved, viewer));

          const againId = await askForApproval(asker.request, kind, { subjectId: approved.subjectId });
          asked.push(againId);
          await requestPage.goto(againId);
          await requestPage.waitUntilInteractive();
          const earlierLink = requestPage.earlier.getByRole("link", {
            name: `Asked on ${dateTimeText(approved.submittedAt)}`,
            exact: true,
          });
          await expect(requestPage.earlierRequests).toHaveCount(1);
          await expect(requestPage.earlierRequests).toContainText("Approved");
          await expect(earlierLink).toHaveAttribute("href", approvalRequestPath(approved.id));
          await capture("approval-earlier-requests-linked", requestPage.earlier);
          await earlierLink.click();
          await expect(page).toHaveURL((url) => url.pathname === approvalRequestPath(approved.id));
          await expect(requestPage.statusLine).toContainText(
            `Approved on ${dateTimeText(approved.closedAt ?? "")}`,
          );
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test("is told a request no longer waits once its asker withdrew it, and shows its latest version", async ({
        page,
        person,
        context,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        const kind = approvalKind(testInfo, theme, "finance");
        const requestPage = new ApprovalRequestPage(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(context);
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          const id = await askForApproval(asker.request, kind);
          asked.push(id);

          await requestPage.goto(id);
          await requestPage.waitUntilInteractive();
          await withdrawApproval(asker.api, id, "Asked twice by mistake.");
          await requestPage.reject.click();
          await requestPage.comment.fill("The amount is wrong.");
          await requestPage.submit.click();
          await expect(requestPage.summary).toBeFocused();
          await expect(requestPage.summary).toContainText(
            "This request no longer waits for a decision: it was decided or withdrawn after the page was loaded. Show the latest version of the request.",
          );
          await capture("approval-request-not-waiting", requestPage.dialog);

          await requestPage.reload.click();
          await requestPage.expectDialogClosed();
          await expect(requestPage.actions).toHaveCount(0);
          await expect(requestPage.statusLine).toBeFocused();
          const withdrawn = await readApprovalRequest(own, id);
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(withdrawn, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(withdrawn, viewer));
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });
    });
  }
});
