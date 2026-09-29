import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isDoneFor, isEdition, isEnterpriseOnly, labelOf, locate, phaseEdition, phaseLabel, phaseStatus, resolveId, serialize, subPhaseLabel, subPhasesAwaitedBy } from '../lib/model.ts';
import { sampleRoadmap } from './fixture.ts';

test('labels are derived from position', () => {
  assert.equal(phaseLabel(0), 'P01');
  assert.equal(phaseLabel(11), 'P12');
  assert.equal(subPhaseLabel(6, 2), 'P07.3');
  const roadmap = sampleRoadmap();
  assert.equal(labelOf(roadmap, 'authentication'), 'P02');
  assert.equal(labelOf(roadmap, 'authentication-ai-helper'), 'P02.2');
  assert.equal(labelOf(roadmap, 'missing'), undefined);
});

test('resolveId accepts slugs and positional labels', () => {
  const roadmap = sampleRoadmap();
  assert.equal(resolveId(roadmap, 'foundation-tooling'), 'foundation-tooling');
  assert.equal(resolveId(roadmap, 'P01'), 'foundation');
  assert.equal(resolveId(roadmap, 'p02.1'), 'authentication-oidc');
  assert.equal(resolveId(roadmap, 'P09'), undefined);
  assert.equal(resolveId(roadmap, 'P01.9'), undefined);
});

test('locate returns phase and sub-phase context', () => {
  const roadmap = sampleRoadmap();
  const phase = locate(roadmap, 'authentication');
  assert.equal(phase?.phaseIndex, 1);
  assert.equal(phase?.subPhase, undefined);
  const sub = locate(roadmap, 'authentication-oidc');
  assert.equal(sub?.subIndex, 0);
  assert.equal(sub?.phase.id, 'authentication');
});

test('phase status is derived from sub-phases', () => {
  const roadmap = sampleRoadmap();
  assert.equal(phaseStatus(roadmap.phases[0]!), 'in-progress');
  assert.equal(phaseStatus(roadmap.phases[1]!), 'planned');
  roadmap.phases[0]!.subPhases[1]!.status = 'done';
  assert.equal(phaseStatus(roadmap.phases[0]!), 'done');
  assert.equal(isDoneFor(roadmap, 'foundation', 'community'), true);
  assert.equal(isDoneFor(roadmap, 'foundation', 'enterprise'), true);
  roadmap.phases[1]!.subPhases[0]!.status = 'blocked';
  assert.equal(phaseStatus(roadmap.phases[1]!), 'blocked');
  roadmap.phases[1]!.subPhases.forEach((s) => (s.status = 'deferred'));
  assert.equal(phaseStatus(roadmap.phases[1]!), 'deferred');
  roadmap.phases[1]!.deferred = true;
  assert.equal(phaseStatus(roadmap.phases[1]!), 'deferred');
});

test('phase edition is derived from its sub-phases', () => {
  const roadmap = sampleRoadmap();
  assert.equal(phaseEdition(roadmap.phases[0]!), 'community');
  assert.equal(phaseEdition(roadmap.phases[1]!), 'mixed');
  assert.equal(isEnterpriseOnly(roadmap, 'authentication'), false);
  assert.equal(isEnterpriseOnly(roadmap, 'authentication-ai-helper'), true);
  assert.equal(isEnterpriseOnly(roadmap, 'authentication-oidc'), false);
  assert.equal(isEnterpriseOnly(roadmap, 'missing'), false);
  roadmap.phases[1]!.subPhases[0]!.edition = 'enterprise';
  assert.equal(phaseEdition(roadmap.phases[1]!), 'enterprise');
  assert.equal(isEnterpriseOnly(roadmap, 'authentication'), true);
  roadmap.phases[1]!.subPhases = [];
  assert.equal(phaseEdition(roadmap.phases[1]!), undefined);
  assert.equal(isEnterpriseOnly(roadmap, 'authentication'), false);
  assert.equal(isEdition('community'), true);
  assert.equal(isEdition('enterprise'), true);
  assert.equal(isEdition('Community'), false);
  assert.equal(isEdition(undefined), false);
});

test('a phase is done for community work once its community sub-phases are and for enterprise work once all of them are', () => {
  const roadmap = sampleRoadmap();
  const authentication = roadmap.phases[1]!;
  const [oidc, helper] = authentication.subPhases;
  assert.deepEqual(
    subPhasesAwaitedBy(authentication, 'community').map((s) => s.id),
    ['authentication-oidc'],
  );
  assert.deepEqual(
    subPhasesAwaitedBy(authentication, 'enterprise').map((s) => s.id),
    ['authentication-oidc', 'authentication-ai-helper'],
  );
  assert.equal(isDoneFor(roadmap, 'authentication', 'community'), false);

  oidc!.status = 'done';
  assert.equal(isDoneFor(roadmap, 'authentication', 'community'), true);
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), false);
  assert.equal(isDoneFor(roadmap, 'authentication-oidc', 'enterprise'), true);
  assert.equal(isDoneFor(roadmap, 'authentication-ai-helper', 'community'), false);
  helper!.status = 'deferred';
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), true);
  helper!.status = 'done';
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), true);

  oidc!.status = 'deferred';
  assert.equal(isDoneFor(roadmap, 'authentication', 'community'), false);
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), true);
  oidc!.status = 'done';
  oidc!.edition = 'enterprise';
  assert.equal(isDoneFor(roadmap, 'authentication', 'community'), false);
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), true);
  authentication.deferred = true;
  assert.equal(isDoneFor(roadmap, 'authentication', 'enterprise'), false);
  assert.equal(isDoneFor(roadmap, 'missing', 'community'), false);
});

test('serialize writes the edition after the status and omits a missing confirmation', () => {
  const parsed = JSON.parse(serialize(sampleRoadmap())) as ReturnType<typeof sampleRoadmap>;
  assert.deepEqual(
    Object.keys(parsed.phases[0]!.subPhases[0]!),
    ['id', 'title', 'scope', 'status', 'edition', 'editionConfirmedOn', 'acceptance', 'tags', 'dependsOn', 'startedOn', 'completedOn'],
  );
  assert.deepEqual(
    Object.keys(parsed.phases[1]!.subPhases[1]!),
    ['id', 'title', 'scope', 'status', 'edition', 'acceptance', 'tags', 'dependsOn'],
  );
});

test('serialize is canonical and stable', () => {
  const roadmap = sampleRoadmap();
  const first = serialize(roadmap);
  const reordered = JSON.parse(first) as ReturnType<typeof sampleRoadmap>;
  reordered.phases[0]!.subPhases[0] = { notes: undefined, ...reordered.phases[0]!.subPhases[0]! } as never;
  assert.equal(serialize(reordered), first);
  assert.ok(first.endsWith('\n'));
  assert.ok(!first.includes('"deferred": false'));
});
