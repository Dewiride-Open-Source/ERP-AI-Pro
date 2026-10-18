import type { Page, Route } from "@playwright/test";

// A Server Function call is a POST to the page's own path carrying the next-action header; holding it keeps the page in the
// state it shows while the call is in flight, for as long as the test needs to look at it.
export async function holdServerFunctionCalls(page: Page, pathname: string): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    (url) => url.pathname === pathname,
    async (route: Route) => {
      const request = route.request();
      if (request.method() !== "POST" || request.headers()["next-action"] === undefined) {
        await route.fallback();
        return;
      }
      await released;
      await route.fallback();
    },
  );
  return release;
}
