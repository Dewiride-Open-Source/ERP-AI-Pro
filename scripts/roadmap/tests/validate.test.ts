import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validate } from '../lib/validate.ts';
import { sampleRoadmap } from './fixture.ts';

const errors = (roadmap: ReturnType<typeof sampleRoadmap>, options = {}) =>
  validate(roadmap, { today: '2026-09-19', ...options })
    .filter((i) => i.level === 'error')
    .map((i) => i.message);

test('a well-formed roadmap has no errors', () => {
  assert.deepEqual(errors(sampleRoadmap()), []);
});

test('ids must be unique slugs', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[1]!.id = 'Authentication';
  assert.ok(errors(roadmap).some((m) => m.includes('not a slug')));
  const duplicate = sampleRoadmap();
  duplicate.phases[1]!.subPhases[0]!.id = 'foundation-tooling';
  assert.ok(errors(duplicate).some((m) => m.includes('duplicate id')));
});

test('dependencies must exist and form a DAG', () => {
  const unknown = sampleRoadmap();
  unknown.phases[1]!.dependsOn = ['nope'];
  assert.ok(errors(unknown).some((m) => m.includes('unknown id "nope"')));
  const cycle = sampleRoadmap();
  cycle.phases[0]!.dependsOn = ['authentication'];
  assert.ok(errors(cycle).some((m) => m.startsWith('dependency cycle')));
  const self = sampleRoadmap();
  self.phases[0]!.dependsOn = ['foundation-tooling'];
  assert.ok(errors(self).some((m) => m.startsWith('dependency cycle')));
});

test('status and dates are consistent', () => {
  const roadmap = sampleRoadmap();
  const sub = roadmap.phases[0]!.subPhases[1]!;
  sub.status = 'done';
  assert.ok(errors(roadmap).some((m) => m.includes('done without completedOn')));
  sub.completedOn = '2027-01-01';
  assert.ok(errors(roadmap).some((m) => m.includes('is in the future')));
  sub.completedOn = '2026-09-20';
  assert.ok(!validate(roadmap, { today: '2026-09-19' }).some((i) => i.message.includes('is in the future')));
  assert.ok(validate(roadmap, { today: '2026-09-18' }).some((i) => i.message.includes('is in the future')));
  sub.completedOn = 'yesterday';
  assert.ok(errors(roadmap).some((m) => m.includes('must be an ISO date')));
  sub.status = 'blocked';
  delete sub.completedOn;
  assert.ok(errors(roadmap).some((m) => m.includes('blocked without blockedReason')));
  sub.status = 'in-progress';
  assert.ok(errors(roadmap).some((m) => m.includes('in-progress without startedOn')));
});

test('a done phase cannot depend on an unfinished phase', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[0]!.subPhases[1]!.status = 'done';
  roadmap.phases[0]!.subPhases[1]!.completedOn = '2026-09-19';
  roadmap.phases[0]!.dependsOn = [];
  roadmap.phases[1]!.dependsOn = [];
  roadmap.phases[0]!.dependsOn = ['authentication-oidc'];
  assert.ok(errors(roadmap).some((m) => m.includes('is done but depends on')));
});

test('only one sub-phase may be in progress unless allowed', () => {
  const roadmap = sampleRoadmap();
  for (const sub of [roadmap.phases[0]!.subPhases[1]!, roadmap.phases[1]!.subPhases[0]!]) {
    sub.status = 'in-progress';
    sub.startedOn = '2026-09-19';
  }
  assert.ok(errors(roadmap).some((m) => m.includes('more than one sub-phase is in progress')));
  assert.deepEqual(errors(roadmap, { allowMultipleWip: true }), []);
});

test('unknown milestone and empty phases are reported', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[1]!.milestone = 'm9-missing';
  assert.ok(errors(roadmap).some((m) => m.includes('unknown milestone')));
  const empty = sampleRoadmap();
  empty.phases[1]!.subPhases = [];
  assert.deepEqual(errors(empty), []);
  assert.ok(validate(empty).some((i) => i.level === 'warning' && i.message.includes('no sub-phases')));
});

test('positional labels in free text are rejected unless quoted as examples', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[1]!.subPhases[0]!.scope = 'Builds on P01.2 and the kernel';
  const issues = validate(roadmap);
  assert.ok(issues.some((i) => i.level === 'error' && i.message.includes('"P01.2"')));

  roadmap.phases[1]!.subPhases[0]!.scope = 'Labels such as `P01.2` are accepted by the CLI';
  assert.deepEqual(validate(roadmap).filter((i) => i.level === 'error'), []);
});
