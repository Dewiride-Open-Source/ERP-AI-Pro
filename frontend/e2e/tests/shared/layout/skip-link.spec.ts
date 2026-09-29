import { expect, forEachTheme, tabOntoLink, test } from "../../../fixtures/test";
import { FeedbackPage } from "../../../pages/platform/design/feedback.page";

test.describe("skip link", () => {
  forEachTheme("moves keyboard focus past the header to the page content", async ({ page, capture }) => {
    const feedback = new FeedbackPage(page);
    const { skipLink, main } = feedback.shell;
    await feedback.goto();

    expect((await skipLink.boundingBox())?.width).toBeLessThanOrEqual(1);
    await tabOntoLink(page, skipLink);
    await expect(skipLink).toBeInViewport();
    expect((await skipLink.boundingBox())?.width).toBeGreaterThan(1);
    await capture("skip-link", skipLink);

    await page.keyboard.press("Enter");
    await expect(main).toBeFocused();
    await expect(page).toHaveURL((url) => url.hash === "#main-content");
    await page.keyboard.press("Tab");
    await expect(feedback.archiveButton).toBeFocused();
  });
});
