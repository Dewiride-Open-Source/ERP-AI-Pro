import assert from "node:assert/strict";
import { test } from "node:test";

import { approvalPageLinks, kindsPage, requestBoxPages } from "./approval-pages.ts";

test("approvalPageLinks_APersonWhoSeesOnlyTheirOwnRequests_LeavesOutAllRequests", () => {
  assert.deepEqual(approvalPageLinks(false), [
    { key: "waiting", path: "/platform/approvals", title: "Waiting for me" },
    { key: "mine", path: "/platform/approvals/mine", title: "My requests" },
    { key: "decided", path: "/platform/approvals/decided", title: "Decided by me" },
    { key: "kinds", path: "/platform/approvals/kinds", title: "Kinds of request" },
  ]);
});

test("approvalPageLinks_APersonWhoSeesEveryRequest_EndsWithAllRequests", () => {
  const links = approvalPageLinks(true);

  assert.deepEqual(
    links.map((link) => link.title),
    ["Waiting for me", "My requests", "Decided by me", "Kinds of request", "All requests"],
  );
  assert.deepEqual(links.at(-1), { key: "all", path: "/platform/approvals/all", title: "All requests" });
});

test("requestBoxPages_EachBox_IsKeyedByItsOwnName", () => {
  for (const [box, page] of Object.entries(requestBoxPages)) {
    assert.equal(page.key, box);
  }
  assert.equal(kindsPage.key, "kinds");
});

test("requestBoxPages_EmptyBoxes_SayWhatWouldAppear", () => {
  assert.deepEqual(requestBoxPages.waiting.empty, {
    title: "Nothing waits for your decision.",
    description: "A request that waits for you, or for a role you hold, appears here.",
  });
  assert.deepEqual(requestBoxPages.mine.empty, {
    title: "You have not asked for approval yet.",
    description: "A request you ask for, or submit for someone else, appears here.",
  });
  assert.deepEqual(requestBoxPages.decided.empty, {
    title: "You have not decided a request yet.",
    description: "A request you approve, send back or reject appears here.",
  });
  assert.deepEqual(requestBoxPages.all.empty, {
    title: "Nobody has asked for approval yet.",
    description: "Every request anyone asks for appears here.",
  });
});

test("kindsPage_NoKindDeclared_SaysNoPartOfTheErpAsksForApproval", () => {
  assert.deepEqual(kindsPage.empty, {
    title: "No part of the ERP asks for approval yet.",
    description: "A kind of request appears here once a part of the ERP that asks for approval is in use.",
  });
});

test("requestBoxPages_EveryPage_HasADistinctAddressAndTitle", () => {
  const pages = [...Object.values(requestBoxPages), kindsPage];

  assert.equal(new Set(pages.map((page) => page.path)).size, pages.length);
  assert.equal(new Set(pages.map((page) => page.title)).size, pages.length);
});
