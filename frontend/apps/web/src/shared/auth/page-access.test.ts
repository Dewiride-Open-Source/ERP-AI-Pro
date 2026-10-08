import assert from "node:assert/strict";
import { test } from "node:test";

import { loginAddressWithoutForeignReturn, signInRedirectFor, type PageRequest } from "./page-access.ts";

const anonymousVisit = (pathname: string, search = "", method = "GET"): PageRequest => ({
  method,
  pathname,
  search,
  hasSessionCookie: false,
});

test("signInRedirectFor_PageWithoutSessionCookie_SendsItToTheSignInPageWithThePage", () => {
  assert.equal(
    signInRedirectFor(anonymousVisit("/platform/attachments", "?page=2")),
    "/login?returnUrl=%2Fplatform%2Fattachments%3Fpage%3D2",
  );
  assert.equal(
    signInRedirectFor(anonymousVisit("/design/kitchen-sink", "", "HEAD")),
    "/login?returnUrl=%2Fdesign%2Fkitchen-sink",
  );
});

test("signInRedirectFor_HomePageWithoutSessionCookie_SendsItToThePlainSignInPage", () => {
  assert.equal(signInRedirectFor(anonymousVisit("/")), "/login");
});

test("signInRedirectFor_PageWithSessionCookie_LetsItThrough", () => {
  assert.equal(
    signInRedirectFor({ ...anonymousVisit("/platform/attachments"), hasSessionCookie: true }),
    undefined,
  );
});

test("signInRedirectFor_SignInPage_LetsItThrough", () => {
  assert.equal(signInRedirectFor(anonymousVisit("/login", "?reason=signed-out")), undefined);
});

test("signInRedirectFor_ServerFunctionCall_LetsItThrough", () => {
  assert.equal(signInRedirectFor(anonymousVisit("/platform/attachments", "", "POST")), undefined);
});

test("signInRedirectFor_FrameworkRequest_LetsItThrough", () => {
  assert.equal(signInRedirectFor(anonymousVisit("/_next/webpack-hmr")), undefined);
  assert.equal(signInRedirectFor(anonymousVisit("/__nextjs_original-stack-frames")), undefined);
});

test("signInRedirectFor_PathTheApiWouldRefuseToReturnTo_SendsItToThePlainSignInPage", () => {
  assert.equal(signInRedirectFor(anonymousVisit("//example.com/platform")), "/login");
});

test("loginAddressWithoutForeignReturn_ForeignReturnAddress_DropsOnlyThatParameter", () => {
  assert.equal(
    loginAddressWithoutForeignReturn(
      new URLSearchParams("returnUrl=https%3A%2F%2Fexample.com&reason=session-ended"),
    ),
    "/login?reason=session-ended",
  );
  assert.equal(
    loginAddressWithoutForeignReturn(new URLSearchParams("returnUrl=%2F%2Fexample.com")),
    "/login",
  );
});

test("loginAddressWithoutForeignReturn_RepeatedReturnAddress_DropsIt", () => {
  assert.equal(
    loginAddressWithoutForeignReturn(new URLSearchParams("returnUrl=%2Fa&returnUrl=%2Fb")),
    "/login",
  );
});

test("loginAddressWithoutForeignReturn_LocalOrMissingReturnAddress_KeepsTheAddress", () => {
  assert.equal(
    loginAddressWithoutForeignReturn(new URLSearchParams("returnUrl=%2Fplatform%2Fattachments")),
    undefined,
  );
  assert.equal(loginAddressWithoutForeignReturn(new URLSearchParams("error=sign-in-failed")), undefined);
});
