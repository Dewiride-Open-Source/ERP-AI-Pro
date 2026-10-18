import assert from "node:assert/strict";
import { test } from "node:test";

import {
  chartPage,
  chartPeople,
  chartPeopleCount,
  initiallyExpanded,
  orgChart,
  peopleAtTheTopPerPage,
  shownChart,
  type ChartNode,
  type ChartPerson,
} from "./org-chart.ts";

function person(
  id: string,
  name: string,
  { managerId, departmentId }: { readonly managerId?: string; readonly departmentId?: string } = {},
): ChartPerson {
  return {
    id,
    name,
    departmentId,
    departmentName: departmentId === undefined ? undefined : `Department ${departmentId}`,
    designationName: undefined,
    managerId,
    managerName: managerId === undefined ? undefined : `Manager ${managerId}`,
  };
}

const head = person("p-head", "Asha Rao", { departmentId: "d-ops" });
const lead = person("p-lead", "Vikram Iyer", { managerId: "p-head", departmentId: "d-north" });
const analyst = person("p-analyst", "Meera Das", { managerId: "p-lead", departmentId: "d-north" });
const clerk = person("p-clerk", "Arjun Nair", { managerId: "p-head", departmentId: "d-ops" });
const orphan = person("p-orphan", "Zoya Khan", { managerId: "p-gone", departmentId: "d-ops" });
const outsider = person("p-outsider", "Ravi Menon", { departmentId: "d-sales" });

const people = [analyst, outsider, head, orphan, lead, clerk];

function outline(nodes: readonly ChartNode[], depth = 0): string[] {
  return nodes.flatMap((node) => [
    `${"  ".repeat(depth)}${node.name}${node.managerOutside === undefined ? "" : ` (${node.managerOutside})`}`,
    ...outline(node.children, depth + 1),
  ]);
}

test("chartPeople_ApiPeople_KeepsThoseWithAnIdAndANameAndNamesAbsentFieldsUndefined", () => {
  assert.deepEqual(
    chartPeople([
      {
        id: "p-1",
        displayName: "Asha Rao",
        departmentId: "d-1",
        departmentName: "Finance",
        designationName: "Head of finance",
        managerId: null,
        managerDisplayName: null,
      },
      { id: null, displayName: "Without an id" },
      { id: "p-2", displayName: "" },
    ]),
    [
      {
        id: "p-1",
        name: "Asha Rao",
        departmentId: "d-1",
        departmentName: "Finance",
        designationName: "Head of finance",
        managerId: undefined,
        managerName: undefined,
      },
    ],
  );
});

test("orgChart_People_NestsEachUnderTheirManagerAlphabeticallyAtEveryLevel", () => {
  assert.deepEqual(outline(orgChart(people)), [
    "Asha Rao",
    "  Arjun Nair",
    "  Vikram Iyer",
    "    Meera Das",
    "Ravi Menon",
    "Zoya Khan (off-chart)",
  ]);
});

test("orgChart_DepartmentsChosen_ShowsTheirPeopleAndExplainsAManagerWhoWorksElsewhere", () => {
  assert.deepEqual(outline(orgChart(people, { departments: new Set(["d-north"]) })), [
    "Vikram Iyer (other-department)",
    "  Meera Das",
  ]);
  assert.deepEqual(outline(orgChart(people, { departments: new Set(["d-ops", "d-north"]) })), [
    "Asha Rao",
    "  Arjun Nair",
    "  Vikram Iyer",
    "    Meera Das",
    "Zoya Khan (off-chart)",
  ]);
});

test("orgChart_DepartmentsChosen_LeavesOutPeopleInNoDepartment", () => {
  const unplaced = person("p-unplaced", "Kiran Shah");

  assert.deepEqual(outline(orgChart([unplaced, head], { departments: new Set(["d-ops"]) })), ["Asha Rao"]);
});

test("orgChart_ReportingLoopInTheList_ShowsEachPersonOnceFromTheFirstName", () => {
  const first = person("p-first", "Anil", { managerId: "p-second" });
  const second = person("p-second", "Bina", { managerId: "p-first" });
  const self = person("p-self", "Chetan", { managerId: "p-self" });

  assert.deepEqual(outline(orgChart([second, self, first])), ["Anil", "  Bina", "Chetan"]);
});

test("chartPage_MoreTopPeopleThanAPageHolds_SplitsThemIntoPagesAndKeepsTheirReports", () => {
  const tops = Array.from({ length: peopleAtTheTopPerPage + 1 }, (_, index) =>
    person(`p-${String(index).padStart(2, "0")}`, `Person ${String(index).padStart(2, "0")}`),
  );
  const report = person("p-report", "Report", { managerId: "p-00" });
  const roots = orgChart([...tops, report]);

  const first = chartPage(roots, 1);
  const second = chartPage(roots, 2);

  assert.equal(first.pageCount, 2);
  assert.equal(first.roots.length, peopleAtTheTopPerPage);
  assert.equal(first.roots[0]?.children[0]?.name, "Report");
  assert.deepEqual(
    second.roots.map((root) => root.name),
    [`Person ${peopleAtTheTopPerPage}`],
  );
});

test("chartPage_PageOutsideTheChart_ShowsTheNearestPage", () => {
  const roots = orgChart([head]);

  assert.deepEqual(chartPage(roots, 5), { roots, page: 1, pageCount: 1 });
  assert.deepEqual(chartPage([], 1), { roots: [], page: 1, pageCount: 1 });
});

test("initiallyExpanded_Chart_OpensOnlyTheTopPeopleWhoManageSomeone", () => {
  assert.deepEqual(initiallyExpanded(orgChart(people)), ["p-head"]);
});

test("chartPeopleCount_Chart_CountsEveryoneAtEveryLevel", () => {
  assert.equal(chartPeopleCount(orgChart(people)), people.length);
  assert.equal(chartPeopleCount([]), 0);
});

const operations = "0199f3a4-5b6c-7d8e-9f01-000000000001";
const north = "0199f3a4-5b6c-7d8e-9f01-000000000002";
const sales = "0199f3a4-5b6c-7d8e-9f01-000000000003";
const deleted = "0199f3a4-5b6c-7d8e-9f01-000000000009";

const departments = [
  { id: operations, name: "Operations", parentId: null, version: "AAAAAAAAB9E=" },
  { id: north, name: "North", parentId: operations, version: "AAAAAAAAB9F=" },
  { id: sales, name: "Sales", parentId: null, version: "AAAAAAAAB9G=" },
];

const responses = [
  { id: "p-head", displayName: "Asha Rao", departmentId: operations, departmentName: "Operations" },
  {
    id: "p-lead",
    displayName: "Vikram Iyer",
    departmentId: north,
    departmentName: "North",
    managerId: "p-head",
  },
  { id: "p-seller", displayName: "Ravi Menon", departmentId: sales, departmentName: "Sales" },
];

function shownNames(chart: ReturnType<typeof shownChart>): string[] {
  const names: string[] = [];
  const visit = (nodes: typeof chart.page.roots) => {
    for (const node of nodes) {
      names.push(node.name);
      visit(node.children);
    }
  };
  visit(chart.page.roots);
  return names;
}

test("shownChart_NoDepartmentChosen_ShowsEveryoneWithEveryDepartmentToChoose", () => {
  const chart = shownChart({ department: undefined, on: undefined, page: 1 }, responses, departments);

  assert.deepEqual(shownNames(chart), ["Asha Rao", "Vikram Iyer", "Ravi Menon"]);
  assert.equal(chart.peopleCount, 3);
  assert.equal(chart.departmentName, undefined);
  assert.deepEqual(
    chart.departments.status === "read" ? chart.departments.options.map((option) => option.value) : [],
    [operations, north, sales],
  );
});

test("shownChart_DepartmentChosen_ShowsThePeopleOfItAndOfTheDepartmentsUnderIt", () => {
  const chart = shownChart({ department: operations, on: undefined, page: 1 }, responses, departments);

  assert.deepEqual(shownNames(chart), ["Asha Rao", "Vikram Iyer"]);
  assert.equal(chart.departmentName, "Operations");
});

test("shownChart_DepartmentTheListNoLongerHolds_StaysChosenAsAnUnknownDepartmentWithNobodyShown", () => {
  const chart = shownChart({ department: deleted, on: undefined, page: 1 }, responses, departments);

  assert.deepEqual(shownNames(chart), []);
  assert.equal(chart.peopleCount, 0);
  assert.equal(chart.departmentName, undefined);
  assert.equal(chart.departments.status, "read");
  assert.deepEqual(chart.departments.status === "read" ? chart.departments.options.at(-1) : undefined, {
    value: deleted,
    label: "Unknown department",
  });
});

test("shownChart_DepartmentsCouldNotBeRead_ShowsOnlyThePeopleDirectlyInTheChosenOne", () => {
  const chart = shownChart({ department: operations, on: undefined, page: 1 }, responses, undefined);

  assert.deepEqual(shownNames(chart), ["Asha Rao"]);
  assert.deepEqual(chart.departments, { status: "failed" });
  assert.equal(chart.departmentName, undefined);
});

test("shownChart_DepartmentsCouldNotBeReadAndNoneChosen_ShowsEveryone", () => {
  const chart = shownChart({ department: undefined, on: undefined, page: 1 }, responses, undefined);

  assert.equal(chart.peopleCount, 3);
  assert.deepEqual(chart.departments, { status: "failed" });
});
