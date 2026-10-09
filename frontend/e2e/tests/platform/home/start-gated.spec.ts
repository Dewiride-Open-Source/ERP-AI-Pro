import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { StartPage } from "../../../pages/platform/home/start.page";

test.describe("start page with every module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "" });

  forEachTheme("says that nothing is open yet instead of showing cards", async ({ page, capture }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.areas).toHaveCount(0);
    await expect(start.noAreas).toHaveText("Nothing is open to you yet. Ask your administrator for access.");
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - paragraph: Home
      - heading /^Welcome, / [level=1]
      - paragraph: Choose where to start.
      - region "Areas you can open":
        - heading "Areas you can open" [level=2]
        - paragraph: Nothing is open to you yet. Ask your administrator for access.
    `);
    await capture("start-no-areas");
  });
});
