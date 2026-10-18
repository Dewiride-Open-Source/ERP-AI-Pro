import assert from "node:assert/strict";
import { test } from "node:test";

import type { Route } from "next";

import { breadcrumbTrail, currentPage, navigationAreas, type TrailSource } from "./navigation-trail.ts";

const sources: readonly TrailSource[] = [
  { basePath: "/platform/system-info", title: "System", areaTitle: "Platform" },
  { basePath: "/platform/attachments", title: "Attachments", areaTitle: "Platform" },
];

const entryPaths: readonly Route[] = ["/platform/system-info", "/platform/attachments"];

const people: TrailSource = { basePath: "/identity/users", title: "People", areaTitle: "Organisation" };

const departments: TrailSource = {
  basePath: "/identity/users/departments",
  title: "Departments",
  areaTitle: "Organisation",
};

const organisationPaths: readonly Route[] = [
  "/identity/users",
  "/identity/users/org-chart",
  "/identity/users/departments",
  "/identity/users/designations",
];

test("currentPage_HomeLink_IsCurrentOnlyOnTheStartPage", () => {
  assert.equal(currentPage("/", "/", entryPaths), "page");
  assert.equal(currentPage("/platform/attachments", "/", entryPaths), undefined);
});

test("currentPage_EntryLink_IsThePageOnItsOwnPathAndTheSectionBelowIt", () => {
  assert.equal(currentPage("/platform/attachments", "/platform/attachments", entryPaths), "page");
  assert.equal(currentPage("/platform/attachments/report", "/platform/attachments", entryPaths), "section");
  assert.equal(currentPage("/platform/attachments-archive", "/platform/attachments", entryPaths), undefined);
  assert.equal(currentPage("/platform/system-info", "/platform/attachments", entryPaths), undefined);
});

test("currentPage_EntryInsideAnotherEntrysPath_MarksOnlyTheEntryWithTheLongestPath", () => {
  for (const pathname of ["/identity/users/departments", "/identity/users/departments/report"]) {
    assert.equal(currentPage(pathname, "/identity/users", organisationPaths), undefined, pathname);
  }
  assert.equal(
    currentPage("/identity/users/departments", "/identity/users/departments", organisationPaths),
    "page",
  );
  assert.equal(
    currentPage("/identity/users/departments/report", "/identity/users/departments", organisationPaths),
    "section",
  );
  assert.equal(
    currentPage("/identity/users/departments", "/identity/users/designations", organisationPaths),
    undefined,
  );
});

test("currentPage_PageBelowTheOuterEntryOnly_MarksTheOuterEntryAsTheSection", () => {
  for (const pathname of [
    "/identity/users/0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e",
    "/identity/users/0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e/edit",
    "/identity/users/permissions",
    "/identity/users/new",
  ]) {
    assert.equal(currentPage(pathname, "/identity/users", organisationPaths), "section", pathname);
    assert.equal(
      currentPage(pathname, "/identity/users/departments", organisationPaths),
      undefined,
      pathname,
    );
  }
});

test("currentPage_LinkMissingFromTheEntryPaths_StillMarksItsOwnSection", () => {
  assert.equal(currentPage("/platform/attachments/report", "/platform/attachments", []), "section");
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

test("breadcrumbTrail_EntryInsideAnotherEntrysPath_EndsOnTheEntryWithTheLongestPathInAnyOrder", () => {
  for (const order of [
    [people, departments],
    [departments, people],
  ]) {
    assert.deepEqual(breadcrumbTrail("/identity/users/departments", order), [
      { kind: "link", label: "Home", href: "/" },
      { kind: "area", label: "Organisation" },
      { kind: "page", label: "Departments" },
    ]);
    assert.deepEqual(breadcrumbTrail("/identity/users/0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e", order), [
      { kind: "link", label: "Home", href: "/" },
      { kind: "area", label: "Organisation" },
      { kind: "link", label: "People", href: "/identity/users" },
    ]);
  }
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
