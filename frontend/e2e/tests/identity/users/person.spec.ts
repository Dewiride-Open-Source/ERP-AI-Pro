import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  createDepartmentRecord,
  createDesignationRecord,
  departmentsApi,
  movesOf,
  ownPersonRecordId,
  peopleApi,
  personRecord,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
  type NamedRecord,
  type PersonRecord,
} from "../../../fixtures/person-records";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, forEachTheme, test, themes, type SignedInApi } from "../../../fixtures/test";
import { EditPersonPage, editPersonPath } from "../../../pages/identity/users/edit-person.page";
import { peoplePath } from "../../../pages/identity/users/people.page";
import { PersonPage, personPath, typedDate } from "../../../pages/identity/users/person.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const joinedOn = "2026-04-01";

const movedOn = "2026-06-01";

const changedMeanwhile =
  "Someone else changed this record after the page was loaded. Reload the page, then try again.";

type Organisation = {
  readonly prefix: string;
  readonly department: NamedRecord;
  readonly designation: NamedRecord;
  readonly manager: PersonRecord;
  readonly person: PersonRecord;
};

// The person reports to the manager from the day they joined, in a department and with a designation of the test's own,
// so the test's assertions meet no other worker's records.
async function seedOrganisation(api: SignedInApi, feature: string): Promise<Organisation> {
  const prefix = uniquePrefix(feature);
  const department = await createDepartmentRecord(api, `${prefix} Accounts`);
  const designation = await createDesignationRecord(api, `${prefix} Analyst`);
  const manager = await registerPersonRecord(api, {
    displayName: `${prefix} Manager`,
    workEmail: `${prefix}-manager@example.com`,
    employeeCode: `${prefix}-M`,
  });
  const person = await registerPersonRecord(api, {
    displayName: `${prefix} Person`,
    workEmail: `${prefix}-person@example.com`,
    employeeCode: `${prefix}-P`,
    phoneNumber: "98765 43210",
    dateOfJoining: joinedOn,
  });
  await recordMoveRecord(api, person.id, {
    effectiveFrom: joinedOn,
    departmentId: department.id,
    designationId: designation.id,
    managerId: manager.id,
  });
  return { prefix, department, designation, manager, person };
}

function removeOrganisation(api: SignedInApi, organisation: Organisation): Promise<void> {
  return removeRecords(api, {
    people: [organisation.person.id, organisation.manager.id],
    departments: [organisation.department.id],
    designations: [organisation.designation.id],
  });
}

function isoDateAfter(days: number): string {
  const day = new Date();
  day.setUTCDate(day.getUTCDate() + days);
  return day.toISOString().slice(0, 10);
}

function dayBefore(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - 1);
  return day.toISOString().slice(0, 10);
}

test.describe("person page", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps every button that opens a dialog disabled so no click is lost", async ({ page, api }) => {
      const organisation = await seedOrganisation(api, "person-early");
      try {
        const personPage = new PersonPage(page);
        await withholdClientBundles(page);
        await page.goto(personPath(organisation.person.id));

        await expect(personPage.heading).toHaveText(organisation.person.displayName);
        for (const button of [
          personPage.statusAction,
          personPage.deleteAction,
          personPage.assignRoles,
          personPage.recordMove,
        ]) {
          await expect(button).toBeDisabled();
          await expect(button).toHaveAttribute("data-hydrating", "");
        }
      } finally {
        await removeOrganisation(api, organisation);
      }
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows the details, today's position, the roles, the moves and the latest changes", async ({
        page,
        api,
        person: administrator,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "person");
        const { person, manager, department, designation } = organisation;
        try {
          const personPage = new PersonPage(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();

          await expect(page).toHaveTitle(`${person.displayName} · ERP-AI-Pro`);
          await expect(personPage.header).toMatchAriaSnapshot(`
            - paragraph: People
            - heading "${person.displayName}" [level=1]
            - paragraph: ${person.workEmail} Active
            - link "Edit details":
              - /url: ${editPersonPath(person.id)}
            - button "Deactivate"
            - button "Delete"
          `);
          await expect(personPage.details).toMatchAriaSnapshot(`
            - heading "Details" [level=2]
            - text: The name and work email come from Microsoft Entra each time the person signs in. The department, designation and manager come from the move that holds today.
            - term: Employee code
            - definition: ${person.employeeCode ?? ""}
            - term: Phone number
            - definition: "+919876543210"
            - term: Date of joining
            - definition: ${typedDate(joinedOn)}
            - term: Last signed in
            - definition: Not signed in yet
            - term: Department
            - definition: ${department.name}
            - term: Designation
            - definition: ${designation.name}
            - term: Manager
            - definition:
              - link "${manager.displayName}":
                - /url: ${personPath(manager.id)}
          `);
          await expect(personPage.roles).toMatchAriaSnapshot(`
            - heading "Roles" [level=2]
            - text: Everyone who signs in is an employee, and administrators are chosen in Microsoft Entra. These are the roles given in the ERP.
            - button "Assign roles"
            - paragraph: No roles are given in the ERP.
            - link "See what each role may do":
              - /url: /identity/users/permissions
          `);
          await expect(personPage.moves).toMatchAriaSnapshot(`
            - heading "Moves" [level=2]
            - text: Where ${person.displayName} works and whom they report to, from each date until their next move.
            - button "Record a move"
            - list "Moves":
              - listitem:
                - heading "From ${typedDate(joinedOn)}" [level=3]
                - text: Held today
                - term: Department
                - definition: ${department.name}
                - term: Designation
                - definition: ${designation.name}
                - term: Manager
                - definition: ${manager.displayName}
                - button "Withdraw the move from ${typedDate(joinedOn)}"
          `);
          await expect(personPage.history.getByTestId("audit-change-summary")).toHaveText([
            `${administrator?.name ?? ""} created a move of ${person.displayName}.`,
            `${administrator?.name ?? ""} created the person ${person.displayName}.`,
          ]);
          await expect(personPage.historyLink).toHaveAttribute("href", `/platform/audit?record=${person.id}`);
          await capture("person");

          await personPage.details.getByRole("link", { name: manager.displayName }).click();
          await expect(page).toHaveURL((url) => url.pathname === personPath(manager.id));
          await expect(personPage.heading).toHaveText(manager.displayName);
          await expect(page).toHaveTitle(`${manager.displayName} · ERP-AI-Pro`);
          await page.goBack();
          await expect(personPage.heading).toHaveText(person.displayName);

          await personPage.historyLink.click();
          await expect(page).toHaveURL(
            (url) => url.pathname === "/platform/audit" && url.searchParams.get("record") === person.id,
          );
          await expect(page.getByRole("heading", { name: "Audit trail", level: 1 })).toBeVisible();
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("assigns roles through the dialog, which stays open while it saves, and explains a record changed meanwhile", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "roles");
        const { person } = organisation;
        try {
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();

          await personPage.assignRoles.click();
          await expect(personPage.rolesDialog).toBeVisible();
          await expect(personPage.rolesDialog).toMatchAriaSnapshot(`
            - dialog "Assign roles":
              - heading "Assign roles" [level=2]
              - paragraph: Choose the roles ${person.displayName} holds in the ERP. Their employee and administrator roles come from Microsoft Entra.
              - group "Roles in the ERP":
                - text: Roles in the ERP
                - checkbox "Finance"
                - checkbox "HR"
                - checkbox "Manager"
              - button "Cancel"
              - button "Save roles"
              - button "Close"
          `);
          await capture("person-roles-dialog", personPage.rolesDialog);

          await personPage.rolesCancel.click();
          await expect(personPage.rolesDialog).toHaveCount(0);
          await expect(personPage.assignRoles).toBeFocused();
          await personPage.assignRoles.click();
          await expect(personPage.rolesDialog).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(personPage.rolesDialog).toHaveCount(0);
          await expect(personPage.assignRoles).toBeFocused();
          await personPage.assignRoles.click();
          await personPage.rolesClose.click();
          await expect(personPage.rolesDialog).toHaveCount(0);
          await expect(personPage.assignRoles).toBeFocused();

          await personPage.assignRoles.click();
          await personPage.roleBox("Finance").check();
          await personPage.roleBox("Manager").check();
          const release = await holdServerFunctionCalls(page, personPath(person.id));
          await personPage.saveRoles.click();
          await expect(personPage.saveRoles).toHaveAttribute("aria-busy", "true");
          await page.keyboard.press("Escape");
          await expect(personPage.rolesDialog).toBeVisible();
          await expect(personPage.rolesCancel).toHaveAttribute("aria-disabled", "true");
          await expect(personPage.rolesClose).toHaveAttribute("aria-disabled", "true");
          await personPage.rolesClose.focus();
          await page.keyboard.press("Enter");
          await expect(personPage.rolesDialog).toBeVisible();
          release();

          await expect(personPage.rolesDialog).toHaveCount(0);
          await expect(shell.toast(`Saved the roles of ${person.displayName}.`)).toBeVisible();
          await expect(personPage.assignRoles).toBeFocused();
          await expect(personPage.roleList).toMatchAriaSnapshot(`
            - list "Roles":
              - listitem: Finance
              - listitem: Manager
          `);
          expect((await personRecord(api, person.id)).roles).toEqual(["finance", "manager"]);

          await personPage.assignRoles.click();
          await expect(personPage.roleBox("Finance")).toBeChecked();
          await expect(personPage.roleBox("HR")).not.toBeChecked();
          await expect(personPage.roleBox("Manager")).toBeChecked();
          const current = await personRecord(api, person.id);
          const changedElsewhere = await api.put(`${peopleApi}/${person.id}/roles`, {
            data: { roles: ["hr"], version: current.version },
          });
          expect(changedElsewhere.status(), "the change made elsewhere").toBe(200);
          await personPage.roleBox("Manager").uncheck();
          await personPage.saveRoles.click();

          await expect(personPage.rolesSummary).toContainText(
            "Someone else changed this record after the page was loaded. Reload the page, then assign the roles again.",
          );
          await expect(personPage.rolesSummary).toBeFocused();
          await expect(personPage.rolesDialog).toBeVisible();
          await capture("person-roles-conflict", personPage.rolesDialog);
          expect((await personRecord(api, person.id)).roles).toEqual(["hr"]);

          await personPage.rolesCancel.click();
          await page.reload();
          await expect(personPage.roleList).toMatchAriaSnapshot(`
            - list "Roles":
              - listitem: HR
          `);
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("records a move dated from the calendar with a manager found by name, refuses a date already taken and withdraws the move", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "moves");
        const { prefix, person, manager, department, designation } = organisation;
        const later = isoDateAfter(400);
        try {
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();

          await personPage.recordMove.click();
          await expect(personPage.moveDialog).toBeVisible();
          await expect(personPage.moveDialog).toMatchAriaSnapshot(`
            - dialog "Record a move":
              - heading "Record a move" [level=2]
              - paragraph: From the date you choose, ${person.displayName} works in this department with this designation and reports to this manager, until their next move. Leave a choice empty for none.
              - group:
                - text: Takes effect on
                - group:
                  - textbox "Takes effect on":
                    - /placeholder: dd-mm-yyyy
                  - group:
                    - button "Choose date"
                - paragraph: A date in the past or the future.
              - group:
                - text: Department
                - group:
                  - combobox "Department"
                  - group:
                    - button "Show options Department"
                  - status
              - group:
                - text: Designation
                - group:
                  - combobox "Designation"
                  - group:
                    - button "Show options Designation"
                  - status
              - group:
                - text: Manager
                - group:
                  - combobox "Manager"
                  - group:
                    - button "Show options Manager"
                  - status
                - paragraph: Type part of a name; only active people are listed.
              - button "Cancel"
              - button "Record the move"
              - button "Close"
          `);
          await capture("person-move-dialog", personPage.moveDialog);

          await personPage.saveMove.click();
          await expect(personPage.moveDate).toBeFocused();
          await expect(personPage.moveDate).toHaveAttribute("aria-invalid", "true");
          await expect(
            personPage.moveDialog.getByText("Enter the date the move takes effect."),
          ).toBeVisible();
          await capture("person-move-refused-in-the-browser", personPage.moveDialog);

          await personPage.moveDate.fill(typedDate(dayBefore(later)));
          await personPage.moveDateTrigger.focus();
          await page.keyboard.press("Enter");
          await expect(personPage.moveCalendar).toBeVisible();
          await expect(personPage.calendarDay(dayBefore(later))).toBeFocused();
          await page.keyboard.press("ArrowRight");
          await expect(personPage.calendarDay(later)).toBeFocused();
          await page.keyboard.press("Enter");
          await expect(personPage.moveCalendar).toBeHidden();
          await expect(personPage.moveDate).toHaveValue(typedDate(later));
          await expect(personPage.moveDateTrigger).toBeFocused();
          await expect(personPage.moveDialog).toBeVisible();
          await page.keyboard.press("Enter");
          await expect(personPage.moveCalendar).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(personPage.moveCalendar).toBeHidden();
          await expect(personPage.moveDialog).toBeVisible();
          await expect(personPage.moveDateTrigger).toBeFocused();
          await expect(personPage.moveDate).toHaveValue(typedDate(later));

          await personPage.choose(personPage.moveDepartment, prefix, department.name);
          await personPage.choose(personPage.moveDesignation, prefix, designation.name);
          await personPage.choose(personPage.moveManager, `${prefix} Man`, manager.displayName);
          const release = await holdServerFunctionCalls(page, personPath(person.id));
          await personPage.saveMove.click();
          await expect(personPage.saveMove).toHaveAttribute("aria-busy", "true");
          await page.keyboard.press("Escape");
          await expect(personPage.moveDialog).toBeVisible();
          await expect(personPage.moveCancel).toHaveAttribute("aria-disabled", "true");
          await expect(personPage.moveClose).toHaveAttribute("aria-disabled", "true");
          await personPage.moveCancel.focus();
          await page.keyboard.press("Enter");
          await expect(personPage.moveDialog).toBeVisible();
          release();

          await expect(personPage.moveDialog).toHaveCount(0);
          await expect(
            shell.toast(`Recorded the move of ${person.displayName} from ${typedDate(later)}.`),
          ).toBeVisible();
          await expect(personPage.recordMove).toBeFocused();
          await expect(personPage.moveItems).toHaveCount(2);
          await expect(personPage.move(`From ${typedDate(later)}`)).toMatchAriaSnapshot(`
            - listitem:
              - heading "From ${typedDate(later)}" [level=3]
              - text: Starts later
              - term: Department
              - definition: ${department.name}
              - term: Designation
              - definition: ${designation.name}
              - term: Manager
              - definition: ${manager.displayName}
              - button "Withdraw the move from ${typedDate(later)}"
          `);
          await expect(
            personPage.move(`From ${typedDate(joinedOn)} to ${typedDate(dayBefore(later))}`),
          ).toContainText("Held today");
          await capture("person-move-recorded", personPage.moves);

          await personPage.recordMove.click();
          await expect(personPage.moveDialog).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(personPage.moveDialog).toHaveCount(0);
          await expect(personPage.recordMove).toBeFocused();
          await personPage.recordMove.click();
          await personPage.moveClose.click();
          await expect(personPage.moveDialog).toHaveCount(0);
          await expect(personPage.recordMove).toBeFocused();

          await personPage.recordMove.click();
          await personPage.moveDate.fill(typedDate(joinedOn));
          await personPage.saveMove.click();
          await expect(personPage.moveSummary).toContainText(
            "A move from this date already exists; withdraw it first.",
          );
          await expect(personPage.moveSummary).toBeFocused();
          await capture("person-move-refused", personPage.moveDialog);
          await personPage.moveCancel.click();
          await expect(personPage.moveDialog).toHaveCount(0);
          await expect(personPage.recordMove).toBeFocused();

          const withdrawal = personPage.confirmation("Withdraw this move?");
          await personPage.withdrawButton(joinedOn).click();
          await expect(withdrawal).toContainText(
            `The move from ${typedDate(joinedOn)} is removed, and from that date they have no department, designation or manager until a later move starts.`,
          );
          await withdrawal.getByRole("button", { name: "Keep it" }).click();
          await expect(withdrawal).toHaveCount(0);
          await expect(personPage.withdrawButton(joinedOn)).toBeFocused();
          await expect(personPage.moveItems).toHaveCount(2);

          await personPage.withdrawButton(later).click();
          await expect(withdrawal).toContainText(
            `The move from ${typedDate(later)} is removed, and the move before it applies again until the next one starts.`,
          );
          await capture("person-move-withdrawal", withdrawal);
          await withdrawal.getByRole("button", { name: "Withdraw", exact: true }).click();

          await expect(personPage.movesHeading).toBeFocused();
          await expect(shell.toast(`Withdrew the move from ${typedDate(later)}.`)).toBeVisible();
          await expect(personPage.moveItems).toHaveCount(1);
          await expect(personPage.move(`From ${typedDate(joinedOn)}`)).toContainText("Held today");
          expect((await movesOf(api, person.id)).map((move) => move.effectiveFrom)).toEqual([joinedOn]);
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("explains a withdrawal the API refuses beside the move, where focus stays", async ({
        page,
        api,
        capture,
      }) => {
        const prefix = uniquePrefix("withdraw-refused");
        const created: { readonly people: string[]; readonly departments: string[] } = {
          people: [],
          departments: [],
        };
        try {
          const department = await createDepartmentRecord(api, `${prefix} Closed`);
          created.departments.push(department.id);
          const person = await registerPersonRecord(api, {
            displayName: `${prefix} Person`,
            workEmail: `${prefix}-person@example.com`,
            dateOfJoining: joinedOn,
          });
          created.people.push(person.id);
          await recordMoveRecord(api, person.id, { effectiveFrom: joinedOn, departmentId: department.id });
          await recordMoveRecord(api, person.id, { effectiveFrom: movedOn });
          const deleted = await api.delete(`${departmentsApi}/${department.id}`);
          expect(deleted.status(), "the deletion of a department only an ended move names").toBe(204);

          const personPage = new PersonPage(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();
          const heldToday = personPage.move(`From ${typedDate(movedOn)}`);
          await personPage.withdrawButton(movedOn).click();
          await personPage
            .confirmation("Withdraw this move?")
            .getByRole("button", { name: "Withdraw", exact: true })
            .click();

          const refusal = heldToday.getByTestId("withdraw-move-error");
          await expect(refusal).toHaveRole("alert");
          await expect(refusal).toContainText(
            "Withdrawing this move would bring back the one before it, which names a department, designation or manager deleted or deactivated since; record a new move instead.",
          );
          await expect(personPage.withdrawButton(movedOn)).toBeFocused();
          await expect(personPage.moveItems).toHaveCount(2);
          await capture("person-move-withdrawal-refused", personPage.moves);
          expect((await movesOf(api, person.id)).map((move) => move.effectiveFrom).sort()).toEqual([
            joinedOn,
            movedOn,
          ]);
        } finally {
          await removeRecords(api, created);
        }
      });

      test("deactivates and reactivates the person", async ({ page, api, capture }) => {
        const organisation = await seedOrganisation(api, "status");
        const { person } = organisation;
        try {
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();

          await personPage.statusAction.click();
          const deactivation = personPage.confirmation(`Deactivate ${person.displayName}?`);
          await expect(deactivation).toMatchAriaSnapshot(`
            - alertdialog "Deactivate ${person.displayName}?":
              - heading "Deactivate ${person.displayName}?" [level=2]
              - paragraph: ${person.displayName} can no longer sign in, and any session they have open ends at their next step in the ERP. You can reactivate them later.
              - button "Keep them active"
              - button "Deactivate"
          `);
          await capture("person-deactivation", deactivation);
          await deactivation.getByRole("button", { name: "Keep them active" }).click();
          await expect(deactivation).toHaveCount(0);
          await expect(personPage.statusAction).toBeFocused();

          await personPage.statusAction.click();
          await deactivation.getByRole("button", { name: "Deactivate", exact: true }).click();
          await expect(shell.toast(`Deactivated ${person.displayName}.`)).toBeVisible();
          await expect(personPage.status).toHaveText("Deactivated");
          await expect(personPage.statusAction).toHaveText("Reactivate");
          expect((await personRecord(api, person.id)).status).toBe("deactivated");
          await capture("person-deactivated", personPage.header);

          await personPage.statusAction.click();
          const reactivation = personPage.confirmation(`Reactivate ${person.displayName}?`);
          await expect(reactivation).toContainText(
            `${person.displayName} can sign in again with their Microsoft account.`,
          );
          await reactivation.getByRole("button", { name: "Keep them deactivated" }).click();
          await expect(reactivation).toHaveCount(0);
          await expect(personPage.statusAction).toBeFocused();
          await expect(personPage.status).toHaveText("Deactivated");

          await personPage.statusAction.click();
          await reactivation.getByRole("button", { name: "Reactivate", exact: true }).click();
          await expect(shell.toast(`Reactivated ${person.displayName}.`)).toBeVisible();
          await expect(personPage.status).toHaveText("Active");
          await expect(personPage.statusAction).toHaveText("Deactivate");
          expect((await personRecord(api, person.id)).status).toBe("active");
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("explains a status change refused because the record changed meanwhile, again when it is retried", async ({
        page,
        api,
        capture,
      }) => {
        const organisation = await seedOrganisation(api, "stale-status");
        const { person } = organisation;
        try {
          const personPage = new PersonPage(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();
          const loaded = await personRecord(api, person.id);
          const changedElsewhere = await api.put(`${peopleApi}/${person.id}/roles`, {
            data: { roles: ["hr"], version: loaded.version },
          });
          expect(changedElsewhere.status(), "the change made elsewhere").toBe(200);

          const deactivation = personPage.confirmation(`Deactivate ${person.displayName}?`);
          await personPage.statusAction.click();
          await deactivation.getByRole("button", { name: "Deactivate", exact: true }).click();
          await expect(personPage.statusError).toHaveRole("alert");
          await expect(personPage.statusError).toContainText(changedMeanwhile);
          await expect(personPage.statusAction).toBeFocused();
          await capture("person-status-refused", personPage.header);

          const release = await holdServerFunctionCalls(page, personPath(person.id));
          await personPage.statusAction.click();
          await deactivation.getByRole("button", { name: "Deactivate", exact: true }).click();
          await expect(personPage.statusError).toHaveCount(0);
          release();
          await expect(personPage.statusError).toHaveRole("alert");
          await expect(personPage.statusError).toContainText(changedMeanwhile);
          expect((await personRecord(api, person.id)).status).toBe("active");
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("refuses to delete a person others report to, then deletes the person who reports", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "delete");
        const { person, manager } = organisation;
        try {
          const personPage = new PersonPage(page);
          const shell = new AppShell(page);
          await personPage.goto(manager.id);
          await personPage.waitUntilInteractive();

          await personPage.deleteAction.click();
          const managerDeletion = personPage.confirmation(`Delete ${manager.displayName}?`);
          await expect(managerDeletion).toMatchAriaSnapshot(`
            - alertdialog "Delete ${manager.displayName}?":
              - heading "Delete ${manager.displayName}?" [level=2]
              - paragraph: ${manager.displayName} is removed from People, and any session they have open ends at their next step. If they can still sign in through Microsoft Entra, their next sign-in creates a new record, so deactivate them instead to keep them out. Their changes stay in the audit trail.
              - button "Keep the record"
              - button "Delete"
          `);
          await capture("person-deletion", managerDeletion);
          await managerDeletion.getByRole("button", { name: "Keep the record" }).click();
          await expect(managerDeletion).toHaveCount(0);
          await expect(personPage.deleteAction).toBeFocused();
          await expect(page).toHaveURL((url) => url.pathname === personPath(manager.id));

          await personPage.deleteAction.click();
          await managerDeletion.getByRole("button", { name: "Delete", exact: true }).click();
          await expect(personPage.deleteError).toContainText(
            "People report to this person today or from a later date. Record another manager for them first.",
          );
          await expect(personPage.deleteError).toHaveRole("alert");
          await expect(page).toHaveURL((url) => url.pathname === personPath(manager.id));
          await capture("person-deletion-refused", personPage.header);
          expect((await personRecord(api, manager.id)).status).toBe("active");

          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();
          await personPage.deleteAction.click();
          await personPage
            .confirmation(`Delete ${person.displayName}?`)
            .getByRole("button", { name: "Delete", exact: true })
            .click();

          await expect(page).toHaveURL((url) => url.pathname === peoplePath);
          await expect(shell.toast(`Deleted ${person.displayName}.`)).toBeVisible();
          expect((await api.get(`${peopleApi}/${person.id}`)).status(), "the deleted record").toBe(404);
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("explains on the administrator's own record why it offers no Deactivate or Delete", async ({
        page,
        api,
        person: administrator,
        capture,
      }) => {
        const ownId = await ownPersonRecordId(api, administrator);
        const personPage = new PersonPage(page);
        await personPage.goto(ownId);
        await personPage.waitUntilInteractive();

        await expect(personPage.heading).toHaveText(administrator?.name ?? "");
        await expect(personPage.actions).toMatchAriaSnapshot(`
          - link "Edit details":
            - /url: ${editPersonPath(ownId)}
          - paragraph: This is your own record. Another administrator can deactivate or delete it.
        `);
        await expect(personPage.statusAction).toHaveCount(0);
        await expect(personPage.deleteAction).toHaveCount(0);
        await capture("person-own-record", personPage.header);

        const own = await personRecord(api, ownId);
        const refused = await api.put(`${peopleApi}/${ownId}/status`, {
          data: { status: "deactivated", version: own.version },
        });
        expect(refused.status(), "the API's answer to deactivating one's own record").toBe(422);
        expect(await refused.json()).toMatchObject({ code: "user.own-status" });
      });

      test("edits the employee code, phone number and date of joining, refused first in the browser and then by the API", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "edit");
        const { prefix, person, manager } = organisation;
        try {
          const personPage = new PersonPage(page);
          const editPage = new EditPersonPage(page);
          const shell = new AppShell(page);
          await personPage.goto(person.id);
          await personPage.waitUntilInteractive();
          await personPage.editLink.click();
          await expect(page).toHaveURL((url) => url.pathname === editPersonPath(person.id));
          await expect(editPage.save).toBeEnabled();

          await expect(page).toHaveTitle(`Edit ${person.displayName} · ERP-AI-Pro`);
          await expect(editPage.heading).toHaveText(`Edit ${person.displayName}`);
          await expect(editPage.identity).toMatchAriaSnapshot(`
            - term: Name
            - definition: ${person.displayName}
            - term: Work email
            - definition: ${person.workEmail}
          `);
          await expect(editPage.employeeCode).toHaveValue(person.employeeCode ?? "");
          await expect(editPage.phoneNumber).toHaveValue("+919876543210");
          await expect(editPage.dateOfJoining).toHaveValue(typedDate(joinedOn));
          await capture("person-edit");

          await editPage.phoneNumber.fill("12345");
          await editPage.phoneNumber.press("Tab");
          await expect(editPage.phoneNumber).toHaveAttribute("aria-invalid", "true");
          await expect(editPage.fieldOf(editPage.phoneNumber)).toContainText(
            "Enter a phone number with + and the country code, such as +44 20 7946 0958, or a 10-digit Indian mobile number.",
          );
          await editPage.save.click();
          await expect(editPage.phoneNumber).toBeFocused();
          await expect(page).toHaveURL((url) => url.pathname === editPersonPath(person.id));
          await capture("person-edit-refused-in-the-browser", editPage.form);

          await editPage.phoneNumber.fill("+44 20 7946 0958");
          await editPage.employeeCode.fill(manager.employeeCode ?? "");
          await editPage.save.click();
          await expect(editPage.summary).toContainText("Another person already has this employee code.");
          await expect(editPage.summary).toBeFocused();
          await expect(page).toHaveURL((url) => url.pathname === editPersonPath(person.id));
          await capture("person-edit-refused", editPage.form);

          await editPage.employeeCode.fill(`${prefix}-N`);
          await editPage.dateOfJoining.fill("15-05-2026");
          await editPage.save.click();

          await expect(shell.toast(`Saved the details of ${person.displayName}.`)).toBeVisible();
          await expect(page).toHaveURL((url) => url.pathname === personPath(person.id));
          await expect(personPage.heading).toHaveText(person.displayName);
          await expect(personPage.details).toContainText(`${prefix}-N`);
          await expect(personPage.details).toContainText("+442079460958");
          await expect(personPage.details).toContainText("15-05-2026");
          const edited = await personRecord(api, person.id);
          expect([edited.employeeCode, edited.phoneNumber, edited.dateOfJoining]).toEqual([
            `${prefix}-N`,
            "+442079460958",
            "2026-05-15",
          ]);
          await page.goBack();
          await expect(page).toHaveURL((url) => url.pathname === personPath(person.id));
          await expect(personPage.heading).toHaveText(person.displayName);

          await editPage.goto(person.id);
          await editPage.cancel.click();
          await expect(page).toHaveURL((url) => url.pathname === personPath(person.id));
        } finally {
          await removeOrganisation(api, organisation);
        }
      });
    });
  }

  test.describe("for an unknown or malformed person", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    test("answers the not-found page", async ({ page }) => {
      for (const path of [
        personPath(randomUUID()),
        personPath("not-a-person"),
        editPersonPath(randomUUID()),
      ]) {
        const response = await page.goto(path);
        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      }
    });
  });
});

test.describe("person page for an employee", () => {
  forEachTheme("tells them they have no access to a person or to editing one", async ({ page, capture }) => {
    for (const path of [personPath(randomUUID()), editPersonPath(randomUUID())]) {
      await page.goto(path);

      await expect(
        page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
      ).toBeVisible();
      await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
      await expect(page.getByTestId("person-details")).toHaveCount(0);
    }
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("person-no-access");
  });

  test("the API refuses them a person, their moves and every change to them", async ({ api }) => {
    const id = randomUUID();
    const refusals = [
      await api.get(`${peopleApi}/${id}`),
      await api.get(`${peopleApi}/${id}/positions`),
      await api.put(`${peopleApi}/${id}`, { data: { version: "AAAAAAAAB9E=" } }),
      await api.put(`${peopleApi}/${id}/status`, {
        data: { status: "deactivated", version: "AAAAAAAAB9E=" },
      }),
      await api.put(`${peopleApi}/${id}/roles`, { data: { roles: [], version: "AAAAAAAAB9E=" } }),
      await api.post(`${peopleApi}/${id}/positions`, {
        headers: { "Idempotency-Key": randomUUID() },
        data: { effectiveFrom: joinedOn },
      }),
      await api.delete(`${peopleApi}/${id}`),
    ];
    for (const refused of refusals) {
      expect(refused.status(), refused.url()).toBe(403);
      expect(await refused.json(), refused.url()).toMatchObject({ code: "request.forbidden" });
    }
  });
});
