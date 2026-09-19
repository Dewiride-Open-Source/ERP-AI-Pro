import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { DATE_PATTERN, ID_PATTERN, paths, today } from '../lib/model.ts';

type Schema = { $defs: Record<string, { pattern?: string; enum?: string[] }> };

test('roadmap.schema.json parses and its patterns agree with the model', () => {
  const schema = JSON.parse(readFileSync(paths.schemaJson, 'utf8')) as Schema;
  const date = new RegExp(schema.$defs.date?.pattern ?? '');
  const slug = new RegExp(schema.$defs.slug?.pattern ?? '');

  assert.equal(date.source, DATE_PATTERN.source);
  assert.equal(slug.source, ID_PATTERN.source);
  assert.ok(date.test('2026-09-19'));
  assert.ok(!date.test('2026-9-19'));
  assert.ok(slug.test('finance-sales'));
  assert.ok(!slug.test('Finance_Sales'));
  assert.deepEqual(schema.$defs.status?.enum, ['planned', 'in-progress', 'blocked', 'deferred', 'done']);
});

test('today() is the local calendar date', () => {
  const value = today();
  const now = new Date();
  assert.match(value, DATE_PATTERN);
  assert.equal(Number(value.slice(0, 4)), now.getFullYear());
  assert.equal(Number(value.slice(5, 7)), now.getMonth() + 1);
  assert.equal(Number(value.slice(8, 10)), now.getDate());
});
