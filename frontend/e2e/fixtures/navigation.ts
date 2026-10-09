import type { Page } from "@playwright/test";

import { AppShell } from "../pages/shared/layout/app-shell.page";

export interface NavigationEntry {
  readonly name: string;
  readonly href: string;
}

// The shell lists the module registry's enabled entries in the primary navigation under the title of their area, after the
// Home link that every person has. Below the md breakpoint that navigation exists only while the drawer is open, so the
// drawer is opened for the reading and closed again.
export async function registeredNavigationEntries(page: Page): Promise<NavigationEntry[]> {
  const shell = new AppShell(page);
  await shell.openNavigation();
  const entries = await shell.primaryNavigation
    .getByRole("list", { name: /\S/ })
    .getByRole("link")
    .evaluateAll((links) =>
      links.map((link) => ({
        name: link.textContent?.trim() ?? "",
        href: link.getAttribute("href") ?? "",
      })),
    );
  await shell.closeDrawer();
  return entries;
}
