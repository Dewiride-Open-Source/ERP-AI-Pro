import { randomUUID } from "node:crypto";

import { designationIdsNamed } from "../../../../fixtures/organisation-records";
import {
  createDesignationRecord,
  designationsApi,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
} from "../../../../fixtures/person-records";
import { signIn } from "../../../../fixtures/sign-in";
import { expect, test, themes, type SignedInApi } from "../../../../fixtures/test";
import { DesignationsPage, designationsPath } from "../../../../pages/identity/users/designations.page";
import { AppShell, showsBreadcrumbs } from "../../../../pages/shared/layout/app-shell.page";

const quoted = JSON.stringify;

async function removeDesignationsNamed(api: SignedInApi, prefix: string): Promise<void> {
  await removeRecords(api, { designations: await designationIdsNamed(api, prefix) });
}

function rowSnapshot(name: string, id: string): string {
  return [
    `- listitem:`,
    `  - paragraph: ${quoted(name)}`,
    `  - button ${quoted(`Rename ${name}`)}`,
    `  - button ${quoted(`Delete ${name}`)}`,
    `  - link ${quoted(`Change history of ${name}`)}:`,
    `    - /url: /platform/audit?record=${id}`,
  ].join("\n");
}

test.describe("designations page", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("lists the designations by name, each with its controls and a link to its changes", async ({
        page,
        api,
        capture,
      }) => {
        const prefix = uniquePrefix("designations-list");
        const lead = await createDesignationRecord(api, `${prefix} Team lead`);
        const analyst = await createDesignationRecord(api, `${prefix} Analyst`);
        try {
          const designations = new DesignationsPage(page);
          await designations.goto();
          await designations.waitUntilInteractive();

          await expect(page).toHaveTitle(/Designations · ERP-AI-Pro/);
          expect(await designations.namesStartingWith(prefix), "the test's designations by name").toEqual([
            analyst.name,
            lead.name,
          ]);
          await expect(designations.row(analyst.name)).toMatchAriaSnapshot(
            rowSnapshot(analyst.name, analyst.id),
          );
          await expect(designations.row(lead.name)).toMatchAriaSnapshot(rowSnapshot(lead.name, lead.id));
          await capture("designations-row", designations.row(analyst.name));

          await designations.history(analyst.name).click();
          await expect(page).toHaveURL(
            (url) => url.pathname === "/platform/audit" && url.searchParams.get("record") === analyst.id,
          );
          await expect(page.getByRole("heading", { name: "Audit trail", level: 1 })).toBeVisible();
        } finally {
          await removeDesignationsNamed(api, prefix);
        }
      });

      test("adds and renames designations through the dialog, refusing a blank name and a name already taken", async ({
        page,
        api,
        capture,
      }) => {
        const prefix = uniquePrefix("designations-edit");
        const analyst = await createDesignationRecord(api, `${prefix} Analyst`);
        const accountant = `${prefix} Accountant`;
        const senior = `${prefix} Senior accountant`;
        try {
          const designations = new DesignationsPage(page);
          const shell = new AppShell(page);
          await designations.goto();
          await designations.waitUntilInteractive();

          await designations.addButton.click();
          await expect(designations.dialog).toMatchAriaSnapshot(`
            - dialog "Add a designation":
              - heading "Add a designation" [level=2]
              - paragraph: Name a designation people can hold, such as Accountant or Team lead.
              - group:
                - text: Name
                - textbox "Name"
                - paragraph: Up to 100 characters. No other designation may have it.
              - button "Cancel"
              - button "Add the designation"
              - button "Close"
          `);
          await capture("designations-add-dialog", designations.dialog);

          await designations.save.click();
          await expect(designations.name).toBeFocused();
          await expect(designations.name).toHaveAttribute("aria-invalid", "true");
          await expect(designations.dialog.getByText("Enter the designation's name.")).toBeVisible();
          await capture("designations-add-refused-in-the-browser", designations.dialog);

          await designations.name.fill(analyst.name.toUpperCase());
          await designations.save.click();
          await expect(designations.summary).toContainText(
            "Another designation already has this name; choose another name.",
          );
          await expect(designations.summary).toBeFocused();
          await capture("designations-add-refused", designations.dialog);

          await designations.cancel.click();
          await expect(designations.dialog).toHaveCount(0);
          await expect(designations.addButton).toBeFocused();
          await designations.addButton.click();
          await page.keyboard.press("Escape");
          await expect(designations.dialog).toHaveCount(0);
          await expect(designations.addButton).toBeFocused();

          await designations.addButton.click();
          await designations.name.fill(accountant);
          await designations.save.click();
          await expect(designations.dialog).toHaveCount(0);
          await expect(shell.toast(`Added ${accountant}.`)).toBeVisible();
          await expect(designations.addButton).toBeFocused();
          expect(await designations.namesStartingWith(prefix)).toEqual([accountant, analyst.name]);

          await designations.rename(accountant).click();
          await expect(designations.dialog.getByRole("heading", { level: 2 })).toHaveText(
            `Rename ${accountant}`,
          );
          await expect(designations.name).toHaveValue(accountant);
          await designations.name.fill(senior);
          await capture("designations-rename-dialog", designations.dialog);
          await designations.save.click();
          await expect(designations.dialog).toHaveCount(0);
          await expect(shell.toast(`Renamed ${accountant} to ${senior}.`)).toBeVisible();
          await expect(designations.rename(senior)).toBeFocused();
          expect(await designations.namesStartingWith(prefix)).toEqual([analyst.name, senior]);

          await designations.rename(senior).click();
          await designations.close.click();
          await expect(designations.dialog).toHaveCount(0);
          await expect(designations.rename(senior)).toBeFocused();
        } finally {
          await removeDesignationsNamed(api, prefix);
        }
      });

      test("deletes a designation after asking, and explains one the API keeps because someone holds it", async ({
        page,
        api,
        capture,
      }) => {
        const prefix = uniquePrefix("designations-delete");
        const held = await createDesignationRecord(api, `${prefix} Held`);
        const free = await createDesignationRecord(api, `${prefix} Free`);
        const person = await registerPersonRecord(api, {
          displayName: `${prefix} Person`,
          workEmail: `${prefix}-person@example.com`,
        });
        try {
          await recordMoveRecord(api, person.id, { effectiveFrom: "2026-04-01", designationId: held.id });
          const designations = new DesignationsPage(page);
          const shell = new AppShell(page);
          await designations.goto();
          await designations.waitUntilInteractive();

          await designations.delete(held.name).click();
          const heldDeletion = designations.confirmation(`Delete ${held.name}?`);
          await expect(heldDeletion).toMatchAriaSnapshot(`
            - alertdialog ${quoted(`Delete ${held.name}?`)}:
              - heading ${quoted(`Delete ${held.name}?`)} [level=2]
              - paragraph: ${quoted(
                `${held.name} is removed from the designations. The moves that named it keep its name, so people's history still shows it.`,
              )}
              - button "Keep it"
              - button "Delete"
          `);
          await capture("designations-deletion", heldDeletion);
          await heldDeletion.getByRole("button", { name: "Keep it" }).click();
          await expect(heldDeletion).toHaveCount(0);
          await expect(designations.delete(held.name)).toBeFocused();

          await designations.delete(held.name).click();
          await heldDeletion.getByRole("button", { name: "Delete", exact: true }).click();
          await expect(designations.refusal(held.name)).toHaveRole("alert");
          await expect(designations.refusal(held.name)).toContainText(
            "Someone holds this designation today or from a later date. Record another designation for them first.",
          );
          await expect(designations.delete(held.name)).toBeFocused();
          await capture("designations-deletion-refused", designations.row(held.name));

          await designations.delete(free.name).click();
          await designations
            .confirmation(`Delete ${free.name}?`)
            .getByRole("button", { name: "Delete", exact: true })
            .click();
          await expect(shell.toast(`Deleted ${free.name}.`)).toBeVisible();
          await expect(designations.row(free.name)).toHaveCount(0);
          await expect(designations.listHeading).toBeFocused();
          expect((await api.get(`${designationsApi}/${free.id}`)).status(), "the deleted designation").toBe(
            404,
          );
        } finally {
          await removeRecords(api, { people: [person.id] });
          await removeDesignationsNamed(api, prefix);
        }
      });
    });
  }

  test("keeps Designations marked in the navigation and ends the breadcrumb there", async ({ page }) => {
    const shell = new AppShell(page);
    await new DesignationsPage(page).goto();
    await shell.waitUntilInteractive();

    await shell.openNavigation();
    await expect(shell.navigationLink("Designations")).toHaveAttribute("aria-current", "page");
    await expect(shell.navigationLink("People")).not.toHaveAttribute("aria-current");
    await shell.closeDrawer();
    if (showsBreadcrumbs(page.viewportSize())) {
      await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
        - navigation "Breadcrumb":
          - list:
            - listitem:
              - link "Home":
                - /url: /
            - listitem: Organisation
            - listitem:
              - link "Designations" [disabled]
      `);
    }
  });
});

test.describe("designations page for an employee", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("lists the designations with no way to change them, and the API refuses a change", async ({
        context,
        page,
        api,
        capture,
      }) => {
        await signIn(context.request, "administrator");
        const prefix = uniquePrefix("designations-employee");
        const analyst = await createDesignationRecord(api, `${prefix} Analyst`);
        try {
          await signIn(context.request, "accountant");
          const designations = new DesignationsPage(page);
          await designations.goto();
          await designations.waitUntilInteractive();

          await expect(page).toHaveTitle(/Designations · ERP-AI-Pro/);
          await expect(designations.addButton).toHaveCount(0);
          await expect(designations.row(analyst.name)).toMatchAriaSnapshot(`
            - listitem:
              - paragraph: ${quoted(analyst.name)}
          `);
          await expect(designations.row(analyst.name).getByRole("button")).toHaveCount(0);
          await expect(designations.row(analyst.name).getByRole("link")).toHaveCount(0);
          await capture("designations-employee", designations.row(analyst.name));

          const refused = await api.post(designationsApi, {
            headers: { "Idempotency-Key": randomUUID() },
            data: { name: `${prefix} Refused` },
          });
          expect(refused.status()).toBe(403);
          expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
        } finally {
          // The open page renews its session once it has loaded, and a sign-in through the same browser context while that
          // renewal is under way leaves the page sending one person's request token with the next person's cookie.
          await page.goto("about:blank");
          await signIn(context.request, "administrator");
          await removeDesignationsNamed(api, prefix);
        }
      });
    });
  }

  test("is a page the employee may open from the navigation", async ({ page }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();
    await shell.openNavigation();
    await shell.navigationLink("Designations").click();

    await expect(page).toHaveURL((url) => url.pathname === designationsPath);
    await expect(new DesignationsPage(page).heading).toBeVisible();
  });
});
