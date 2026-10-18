import type { Page, Request } from "@playwright/test";
import { randomUUID } from "node:crypto";

import {
  deactivationOf,
  inviteLinkedPerson,
  setAccountState,
  takeAccountCheckTurn,
} from "../../../../fixtures/account-check";
import {
  deactivatePersonRecord,
  peopleApi,
  personRecord,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
  type PersonRecord,
} from "../../../../fixtures/person-records";
import { holdServerFunctionCalls } from "../../../../fixtures/server-functions";
import { expect, test, themes, type SignedInApi } from "../../../../fixtures/test";
import { AccountCheckDialog } from "../../../../pages/identity/users/account-check.page";
import { PeoplePage } from "../../../../pages/identity/users/people.page";
import { PersonPage, personPath } from "../../../../pages/identity/users/person.page";
import { TeamHandoverDialog } from "../../../../pages/identity/users/team-handover.page";
import { AppShell } from "../../../../pages/shared/layout/app-shell.page";

const joinedOn = "2026-04-01";

const byAnAdministrator = "Deactivated by an administrator.";

const byTheCheck = "Deactivated by the account check, which found their Microsoft account disabled.";

type Team<TManager extends PersonRecord> = {
  readonly prefix: string;
  readonly manager: TManager;
  readonly kiran: PersonRecord;
  readonly meera: PersonRecord;
  readonly successor: PersonRecord;
};

// A person who reports to someone keeps that person's record until they are gone, so the reports are removed first.
type Created = { readonly reports: string[]; readonly managers: string[] };

function nothingCreated(): Created {
  return { reports: [], managers: [] };
}

function removeCreated(api: SignedInApi, created: Created): Promise<void> {
  return removeRecords(api, { people: [...created.reports, ...created.managers] });
}

// Kiran and Meera report to the manager from the day they joined, and the successor, active and outside the team, is the
// one they may report to next. Every name starts with the test's own prefix, so a search for it finds only the test's people.
async function seedTeam<TManager extends PersonRecord>(
  api: SignedInApi,
  created: Created,
  feature: string,
  manager: (prefix: string) => Promise<TManager>,
): Promise<Team<TManager>> {
  const prefix = uniquePrefix(feature);
  const register = (name: string, slug: string) =>
    registerPersonRecord(api, {
      displayName: `${prefix} ${name}`,
      workEmail: `${prefix}-${slug}@example.com`,
      dateOfJoining: joinedOn,
    });
  const head = await manager(prefix);
  created.managers.push(head.id);
  const successor = await register("Sunita Rao", "successor");
  created.managers.push(successor.id);
  const kiran = await register("Kiran Das", "kiran");
  created.reports.push(kiran.id);
  const meera = await register("Meera Nair", "meera");
  created.reports.push(meera.id);
  for (const report of [kiran, meera]) {
    await recordMoveRecord(api, report.id, { effectiveFrom: joinedOn, managerId: head.id });
  }
  return { prefix, manager: head, kiran, meera, successor };
}

function registeredManager(api: SignedInApi): (prefix: string) => Promise<PersonRecord> {
  return (prefix) =>
    registerPersonRecord(api, {
      displayName: `${prefix} Ravi Menon`,
      workEmail: `${prefix}-ravi@example.com`,
      dateOfJoining: joinedOn,
    });
}

function deactivationDialog(page: Page, manager: PersonRecord): TeamHandoverDialog {
  return new TeamHandoverDialog(page, {
    title: `Deactivate ${manager.displayName}?`,
    cancel: "Keep them active",
  });
}

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

// The page's Server Function calls also search for the new manager, so a handover is told apart by the form it sends.
function isHandoverOf(path: string): (request: Request) => boolean {
  return (request) =>
    request.method() === "POST" &&
    new URL(request.url()).pathname === path &&
    request.headers()["next-action"] !== undefined &&
    (request.postData() ?? "").includes('"reportsTo"');
}

async function managerOfToday(api: SignedInApi, id: string): Promise<string | null> {
  return (await deactivationOf(api, id)).managerId;
}

// A dialog that starts to close turns its data-state to "closed" at once but stays on the page through its exit animation,
// so every attempt is judged by that state, never by visibility.
async function triesToCloseWhileSaving(page: Page, handover: TeamHandoverDialog): Promise<void> {
  await page.keyboard.press("Escape");
  await expect(handover.dialog).toHaveAttribute("data-state", "open");
  for (const closer of [handover.close, handover.cancel]) {
    await expect(closer).toHaveAttribute("aria-disabled", "true");
    await closer.focus();
    await page.keyboard.press("Enter");
    await expect(handover.dialog).toHaveAttribute("data-state", "open");
  }
}

test.describe("deactivating a manager and handing over their team", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("asks who the team reports to from today, refuses to go on without a choice and hands the team to the person chosen", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const created = nothingCreated();
        try {
          const { prefix, manager, kiran, meera, successor } = await seedTeam(
            api,
            created,
            "team-deactivate",
            registeredManager(api),
          );
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          const handover = deactivationDialog(page, manager);
          const handovers = watch(page, isHandoverOf(personPath(manager.id)));
          await personPage.goto(manager.id);
          await personPage.waitUntilInteractive();

          await personPage.statusAction.click();
          await expect(handover.dialog).toBeVisible();
          await expect(handover.team).toBeFocused();
          await expect(handover.dialog).toMatchAriaSnapshot(`
            - dialog "Deactivate ${manager.displayName}?":
              - heading "Deactivate ${manager.displayName}?" [level=2]
              - paragraph: ${manager.displayName} can no longer sign in, and any session they have open ends at their next step in the ERP. You can reactivate them later.
              - paragraph: 2 people report to ${manager.displayName} today or from a later date.
              - list "People who report to ${manager.displayName}":
                - listitem: ${kiran.displayName}
                - listitem: ${meera.displayName}
              - group "Who the team of ${manager.displayName} reports to from today":
                - text: Who the team of ${manager.displayName} reports to from today
                - radiogroup "Who the team of ${manager.displayName} reports to from today":
                  - radio "Another person"
                  - radio "No manager"
                  - paragraph: They report to no one until a move gives them a manager.
              - button "Keep them active"
              - button "Deactivate"
              - button "Close"
          `);
          await capture("team-handover-deactivate", handover.dialog);
          await handover.cancel.click();
          await expect(handover.dialog).toHaveCount(0);
          await expect(personPage.statusAction).toBeFocused();
          await personPage.statusAction.click();
          await page.keyboard.press("Escape");
          await expect(handover.dialog).toHaveCount(0);
          await expect(personPage.statusAction).toBeFocused();

          await personPage.statusAction.click();
          await handover.save.click();
          await expect(handover.anotherPerson).toBeFocused();
          await expect(handover.choice).toHaveAttribute("aria-invalid", "true");
          await expect(handover.fieldOf(handover.choice)).toContainText(
            "Choose who the team reports to from today.",
          );
          await capture("team-handover-refused-in-the-browser", handover.dialog);

          await handover.anotherPerson.check();
          await expect(handover.manager).toBeVisible();
          await handover.save.click();
          await expect(handover.manager).toBeFocused();
          await expect(handover.fieldOf(handover.manager)).toContainText(
            "Choose their new manager from the list.",
          );
          expect(handovers.count(), "handovers sent before the choice was complete").toBe(0);

          await handover.manager.fill(prefix);
          const options = page.getByRole("listbox").getByRole("option");
          await expect(options).toHaveCount(1);
          await expect(options).toContainText(successor.displayName);
          await personPage.choose(handover.manager, `${prefix} Sun`, successor.displayName);
          await capture("team-handover-new-manager", handover.dialog);

          const release = await holdServerFunctionCalls(page, personPath(manager.id));
          await handover.save.click();
          await expect(handover.save).toHaveAttribute("aria-busy", "true");
          await triesToCloseWhileSaving(page, handover);
          release();

          await expect(handover.dialog).toHaveCount(0);
          await expect(shell.toast(`Deactivated ${manager.displayName}.`)).toBeVisible();
          expect(handovers.count(), "handovers sent").toBe(1);
          await expect(personPage.status).toHaveText("Deactivated");
          await expect(personPage.deactivationReason).toHaveText(byAnAdministrator);
          await expect(personPage.statusAction).toHaveText("Reactivate");
          await expect(personPage.statusAction).toBeFocused();
          await expect(personPage.handOverTeam).toHaveCount(0);
          expect(await managerOfToday(api, kiran.id)).toBe(successor.id);
          expect(await managerOfToday(api, meera.id)).toBe(successor.id);
          expect(await deactivationOf(api, manager.id)).toMatchObject({
            status: "deactivated",
            deactivatedBecause: "administrator",
          });

          await personPage.goto(kiran.id);
          await expect(personPage.details.getByRole("link", { name: successor.displayName })).toBeVisible();
          await expect(personPage.needsNewManager).toHaveCount(0);
        } finally {
          await removeCreated(api, created);
        }
      });

      test("explains a new manager the API refuses beside the buttons and keeps the dialog and the team as they were", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const created = nothingCreated();
        try {
          const { prefix, manager, kiran, successor } = await seedTeam(
            api,
            created,
            "team-refused",
            registeredManager(api),
          );
          const personPage = new PersonPage(page);
          const handover = deactivationDialog(page, manager);
          await personPage.goto(manager.id);
          await personPage.waitUntilInteractive();
          await personPage.statusAction.click();
          await handover.anotherPerson.check();
          await personPage.choose(handover.manager, `${prefix} Sun`, successor.displayName);
          await deactivatePersonRecord(api, await personRecord(api, successor.id));

          await handover.save.click();
          await expect(handover.summary).toContainText(
            "That person's record is deactivated, so they cannot take the team; choose an active person.",
          );
          await expect(handover.summary).toContainText(/Reference: \S+/);
          await expect(handover.summary).toBeFocused();
          await expect(handover.manager).toHaveValue(successor.displayName);
          await capture("team-handover-refused", handover.dialog);
          expect((await personRecord(api, manager.id)).status).toBe("active");
          expect(await managerOfToday(api, kiran.id)).toBe(manager.id);
        } finally {
          await removeCreated(api, created);
        }
      });

      test("explains a team that appeared after the page was read, and asks who it reports to once the page is reloaded", async ({
        page,
        api,
        capture,
      }) => {
        const created = nothingCreated();
        try {
          const prefix = uniquePrefix("team-appeared");
          const manager = await registeredManager(api)(prefix);
          created.managers.push(manager.id);
          const report = await registerPersonRecord(api, {
            displayName: `${prefix} Kiran Das`,
            workEmail: `${prefix}-kiran@example.com`,
          });
          created.reports.push(report.id);
          const personPage = new PersonPage(page);
          await personPage.goto(manager.id);
          await personPage.waitUntilInteractive();
          await recordMoveRecord(api, report.id, { effectiveFrom: joinedOn, managerId: manager.id });

          await personPage.statusAction.click();
          await personPage
            .confirmation(`Deactivate ${manager.displayName}?`)
            .getByRole("button", { name: "Deactivate", exact: true })
            .click();
          await expect(personPage.statusError).toHaveRole("alert");
          await expect(personPage.statusError).toContainText(
            "People report to this person today or from a later date. Reload the page to choose who they report to, then deactivate them again.",
          );
          await expect(personPage.statusAction).toBeFocused();
          await capture("team-handover-needed", personPage.header);
          expect((await personRecord(api, manager.id)).status).toBe("active");

          await page.reload();
          await personPage.waitUntilInteractive();
          await personPage.statusAction.click();
          await expect(deactivationDialog(page, manager).members).toHaveText(report.displayName);
        } finally {
          await removeCreated(api, created);
        }
      });

      test("marks the team of a manager the check deactivated as needing a new manager until the team is handed over", async ({
        page,
        api,
        request,
        capture,
      }) => {
        test.slow();
        const created = nothingCreated();
        try {
          const { prefix, manager, kiran, meera, successor } = await seedTeam(
            api,
            created,
            "team-mark",
            (teamPrefix) =>
              inviteLinkedPerson(api, request, {
                displayName: `${teamPrefix} Ravi Menon`,
                userPrincipalName: `${teamPrefix}-ravi@example.com`,
              }),
          );
          const people = new PeoplePage(page);
          const check = new AccountCheckDialog(page);
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          const marked = `${manager.displayName} Needs a new manager`;
          await people.goto(`?name=${prefix}`);
          await people.waitUntilInteractive();

          const endTurn = await takeAccountCheckTurn(api);
          try {
            await setAccountState(request, manager, "disabled");
            await check.open();
            await check.run.click();
            await expect(check.status).toHaveText(
              /^Checked [\d,]+ people\. Deactivated 1 person whose Microsoft account is disabled or deleted\.$/,
            );
          } finally {
            await endTurn();
          }
          await expect(people.row(kiran.displayName)).toContainText(marked);
          await expect(people.row(meera.displayName)).toContainText(marked);

          await check.deactivatedPerson(manager.displayName).click();
          await expect(check.dialog).toHaveCount(0);
          await expect(page).toHaveURL((url) => url.pathname === personPath(manager.id));
          await expect(personPage.status).toHaveText("Deactivated");
          await expect(personPage.deactivationReason).toHaveText(byTheCheck);
          await personPage.waitUntilInteractive();
          await expect(personPage.actions).toMatchAriaSnapshot(`
            - link "Edit details"
            - button "Reactivate"
            - button "Hand over their team"
            - button "Delete"
          `);
          await capture("team-handover-deactivated-manager", personPage.header);

          await page.goBack();
          await expect(people.heading).toBeVisible();
          await people.waitUntilInteractive();
          await expect(people.row(kiran.displayName)).toContainText(marked);
          await capture("team-handover-people-marked", people.listCard);

          await people.nameLink(kiran.displayName).click();
          await expect(page).toHaveURL((url) => url.pathname === personPath(kiran.id));
          await expect(personPage.details).toMatchAriaSnapshot(`
            - term: Manager
            - definition:
              - link "${manager.displayName}":
                - /url: ${personPath(manager.id)}
              - text: Needs a new manager
          `);
          await capture("team-handover-member-marked", personPage.details);

          await personPage.goto(manager.id);
          await personPage.waitUntilInteractive();
          const handover = new TeamHandoverDialog(page, {
            title: `Hand over the team of ${manager.displayName}`,
            cancel: "Cancel",
          });
          await personPage.handOverTeam.click();
          await expect(handover.team).toBeFocused();
          await expect(handover.dialog).toMatchAriaSnapshot(`
            - dialog "Hand over the team of ${manager.displayName}":
              - heading "Hand over the team of ${manager.displayName}" [level=2]
              - paragraph: ${manager.displayName} is deactivated, so the people who report to them need someone else to report to.
              - paragraph: 2 people report to ${manager.displayName} today or from a later date.
              - list "People who report to ${manager.displayName}":
                - listitem: ${kiran.displayName}
                - listitem: ${meera.displayName}
              - group "Who the team of ${manager.displayName} reports to from today"
              - button "Cancel"
              - button "Hand over the team"
              - button "Close"
          `);
          await capture("team-handover-hand-over", handover.dialog);
          await handover.cancel.click();
          await expect(handover.dialog).toHaveCount(0);
          await expect(personPage.handOverTeam).toBeFocused();

          await personPage.handOverTeam.click();
          await handover.anotherPerson.check();
          await personPage.choose(handover.manager, `${prefix} Sun`, successor.displayName);
          await handover.save.click();

          await expect(handover.dialog).toHaveCount(0);
          await expect(shell.toast(`Handed over the team of ${manager.displayName}.`)).toBeVisible();
          await expect(personPage.handOverTeam).toHaveCount(0);
          await expect(personPage.statusAction).toBeFocused();
          await expect(personPage.deactivationReason).toHaveText(byTheCheck);
          expect(await deactivationOf(api, kiran.id)).toMatchObject({
            managerId: successor.id,
            managerActive: true,
          });

          await personPage.goto(kiran.id);
          await expect(personPage.details.getByRole("link", { name: successor.displayName })).toBeVisible();
          await expect(personPage.needsNewManager).toHaveCount(0);
          await people.goto(`?name=${prefix}`);
          await people.waitUntilInteractive();
          await expect(people.row(meera.displayName)).toContainText(successor.displayName);
          await expect(people.listCard.getByTestId("needs-new-manager")).toHaveCount(0);
        } finally {
          await removeCreated(api, created);
        }
      });
    });
  }

  test("hands the team of a manager over to no manager", async ({ page, api }) => {
    const created = nothingCreated();
    try {
      const { manager, kiran, meera } = await seedTeam(api, created, "team-nobody", registeredManager(api));
      const personPage = new PersonPage(page);
      const handover = deactivationDialog(page, manager);
      await personPage.goto(manager.id);
      await personPage.waitUntilInteractive();
      await personPage.statusAction.click();
      await handover.anotherPerson.check();
      await expect(handover.manager).toBeVisible();
      await handover.noManager.check();
      await expect(handover.manager).toHaveCount(0);
      await handover.save.click();

      await expect(handover.dialog).toHaveCount(0);
      await expect(personPage.status).toHaveText("Deactivated");
      expect(await managerOfToday(api, kiran.id)).toBeNull();
      expect(await managerOfToday(api, meera.id)).toBeNull();
      await personPage.goto(kiran.id);
      await expect(personPage.details).toMatchAriaSnapshot(`
        - term: Manager
        - definition: None
      `);
    } finally {
      await removeCreated(api, created);
    }
  });
});

test.describe("deactivating a manager for an employee", () => {
  test("the API refuses them a person's team and a deactivation that hands a team over", async ({ api }) => {
    const id = randomUUID();
    const refusals = [
      await api.get(`${peopleApi}/${id}/team`),
      await api.put(`${peopleApi}/${id}/status`, {
        data: { status: "deactivated", version: "AAAAAAAAB9E=", team: { managerId: null } },
      }),
    ];

    for (const refused of refusals) {
      expect(refused.status(), refused.url()).toBe(403);
      expect(await refused.json(), refused.url()).toMatchObject({ code: "request.forbidden" });
    }
  });
});
