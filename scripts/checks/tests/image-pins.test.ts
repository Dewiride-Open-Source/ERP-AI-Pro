import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { repoRoot } from '../lib/walk.ts';

const read = (path: string): string => readFileSync(join(repoRoot, path), 'utf8');

const azuriteRepository = 'mcr.microsoft.com/azure-storage/azurite';
const azuriteComposeFile = 'infra/compose/compose.override.yaml';
const azuriteFollowers = ['.github/actions/start-azurite/action.yml', 'docs/guides/local-development.md'];
const azuritePinned = /^mcr\.microsoft\.com\/azure-storage\/azurite:\d+\.\d+\.\d+@sha256:[0-9a-f]{64}$/;

const azuriteReferences = (path: string): string[] =>
  [...read(path).matchAll(/mcr\.microsoft\.com\/azure-storage\/azurite[^\s"'`]*/g)].map((match) => match[0]);

const buildkitPinFile = '.github/buildkit/Dockerfile';
const workflowFolder = '.github/workflows';
const buildkitPinned = /^FROM moby\/buildkit:v\d+\.\d+\.\d+@sha256:[0-9a-f]{64}$/;
const takesThePin = 'driver-opts: image=${{ steps.buildkit.outputs.image }}';

const count = (text: string, part: string): number => text.split(part).length - 1;

const buildxWorkflows = (): string[] =>
  readdirSync(join(repoRoot, workflowFolder))
    .filter((name) => name.endsWith('.yml'))
    .map((name) => `${workflowFolder}/${name}`)
    .filter((path) => read(path).includes('docker/setup-buildx-action@'));

test('compose.override.yaml pins the Azurite image by version and digest exactly once', () => {
  const images = azuriteReferences(azuriteComposeFile);
  assert.equal(images.length, 1, `${azuriteComposeFile} names ${azuriteRepository} ${images.length} times`);
  assert.match(images[0] ?? '', azuritePinned);
});

test('every other Azurite reference equals the one Dependabot keeps current in compose.override.yaml', () => {
  const [expected] = azuriteReferences(azuriteComposeFile);
  for (const path of azuriteFollowers) {
    const images = azuriteReferences(path);
    assert.ok(images.length > 0, `${path} does not name ${azuriteRepository}`);
    for (const image of images) {
      assert.equal(image, expected, `${path} runs ${image}; bump it to the reference in ${azuriteComposeFile}`);
    }
  }
});

test('the BuildKit Dockerfile pins one BuildKit image by version and digest', () => {
  const instructions = read(buildkitPinFile)
    .split(/\r?\n/)
    .filter((line) => line !== '' && !line.startsWith('#'));
  assert.equal(instructions.length, 1, `${buildkitPinFile} holds ${instructions.length} instructions`);
  assert.match(instructions[0] ?? '', buildkitPinned);
});

test('the release and the image build both set up Buildx', () => {
  assert.deepEqual(buildxWorkflows().sort(), [`${workflowFolder}/docker-build.yml`, `${workflowFolder}/release.yml`]);
});

test('every Buildx a workflow sets up runs the BuildKit the Dockerfile pins', () => {
  for (const path of buildxWorkflows()) {
    const workflow = read(path);
    const setups = count(workflow, 'docker/setup-buildx-action@');
    assert.equal(count(workflow, takesThePin), setups, `${path} sets up Buildx ${setups} times, but not each time with "${takesThePin}"`);
    assert.ok(workflow.includes(`sed -n 's/^FROM //p' ${buildkitPinFile}`), `${path} does not read its BuildKit from ${buildkitPinFile}`);
    assert.doesNotMatch(workflow, /moby\/buildkit:(?!v\[0-9\])/, `${path} names a BuildKit image itself; ${buildkitPinFile} pins it`);
  }
});
