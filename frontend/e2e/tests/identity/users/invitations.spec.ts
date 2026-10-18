import type { APIRequestContext, Page, Request } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import { addDirectoryPerson } from "../../../fixtures/directory";
import {
  peopleApi,
  personRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
} from "../../../fixtures/person-records";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, test, themes, type SignedInApi } from "../../../fixtures/test";
import { InvitationDialog, isPeopleServerFunctionCall } from "../../../pages/identity/users/invitation.page";
import { PeoplePage, peoplePath } from "../../../pages/identity/users/people.page";
import { PersonPage, personPath } from "../../../pages/identity/users/person.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const personIdInPath = /^\/identity\/users\/([0-9a-f-]{36})$/;

const dialogDescription =
  "Find the person in your organisation's Microsoft Entra directory, then add their details. They can sign in once an administrator gives them an app role of the ERP in Microsoft Entra.";

type DirectoryEntry = {
  readonly objectId: string;
  readonly displayName: string;
  readonly signInName: string;
};

type DirectoryPeople = {
  readonly prefix: string;
  readonly asha: DirectoryEntry;
  readonly kiran: DirectoryEntry;
};

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

// Other workers add people to the same test directory, so the user principal name of every person a test adds starts with
// the test's own prefix, which its searches type: the directory matches a user principal name by its start.
async function seedDirectory(request: APIRequestContext, feature: string): Promise<DirectoryPeople> {
  const prefix = uniquePrefix(feature);
  const asha = {
    objectId: randomUUID(),
    displayName: `${prefix} Asha Rao`,
    signInName: `${prefix}-asha.rao@example.com`,
  };
  const kiran = {
    objectId: randomUUID(),
    displayName: `${prefix} Kiran Das`,
    signInName: `${prefix}-kiran.das@example.com`,
  };
  for (const person of [asha, kiran]) {
    await addDirectoryPerson(request, {
      objectId: person.objectId,
      displayName: person.displayName,
      userPrincipalName: person.signInName,
    });
  }
  return { prefix, asha, kiran };
}

async function recordsWithWorkEmail(api: SignedInApi, workEmail: string): Promise<readonly unknown[]> {
  const query = new URLSearchParams({ filter: `workEmail:eq:${encodeURIComponent(workEmail)}` });
  const response = await api.get(`${peopleApi}?${query.toString()}`);
  expect(response.status(), `the records with the work email ${workEmail}`).toBe(200);
  return ((await response.json()) as { items?: readonly unknown[] }).items ?? [];
}

test.describe("inviting a person from the directory", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps the invite button disabled so no click is lost", async ({ page }) => {
      const people = new PeoplePage(page);
      await withholdClientBundles(page);
      await page.goto(peoplePath);

      await expect(people.heading).toBeVisible();
      await expect(people.inviteButton).toBeDisabled();
      await expect(people.inviteButton).toHaveAttribute("data-hydrating", "");
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds people in the directory, invites one with their details and then marks them as already in the ERP", async ({
        page,
        api,
        request,
        capture,
      }) => {
        test.slow();
        const people = new PeoplePage(page);
        const invitation = new InvitationDialog(page);
        const personPage = new PersonPage(page);
        const shell = new AppShell(page);
        const { prefix, asha, kiran } = await seedDirectory(request, "invite");
        let invitedId: string | undefined;
        try {
          await people.goto();
          await invitation.open();

          await invitation.find(prefix, "2 people found.");
          await expect(invitation.results).toMatchAriaSnapshot(`
            - list "People in the directory":
              - listitem:
                - button "${asha.displayName} ${asha.signInName}"
              - listitem:
                - button "${kiran.displayName} ${kiran.signInName}"
          `);
          await capture("invitation-search", invitation.dialog);

          await invitation.choice(kiran.displayName).click();
          await expect(invitation.chosenName).toHaveText(kiran.displayName);
          await expect(invitation.chosenName).toBeFocused();
          await invitation.back.click();
          await expect(invitation.search).toBeFocused();
          await expect(invitation.search).toHaveValue(prefix);
          await expect(invitation.status).toHaveText("2 people found.");

          await page.keyboard.press("Tab");
          await expect(invitation.choice(asha.displayName)).toBeFocused();
          await page.keyboard.press("Enter");
          await expect(invitation.chosenName).toBeFocused();
          await expect(invitation.dialog).toMatchAriaSnapshot(`
            - dialog "Invite from the directory":
              - heading "Invite from the directory" [level=2]
              - paragraph: ${dialogDescription}
              - heading "${asha.displayName}" [level=3]
              - paragraph: ${asha.signInName}
              - paragraph: Their name and work email in the ERP come from the company directory.
              - group:
                - text: Employee code
                - textbox "Employee code"
                - paragraph: Optional. Letters, digits, hyphens, slashes and underscores.
              - group:
                - text: Phone number
                - textbox "Phone number"
                - paragraph: Optional. With + and the country code, or a 10-digit Indian mobile number.
              - group:
                - text: Date of joining
                - group:
                  - textbox "Date of joining":
                    - /placeholder: dd-mm-yyyy
                  - group:
                    - button "Choose date"
                - paragraph: Optional.
              - group "Roles in the ERP":
                - text: Roles in the ERP
                - paragraph: Optional. Everyone who signs in is an employee, and administrators are chosen in Microsoft Entra.
                - checkbox "Finance"
                - checkbox "HR"
                - checkbox "Manager"
              - button "Back to the search"
              - button "Invite"
              - button "Close"
          `);
          await capture("invitation-details", invitation.dialog);

          await invitation.employeeCode.fill(`${prefix}-I`);
          await invitation.phoneNumber.fill("98765 43210");
          await invitation.dateOfJoining.fill("01-04-2026");
          await invitation.role("Finance").check();
          await capture("invitation-details-filled", invitation.dialog);

          const release = await holdServerFunctionCalls(page, peoplePath);
          await invitation.invite.click();
          await expect(invitation.invite).toHaveAttribute("aria-busy", "true");
          await page.keyboard.press("Escape");
          await expect(invitation.dialog).toHaveAttribute("data-state", "open");
          for (const control of [invitation.close, invitation.back]) {
            await expect(control).toHaveAttribute("aria-disabled", "true");
            await control.focus();
            await page.keyboard.press("Enter");
            await expect(invitation.dialog).toHaveAttribute("data-state", "open");
          }
          await expect(invitation.chosenName).toHaveText(asha.displayName);
          release();

          await expect(shell.toast(`Invited ${asha.displayName}.`)).toBeVisible();
          await expect(page).toHaveURL((url) => personIdInPath.test(url.pathname));
          invitedId = personIdInPath.exec(new URL(page.url()).pathname)?.[1];
          await expect(personPage.heading).toHaveText(asha.displayName);
          await expect(page).toHaveTitle(`${asha.displayName} · ERP-AI-Pro`);
          await expect(personPage.header).toContainText(asha.signInName);
          await expect(personPage.status).toHaveText("Active");
          await expect(personPage.details).toContainText(`${prefix}-I`);
          await expect(personPage.details).toContainText("+919876543210");
          await expect(personPage.details).toContainText("01-04-2026");
          await expect(personPage.details).toContainText("Not signed in yet");
          await expect(personPage.roleList).toMatchAriaSnapshot(`
            - list "Roles":
              - listitem: Finance
          `);
          expect(await personRecord(api, invitedId ?? "")).toMatchObject({
            displayName: asha.displayName,
            workEmail: asha.signInName,
            employeeCode: `${prefix}-I`,
            phoneNumber: "+919876543210",
            dateOfJoining: "2026-04-01",
            roles: ["finance"],
            status: "active",
          });

          await page.goBack();
          await expect(people.heading).toBeVisible();
          await invitation.open();
          await invitation.find(prefix, "2 people found.");
          await expect(invitation.results).toMatchAriaSnapshot(`
            - list "People in the directory":
              - listitem:
                - link "${asha.displayName}":
                  - /url: ${personPath(invitedId ?? "")}
                - text: ${asha.signInName} Already in the ERP
              - listitem:
                - button "${kiran.displayName} ${kiran.signInName}"
          `);
          await capture("invitation-already-in-the-erp", invitation.dialog);

          await invitation.registeredLink(asha.displayName).click();
          await expect(invitation.dialog).toHaveCount(0);
          await expect(page).toHaveURL((url) => url.pathname === personPath(invitedId ?? ""));
          await expect(personPage.heading).toHaveText(asha.displayName);
        } finally {
          await removeRecords(api, { people: invitedId === undefined ? [] : [invitedId] });
        }
      });

      test("opens and closes the dialog and checks the search and the details in the browser before anything is sent", async ({
        page,
        request,
        capture,
      }) => {
        const people = new PeoplePage(page);
        const invitation = new InvitationDialog(page);
        const { prefix, asha } = await seedDirectory(request, "invite-checks");
        const sent = watch(page, isPeopleServerFunctionCall);
        await people.goto();

        await invitation.open();
        await expect(invitation.dialog).toMatchAriaSnapshot(`
          - dialog "Invite from the directory":
            - heading "Invite from the directory" [level=2]
            - paragraph: ${dialogDescription}
            - group:
              - text: Search the company directory
              - searchbox "Search the company directory"
              - paragraph: At least 2 characters of their name or email address.
            - status
            - button "Cancel"
            - button "Close"
        `);
        await capture("invitation", invitation.dialog);
        await invitation.cancel.click();
        await expect(invitation.dialog).toHaveCount(0);
        await expect(invitation.trigger).toBeFocused();
        await invitation.open();
        await page.keyboard.press("Escape");
        await expect(invitation.dialog).toHaveCount(0);
        await expect(invitation.trigger).toBeFocused();
        await invitation.open();
        await invitation.close.click();
        await expect(invitation.dialog).toHaveCount(0);
        await expect(invitation.trigger).toBeFocused();

        await invitation.open();
        await invitation.search.fill("a");
        await expect(invitation.status).toHaveText("Type at least 2 characters to search.");
        await expect(invitation.results).toHaveCount(0);
        await invitation.search.fill(`${prefix}&`);
        await expect(invitation.search).toHaveAttribute("aria-invalid", "true");
        await expect(invitation.fieldOf(invitation.search)).toContainText(
          "Leave out the & sign: the company directory cannot search for it.",
        );
        await expect(invitation.status).toHaveText("");
        await capture("invitation-search-refused", invitation.dialog);
        expect(sent.count(), "searches of text the API would refuse").toBe(0);

        await invitation.find(prefix, "2 people found.");
        await expect(invitation.search).not.toHaveAttribute("aria-invalid");
        await invitation.choice(asha.displayName).click();
        const searches = sent.count();
        await invitation.employeeCode.fill("-0042");
        await invitation.phoneNumber.fill("12345");
        await invitation.dateOfJoining.fill("31-02-2026");
        await invitation.invite.click();

        await expect(invitation.employeeCode).toBeFocused();
        await expect(invitation.employeeCode).toHaveAttribute("aria-invalid", "true");
        await expect(invitation.fieldOf(invitation.employeeCode)).toContainText(
          "Use letters, digits, hyphens, slashes and underscores, starting with a letter or a digit.",
        );
        await expect(invitation.fieldOf(invitation.phoneNumber)).toContainText(
          "Enter a phone number with + and the country code, such as +44 20 7946 0958, or a 10-digit Indian mobile number.",
        );
        await expect(invitation.fieldOf(invitation.dateOfJoining)).toContainText(
          "Enter a real date as day-month-year, for example 31-03-2026.",
        );
        await expect(invitation.chosenName).toHaveText(asha.displayName);
        await capture("invitation-refused-in-the-browser", invitation.dialog);
        expect(sent.count(), "invitations sent").toBe(searches);
      });

      test("explains an employee code another person has, keeps the details and invites no one", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const people = new PeoplePage(page);
        const invitation = new InvitationDialog(page);
        const { prefix, asha } = await seedDirectory(request, "invite-taken");
        const holder = await registerPersonRecord(api, {
          displayName: `${prefix} Holder`,
          workEmail: `${prefix}-holder@example.com`,
          employeeCode: `${prefix}-T`,
        });
        try {
          await people.goto();
          await invitation.open();
          await invitation.find(prefix, "2 people found.");
          await invitation.choice(asha.displayName).click();
          await invitation.employeeCode.fill(`${prefix}-T`);
          await invitation.invite.click();

          await expect(invitation.summary).toContainText("Another person already has this employee code.");
          await expect(invitation.summary).toContainText(/Reference: \S+/);
          await expect(invitation.summary).toBeFocused();
          await expect(invitation.employeeCode).toHaveValue(`${prefix}-T`);
          await expect(invitation.chosenName).toHaveText(asha.displayName);
          await expect(page).toHaveURL((url) => url.pathname === peoplePath);
          await capture("invitation-refused", invitation.dialog);
          expect(
            await recordsWithWorkEmail(api, asha.signInName),
            "records of the refused invitation",
          ).toEqual([]);
        } finally {
          await removeRecords(api, { people: [holder.id] });
        }
      });

      test.describe("when the search does not reach the web server", () => {
        // Chromium logs the provoked failure; WebKit and Firefox do not.
        test.use({ expectedConsoleError: /^Failed to load resource: net::ERR_CONNECTION_RESET$/ });

        test("says the service did not respond and finds the people once it answers again", async ({
          page,
          request,
          capture,
        }) => {
          const people = new PeoplePage(page);
          const invitation = new InvitationDialog(page);
          const { prefix } = await seedDirectory(request, "invite-failed");
          await people.goto();
          await invitation.open();
          const pattern = `**${peoplePath}`;

          await page.route(pattern, (route) =>
            isPeopleServerFunctionCall(route.request()) ? route.abort("connectionreset") : route.fallback(),
          );
          await invitation.search.fill(prefix);
          await expect(invitation.failure).toContainText("The ERP service did not respond. Try again.");
          await expect(invitation.status).toHaveText("");
          await expect(invitation.results).toHaveCount(0);
          await capture("invitation-search-failed", invitation.dialog);
          await page.unroute(pattern);

          await invitation.find(`${prefix} `, "2 people found.");
          await expect(invitation.failure).toHaveCount(0);
        });
      });
    });
  }
});

test.describe("inviting a person from the directory for an employee", () => {
  test("shows them no invite button on the page they have no access to", async ({ page }) => {
    const people = new PeoplePage(page);
    await page.goto(peoplePath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(people.inviteButton).toHaveCount(0);
  });

  test("the API refuses them the directory search and the invitation", async ({ api }) => {
    const search = await api.get(`${peopleApi}/directory?search=as`);
    expect(search.status()).toBe(403);
    expect(await search.json()).toMatchObject({ code: "request.forbidden" });

    const invitation = await api.post(`${peopleApi}/invitations`, {
      headers: { "Idempotency-Key": randomUUID() },
      data: {
        entraObjectId: randomUUID(),
        employeeCode: null,
        phoneNumber: null,
        dateOfJoining: null,
        roles: [],
      },
    });
    expect(invitation.status()).toBe(403);
    expect(await invitation.json()).toMatchObject({ code: "request.forbidden" });
  });
});
