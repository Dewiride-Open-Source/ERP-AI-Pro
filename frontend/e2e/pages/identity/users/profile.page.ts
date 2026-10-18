import { expect, type Locator, type Page } from "@playwright/test";

import type { DirectoryPhoto } from "../../../fixtures/directory";

export const profilePath = "/profile";

export const ownProfileApi = "/api/identity/users/me";

export const ownPhotoApi = "/api/identity/users/me/photo";

export type ThemeName = "system" | "light" | "dark";

export type OwnProfile = {
  readonly id: string;
  readonly displayName: string;
  readonly workEmail: string;
  readonly roles: readonly string[];
  readonly theme: ThemeName;
};

// Eight by eight pixels of one colour, a PNG every browser decodes, so the photo shows wherever the page puts it.
export const ownPhoto: DirectoryPhoto = {
  contentType: "image/png",
  base64:
    "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEUlEQVR4nGPgL8vDihiGlgQApWE8wfekj3cAAAAASUVORK5CYII=",
};

export const ownPhotoWidth = 8;

const themeLabels: Readonly<Record<ThemeName, string>> = { system: "System", light: "Light", dark: "Dark" };

export class ProfilePage {
  readonly heading: Locator;
  readonly person: Locator;
  readonly photoImage: Locator;
  readonly initials: Locator;
  readonly details: Locator;
  readonly roles: Locator;
  readonly roleList: Locator;
  readonly rolesLink: Locator;
  readonly theme: Locator;
  readonly themeChooser: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "My profile", level: 1 });
    this.person = page.getByTestId("profile-person");
    this.photoImage = this.person.locator("[data-slot='avatar-image']");
    this.initials = this.person.locator("[data-slot='avatar-fallback']");
    this.details = page.getByTestId("profile-details");
    this.roles = page.getByTestId("profile-roles");
    this.roleList = this.roles.getByRole("list", { name: "Your roles" });
    this.rolesLink = this.roles.getByRole("link", { name: "See what each role may do" });
    this.theme = page.getByTestId("profile-theme");
    this.themeChooser = this.theme.getByRole("radiogroup", { name: "Colour theme" });
    this.unavailable = page.getByTestId("profile-unavailable");
  }

  async goto(): Promise<void> {
    await this.page.goto(profilePath);
    await expect(this.heading).toBeVisible();
  }

  themeOption(theme: ThemeName): Locator {
    return this.themeChooser.getByRole("radio", { name: themeLabels[theme], exact: true });
  }

  // The chooser shows the browser's theme only once the page is interactive.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.themeChooser).not.toHaveAttribute("data-hydrating");
    await expect(this.themeOption("system")).toBeEnabled();
  }
}
