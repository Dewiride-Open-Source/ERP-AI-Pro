import assert from "node:assert/strict";
import { afterEach, test, type TestContext } from "node:test";

import { parseJson, renewBrowserAntiforgeryTokens } from "./browser-antiforgery.ts";

type Answer = { readonly signedOut: unknown };

type Renewal = { readonly url: string; readonly init: RequestInit | undefined };

const signedOut = (body: unknown): Answer => ({ signedOut: body });

function browserWithCookies(cookie: string): { cookie: string } {
  const document = { cookie };
  Object.defineProperty(globalThis, "document", { value: document, configurable: true, writable: true });
  return document;
}

function answerRenewals(
  t: TestContext,
  answer: (document: { cookie: string }) => Promise<Response>,
  document: { cookie: string },
): Renewal[] {
  const renewals: Renewal[] = [];
  t.mock.method(globalThis, "fetch", async (input: RequestInfo | URL, init?: RequestInit) => {
    renewals.push({ url: String(input), init });
    return answer(document);
  });
  return renewals;
}

afterEach(() => {
  Reflect.deleteProperty(globalThis, "document");
});

test("renewBrowserAntiforgeryTokens_Renewed_ReturnsTheCookiesTheAnswerSet", async (t) => {
  const document = browserWithCookies("__Host-erp-xsrf=old");
  const renewals = answerRenewals(
    t,
    async (browser) => {
      browser.cookie = "__Host-erp-xsrf=renewed";
      return new Response(null, { status: 204 });
    },
    document,
  );

  const renewal = await renewBrowserAntiforgeryTokens(signedOut);

  assert.deepEqual(renewal, { outcome: "renewed", cookies: "__Host-erp-xsrf=renewed" });
  assert.equal(renewals.length, 1);
  assert.equal(renewals[0]?.url, "/api/auth/antiforgery");
  assert.equal(renewals[0]?.init?.credentials, "same-origin");
  assert.equal(renewals[0]?.init?.cache, "no-store");
  assert.equal(renewals[0]?.init?.method, undefined);
});

test("renewBrowserAntiforgeryTokens_Unauthenticated_AnswersAsTheCallerWordsAnEndedSession", async (t) => {
  const document = browserWithCookies("");
  answerRenewals(
    t,
    async () =>
      new Response(JSON.stringify({ code: "request.unauthenticated" }), {
        status: 401,
        headers: { "content-type": "application/problem+json" },
      }),
    document,
  );

  const renewal = await renewBrowserAntiforgeryTokens(signedOut);

  assert.deepEqual(renewal, {
    outcome: "unauthenticated",
    answer: { signedOut: { code: "request.unauthenticated" } },
  });
});

test("renewBrowserAntiforgeryTokens_UnauthenticatedWithoutABody_PassesNothingToTheCaller", async (t) => {
  const document = browserWithCookies("");
  answerRenewals(t, async () => new Response(null, { status: 401 }), document);

  assert.deepEqual(await renewBrowserAntiforgeryTokens(signedOut), {
    outcome: "unauthenticated",
    answer: { signedOut: undefined },
  });
});

test("renewBrowserAntiforgeryTokens_AnyOtherAnswer_Fails", async (t) => {
  const document = browserWithCookies("__Host-erp-xsrf=old");
  answerRenewals(
    t,
    async () => new Response("<html>Internal Server Error</html>", { status: 500 }),
    document,
  );

  assert.deepEqual(await renewBrowserAntiforgeryTokens(signedOut), { outcome: "failed" });
});

test("renewBrowserAntiforgeryTokens_RequestThatNeverArrives_Fails", async (t) => {
  const document = browserWithCookies("__Host-erp-xsrf=old");
  answerRenewals(
    t,
    async () => {
      throw new TypeError("Failed to fetch");
    },
    document,
  );

  assert.deepEqual(await renewBrowserAntiforgeryTokens(signedOut), { outcome: "failed" });
});

test("parseJson_EmptyText_IsNothing", () => {
  assert.equal(parseJson(""), undefined);
});

test("parseJson_TextThatIsNotJson_IsNothing", () => {
  assert.equal(parseJson("<html>Bad Gateway</html>"), undefined);
  assert.equal(parseJson("{"), undefined);
});

test("parseJson_JsonText_IsItsValue", () => {
  assert.deepEqual(parseJson('{"code":"antiforgery.token-invalid","status":400}'), {
    code: "antiforgery.token-invalid",
    status: 400,
  });
  assert.equal(parseJson("null"), null);
});
