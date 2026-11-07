import assert from "node:assert/strict";
import { test } from "node:test";

import { indianStateRow, kindLabel } from "./indian-state-rows.ts";

test("indianStateRow_CodeThatEnded_ShowsItsFirstAndLastDayAsDayMonthYear", () => {
  assert.deepEqual(
    indianStateRow({
      gstCode: "91",
      name: "Example territory",
      kind: "unionTerritory",
      validFrom: "2017-07-01",
      validUntil: "2020-07-31",
    }),
    {
      id: "91-2017-07-01",
      cells: {
        code: "91",
        name: "Example territory",
        kind: "Union territory",
        from: "01-07-2017",
        until: "31-07-2020",
      },
    },
  );
});

test("indianStateRow_CodeWithoutALastDay_IsInForce", () => {
  const row = indianStateRow({
    gstCode: "92",
    name: "Example state",
    kind: "state",
    validFrom: "2018-04-01",
    validUntil: null,
  });

  assert.equal(row.cells.until, "In force");
  assert.equal(row.id, "92-2018-04-01");
});

test("indianStateRow_SameCodeFromAnotherDay_IsARowOfItsOwn", () => {
  const before = indianStateRow({
    gstCode: "93",
    name: "Example region",
    kind: "state",
    validFrom: "2017-07-01",
    validUntil: "2019-10-30",
  });
  const after = indianStateRow({
    gstCode: "93",
    name: "Example region",
    kind: "unionTerritory",
    validFrom: "2019-10-31",
    validUntil: null,
  });

  assert.notEqual(before.id, after.id);
  assert.deepEqual([before.cells.kind, after.cells.kind], ["State", "Union territory"]);
});

test("kindLabel_EveryKind_IsNamedInPlainWords", () => {
  assert.deepEqual(
    (["state", "unionTerritory", "otherTerritory"] as const).map((kind) => kindLabel(kind)),
    ["State", "Union territory", "Other territory"],
  );
});
