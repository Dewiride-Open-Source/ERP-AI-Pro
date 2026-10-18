import type { Browser, BrowserContext } from "@playwright/test";

import { addDirectoryPerson } from "../../../fixtures/directory";
import { expect, pressArrowUntilChecked, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  ownPhoto,
  ownPhotoWidth,
  ownProfileApi,
  profilePath,
  ProfilePage,
  type OwnProfile,
  type ThemeName,
} from "../../../pages/identity/users/profile.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const darkClass = /(^|\s)dark(\s|$)/;

async function ownProfile(api: SignedInApi): Promise<OwnProfile> {
  const response = await api.get(ownProfileApi);
  expect(response.status(), "the signed-in person's own record").toBe(200);
  return (await response.json()) as OwnProfile;
}

function savedTheme(api: SignedInApi): () => Promise<ThemeName> {
  return async () => (await ownProfile(api)).theme;
}

function initialsOf(name: string): string {
  const words = name.trim().split(/\s+/);
  return [words[0], words.length > 1 ? words.at(-1) : undefined]
    .map((word) => word?.charAt(0).toUpperCase() ?? "")
    .join("");
}

// A second browser of the same person: the context's cookies hold the session, and its storage holds no theme of its own.
async function sameSignInElsewhere(
  browser: Browser,
  context: BrowserContext,
  baseURL: string | undefined,
): Promise<BrowserContext> {
  const elsewhere = await browser.newContext({
    ...(baseURL === undefined ? {} : { baseURL }),
    ignoreHTTPSErrors: true,
    colorScheme: "light",
    locale: "en-IN",
    timezoneId: "Asia/Kolkata",
  });
  await elsewhere.addCookies(await context.cookies());
  return elsewhere;
}

test.describe("My profile", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows an employee their own record and roles, and their initials while the directory holds no photo", async ({
        page,
        person,
        capture,
      }) => {
        const profile = new ProfilePage(page);
        const shell = new AppShell(page);
        const name = person?.name ?? "";

        await profile.goto();
        await shell.waitUntilInteractive();
        await profile.waitUntilInteractive();

        await expect(page).toHaveTitle("My profile · ERP-AI-Pro");
        await expect(profile.person.getByRole("heading", { level: 2 })).toHaveText(name);
        await expect(profile.person).toContainText(person?.userName ?? "");
        await expect(profile.details).toMatchAriaSnapshot(`
          - heading "Your details" [level=2]
          - text: The department, designation and manager are those of today. To change any of these, ask an administrator.
          - term: Employee code
          - definition: Not recorded
          - term: Phone number
          - definition: Not recorded
          - term: Date of joining
          - definition: Not recorded
          - term: Department
          - definition: Not recorded
          - term: Designation
          - definition: Not recorded
          - term: Manager
          - definition: Not recorded
        `);
        await expect(profile.roleList).toMatchAriaSnapshot(`
          - list "Your roles":
            - listitem: Employee
        `);
        await expect(profile.rolesLink).toHaveCount(0);
        await expect(profile.initials).toHaveText(initialsOf(name));
        await expect(profile.photoImage).toHaveCount(0);
        await expect(shell.userMenuButton.locator("[data-slot='avatar-fallback']")).toHaveText(
          initialsOf(name),
        );
        await expect(shell.userMenuButton.locator("[data-slot='avatar-image']")).toHaveCount(0);
        await expect(profile.themeChooser).toMatchAriaSnapshot(`
          - radiogroup "Colour theme":
            - radio "System" [checked]
            - radio "Light"
            - radio "Dark"
        `);

        await capture("profile-employee");
      });

      test("shows the photo of the company directory on the page and on the account button", async ({
        page,
        person,
        request,
        capture,
      }) => {
        const name = person?.name ?? "";
        await addDirectoryPerson(request, {
          objectId: person?.id ?? "",
          displayName: name,
          userPrincipalName: person?.userName ?? "",
          photo: ownPhoto,
        });
        const profile = new ProfilePage(page);
        const shell = new AppShell(page);

        await profile.goto();
        await shell.waitUntilInteractive();
        await profile.waitUntilInteractive();

        await expect(profile.photoImage).toBeVisible();
        await expect(profile.photoImage).toHaveAccessibleName(`Photo of ${name}`);
        await expect
          .poll(() => profile.photoImage.evaluate((image) => (image as HTMLImageElement).naturalWidth))
          .toBe(ownPhotoWidth);
        await expect(profile.initials).toHaveCount(0);
        await expect(shell.userMenuButton.locator("[data-slot='avatar-image']")).toBeVisible();
        await expect(shell.userMenuButton.locator("[data-slot='avatar-fallback']")).toHaveCount(0);
        await expect(shell.userMenuButton).toHaveAccessibleName(`Signed in as ${name}`);

        await capture("profile-with-photo");
      });
    });
  }

  test("opens from My profile in the account menu", async ({ page }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();

    await shell.openUserMenu();
    await expect(shell.profileItem).toHaveAttribute("href", profilePath);
    await shell.profileItem.click();

    await expect(page).toHaveURL((url) => url.pathname === profilePath);
    await expect(new ProfilePage(page).heading).toBeVisible();
    await expect(shell.userMenu).toBeHidden();
  });

  test("saves the theme chosen on My profile, which another browser of the same person then shows", async ({
    page,
    context,
    browser,
    api,
    baseURL,
  }) => {
    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();
    await expect(profile.themeOption("system")).toBeChecked();

    await profile.themeOption("dark").click();

    await expect(profile.themeOption("dark")).toBeChecked();
    await expect(new AppShell(page).theme("dark")).toHaveAttribute("aria-checked", "true");
    await expect(page.locator("html")).toHaveClass(darkClass);
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");

    const elsewhere = await sameSignInElsewhere(browser, context, baseURL);
    try {
      const otherPage = await elsewhere.newPage();
      const errors: string[] = [];
      otherPage.on("console", (message) => {
        if (message.type() === "error") errors.push(message.text());
      });
      const otherProfile = new ProfilePage(otherPage);

      await otherProfile.goto();
      await otherProfile.waitUntilInteractive();

      await expect(otherPage.locator("html")).toHaveClass(darkClass);
      await expect(otherProfile.themeOption("dark")).toBeChecked();
      expect(errors, "console errors of the other browser").toEqual([]);
    } finally {
      await elsewhere.close();
    }
  });

  test("saves the theme chosen with the theme button at the top of the page", async ({ page, api }) => {
    const profile = new ProfilePage(page);
    const shell = new AppShell(page);
    await profile.goto();
    await profile.waitUntilInteractive();

    await shell.theme("light").click();

    await expect(shell.theme("light")).toHaveAttribute("aria-checked", "true");
    await expect(profile.themeOption("light")).toBeChecked();
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("light");

    await shell.theme("system").click();

    await expect(profile.themeOption("system")).toBeChecked();
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("system");
  });

  test("moves and saves the theme from the keyboard", async ({ page, api }) => {
    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();
    await profile.themeOption("system").focus();

    await pressArrowUntilChecked(page, "ArrowDown", profile.themeOption("light"));
    await expect(profile.themeOption("light")).toBeFocused();
    await expect(page.locator("html")).not.toHaveClass(darkClass);
    await pressArrowUntilChecked(page, "ArrowDown", profile.themeOption("dark"));
    await expect(profile.themeOption("dark")).toBeFocused();
    await expect(page.locator("html")).toHaveClass(darkClass);

    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");
    await expect(page.locator("html")).toHaveClass(darkClass);
  });

  test("keeps the theme chosen just before the page reloads", async ({ page, api }) => {
    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();

    await profile.themeOption("dark").click();
    await page.reload();
    await profile.waitUntilInteractive();

    await expect(profile.themeOption("dark")).toBeChecked();
    await expect(page.locator("html")).toHaveClass(darkClass);
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");
  });

  test.describe("for an administrator", () => {
    test.use({ persona: "administrator" });

    for (const theme of themes) {
      test.describe(theme, () => {
        test.use({ theme });

        test("shows the roles their access gives them and the way to what each role may do", async ({
          page,
          capture,
        }) => {
          const profile = new ProfilePage(page);
          await profile.goto();
          await new AppShell(page).waitUntilInteractive();
          await profile.waitUntilInteractive();

          await expect(profile.roleList).toMatchAriaSnapshot(`
            - list "Your roles":
              - listitem: Employee
              - listitem: Administrator
          `);
          await expect(profile.rolesLink).toHaveAttribute("href", "/identity/users/permissions");

          await capture("profile-administrator");
        });
      });
    }

    test("opens what each role may do from the roles card", async ({ page }) => {
      const profile = new ProfilePage(page);
      await profile.goto();
      await new AppShell(page).waitUntilInteractive();

      await profile.rolesLink.click();

      await expect(page).toHaveURL((url) => url.pathname === "/identity/users/permissions");
      await expect(page.getByRole("heading", { name: "Roles and permissions", level: 1 })).toBeVisible();
    });
  });
});
