import { expect, forEachTheme, test } from "../../../fixtures/test";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { StartPage } from "../../../pages/platform/home/start.page";

test.describe("start page", () => {
  forEachTheme("greets the person and opens every area from its card", async ({ page, capture }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(page).toHaveTitle(/Home · ERP-AI-Pro/);
    await expect(start.areas).toHaveCount(2);
    await expect(start.noAreas).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - paragraph: Home
      - heading /^Welcome, / [level=1]
      - paragraph: Choose where to start.
      - region "Areas you can open":
        - heading "Areas you can open" [level=2]
        - list:
          - listitem:
            - heading "System" [level=3]
            - text: /version of the ERP/
            - link "Open System"
          - listitem:
            - heading "Attachments" [level=3]
            - text: /Upload, download and delete/
            - link "Open Attachments"
    `);
    await capture("start");

    await start.openLink("Attachments").click();
    await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    await page.goBack();
    await expect(start.heading).toBeVisible();
    await start.openLink("System").click();
    await expect(page).toHaveURL((url) => url.pathname === "/platform/system-info");
  });

  test("names the signed-in person", async ({ page, person }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.heading).toHaveText(`Welcome, ${person?.name ?? ""}`);
  });
});
