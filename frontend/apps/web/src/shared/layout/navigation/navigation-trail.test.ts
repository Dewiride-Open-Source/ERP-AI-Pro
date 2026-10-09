import assert from "node:assert/strict";
import { test } from "node:test";

import { breadcrumbTrail, currentPage, navigationAreas, type TrailSource } from "./navigation-trail.ts";

const sources: readonly TrailSource[] = [
  { basePath: "/platform/system-info", title: "System", areaTitle: "Platform" },
  { basePath: "/platform/attachments", title: "Attachments", areaTitle: "Platform" },
];

test("currentPage_HomeLink_IsCurrentOnlyOnTheStartPage", () => {
  assert.equal(currentPage("/", "/"), "page");
  assert.equal(currentPage("/platform/attachments", "/"), undefined);
});

test("currentPage_EntryLink_IsThePageOnItsOwnPathAndTheSectionBelowIt", () => {
  assert.equal(currentPage("/platform/attachments", "/platform/attachments"), "page");
  assert.equal(currentPage("/platform/attachments/report", "/platform/attachments"), "section");
  assert.equal(currentPage("/platform/attachments-archive", "/platform/attachments"), undefined);
  assert.equal(currentPage("/platform/system-info", "/platform/attachments"), undefined);
});

test("breadcrumbTrail_StartPage_IsHomeAsTheCurrentPage", () => {
  assert.deepEqual(breadcrumbTrail("/", sources), [{ kind: "page", label: "Home" }]);
});

test("breadcrumbTrail_EntryPage_LinksHomeNamesTheAreaAndEndsOnThePage", () => {
  assert.deepEqual(breadcrumbTrail("/platform/attachments", sources), [
    { kind: "link", label: "Home", href: "/" },
    { kind: "area", label: "Platform" },
    { kind: "page", label: "Attachments" },
  ]);
});

test("breadcrumbTrail_PageBelowAnEntry_LinksTheEntry", () => {
  assert.deepEqual(breadcrumbTrail("/platform/system-info/startups", sources), [
    { kind: "link", label: "Home", href: "/" },
    { kind: "area", label: "Platform" },
    { kind: "link", label: "System", href: "/platform/system-info" },
  ]);
});

test("breadcrumbTrail_PageTheRegistryDoesNotReach_IsHomeAlone", () => {
  assert.deepEqual(breadcrumbTrail("/design/kitchen-sink", sources), [
    { kind: "link", label: "Home", href: "/" },
  ]);
  assert.deepEqual(breadcrumbTrail("/platform/attachments-archive", sources), [
    { kind: "link", label: "Home", href: "/" },
  ]);
});

test("navigationAreas_EntriesOfSeveralAreas_GroupsThemInTheOrderTheyFirstAppear", () => {
  const entries = [
    { id: "system", area: { id: "platform", title: "Platform" } },
    { id: "invoices", area: { id: "finance", title: "Finance" } },
    { id: "attachments", area: { id: "platform", title: "Platform" } },
  ];
  assert.deepEqual(
    navigationAreas(entries).map((area) => [area.id, area.title, area.entries.map((entry) => entry.id)]),
    [
      ["platform", "Platform", ["system", "attachments"]],
      ["finance", "Finance", ["invoices"]],
    ],
  );
});

test("navigationAreas_NoEntries_HasNoAreas", () => {
  assert.deepEqual(navigationAreas([]), []);
});
