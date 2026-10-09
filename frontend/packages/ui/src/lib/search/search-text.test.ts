import assert from "node:assert/strict";
import { test } from "node:test";

import { searchText } from "./search-text.ts";

test("searchText_AccentsCaseAndOuterSpaces_AreRemoved", () => {
  assert.equal(searchText("  Café  "), "cafe");
  assert.equal(searchText("ÉLAN"), "elan");
});
