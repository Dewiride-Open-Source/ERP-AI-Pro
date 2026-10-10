import type { Page } from "@playwright/test";

// Answering the page's client bundles with empty scripts keeps React from hydrating the page, while the inline scripts of
// the server's HTML still reveal the parts that stream in, so the page shows what a person meets before it is interactive;
// with JavaScript switched off those parts would stay hidden.
export async function withholdClientBundles(page: Page): Promise<void> {
  await page.route(
    (url) => url.pathname.startsWith("/_next/static/") && url.pathname.endsWith(".js"),
    (route) => route.fulfill({ contentType: "text/javascript", body: "" }),
  );
}
