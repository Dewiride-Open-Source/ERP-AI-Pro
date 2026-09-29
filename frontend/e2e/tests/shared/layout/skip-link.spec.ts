import { expect, forEachTheme, test } from "../../../fixtures/test";
import { FeedbackPage } from "../../../pages/platform/design/feedback.page";

test.describe("skip link", () => {
  forEachTheme("moves keyboard focus past the header to the page content", async ({ page, capture }) => {
    const feedback = new FeedbackPage(page);
    const { skipLink, main } = feedback.shell;
    await feedback.goto();

    expect((await skipLink.boundingBox())?.width).toBeLessThanOrEqual(1);
    // The skip link is the page's first focusable element, so the first Tab reaches it. WebKit leaves links out of the Tab
    // order unless Safari's "Press Tab to highlight each item" is on, so there it is focused from script right after the key
    // press, which :focus-visible treats as keyboard focus.
    await page.keyboard.press("Tab");
    if (page.context().browser()?.browserType().name() === "webkit") await skipLink.focus();
    await expect(skipLink).toBeFocused();
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
