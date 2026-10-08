import assert from "node:assert/strict";
import { test } from "node:test";

import { contentSecurityPolicy, isSecureRequest } from "./content-security-policy.ts";

const nonce = "MWY5ZDZlNWEtNGQ3Yy00YjZlLWE3ZTYtOTBiMzhkNmZkMmM1";

const directives = (policy: string) =>
  new Map(
    policy.split("; ").map((directive) => {
      const [name = "", ...sources] = directive.split(" ");
      return [name, sources] as const;
    }),
  );

test("contentSecurityPolicy_ProductionOverPlainHttp_ReturnsEveryDirectiveInOrder", () => {
  assert.equal(
    contentSecurityPolicy({ nonce, secure: false, development: false }),
    [
      "default-src 'self'",
      `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'`,
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' blob: data:",
      "font-src 'self' data:",
      "connect-src 'self'",
      "object-src 'none'",
      "base-uri 'self'",
      "form-action 'self' https://login.microsoftonline.com",
      "frame-ancestors 'none'",
    ].join("; "),
  );
});

test("contentSecurityPolicy_Production_OmitsUnsafeEval", () => {
  const policy = contentSecurityPolicy({ nonce, secure: true, development: false });

  assert.deepEqual(directives(policy).get("script-src"), ["'self'", `'nonce-${nonce}'`, "'strict-dynamic'"]);
  assert.doesNotMatch(policy, /'unsafe-eval'/);
});

test("contentSecurityPolicy_Development_AllowsUnsafeEvalInScriptsOnly", () => {
  const policy = directives(contentSecurityPolicy({ nonce, secure: false, development: true }));

  assert.deepEqual(policy.get("script-src"), [
    "'self'",
    `'nonce-${nonce}'`,
    "'strict-dynamic'",
    "'unsafe-eval'",
  ]);
  for (const [name, sources] of policy) {
    if (name !== "script-src") assert.ok(!sources.includes("'unsafe-eval'"), name);
  }
});

test("contentSecurityPolicy_SecureRequest_UpgradesInsecureRequests", () => {
  const policy = directives(contentSecurityPolicy({ nonce, secure: true, development: false }));

  assert.deepEqual(policy.get("upgrade-insecure-requests"), []);
  assert.equal([...policy.keys()].at(-1), "upgrade-insecure-requests");
});

test("contentSecurityPolicy_PlainHttpRequest_DoesNotUpgradeInsecureRequests", () => {
  for (const development of [false, true]) {
    const policy = directives(contentSecurityPolicy({ nonce, secure: false, development }));

    assert.equal(policy.has("upgrade-insecure-requests"), false);
  }
});

test("contentSecurityPolicy_DifferentNonces_AppearOnlyInTheirOwnPolicy", () => {
  const first = contentSecurityPolicy({ nonce: "Zmlyc3Q=", secure: false, development: false });
  const second = contentSecurityPolicy({ nonce: "c2Vjb25k", secure: false, development: false });

  assert.match(first, /'nonce-Zmlyc3Q='/);
  assert.doesNotMatch(first, /c2Vjb25k/);
  assert.match(second, /'nonce-c2Vjb25k'/);
  assert.doesNotMatch(second, /Zmlyc3Q=/);
});

test("contentSecurityPolicy_AnyRequest_DeniesFramingAndPluginsAndPostsFormsOnlyHereOrToMicrosoftSignIn", () => {
  const policy = directives(contentSecurityPolicy({ nonce, secure: false, development: true }));

  assert.deepEqual(policy.get("frame-ancestors"), ["'none'"]);
  assert.deepEqual(policy.get("object-src"), ["'none'"]);
  assert.deepEqual(policy.get("base-uri"), ["'self'"]);
  assert.deepEqual(policy.get("form-action"), ["'self'", "https://login.microsoftonline.com"]);
  assert.deepEqual(policy.get("connect-src"), ["'self'"]);
});

test("isSecureRequest_HttpsProtocol_ReturnsTrue", () => {
  assert.equal(isSecureRequest({ protocol: "https:" }, new Headers()), true);
});

test("isSecureRequest_EdgeProxyForwardedHttps_ReturnsTrue", () => {
  assert.equal(isSecureRequest({ protocol: "http:" }, new Headers({ "X-Forwarded-Proto": "https" })), true);
});

test("isSecureRequest_PlainHttp_ReturnsFalse", () => {
  assert.equal(isSecureRequest({ protocol: "http:" }, new Headers()), false);
});

test("isSecureRequest_ForwardedProtoOtherThanHttps_ReturnsFalse", () => {
  for (const forwardedProto of ["http", ""]) {
    const headers = new Headers({ "x-forwarded-proto": forwardedProto });

    assert.equal(isSecureRequest({ protocol: "http:" }, headers), false, forwardedProto);
  }
});
