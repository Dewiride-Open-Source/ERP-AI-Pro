import assert from "node:assert/strict";
import { test } from "node:test";

import { formatBytes } from "./sizes.ts";

test("formatBytes_BelowOneKilobyte_CountsBytes", () => {
  assert.equal(formatBytes(0), "0 bytes");
  assert.equal(formatBytes(1), "1 byte");
  assert.equal(formatBytes(1023), "1023 bytes");
});

test("formatBytes_Kilobytes_UsesOneDecimalAtMost", () => {
  assert.equal(formatBytes(1024), "1 KB");
  assert.equal(formatBytes(1536), "1.5 KB");
});

test("formatBytes_LargerSizes_MoveToTheNextUnitAtEach1024", () => {
  assert.equal(formatBytes(25 * 1024 * 1024), "25 MB");
  assert.equal(formatBytes(3.25 * 1024 * 1024 * 1024), "3.3 GB");
  assert.equal(formatBytes(5000 * 1024 * 1024 * 1024), "5,000 GB");
});

test("formatBytes_NotAFiniteSize_ShowsZeroBytes", () => {
  assert.equal(formatBytes(Number.NaN), "0 bytes");
  assert.equal(formatBytes(-5), "0 bytes");
});
