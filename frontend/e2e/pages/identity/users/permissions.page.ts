import { expect, type Locator, type Page } from "@playwright/test";

export const permissionsPath = "/identity/users/permissions";

export const permissionsApi = "/api/identity/users/permissions";

export type CataloguedPermission = {
  readonly name: string;
  readonly description: string;
  readonly heldBy: readonly string[];
};

export type PermissionCatalog = {
  readonly roles: readonly string[];
  readonly permissions: readonly CataloguedPermission[];
};

export class PermissionsPage {
  readonly heading: Locator;
  readonly matrixHeading: Locator;
  readonly card: Locator;
  readonly matrix: Locator;
  readonly scroller: Locator;
  readonly rows: Locator;
  readonly peopleLink: Locator;
  readonly unavailable: Locator;
  readonly empty: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Roles and permissions", level: 1 });
    this.matrixHeading = page.getByRole("heading", { name: "What each role may do", level: 2 });
    this.card = page.getByTestId("permissions-card");
    this.matrix = this.card.getByTestId("permissions-matrix");
    this.scroller = this.card.locator("[data-slot='table-container']").filter({ has: this.matrix });
    this.rows = this.matrix.getByTestId("permissions-row");
    this.peopleLink = page.getByTestId("permissions-people-link");
    this.unavailable = page.getByTestId("permissions-unavailable");
    this.empty = page.getByTestId("permissions-empty");
  }

  async goto(): Promise<void> {
    await this.page.goto(permissionsPath);
    await expect(this.heading).toBeVisible();
  }

  row(name: string): Locator {
    return this.rows.filter({ has: this.page.getByText(name, { exact: true }) });
  }

  names(): Promise<string[]> {
    return this.rows.getByRole("rowheader").locator("code").allInnerTexts();
  }
}
