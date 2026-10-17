import type { Page, Request } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  peopleApi,
  personRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
} from "../../../fixtures/person-records";
import { expect, forEachTheme, test, themes } from "../../../fixtures/test";
import { peoplePath } from "../../../pages/identity/users/people.page";
import { PersonPage } from "../../../pages/identity/users/person.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";
import {
  isRegistrationRequest,
  RegistrationPage,
  registrationPath,
} from "../../../pages/identity/users/registration.page";

const personIdInPath = /^\/identity\/users\/([0-9a-f-]{36})$/;

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

test.describe("registering a person", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps Register disabled so no click is lost", async ({ page }) => {
      const registration = new RegistrationPage(page);
      await withholdClientBundles(page);
      await page.goto(registrationPath);

      await expect(registration.heading).toBeVisible();
      await expect(registration.save).toBeDisabled();
      await expect(registration.save).toHaveAttribute("data-hydrating", "");
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("registers a person with their details and roles and opens their page", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const registration = new RegistrationPage(page);
        const personPage = new PersonPage(page);
        const shell = new AppShell(page);
        const prefix = uniquePrefix("register");
        const name = `${prefix} Meera Nair`;
        let registeredId: string | undefined;
        try {
          await registration.goto();

          await expect(page).toHaveTitle("Register a person · ERP-AI-Pro");
          await expect(registration.form).toMatchAriaSnapshot(`
            - form "Their details":
              - group:
                - text: Name
                - textbox "Name"
                - paragraph: Shown until their first sign-in brings the name kept in Microsoft Entra.
              - group:
                - text: Work email
                - textbox "Work email"
                - paragraph: The address they sign in with.
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
              - button "Register the person"
              - link "Cancel":
                - /url: ${peoplePath}
          `);
          await expect(registration.displayName).toHaveAttribute("aria-required", "true");
          await expect(registration.workEmail).toHaveAttribute("aria-required", "true");
          await capture("registration");

          await registration.displayName.fill(name);
          await registration.workEmail.fill(` ${prefix}-Meera@Example.com `);
          await registration.employeeCode.fill(`${prefix}-R`);
          await registration.phoneNumber.fill("98765 43210");
          await registration.dateOfJoining.fill("01-04-2026");
          await registration.role("Finance").check();
          await registration.role("Manager").check();
          await capture("registration-filled", registration.form);
          await registration.save.click();

          await expect(shell.toast(`Registered ${name}.`)).toBeVisible();
          await expect(page).toHaveURL((url) => personIdInPath.test(url.pathname));
          registeredId = personIdInPath.exec(new URL(page.url()).pathname)?.[1];
          await expect(personPage.heading).toHaveText(name);
          await expect(page).toHaveTitle(`${name} · ERP-AI-Pro`);
          await expect(personPage.header).toContainText(`${prefix}-meera@example.com`);
          await expect(personPage.status).toHaveText("Active");
          await expect(personPage.details).toContainText(`${prefix}-R`);
          await expect(personPage.details).toContainText("+919876543210");
          await expect(personPage.details).toContainText("01-04-2026");
          await expect(personPage.details).toContainText("Not signed in yet");
          await expect(personPage.roleList).toMatchAriaSnapshot(`
            - list "Roles":
              - listitem: Finance
              - listitem: Manager
          `);
          await expect(personPage.movesNone).toHaveText("No moves are recorded yet.");
          const registered = await personRecord(api, registeredId ?? "");
          expect(registered).toMatchObject({
            displayName: name,
            workEmail: `${prefix}-meera@example.com`,
            employeeCode: `${prefix}-R`,
            phoneNumber: "+919876543210",
            dateOfJoining: "2026-04-01",
            roles: ["finance", "manager"],
            status: "active",
          });

          await page.goBack();
          await expect(page).not.toHaveURL((url) => url.pathname === registrationPath);
        } finally {
          await removeRecords(api, { people: registeredId === undefined ? [] : [registeredId] });
        }
      });

      test("checks the details in the browser before anything is sent", async ({ page, capture }) => {
        const registration = new RegistrationPage(page);
        const sent = watch(page, isRegistrationRequest);
        await registration.goto();

        await registration.save.click();
        await expect(registration.displayName).toBeFocused();
        await expect(registration.displayName).toHaveAttribute("aria-invalid", "true");
        await expect(registration.fieldOf(registration.displayName)).toContainText(
          "Enter the person's name.",
        );
        await expect(registration.fieldOf(registration.workEmail)).toContainText("Enter their work email.");
        await capture("registration-refused-in-the-browser", registration.form);

        await registration.displayName.fill("Meera Nair");
        await registration.workEmail.fill("meera.nair");
        await registration.employeeCode.fill("-0042");
        await registration.phoneNumber.fill("12345");
        await registration.dateOfJoining.fill("31-02-2026");
        await registration.save.click();

        await expect(registration.workEmail).toBeFocused();
        await expect(registration.fieldOf(registration.workEmail)).toContainText(
          "Enter a work email such as name@example.com.",
        );
        await expect(registration.fieldOf(registration.employeeCode)).toContainText(
          "Use letters, digits, hyphens, slashes and underscores, starting with a letter or a digit.",
        );
        await expect(registration.fieldOf(registration.phoneNumber)).toContainText(
          "Enter a phone number with + and the country code, such as +44 20 7946 0958, or a 10-digit Indian mobile number.",
        );
        await expect(registration.fieldOf(registration.dateOfJoining)).toContainText(
          "Enter a real date as day-month-year, for example 31-03-2026.",
        );
        await expect(page).toHaveURL((url) => url.pathname === registrationPath);
        expect(sent.count(), "Server Function calls").toBe(0);
      });

      test("explains a work email that is already registered and keeps the details", async ({
        page,
        api,
        capture,
      }) => {
        const registration = new RegistrationPage(page);
        const prefix = uniquePrefix("register-taken");
        const registered = await registerPersonRecord(api, {
          displayName: `${prefix} Kept`,
          workEmail: `${prefix}@example.com`,
        });
        try {
          await registration.goto();
          await registration.displayName.fill(`${prefix} Second`);
          await registration.workEmail.fill(`${prefix.toUpperCase()}@EXAMPLE.COM`);
          await registration.save.click();

          await expect(registration.summary).toContainText(
            "A person with this work email is already registered.",
          );
          await expect(registration.summary).toContainText(/Reference: \S+/);
          await expect(registration.summary).toBeFocused();
          await expect(registration.displayName).toHaveValue(`${prefix} Second`);
          await expect(page).toHaveURL((url) => url.pathname === registrationPath);
          await capture("registration-refused", registration.form);

          await registration.cancel.click();
          await expect(page).toHaveURL((url) => url.pathname === peoplePath);
        } finally {
          await removeRecords(api, { people: [registered.id] });
        }
      });
    });
  }
});

test.describe("registering a person for an employee", () => {
  forEachTheme("tells them they have no access to it", async ({ page, capture }) => {
    await page.goto(registrationPath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("register-person-form")).toHaveCount(0);
    await capture("registration-no-access");
  });

  test("the API refuses them a registration", async ({ api }) => {
    const prefix = uniquePrefix("register-refused");
    const refused = await api.post(peopleApi, {
      headers: { "Idempotency-Key": randomUUID() },
      data: { displayName: `${prefix} Refused`, workEmail: `${prefix}@example.com` },
    });

    expect(refused.status()).toBe(403);
    expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
  });
});
