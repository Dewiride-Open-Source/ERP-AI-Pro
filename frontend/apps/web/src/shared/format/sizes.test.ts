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

test("formatBytes_ExactUnitBoundaries_ShowOneOfTheLargerUnit", () => {
  assert.equal(formatBytes(1024 * 1024), "1 MB");
  assert.equal(formatBytes(1024 * 1024 * 1024), "1 GB");
});

test("formatBytes_ValueThatRoundsTo1024_MovesToTheNextUnit", () => {
  assert.equal(formatBytes(1048525), "1 MB");
  assert.equal(formatBytes(1048575), "1 MB");
  assert.equal(formatBytes(1073689396), "1 GB");
  assert.equal(formatBytes(1073741000), "1 GB");
  assert.equal(formatBytes(1073741823), "1 GB");
});

test("formatBytes_ValueThatRoundsBelow1024_KeepsTheSmallerUnit", () => {
  assert.equal(formatBytes(1048524), "1,023.9 KB");
  assert.equal(formatBytes(1073689395), "1,023.9 MB");
});

test("formatBytes_NotAFiniteSize_ShowsZeroBytes", () => {
  assert.equal(formatBytes(Number.NaN), "0 bytes");
  assert.equal(formatBytes(-5), "0 bytes");
});
