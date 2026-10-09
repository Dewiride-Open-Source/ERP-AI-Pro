import assert from "node:assert/strict";
import { test } from "node:test";

import {
  commandSections,
  filterCommands,
  nextCommandIndex,
  type CommandItem,
} from "./command-palette-model.ts";

const pages: readonly CommandItem[] = [
  { id: "home", label: "Home" },
  { id: "system", label: "System", description: "Which version is running", group: "Platform" },
  { id: "attachments", label: "Attachments", description: "Files kept with the ERP", group: "Platform" },
  { id: "invoices", label: "Invoices", group: "Finance", keywords: ["bills", "GST"] },
  { id: "cafe", label: "Café expenses", group: "Finance" },
];

const ids = (items: readonly CommandItem[]) => items.map((item) => item.id);

test("filterCommands_EmptyOrBlankQuery_KeepsEveryItemInOrder", () => {
  assert.deepEqual(ids(filterCommands(pages, "")), ["home", "system", "attachments", "invoices", "cafe"]);
  assert.deepEqual(ids(filterCommands(pages, "   ")), ["home", "system", "attachments", "invoices", "cafe"]);
});

test("filterCommands_OneWord_MatchesLabelDescriptionGroupOrKeywordWithoutCaseOrAccents", () => {
  assert.deepEqual(ids(filterCommands(pages, "ATTACH")), ["attachments"]);
  assert.deepEqual(ids(filterCommands(pages, "version")), ["system"]);
  assert.deepEqual(ids(filterCommands(pages, "platform")), ["system", "attachments"]);
  assert.deepEqual(ids(filterCommands(pages, "gst")), ["invoices"]);
  assert.deepEqual(ids(filterCommands(pages, "cafe")), ["cafe"]);
});

test("filterCommands_SeveralWords_KeepsOnlyItemsThatHoldEveryWord", () => {
  assert.deepEqual(ids(filterCommands(pages, "plat att")), ["attachments"]);
  assert.deepEqual(ids(filterCommands(pages, "finance  bills")), ["invoices"]);
  assert.deepEqual(ids(filterCommands(pages, "platform invoices")), []);
});

test("filterCommands_NothingMatches_ReturnsNoItems", () => {
  assert.deepEqual(filterCommands(pages, "payroll"), []);
});

test("commandSections_ItemsInOrder_GroupsNeighboursAndKeepsTheirPlaceInTheList", () => {
  const sections = commandSections(pages);
  assert.deepEqual(
    sections.map((section) => [section.group, section.entries.map((entry) => [entry.item.id, entry.index])]),
    [
      [undefined, [["home", 0]]],
      [
        "Platform",
        [
          ["system", 1],
          ["attachments", 2],
        ],
      ],
      [
        "Finance",
        [
          ["invoices", 3],
          ["cafe", 4],
        ],
      ],
    ],
  );
});

test("commandSections_NoItems_ReturnsNoSections", () => {
  assert.deepEqual(commandSections([]), []);
});

test("nextCommandIndex_Moves_WrapsAtBothEnds", () => {
  assert.equal(nextCommandIndex(3, 0, 1), 1);
  assert.equal(nextCommandIndex(3, 2, 1), 0);
  assert.equal(nextCommandIndex(3, 0, -1), 2);
  assert.equal(nextCommandIndex(3, 1, -1), 0);
});

test("nextCommandIndex_NoActiveItem_StartsAtTheEndItMovesFrom", () => {
  assert.equal(nextCommandIndex(3, -1, 1), 0);
  assert.equal(nextCommandIndex(3, -1, -1), 2);
  assert.equal(nextCommandIndex(3, 7, 1), 0);
});

test("nextCommandIndex_NoItems_HasNoActiveItem", () => {
  assert.equal(nextCommandIndex(0, -1, 1), -1);
  assert.equal(nextCommandIndex(0, 0, -1), -1);
});
