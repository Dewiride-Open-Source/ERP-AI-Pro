import assert from "node:assert/strict";
import { dirname, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

import { registrationAudit } from "../lib/audit.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

test("every module of Modules.All is registered everywhere a module is registered by hand, and nothing is registered for a module that is not", () => {
  assert.deepEqual(registrationAudit(repoRoot), []);
});
