import assert from "node:assert/strict";
import { test } from "node:test";

import { isFeatureEnabled } from "./feature-flags.ts";

test("isFeatureEnabled_FlagReportedEnabled_ReturnsTrue", () => {
  assert.equal(
    isFeatureEnabled(new Map([["Erp.Modules.Platform.SystemInfo", true]]), "Erp.Modules.Platform.SystemInfo"),
    true,
  );
});

test("isFeatureEnabled_FlagReportedDisabled_ReturnsFalse", () => {
  assert.equal(
    isFeatureEnabled(
      new Map([["Erp.Modules.Platform.SystemInfo", false]]),
      "Erp.Modules.Platform.SystemInfo",
    ),
    false,
  );
});

test("isFeatureEnabled_FlagUnknown_ReturnsTrue", () => {
  assert.equal(isFeatureEnabled(new Map(), "Erp.Modules.Finance.Sales"), true);
  assert.equal(
    isFeatureEnabled(new Map([["Erp.Modules.Platform.SystemInfo", false]]), "Erp.Modules.Finance.Sales"),
    true,
  );
});
