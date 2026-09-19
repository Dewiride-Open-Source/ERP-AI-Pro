import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { serialize } from '../lib/model.ts';
import { RoadmapError, addPhase, addSubPhase, block, defer, done, move, note, resume, setAcceptance, start } from '../lib/mutate.ts';
import { nextCandidates, render } from '../lib/render.ts';
import { validate } from '../lib/validate.ts';
import { sampleRoadmap } from './fixture.ts';

const DATE = '2026-09-20';

test('start, done, block, defer and resume update status and dates', () => {
  const roadmap = sampleRoadmap();
  const sub = start(roadmap, 'P01.2', DATE);
  assert.equal(sub.status, 'in-progress');
  assert.equal(sub.startedOn, DATE);
  assert.equal(roadmap.updatedOn, DATE);
  assert.throws(() => start(roadmap, 'authentication-oidc', DATE), RoadmapError);
  start(roadmap, 'authentication-oidc', DATE, true);

  done(roadmap, 'foundation-tooling', DATE);
  assert.equal(sub.completedOn, DATE);
  assert.throws(() => start(roadmap, 'foundation-tooling', DATE), RoadmapError);

  const blocked = block(roadmap, 'P02.1', 'waiting for app registration', DATE);
  assert.equal(blocked.status, 'blocked');
  assert.equal(blocked.blockedReason, 'waiting for app registration');
  assert.throws(() => block(roadmap, 'P02.1', '   ', DATE), RoadmapError);

  const resumed = resume(roadmap, 'P02.1', DATE);
  assert.equal(resumed.status, 'planned');
  assert.equal('blockedReason' in resumed, false);

  const deferred = defer(roadmap, 'authentication', DATE);
  assert.equal('deferred' in deferred && deferred.deferred, true);
  resume(roadmap, 'authentication', DATE);
  assert.equal('deferred' in roadmap.phases[1]!, false);

  assert.throws(() => done(roadmap, 'authentication', DATE), RoadmapError);
  assert.throws(() => start(roadmap, 'nope', DATE), RoadmapError);
});

test('note and acceptance edit a sub-phase', () => {
  const roadmap = sampleRoadmap();
  note(roadmap, 'P01.2', 'CLI shipped', DATE);
  assert.equal(roadmap.phases[0]!.subPhases[1]!.notes, 'CLI shipped');
  note(roadmap, 'P01.2', '', DATE);
  assert.equal('notes' in roadmap.phases[0]!.subPhases[1]!, false);
  setAcceptance(roadmap, 'P01.2', [' a ', '', 'b'], DATE);
  assert.deepEqual(roadmap.phases[0]!.subPhases[1]!.acceptance, ['a', 'b']);
});

test('phases and sub-phases can be inserted anywhere without renumbering ids', () => {
  const roadmap = sampleRoadmap();
  addPhase(roadmap, { id: 'clients', title: 'Clients', goal: 'Client master', milestone: 'm1-live', after: 'foundation' }, DATE);
  assert.deepEqual(
    roadmap.phases.map((p) => p.id),
    ['foundation', 'clients', 'authentication'],
  );
  assert.throws(() => addPhase(roadmap, { id: 'clients', title: 'x', goal: 'y', milestone: 'm1-live' }, DATE), RoadmapError);
  assert.throws(() => addPhase(roadmap, { id: 'Bad Id', title: 'x', goal: 'y', milestone: 'm1-live' }, DATE), RoadmapError);
  assert.throws(() => addPhase(roadmap, { id: 'ok', title: 'x', goal: 'y', milestone: 'missing' }, DATE), RoadmapError);

  addSubPhase(roadmap, { phase: 'P01', id: 'foundation-docs', title: 'Docs', scope: 'Write docs', after: 'foundation-governance', tags: ['docs'] }, DATE);
  assert.deepEqual(
    roadmap.phases[0]!.subPhases.map((s) => s.id),
    ['foundation-governance', 'foundation-docs', 'foundation-tooling'],
  );
  assert.deepEqual(validate(roadmap, { today: DATE }).filter((i) => i.level === 'error'), []);
});

test('move relocates a sub-phase between phases', () => {
  const roadmap = sampleRoadmap();
  move(roadmap, 'authentication-ai-helper', 'foundation', 1, DATE);
  assert.deepEqual(
    roadmap.phases[0]!.subPhases.map((s) => s.id),
    ['authentication-ai-helper', 'foundation-governance', 'foundation-tooling'],
  );
  assert.equal(roadmap.phases[1]!.subPhases.length, 1);
});

test('next candidates respect order and dependencies', () => {
  const roadmap = sampleRoadmap();
  assert.deepEqual(
    nextCandidates(roadmap, 5).map((c) => c.subPhase.id),
    ['foundation-tooling'],
  );
  done(roadmap, 'foundation-tooling', DATE);
  assert.deepEqual(
    nextCandidates(roadmap, 5).map((c) => c.label),
    ['P02.1'],
  );
  done(roadmap, 'authentication-oidc', DATE);
  assert.deepEqual(
    nextCandidates(roadmap, 5).map((c) => c.subPhase.id),
    ['authentication-ai-helper'],
  );
});

test('render produces task lists, anchors and markers', () => {
  const roadmap = sampleRoadmap();
  start(roadmap, 'foundation-tooling', DATE);
  block(roadmap, 'authentication-oidc', 'needs tenant', DATE);
  defer(roadmap, 'authentication-ai-helper', DATE);
  const markdown = render(roadmap);
  assert.match(markdown, /^# ERP-AI-Pro Roadmap/);
  assert.ok(markdown.includes('- [x] **P01.1** Governance (`foundation-governance`) — done 2026-09-19 <a id="foundation-governance"></a>'));
  assert.ok(markdown.includes('- [ ] **P01.2** Tooling (`foundation-tooling`) — ⏳ in progress since 2026-09-20'));
  assert.ok(markdown.includes('⛔ blocked: needs tenant'));
  assert.ok(markdown.includes('⏸ deferred'));
  assert.ok(markdown.includes('<a id="phase-authentication"></a>'));
  assert.ok(markdown.includes('  - LICENSE exists'));
  assert.ok(markdown.includes('Depends on: `authentication-oidc`'));
  assert.ok(markdown.endsWith('\n'));
});

test('the CLI writes canonical json, regenerates markdown and detects staleness', () => {
  const dir = mkdtempSync(join(tmpdir(), 'roadmap-'));
  const jsonFile = join(dir, 'roadmap.json');
  const markdownFile = join(dir, 'ROADMAP.md');
  writeFileSync(jsonFile, JSON.stringify(sampleRoadmap()));
  const cli = resolve(fileURLToPath(import.meta.url), '..', '..', 'roadmap.ts');
  const run = (...args: string[]) => execFileSync(process.execPath, [cli, ...args, '--file', jsonFile], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });

  assert.throws(() => run('check'), /not in canonical form/);
  run('build');
  assert.equal(readFileSync(jsonFile, 'utf8'), serialize(sampleRoadmap()));
  assert.match(run('check'), /fresh/);
  run('start', 'P01.2', '--date', DATE);
  assert.equal(readFileSync(jsonFile, 'utf8'), serialize(JSON.parse(readFileSync(jsonFile, 'utf8'))));
  assert.match(run('check', '--date', DATE), /fresh/);
  writeFileSync(markdownFile, '# stale\n');
  assert.throws(() => run('check', '--date', DATE), /stale/);
  run('build', '--date', DATE);
  assert.match(run('next'), /P02\.1|foundation-tooling/);
  assert.match(run('show', 'P01.2'), /"status": "in-progress"/);
});
