import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { repoRoot } from '../lib/walk.ts';

const repository = 'mcr.microsoft.com/azure-storage/azurite';
const composeFile = 'infra/compose/compose.override.yaml';
const followers = ['.github/actions/start-azurite/action.yml', 'docs/guides/local-development.md'];
const pinned = /^mcr\.microsoft\.com\/azure-storage\/azurite:\d+\.\d+\.\d+@sha256:[0-9a-f]{64}$/;

const references = (path: string): string[] =>
  [...readFileSync(join(repoRoot, path), 'utf8').matchAll(/mcr\.microsoft\.com\/azure-storage\/azurite[^\s"'`]*/g)].map((match) => match[0]);

test('compose.override.yaml pins the Azurite image by version and digest exactly once', () => {
  const images = references(composeFile);
  assert.equal(images.length, 1, `${composeFile} names ${repository} ${images.length} times`);
  assert.match(images[0] ?? '', pinned);
});

test('every other Azurite reference equals the one Dependabot keeps current in compose.override.yaml', () => {
  const [expected] = references(composeFile);
  for (const path of followers) {
    const images = references(path);
    assert.ok(images.length > 0, `${path} does not name ${repository}`);
    for (const image of images) {
      assert.equal(image, expected, `${path} runs ${image}; bump it to the reference in ${composeFile}`);
    }
  }
});
