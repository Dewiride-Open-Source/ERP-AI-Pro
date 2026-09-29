import type { Page } from "@playwright/test";

export interface NavigationEntry {
  readonly name: string;
  readonly href: string;
}

// The shell renders the module registry's enabled entries in its primary navigation, which is hidden below the sm
// breakpoint until the navigation drawer exists, so the entries are read from the markup rather than from what is shown.
export async function registeredNavigationEntries(page: Page): Promise<NavigationEntry[]> {
  return page
    .getByRole("navigation", { name: "Primary", includeHidden: true })
    .getByRole("link", { includeHidden: true })
    .evaluateAll((links) =>
      links.map((link) => ({
        name: link.textContent?.trim() ?? "",
        href: link.getAttribute("href") ?? "",
      })),
    );
}
