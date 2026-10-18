import type { APIRequestContext } from "@playwright/test";

import {
  accountCheckApi,
  deactivationOf,
  inviteLinkedPerson,
  setAccountState,
  takeAccountCheckTurn,
  type LinkedPerson,
} from "../../../../fixtures/account-check";
import { withholdClientBundles } from "../../../../fixtures/client-bundles";
import { setApplicationAccess } from "../../../../fixtures/directory";
import { removeRecords, uniquePrefix } from "../../../../fixtures/person-records";
import { holdServerFunctionCalls } from "../../../../fixtures/server-functions";
import { expect, test, themes, type SignedInApi } from "../../../../fixtures/test";
import { AccountCheckDialog } from "../../../../pages/identity/users/account-check.page";
import { PeoplePage, peoplePath } from "../../../../pages/identity/users/people.page";
import { PersonPage, personPath } from "../../../../pages/identity/users/person.page";

const dialogDescription =
  "The ERP asks Microsoft about everyone in People who is linked to a Microsoft account, and deactivates each person whose account is disabled or deleted. Any page they have open ends at their next click. You can reactivate a person from their page.";

const accessDenied =
  "Microsoft has not granted the ERP permission to read accounts, so no one was checked. Whoever looks after your organisation's Microsoft Entra needs to grant it.";

const refusedWithAReference = new RegExp(`^${accessDenied.replaceAll(".", "\\.")} Reference: \\S+$`);

type LinkedPeople = {
  readonly prefix: string;
  readonly asha: LinkedPerson;
  readonly kiran: LinkedPerson;
  readonly meera: LinkedPerson;
};

// Other workers add people at the same time, so every name starts with the test's own prefix, which the People list is
// filtered by, and every person is linked to an account only the test's directory entry describes.
async function invitePeople(
  api: SignedInApi,
  request: APIRequestContext,
  created: string[],
): Promise<LinkedPeople> {
  const prefix = uniquePrefix("account-check");
  const invite = async (name: string, slug: string) => {
    const person = await inviteLinkedPerson(api, request, {
      displayName: `${prefix} ${name}`,
      userPrincipalName: `${prefix}-${slug}@example.com`,
    });
    created.push(person.id);
    return person;
  };
  return {
    prefix,
    asha: await invite("Asha Rao", "asha"),
    kiran: await invite("Kiran Das", "kiran"),
    meera: await invite("Meera Nair", "meera"),
  };
}

test.describe("checking the Microsoft accounts", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps the check button disabled so no click is lost", async ({ page }) => {
      const check = new AccountCheckDialog(page);
      await withholdClientBundles(page);
      await page.goto(peoplePath);

      await expect(new PeoplePage(page).heading).toBeVisible();
      await expect(check.trigger).toBeDisabled();
      await expect(check.trigger).toHaveAttribute("data-hydrating", "");
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("deactivates the people whose Microsoft account is disabled or deleted, lists them and says why on their pages", async ({
        page,
        api,
        request,
        capture,
      }) => {
        test.slow();
        const created: string[] = [];
        try {
          const { prefix, asha, kiran, meera } = await invitePeople(api, request, created);
          const people = new PeoplePage(page);
          const check = new AccountCheckDialog(page);
          const personPage = new PersonPage(page);
          await people.goto(`?name=${prefix}`);
          await people.waitUntilInteractive();

          await check.open();
          await expect(check.dialog).toMatchAriaSnapshot(`
            - dialog "Check Microsoft accounts":
              - heading "Check Microsoft accounts" [level=2]
              - paragraph: ${dialogDescription}
              - status
              - button "Cancel"
              - button "Check"
              - button "Close"
          `);
          await capture("account-check", check.dialog);
          await check.cancel.click();
          await expect(check.dialog).toHaveCount(0);
          await expect(check.trigger).toBeFocused();
          await check.open();
          await page.keyboard.press("Escape");
          await expect(check.dialog).toHaveCount(0);
          await expect(check.trigger).toBeFocused();
          await check.open();
          await check.close.click();
          await expect(check.dialog).toHaveCount(0);
          await expect(check.trigger).toBeFocused();

          const endTurn = await takeAccountCheckTurn(api);
          try {
            await setAccountState(request, kiran, "disabled");
            await setAccountState(request, meera, "deleted");
            await check.open();
            const release = await holdServerFunctionCalls(page, peoplePath);
            await check.run.click();
            await expect(check.run).toHaveAttribute("aria-busy", "true");
            await expect(check.status).toHaveText("Asking Microsoft about the accounts…");
            await page.keyboard.press("Escape");
            await expect(check.dialog).toHaveAttribute("data-state", "open");
            for (const closer of [check.close, check.cancel]) {
              await expect(closer).toHaveAttribute("aria-disabled", "true");
              await closer.focus();
              await page.keyboard.press("Enter");
              await expect(check.dialog).toHaveAttribute("data-state", "open");
            }
            await capture("account-check-running", check.dialog);
            release();

            await expect(check.status).toHaveText(
              /^Checked [\d,]+ people\. Deactivated 2 people whose Microsoft account is disabled or deleted\.$/,
            );
          } finally {
            await endTurn();
          }
          await expect(check.status).toBeFocused();
          await expect(check.deactivated).toMatchAriaSnapshot(`
            - list "People the check deactivated":
              - listitem:
                - link "${kiran.displayName}":
                  - /url: ${personPath(kiran.id)}
                - text: Microsoft account disabled
              - listitem:
                - link "${meera.displayName}":
                  - /url: ${personPath(meera.id)}
                - text: Microsoft account deleted
          `);
          await capture("account-check-result", check.dialog);
          expect(await deactivationOf(api, kiran.id)).toMatchObject({
            status: "deactivated",
            deactivatedBecause: "accountDisabled",
          });
          expect(await deactivationOf(api, meera.id)).toMatchObject({
            status: "deactivated",
            deactivatedBecause: "accountDeleted",
          });
          expect(await deactivationOf(api, asha.id)).toMatchObject({ status: "active" });

          await check.done.click();
          await expect(check.dialog).toHaveCount(0);
          await expect(check.trigger).toBeFocused();
          await expect(people.row(kiran.displayName)).toContainText("Deactivated");
          await expect(people.row(meera.displayName)).toContainText("Deactivated");
          await expect(people.row(asha.displayName)).toContainText("Active");

          await people.nameLink(kiran.displayName).click();
          await expect(page).toHaveURL((url) => url.pathname === personPath(kiran.id));
          await expect(personPage.status).toHaveText("Deactivated");
          await expect(personPage.deactivationReason).toHaveText(
            "Deactivated by the account check, which found their Microsoft account disabled.",
          );
          await personPage.waitUntilInteractive();
          await capture("account-check-deactivated-person", personPage.header);
          await personPage.statusAction.click();
          await expect(personPage.confirmation(`Reactivate ${kiran.displayName}?`)).toContainText(
            `${kiran.displayName} can sign in again once their Microsoft account is enabled. While it stays disabled, the next check of Microsoft accounts deactivates them again.`,
          );
          await page.keyboard.press("Escape");
          await expect(personPage.statusAction).toBeFocused();

          await personPage.goto(meera.id);
          await expect(personPage.deactivationReason).toHaveText(
            "Deactivated by the account check, which found their Microsoft account deleted.",
          );
          await personPage.goto(asha.id);
          await expect(personPage.status).toHaveText("Active");
          await expect(personPage.deactivationReason).toHaveCount(0);

          await people.goto(`?name=${prefix}`);
          await people.waitUntilInteractive();
          const secondTurnEnds = await takeAccountCheckTurn(api);
          try {
            await check.open();
            await check.run.click();
            await expect(check.status).toHaveText(/^Checked [\d,]+ people\. Every account is active\.$/);
          } finally {
            await secondTurnEnds();
          }
          await expect(check.status).toBeFocused();
          await expect(check.deactivated).toHaveCount(0);
          await capture("account-check-every-account-active", check.dialog);
        } finally {
          await removeRecords(api, { people: created });
        }
      });

      test("explains a check Microsoft refused, beside the buttons, and announces it again on every try", async ({
        page,
        api,
        request,
        capture,
      }) => {
        test.slow();
        const check = new AccountCheckDialog(page);
        const people = new PeoplePage(page);
        const endTurn = await takeAccountCheckTurn(api);
        await setApplicationAccess(request, { refused: true });
        try {
          await people.goto();
          await people.waitUntilInteractive();
          await check.open();
          await check.run.focus();
          await page.keyboard.press("Enter");

          await expect(check.error).toHaveRole("alert");
          await expect(check.error).toHaveText(refusedWithAReference);
          await expect(check.run).toBeFocused();
          await expect(check.status).toHaveText("");
          await expect(check.dialog).toMatchAriaSnapshot(`
            - dialog "Check Microsoft accounts":
              - heading "Check Microsoft accounts" [level=2]
              - paragraph: ${dialogDescription}
              - status
              - alert: /^Microsoft has not granted the ERP permission to read accounts.+ Reference:\\s\\S+$/
              - button "Cancel"
              - button "Check"
              - button "Close"
          `);
          await capture("account-check-refused", check.dialog);

          const release = await holdServerFunctionCalls(page, peoplePath);
          await page.keyboard.press("Enter");
          await expect(check.error).toHaveCount(0);
          release();
          await expect(check.error).toHaveText(refusedWithAReference);
          await expect(check.run).toBeFocused();
        } finally {
          await setApplicationAccess(request, { refused: false });
          await endTurn();
        }
      });
    });
  }
});

test.describe("checking the Microsoft accounts for an employee", () => {
  test("shows them no check button on the page they have no access to", async ({ page }) => {
    const check = new AccountCheckDialog(page);
    await page.goto(peoplePath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(check.trigger).toHaveCount(0);
  });

  test("the API refuses them the check", async ({ api }) => {
    const refused = await api.post(accountCheckApi);

    expect(refused.status()).toBe(403);
    expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
  });
});
