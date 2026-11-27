import {
  approvalKind,
  askForApproval,
  readApprovalRequest,
  reportTo,
  signInAdministrator,
  signInColleague,
  withdrawWaiting,
} from "../../../fixtures/records/approval-records";
import { expect, signedInApi, test, themes } from "../../../fixtures/test";
import {
  ApprovalRequestPage,
  headerSnapshot,
  stepsSnapshot,
} from "../../../pages/platform/approvals/approval-request.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// The test host's refused kinds stand for a record its module will not approve as it stands; their module still accepts the
// request being sent back, rejected or withdrawn, so a request it refuses to approve never stays undecidable.
const refusal = "The test record cannot be approved as it stands; send it back or reject it instead.";

test.describe("an approval its record refuses", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows the record's refusal in the dialog, keeps the request waiting, and sends it back instead", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "refused");
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(page.context());
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          const id = await askForApproval(asker.request, kind);
          asked.push(id);
          const waiting = await readApprovalRequest(own, id);

          await requestPage.goto(id);
          await requestPage.waitUntilInteractive();
          await requestPage.approve.click();
          await requestPage.comment.fill("Looks fine to me.");
          await requestPage.submit.click();
          await expect(requestPage.summary).toBeFocused();
          await expect(requestPage.summary).toContainText(refusal);
          await expect(requestPage.summary).toContainText("Reference:");
          await expect(requestPage.reload).toHaveCount(0);
          await expect(requestPage.dialog).toBeVisible();
          await expect(requestPage.comment, "the note stays as it was typed").toHaveValue(
            "Looks fine to me.",
          );
          await capture("approval-refused", requestPage.dialog);

          const unchanged = await readApprovalRequest(own, id);
          expect(unchanged.state, "the request after the refusal").toBe("submitted");
          expect(unchanged.version, "the request's version after the refusal").toBe(waiting.version);
          await requestPage.cancel.click();
          await requestPage.expectDialogClosed();
          await expect(requestPage.approve).toBeFocused();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(unchanged, viewer, ["approve", "return", "reject"]),
          );

          await requestPage.sendBack.click();
          await requestPage.comment.fill("Correct the record, then ask again.");
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You sent the request back.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          const returned = await readApprovalRequest(own, id);
          expect(returned.state, "the request sent back").toBe("returned");
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(returned, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(returned, viewer));
          await capture("approval-refused-sent-back");
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });
    });
  }
});
