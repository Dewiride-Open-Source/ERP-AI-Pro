import assert from "node:assert/strict";
import { test } from "node:test";

import {
  antiforgeryFetch,
  antiforgeryHeaderName,
  antiforgeryRenewalPath,
  isAntiforgeryRefusal,
  isUnsafeMethod,
  mergeCookies,
  parseSetCookie,
  readCookie,
  requestTokenCookieName,
  sendWithAntiforgery,
  type AntiforgeryAttempt,
  type AntiforgeryRenewal,
  type SetCookie,
} from "./antiforgery.ts";

type Answer = { readonly status: number; readonly body?: unknown };

const antiforgeryCookieName = "__Host-erp-antiforgery";

const refusal: Answer = {
  status: 400,
  body: { status: 400, type: "/problems/antiforgery.token-missing", code: "antiforgery.token-missing" },
};

const created: Answer = { status: 201, body: { id: "0199a4c2-7d1e-7c3a-9f1b-2b6d4e8a1c00" } };

const signedOut: Answer = { status: 401, body: { status: 401, code: "request.unauthenticated" } };

const sessionCookies = `__Host-erp-session=chunks-2; ${requestTokenCookieName}=first-token; ${antiforgeryCookieName}=first-cookie-token`;

const renewedCookies = `__Host-erp-session=chunks-2; ${requestTokenCookieName}=renewed-token; ${antiforgeryCookieName}=renewed-cookie-token`;

function setCookie(
  name: string,
  value: string,
  attributes: Partial<Omit<SetCookie, "name" | "value">> = {},
): SetCookie {
  return {
    name,
    value,
    httpOnly: false,
    secure: false,
    sameSite: undefined,
    path: undefined,
    maxAge: undefined,
    expires: undefined,
    ...attributes,
  };
}

function exchange({
  method,
  cookies = sessionCookies,
  answers,
  renewal,
  resendable = true,
}: {
  method: string | undefined;
  cookies?: string;
  answers: readonly Answer[];
  renewal?: AntiforgeryRenewal<Answer>;
  resendable?: boolean;
}) {
  const attempts: AntiforgeryAttempt[] = [];
  const pending = [...answers];
  let renewals = 0;
  const sent = sendWithAntiforgery<Answer>({
    method,
    cookies,
    send: (attempt) => {
      attempts.push(attempt);
      const answer = pending.shift();
      return answer === undefined
        ? Promise.reject(new Error("sent more often than expected"))
        : Promise.resolve(answer);
    },
    isRefusal: (answer) => isAntiforgeryRefusal(answer.status, answer.body),
    renew: resendable
      ? () => {
          renewals += 1;
          return Promise.resolve(renewal ?? { outcome: "failed" });
        }
      : undefined,
  });
  return { sent, attempts, renewals: () => renewals };
}

test("antiforgeryRenewalPath_UnderTheApiBasePath_IsTheApiRenewalRoute", () => {
  assert.equal(antiforgeryRenewalPath, "/api/auth/antiforgery");
});

test("isUnsafeMethod_MethodsThatChangeData_AreUnsafeInAnyCase", () => {
  for (const method of ["POST", "PUT", "PATCH", "DELETE", "post", "Patch", "dElEtE"]) {
    assert.equal(isUnsafeMethod(method), true, method);
  }
});

test("isUnsafeMethod_SafeOrMissingMethod_IsSafe", () => {
  for (const method of [undefined, "GET", "get", "HEAD", "OPTIONS", "TRACE", "", "POSTS", " POST"]) {
    assert.equal(isUnsafeMethod(method), false, String(method));
  }
});

test("readCookie_CookieAmongOthers_ReturnsItsValue", () => {
  assert.equal(
    readCookie(`theme=dark; ${requestTokenCookieName}=CfDJ8token; lang=en`, requestTokenCookieName),
    "CfDJ8token",
  );
  assert.equal(
    readCookie(`theme=dark;${requestTokenCookieName}=CfDJ8token`, requestTokenCookieName),
    "CfDJ8token",
  );
});

test("readCookie_NameThatOnlyContainsTheCookieName_IsNotTheCookie", () => {
  const cookies = `${requestTokenCookieName}-old=stale; x${requestTokenCookieName}=other; ${requestTokenCookieName.toLowerCase()}=lower`;

  assert.equal(readCookie(cookies, requestTokenCookieName), undefined);
});

test("readCookie_ValueContainingEquals_KeepsEverythingAfterTheFirstEquals", () => {
  assert.equal(readCookie(`${requestTokenCookieName}=CfDJ8a==b=`, requestTokenCookieName), "CfDJ8a==b=");
});

test("readCookie_WhitespaceAroundTheNameAndTheValue_IsIgnored", () => {
  assert.equal(
    readCookie(`  ${requestTokenCookieName} =  CfDJ8token  ; lang=en`, requestTokenCookieName),
    "CfDJ8token",
  );
});

test("readCookie_EscapedValue_IsUnescaped", () => {
  assert.equal(readCookie(`${requestTokenCookieName}=a%2Bb%2F%3D%20c`, requestTokenCookieName), "a+b/= c");
});

test("readCookie_EmptyValueOrNoEquals_IsAbsent", () => {
  for (const cookies of [
    `${requestTokenCookieName}=; lang=en`,
    `${requestTokenCookieName}`,
    `lang=en; ${requestTokenCookieName}=`,
  ]) {
    assert.equal(readCookie(cookies, requestTokenCookieName), undefined, cookies);
  }
});

test("readCookie_ValueThatCannotBeUnescaped_IsAbsent", () => {
  assert.equal(readCookie(`${requestTokenCookieName}=%E0%A4%A`, requestTokenCookieName), undefined);
});

test("readCookie_NoCookies_IsAbsent", () => {
  for (const cookies of [null, undefined, "", " ; ; "]) {
    assert.equal(readCookie(cookies, requestTokenCookieName), undefined, String(cookies));
  }
});

test("readCookie_RepeatedName_ReturnsTheFirst", () => {
  assert.equal(
    readCookie(`${requestTokenCookieName}=first; ${requestTokenCookieName}=second`, requestTokenCookieName),
    "first",
  );
});

test("isAntiforgeryRefusal_BadRequestWithAnAntiforgeryCode_IsARefusal", () => {
  for (const code of ["antiforgery.token-missing", "antiforgery.token-invalid"]) {
    assert.equal(isAntiforgeryRefusal(400, { status: 400, code, title: "Refused." }), true, code);
  }
});

test("isAntiforgeryRefusal_BadRequestWithAnotherCodeOrNoCode_IsNotARefusal", () => {
  const bodies: unknown[] = [
    { code: "request.invalid" },
    { code: "idempotency.key-missing" },
    { code: "antiforgery" },
    { code: "x-antiforgery.token-missing" },
    { code: "Antiforgery.token-missing" },
    { code: 400 },
    { type: "/problems/antiforgery.token-missing" },
    {},
    null,
    undefined,
    "antiforgery.token-missing",
  ];

  for (const body of bodies) {
    assert.equal(isAntiforgeryRefusal(400, body), false, JSON.stringify(body));
  }
});

test("isAntiforgeryRefusal_AntiforgeryCodeWithAnotherStatus_IsNotARefusal", () => {
  for (const status of [0, 200, 401, 403, 404, 409, 422, 500]) {
    assert.equal(isAntiforgeryRefusal(status, { code: "antiforgery.token-missing" }), false, String(status));
  }
});

test("parseSetCookie_RequestTokenWrittenByAspNetCore_ReadsItsAttributes", () => {
  assert.deepEqual(
    parseSetCookie(`${requestTokenCookieName}=CfDJ8request; path=/; secure; samesite=lax`),
    setCookie(requestTokenCookieName, "CfDJ8request", { secure: true, sameSite: "lax", path: "/" }),
  );
});

test("parseSetCookie_CookieTokenWrittenByAspNetCore_IsHttpOnlyAndStrict", () => {
  assert.deepEqual(
    parseSetCookie(`${antiforgeryCookieName}=CfDJ8cookie; path=/; secure; samesite=strict; httponly`),
    setCookie(antiforgeryCookieName, "CfDJ8cookie", {
      httpOnly: true,
      secure: true,
      sameSite: "strict",
      path: "/",
    }),
  );
});

test("parseSetCookie_AttributeNamesAndSameSiteValues_AreCaseInsensitive", () => {
  assert.deepEqual(
    parseSetCookie("theme=dark; Path=/settings; SECURE; HttpOnly; SameSite=None"),
    setCookie("theme", "dark", { httpOnly: true, secure: true, sameSite: "none", path: "/settings" }),
  );
});

test("parseSetCookie_MaxAgeAndExpires_AreRead", () => {
  assert.deepEqual(
    parseSetCookie("theme=dark; Max-Age=3600; Expires=Fri, 01 Jan 2100 00:00:00 GMT"),
    setCookie("theme", "dark", { maxAge: 3600, expires: new Date(Date.UTC(2100, 0, 1)) }),
  );
  assert.equal(parseSetCookie("theme=dark; max-age=-1")?.maxAge, -1);
});

test("parseSetCookie_CookieDeletedByAspNetCore_HasAnEmptyValueAndAPastExpiry", () => {
  assert.deepEqual(
    parseSetCookie(
      `${requestTokenCookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; secure; samesite=lax`,
    ),
    setCookie(requestTokenCookieName, "", { secure: true, sameSite: "lax", path: "/", expires: new Date(0) }),
  );
});

test("parseSetCookie_AttributeValuesThatDoNotParse_AreIgnored", () => {
  assert.deepEqual(
    parseSetCookie(
      "theme=dark; Max-Age=soon; Max-Age=1.5; Max-Age=+60; Expires=never; SameSite=sometimes; Path=settings",
    ),
    setCookie("theme", "dark"),
  );
});

test("parseSetCookie_RepeatedAttribute_KeepsTheLastValidOne", () => {
  assert.deepEqual(
    parseSetCookie(
      "theme=dark; Path=/one; Path=/two; Max-Age=60; Max-Age=later; SameSite=Strict; SameSite=Lax",
    ),
    setCookie("theme", "dark", { path: "/two", maxAge: 60, sameSite: "lax" }),
  );
  assert.equal(parseSetCookie("theme=dark; SameSite=Strict; SameSite=sometimes")?.sameSite, undefined);
  assert.equal(parseSetCookie("theme=dark; Path=/one; Path=relative")?.path, undefined);
});

test("parseSetCookie_EscapedValueContainingEquals_IsUnescapedWhole", () => {
  assert.equal(parseSetCookie(`${requestTokenCookieName}=CfDJ8%2Ba==; path=/`)?.value, "CfDJ8+a==");
});

test("parseSetCookie_AttributesTheWebAppDoesNotRelay_AreLeftOut", () => {
  assert.deepEqual(
    parseSetCookie("theme=dark; Domain=api.internal; Partitioned; Priority=High"),
    setCookie("theme", "dark"),
  );
});

test("parseSetCookie_WithoutANameOrAnEquals_IsIgnored", () => {
  for (const header of ["dark", "=dark", " =dark; path=/", "", "; path=/"]) {
    assert.equal(parseSetCookie(header), undefined, header);
  }
});

test("parseSetCookie_ValueThatCannotBeUnescaped_IsIgnored", () => {
  assert.equal(parseSetCookie(`${requestTokenCookieName}=%E0%A4%A; path=/`), undefined);
});

test("mergeCookies_RenewedCookies_ReplaceTheCookiesOfTheirNameAndKeepTheRest", () => {
  const merged = mergeCookies(
    `__Host-erp-session=chunks-2; ${requestTokenCookieName}=old; theme=dark; ${antiforgeryCookieName}=old-cookie`,
    [
      { name: requestTokenCookieName, value: "renewed" },
      { name: antiforgeryCookieName, value: "renewed-cookie" },
    ],
  );

  assert.equal(
    merged,
    `__Host-erp-session=chunks-2; ${requestTokenCookieName}=renewed; theme=dark; ${antiforgeryCookieName}=renewed-cookie`,
  );
});

test("mergeCookies_CookieTheHeaderDoesNotCarry_IsAppended", () => {
  assert.equal(
    mergeCookies("__Host-erp-session=chunks-2", [{ name: requestTokenCookieName, value: "renewed" }]),
    `__Host-erp-session=chunks-2; ${requestTokenCookieName}=renewed`,
  );
  assert.equal(
    mergeCookies(`${requestTokenCookieName}-old=stale`, [{ name: requestTokenCookieName, value: "renewed" }]),
    `${requestTokenCookieName}-old=stale; ${requestTokenCookieName}=renewed`,
  );
});

test("mergeCookies_NoHeader_IsTheRenewedCookies", () => {
  for (const cookies of [null, undefined, ""]) {
    assert.equal(
      mergeCookies(cookies, [
        { name: requestTokenCookieName, value: "renewed" },
        { name: antiforgeryCookieName, value: "renewed-cookie" },
      ]),
      `${requestTokenCookieName}=renewed; ${antiforgeryCookieName}=renewed-cookie`,
      String(cookies),
    );
  }
});

test("mergeCookies_NothingRenewed_KeepsTheHeader", () => {
  assert.equal(mergeCookies("theme=dark; lang=en", []), "theme=dark; lang=en");
});

test("mergeCookies_RepeatedNameInTheHeader_KeepsOnlyTheRenewedValue", () => {
  assert.equal(
    mergeCookies(`${requestTokenCookieName}=first; theme=dark; ${requestTokenCookieName}=second`, [
      { name: requestTokenCookieName, value: "renewed" },
    ]),
    `${requestTokenCookieName}=renewed; theme=dark`,
  );
});

test("mergeCookies_CookieRenewedTwice_KeepsTheLastValue", () => {
  assert.equal(
    mergeCookies("theme=dark", [
      { name: requestTokenCookieName, value: "first" },
      { name: requestTokenCookieName, value: "second" },
    ]),
    `theme=dark; ${requestTokenCookieName}=second`,
  );
});

test("mergeCookies_ValueThatNeedsEscaping_IsEscapedAndReadsBackUnchanged", () => {
  const merged = mergeCookies("theme=dark", [{ name: requestTokenCookieName, value: "a+b/=; c" }]);

  assert.equal(merged, `theme=dark; ${requestTokenCookieName}=a%2Bb%2F%3D%3B%20c`);
  assert.equal(readCookie(merged, requestTokenCookieName), "a+b/=; c");
});

test("sendWithAntiforgery_SafeMethod_SendsOnceWithoutATokenAndNeverRenews", async () => {
  for (const method of [undefined, "GET", "head"]) {
    const { sent, attempts, renewals } = exchange({ method, answers: [refusal] });

    assert.equal(await sent, refusal, String(method));
    assert.deepEqual(attempts, [{ cookies: sessionCookies, token: undefined }], String(method));
    assert.equal(renewals(), 0, String(method));
  }
});

test("sendWithAntiforgery_UnsafeMethod_SendsTheRequestTokenOfTheCookies", async () => {
  for (const method of ["POST", "PUT", "PATCH", "DELETE"]) {
    const { sent, attempts, renewals } = exchange({ method, answers: [created] });

    assert.equal(await sent, created, method);
    assert.deepEqual(attempts, [{ cookies: sessionCookies, token: "first-token" }], method);
    assert.equal(renewals(), 0, method);
  }
});

test("sendWithAntiforgery_UnsafeMethodWithoutTheRequestTokenCookie_SendsWithoutAToken", async () => {
  const { sent, attempts } = exchange({ method: "POST", cookies: "theme=dark", answers: [created] });

  assert.equal(await sent, created);
  assert.deepEqual(attempts, [{ cookies: "theme=dark", token: undefined }]);
});

test("sendWithAntiforgery_AnswerThatIsNotAnAntiforgeryRefusal_IsReturnedWithoutRenewing", async () => {
  const answers: readonly Answer[] = [
    { status: 400, body: { status: 400, code: "request.invalid" } },
    { status: 415, body: { status: 415, code: "attachment.unsupported-type" } },
    signedOut,
    { status: 500 },
  ];

  for (const answer of answers) {
    const { sent, attempts, renewals } = exchange({ method: "POST", answers: [answer] });

    assert.equal(await sent, answer, String(answer.status));
    assert.equal(attempts.length, 1, String(answer.status));
    assert.equal(renewals(), 0, String(answer.status));
  }
});

test("sendWithAntiforgery_Refusal_RenewsOnceAndSendsOnceMoreWithTheRenewedToken", async () => {
  const { sent, attempts, renewals } = exchange({
    method: "POST",
    answers: [refusal, created],
    renewal: { outcome: "renewed", cookies: renewedCookies },
  });

  assert.equal(await sent, created);
  assert.equal(renewals(), 1);
  assert.deepEqual(attempts, [
    { cookies: sessionCookies, token: "first-token" },
    { cookies: renewedCookies, token: "renewed-token" },
  ]);
});

test("sendWithAntiforgery_RenewalWithoutARequestTokenCookie_SendsOnceMoreWithoutAToken", async () => {
  const { sent, attempts } = exchange({
    method: "POST",
    answers: [refusal, refusal],
    renewal: { outcome: "renewed", cookies: "__Host-erp-session=chunks-2" },
  });

  assert.equal(await sent, refusal);
  assert.deepEqual(attempts[1], { cookies: "__Host-erp-session=chunks-2", token: undefined });
});

test("sendWithAntiforgery_RefusedAgainAfterTheRenewal_ReturnsTheSecondRefusalWithoutRenewingAgain", async () => {
  const second: Answer = { status: 400, body: { status: 400, code: "antiforgery.token-invalid" } };
  const { sent, attempts, renewals } = exchange({
    method: "POST",
    answers: [refusal, second],
    renewal: { outcome: "renewed", cookies: renewedCookies },
  });

  assert.equal(await sent, second);
  assert.equal(attempts.length, 2);
  assert.equal(renewals(), 1);
});

test("sendWithAntiforgery_RenewalAnswersUnauthenticated_ReturnsThatAnswerWithoutSendingAgain", async () => {
  const { sent, attempts, renewals } = exchange({
    method: "POST",
    answers: [refusal],
    renewal: { outcome: "unauthenticated", answer: signedOut },
  });

  assert.equal(await sent, signedOut);
  assert.equal(attempts.length, 1);
  assert.equal(renewals(), 1);
});

test("sendWithAntiforgery_RenewalFails_ReturnsTheOriginalRefusalWithoutSendingAgain", async () => {
  const { sent, attempts, renewals } = exchange({
    method: "POST",
    answers: [refusal],
    renewal: { outcome: "failed" },
  });

  assert.equal(await sent, refusal);
  assert.equal(attempts.length, 1);
  assert.equal(renewals(), 1);
});

test("sendWithAntiforgery_RequestThatCannotBeSentAgain_ReturnsTheRefusalWithoutRenewing", async () => {
  const { sent, attempts, renewals } = exchange({ method: "POST", answers: [refusal], resendable: false });

  assert.equal(await sent, refusal);
  assert.equal(attempts.length, 1);
  assert.equal(renewals(), 0);
});

test("sendWithAntiforgery_RefusalCheckThatReadsTheBodyLater_IsAwaited", async () => {
  const attempts: AntiforgeryAttempt[] = [];
  const answers = [refusal, created];

  const answer = await sendWithAntiforgery<Answer>({
    method: "DELETE",
    cookies: sessionCookies,
    send: (attempt) => {
      attempts.push(attempt);
      return Promise.resolve(answers[attempts.length - 1] ?? created);
    },
    isRefusal: (candidate) => Promise.resolve(isAntiforgeryRefusal(candidate.status, candidate.body)),
    renew: () => Promise.resolve({ outcome: "renewed", cookies: renewedCookies }),
  });

  assert.equal(answer, created);
  assert.equal(attempts.length, 2);
});

test("sendWithAntiforgery_SendThatRejects_RejectsWithoutRenewing", async () => {
  const failure = new Error("network down");
  let renewals = 0;

  await assert.rejects(
    sendWithAntiforgery<Answer>({
      method: "POST",
      cookies: sessionCookies,
      send: () => Promise.reject(failure),
      isRefusal: () => true,
      renew: () => {
        renewals += 1;
        return Promise.resolve({ outcome: "failed" });
      },
    }),
    (thrown) => thrown === failure,
  );
  assert.equal(renewals, 0);
});

test("antiforgeryNames_SharedWithTheApi_AreTheNamesTheApiReads", () => {
  assert.equal(antiforgeryHeaderName, "X-XSRF-TOKEN");
  assert.equal(requestTokenCookieName, "__Host-erp-xsrf");
});

type Fetched = { readonly url: string; readonly init: RequestInit };

const apiUrl = "http://api.internal/api/platform/attachments/0199a4c2-7d1e-7c3a-9f1b-2b6d4e8a1c00";

const renewalUrl = "http://api.internal/api/auth/antiforgery";

const forwardedCookie = `__Host-erp-session=session; ${requestTokenCookieName}=first-token; ${antiforgeryCookieName}=first-cookie-token`;

function problemResponse(status: number, code: string): Response {
  return new Response(JSON.stringify({ status, type: `/problems/${code}`, code }), {
    status,
    headers: { "content-type": "application/problem+json" },
  });
}

function renewedPair(): Response {
  return new Response(null, {
    status: 204,
    headers: [
      ["set-cookie", `${requestTokenCookieName}=renewed-token; path=/; secure; samesite=lax`],
      [
        "set-cookie",
        `${antiforgeryCookieName}=renewed-cookie-token; path=/; secure; samesite=strict; httponly`,
      ],
    ],
  });
}

function apiFetch(answers: readonly (Response | Error)[]) {
  const fetched: Fetched[] = [];
  const relayed: SetCookie[][] = [];
  const pending = [...answers];
  const send = antiforgeryFetch({
    fetch: (url, init) => {
      fetched.push({ url, init });
      const answer = pending.shift();
      if (answer === undefined) return Promise.reject(new Error("fetched more often than expected"));
      return answer instanceof Error ? Promise.reject(answer) : Promise.resolve(answer);
    },
    renewalUrl,
    renewalHeaders: () => Promise.resolve({ cookie: forwardedCookie, traceparent: "00-renewal" }),
    relay: (renewed) => {
      relayed.push([...renewed]);
      return Promise.resolve();
    },
  });
  return { send, fetched, relayed };
}

function headerOf({ init }: Fetched, name: string): string | null {
  return new Headers(init.headers).get(name);
}

test("antiforgeryFetch_CallThatChangesData_SendsTheRequestTokenOfTheForwardedCookie", async () => {
  for (const method of ["POST", "PUT", "PATCH", "DELETE"]) {
    const { send, fetched } = apiFetch([new Response(null, { status: 204 })]);

    const response = await send(apiUrl, {
      method,
      headers: { cookie: forwardedCookie, traceparent: "00-call" },
      body: "{}",
    });

    assert.equal(response.status, 204, method);
    assert.equal(fetched.length, 1, method);
    assert.equal(fetched[0]?.url, apiUrl, method);
    assert.equal(headerOf(fetched[0]!, "x-xsrf-token"), "first-token", method);
    assert.equal(headerOf(fetched[0]!, "cookie"), forwardedCookie, method);
    assert.equal(headerOf(fetched[0]!, "traceparent"), "00-call", method);
    assert.equal(fetched[0]?.init.body, "{}", method);
  }
});

test("antiforgeryFetch_SafeCall_SendsNoRequestToken", async () => {
  for (const method of ["GET", "HEAD", undefined]) {
    const { send, fetched } = apiFetch([new Response("[]", { status: 200 })]);

    const headers = { cookie: forwardedCookie };
    await send(apiUrl, method === undefined ? { headers } : { method, headers });

    assert.equal(headerOf(fetched[0]!, "x-xsrf-token"), null, String(method));
  }
});

test("antiforgeryFetch_CallWithoutTheRequestTokenCookie_SendsNoToken", async () => {
  const { send, fetched } = apiFetch([new Response(null, { status: 204 })]);

  await send(apiUrl, { method: "DELETE", headers: { cookie: "__Host-erp-session=session" } });

  assert.equal(headerOf(fetched[0]!, "x-xsrf-token"), null);
});

test("antiforgeryFetch_AntiforgeryRefusal_RenewsOnceRelaysThePairAndSendsOnceMoreWithIt", async () => {
  const { send, fetched, relayed } = apiFetch([
    problemResponse(400, "antiforgery.token-invalid"),
    renewedPair(),
    new Response(null, { status: 204 }),
  ]);

  const response = await send(apiUrl, { method: "DELETE", headers: { cookie: forwardedCookie } });

  assert.equal(response.status, 204);
  assert.equal(fetched.length, 3);
  assert.equal(fetched[1]?.url, renewalUrl);
  assert.equal(fetched[1]?.init.method, undefined);
  assert.equal(headerOf(fetched[1]!, "cookie"), forwardedCookie);
  assert.equal(headerOf(fetched[1]!, "traceparent"), "00-renewal");
  assert.equal(headerOf(fetched[1]!, "accept"), "application/json");
  assert.deepEqual(relayed, [
    [
      setCookie(requestTokenCookieName, "renewed-token", { secure: true, sameSite: "lax", path: "/" }),
      setCookie(antiforgeryCookieName, "renewed-cookie-token", {
        httpOnly: true,
        secure: true,
        sameSite: "strict",
        path: "/",
      }),
    ],
  ]);
  assert.equal(fetched[2]?.url, apiUrl);
  assert.equal(fetched[2]?.init.method, "DELETE");
  assert.equal(headerOf(fetched[2]!, "x-xsrf-token"), "renewed-token");
  assert.equal(
    headerOf(fetched[2]!, "cookie"),
    `__Host-erp-session=session; ${requestTokenCookieName}=renewed-token; ${antiforgeryCookieName}=renewed-cookie-token`,
  );
});

test("antiforgeryFetch_RefusalWhoseRenewalAnswersUnauthenticated_ReturnsThatAnswerWithoutRelaying", async () => {
  const { send, fetched, relayed } = apiFetch([
    problemResponse(400, "antiforgery.token-missing"),
    problemResponse(401, "request.unauthenticated"),
  ]);

  const response = await send(apiUrl, { method: "POST", headers: { cookie: forwardedCookie }, body: "{}" });

  assert.equal(response.status, 401);
  assert.equal(fetched.length, 2);
  assert.deepEqual(relayed, []);
});

test("antiforgeryFetch_RefusalWhoseRenewalFails_ReturnsTheRefusalWithItsBodyStillReadable", async () => {
  for (const renewal of [new Response("unavailable", { status: 503 }), new Error("connect ECONNREFUSED")]) {
    const { send, fetched, relayed } = apiFetch([problemResponse(400, "antiforgery.token-missing"), renewal]);

    const response = await send(apiUrl, { method: "POST", headers: { cookie: forwardedCookie }, body: "{}" });

    assert.equal(response.status, 400);
    assert.deepEqual(await response.json(), {
      status: 400,
      type: "/problems/antiforgery.token-missing",
      code: "antiforgery.token-missing",
    });
    assert.equal(fetched.length, 2);
    assert.deepEqual(relayed, []);
  }
});

test("antiforgeryFetch_OtherBadRequest_IsReturnedWithoutRenewing", async () => {
  const { send, fetched } = apiFetch([problemResponse(400, "request.invalid")]);

  const response = await send(apiUrl, { method: "POST", headers: { cookie: forwardedCookie }, body: "{}" });

  assert.equal(response.status, 400);
  assert.equal(((await response.json()) as { code: string }).code, "request.invalid");
  assert.equal(fetched.length, 1);
});

test("antiforgeryFetch_RefusalOfAStreamedBody_IsReturnedWithoutRenewing", async () => {
  const { send, fetched } = apiFetch([problemResponse(400, "antiforgery.token-missing")]);
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(new TextEncoder().encode("{}"));
      controller.close();
    },
  });

  const response = await send(apiUrl, { method: "POST", headers: { cookie: forwardedCookie }, body });

  assert.equal(response.status, 400);
  assert.equal(fetched.length, 1);
});
