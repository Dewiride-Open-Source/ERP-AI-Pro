import assert from "node:assert/strict";
import { test } from "node:test";

import { sidebarStartsOpen } from "./sidebar-state.ts";

test("sidebarStartsOpen_NoCookie_StartsOpen", () => {
  assert.equal(sidebarStartsOpen(undefined), true);
});

test("sidebarStartsOpen_CookieTheSidebarWrote_FollowsIt", () => {
  assert.equal(sidebarStartsOpen("true"), true);
  assert.equal(sidebarStartsOpen("false"), false);
});

test("sidebarStartsOpen_AnyOtherValue_StartsOpen", () => {
  assert.equal(sidebarStartsOpen(""), true);
  assert.equal(sidebarStartsOpen("FALSE"), true);
});
