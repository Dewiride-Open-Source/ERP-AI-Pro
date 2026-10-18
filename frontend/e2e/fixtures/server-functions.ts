import type { Page, Route } from "@playwright/test";

function isServerFunctionCall(route: Route): boolean {
  const request = route.request();
  return request.method() === "POST" && request.headers()["next-action"] !== undefined;
}

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
      if (!isServerFunctionCall(route)) {
        await route.fallback();
        return;
      }
      await released;
      await route.fallback();
    },
  );
  return release;
}

// A Server Function's answer is a React Flight stream whose first row holds { a: <the function's result> }; Next.js resolves
// the call with that result and, with no page data beside it, leaves the page as it is. Flight reads a string that starts with
// $ as a reference, so no string in the result may start with one. The answer stands in for an outcome no test host can
// produce on demand, and the returned function removes it.
export async function answerServerFunctionCalls(
  page: Page,
  pathname: string,
  result: unknown,
): Promise<() => Promise<void>> {
  const calls = (url: URL) => url.pathname === pathname;
  await page.route(calls, async (route: Route) => {
    if (!isServerFunctionCall(route)) {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "text/x-component",
      body: `0:${JSON.stringify({ a: result })}\n`,
    });
  });
  return () => page.unroute(calls);
}
