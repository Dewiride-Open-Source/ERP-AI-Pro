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
  const empty = sampleRoadmap();
  empty.phases.push(
    { id: 'clients', title: 'Clients', goal: 'Client master', milestone: 'm1-live', dependsOn: ['vendors'], subPhases: [] },
    { id: 'vendors', title: 'Vendors', goal: 'Vendor master', milestone: 'm1-live', dependsOn: ['clients'], subPhases: [] },
  );
  assert.deepEqual(errors(empty), ['dependency cycle: clients -> vendors -> clients']);
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

test('every sub-phase names a known edition', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[0]!.subPhases[1]!.edition = 'premium' as never;
  assert.ok(errors(roadmap).some((m) => m.includes('(foundation-tooling): edition must be community or enterprise, got "premium"')));
  const missing = sampleRoadmap();
  delete (missing.phases[1]!.subPhases[1] as { edition?: string }).edition;
  assert.ok(errors(missing).some((m) => m.includes('(authentication-ai-helper): edition must be community or enterprise')));
});

test('work cannot start or finish before the owner confirms the edition', () => {
  const roadmap = sampleRoadmap();
  const sub = roadmap.phases[1]!.subPhases[1]!;
  sub.status = 'in-progress';
  sub.startedOn = '2026-09-19';
  assert.ok(errors(roadmap).some((m) => m.includes('in-progress without editionConfirmedOn; the owner confirms the edition before work starts')));
  sub.status = 'done';
  sub.completedOn = '2026-09-19';
  assert.ok(errors(roadmap).some((m) => m.includes('done without editionConfirmedOn')));
  sub.status = 'blocked';
  sub.blockedReason = 'waiting for the owner';
  delete sub.completedOn;
  assert.deepEqual(errors(roadmap), []);
  sub.status = 'done';
  sub.completedOn = '2026-09-19';
  delete sub.blockedReason;
  sub.editionConfirmedOn = '2026-09-19';
  assert.deepEqual(errors(roadmap), []);
});

test('editionConfirmedOn must be an ISO date that is not in the future', () => {
  const roadmap = sampleRoadmap();
  const sub = roadmap.phases[1]!.subPhases[1]!;
  sub.editionConfirmedOn = 'soon';
  assert.ok(errors(roadmap).some((m) => m.includes('editionConfirmedOn must be an ISO date')));
  sub.editionConfirmedOn = '2027-01-01';
  assert.ok(errors(roadmap).some((m) => m.includes('editionConfirmedOn 2027-01-01 is in the future')));
  sub.editionConfirmedOn = '2026-09-20';
  assert.deepEqual(errors(roadmap), []);
  assert.ok(errors(roadmap, { today: '2026-09-18' }).some((m) => m.includes('editionConfirmedOn 2026-09-20 is in the future')));
});

test('community work cannot depend on enterprise-only work', () => {
  const subEdge = sampleRoadmap();
  subEdge.phases[1]!.dependsOn = [];
  subEdge.phases[0]!.subPhases[1]!.dependsOn = ['authentication-ai-helper'];
  assert.deepEqual(errors(subEdge), ['sub-phase "foundation-tooling" is Community work but depends on "authentication-ai-helper", which is Enterprise work only']);

  const phaseEdge = sampleRoadmap();
  for (const sub of phaseEdge.phases[0]!.subPhases) sub.edition = 'enterprise';
  assert.deepEqual(errors(phaseEdge), ['phase "authentication" holds Community sub-phases but depends on "foundation", which is Enterprise work only']);
  phaseEdge.phases[1]!.subPhases[0]!.edition = 'enterprise';
  assert.deepEqual(errors(phaseEdge), []);
});

test('a done phase needs each prerequisite done for every edition among its sub-phases', () => {
  const roadmap = sampleRoadmap();
  const finished = { status: 'done' as const, editionConfirmedOn: '2026-09-19', startedOn: '2026-09-19', completedOn: '2026-09-19' };
  Object.assign(roadmap.phases[0]!.subPhases[1]!, finished);
  Object.assign(roadmap.phases[1]!.subPhases[0]!, finished);
  roadmap.phases.push({
    id: 'clients',
    title: 'Clients',
    goal: 'Client master',
    milestone: 'm1-live',
    dependsOn: ['authentication'],
    subPhases: [{ id: 'clients-master', title: 'Client master', scope: 'Clients', edition: 'community', acceptance: [], tags: [], dependsOn: [], ...finished }],
  });
  assert.deepEqual(errors(roadmap), []);

  roadmap.phases[2]!.subPhases.push({ id: 'clients-ai-insights', title: 'AI: insights', scope: 'Insights', edition: 'enterprise', acceptance: [], tags: ['ai'], dependsOn: [], ...finished });
  assert.deepEqual(errors(roadmap), ['phase "clients" is done but depends on "authentication" which is not done']);
  Object.assign(roadmap.phases[1]!.subPhases[1]!, finished);
  assert.deepEqual(errors(roadmap), []);
});

test('a sub-phase that depends on a phase which depends on its own phase is a dependency cycle', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases[0]!.subPhases[1]!.dependsOn = ['authentication'];
  assert.deepEqual(errors(roadmap), ['dependency cycle: foundation-tooling -> authentication -> authentication-oidc -> authentication -> foundation -> foundation-tooling']);
});

test('the cycle check follows what each edition waits for', () => {
  const roadmap = sampleRoadmap();
  roadmap.phases.push({
    id: 'clients',
    title: 'Clients',
    goal: 'Client master',
    milestone: 'm1-live',
    dependsOn: ['authentication'],
    subPhases: [{ id: 'clients-master', title: 'Client master', scope: 'Clients', status: 'planned', edition: 'community', acceptance: [], tags: [], dependsOn: [] }],
  });
  roadmap.phases[1]!.subPhases[1]!.dependsOn = ['authentication-oidc', 'clients'];
  assert.deepEqual(errors(roadmap), []);
  roadmap.phases[2]!.subPhases[0]!.edition = 'enterprise';
  assert.deepEqual(errors(roadmap), ['dependency cycle: authentication-ai-helper -> clients -> clients-master -> clients -> authentication -> authentication-ai-helper']);

  const deferred = sampleRoadmap();
  deferred.phases[0]!.subPhases.push({ id: 'foundation-audit', title: 'Audit', scope: 'Audit trail', status: 'planned', edition: 'enterprise', acceptance: [], tags: [], dependsOn: ['authentication'] });
  assert.ok(errors(deferred).some((m) => m.startsWith('dependency cycle')));
  deferred.phases[1]!.subPhases[1]!.status = 'deferred';
  assert.deepEqual(errors(deferred), []);

  const enterprise = sampleRoadmap();
  enterprise.phases[1]!.subPhases[0]!.edition = 'enterprise';
  enterprise.phases[1]!.subPhases[1]!.dependsOn = ['foundation-tooling'];
  assert.deepEqual(errors(enterprise), []);
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
