import assert from "node:assert/strict";
import { test } from "node:test";

import {
  localReturnPath,
  loginHref,
  maxReturnPathLength,
  signInHref,
  signInReason,
} from "./sign-in-addresses.ts";

test("localReturnPath_PathOnThisSite_ReturnsItUnchanged", () => {
  for (const path of [
    "/",
    "/platform/attachments",
    "/platform/attachments?page=2&sort=fileName:asc",
    "/a/b#c",
  ]) {
    assert.equal(localReturnPath(path), path);
  }
});

test("localReturnPath_AddressOutsideThisSite_ReturnsUndefined", () => {
  for (const value of [
    "https://example.com",
    "//example.com",
    "/\\example.com",
    "~/platform/attachments",
    "platform/attachments",
    "javascript:alert(1)",
  ]) {
    assert.equal(localReturnPath(value), undefined, value);
  }
});

test("localReturnPath_CharacterOutsideVisibleAscii_ReturnsUndefined", () => {
  for (const value of [
    "/platform attachments",
    "/platform\tattachments",
    "/platform\nattachments",
    "/ünicode",
    "/\u007f",
  ]) {
    assert.equal(localReturnPath(value), undefined, JSON.stringify(value));
  }
});

test("localReturnPath_LongestAcceptedPath_IsKeptAndOneMoreCharacterIsRefused", () => {
  const longest = `/${"a".repeat(maxReturnPathLength - 1)}`;

  assert.equal(localReturnPath(longest), longest);
  assert.equal(localReturnPath(`${longest}a`), undefined);
});

test("localReturnPath_MissingEmptyOrRepeatedValue_ReturnsUndefined", () => {
  assert.equal(localReturnPath(undefined), undefined);
  assert.equal(localReturnPath(null), undefined);
  assert.equal(localReturnPath(""), undefined);
  assert.equal(localReturnPath(["/platform/attachments", "/platform/system-info"]), undefined);
  assert.equal(localReturnPath(["/platform/attachments"]), "/platform/attachments");
});

test("signInReason_KnownReason_ReturnsIt", () => {
  assert.equal(signInReason("session-ended"), "session-ended");
  assert.equal(signInReason("signed-out"), "signed-out");
  assert.equal(signInReason(["signed-out"]), "signed-out");
});

test("signInReason_AnyOtherValue_ReturnsUndefined", () => {
  for (const value of [
    undefined,
    "",
    "Your account is locked",
    "SIGNED-OUT",
    ["signed-out", "session-ended"],
  ]) {
    assert.equal(signInReason(value), undefined, JSON.stringify(value));
  }
});

test("signInHref_LocalPath_StartsTheApiSignInWithItEncoded", () => {
  assert.equal(
    signInHref("/platform/attachments?page=2&sort=fileName:asc"),
    "/api/auth/login?returnUrl=%2Fplatform%2Fattachments%3Fpage%3D2%26sort%3DfileName%3Aasc",
  );
});

test("signInHref_MissingOrForeignPath_ReturnsToTheHomePage", () => {
  assert.equal(signInHref(undefined), "/api/auth/login?returnUrl=%2F");
  assert.equal(signInHref("https://example.com"), "/api/auth/login?returnUrl=%2F");
});

test("loginHref_PathAndReason_PutsBothOnTheSignInPage", () => {
  assert.equal(
    loginHref("/platform/attachments?page=2", "session-ended"),
    "/login?returnUrl=%2Fplatform%2Fattachments%3Fpage%3D2&reason=session-ended",
  );
});

test("loginHref_HomePageOrForeignPath_LeavesTheReturnAddressOut", () => {
  assert.equal(loginHref("/"), "/login");
  assert.equal(loginHref(undefined, "signed-out"), "/login?reason=signed-out");
  assert.equal(loginHref("//example.com", "session-ended"), "/login?reason=session-ended");
});
