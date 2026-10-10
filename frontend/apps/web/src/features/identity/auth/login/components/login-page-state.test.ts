import assert from "node:assert/strict";
import { test } from "node:test";

import { loginPageState, loginPageTitle } from "./login-page-state.ts";

test("loginPageState_FailedSignIn_IsTheFailureEvenWithAReason", () => {
  assert.equal(loginPageState({ error: "sign-in-failed" }), "sign-in-failed");
  assert.equal(loginPageState({ error: "sign-in-failed", reason: "signed-out" }), "sign-in-failed");
});

test("loginPageState_DeactivatedAccount_IsTheRefusalEvenWithAReason", () => {
  assert.equal(loginPageState({ error: "account-deactivated" }), "account-deactivated");
  assert.equal(
    loginPageState({ error: "account-deactivated", reason: "session-ended" }),
    "account-deactivated",
  );
});

test("loginPageState_KnownReason_IsThatReason", () => {
  assert.equal(
    loginPageState({ reason: "session-ended", returnUrl: "/platform/attachments" }),
    "session-ended",
  );
  assert.equal(loginPageState({ reason: "signed-out" }), "signed-out");
});

test("loginPageState_AnyOtherValue_IsNoState", () => {
  assert.equal(loginPageState({}), undefined);
  assert.equal(loginPageState({ error: "Your account is locked", reason: "Call 555-0100" }), undefined);
  assert.equal(loginPageState({ error: ["sign-in-failed", "sign-in-failed"] }), undefined);
  assert.equal(loginPageState({ reason: ["signed-out", "session-ended"] }), undefined);
});

test("loginPageTitle_EachState_NamesItAndOtherwiseSaysSignIn", () => {
  assert.equal(loginPageTitle({ error: "sign-in-failed" }), "Sign-in failed");
  assert.equal(loginPageTitle({ error: "account-deactivated" }), "Account deactivated");
  assert.equal(loginPageTitle({ reason: "session-ended" }), "Session ended");
  assert.equal(loginPageTitle({ reason: "signed-out" }), "Signed out");
  assert.equal(loginPageTitle({ returnUrl: "/design/form-kit" }), "Sign in");
});
