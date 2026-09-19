import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isDone, labelOf, locate, phaseLabel, phaseStatus, resolveId, serialize, subPhaseLabel } from '../lib/model.ts';
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
  assert.equal(isDone(roadmap, 'foundation'), true);
  roadmap.phases[1]!.subPhases[0]!.status = 'blocked';
  assert.equal(phaseStatus(roadmap.phases[1]!), 'blocked');
  roadmap.phases[1]!.subPhases.forEach((s) => (s.status = 'deferred'));
  assert.equal(phaseStatus(roadmap.phases[1]!), 'deferred');
  roadmap.phases[1]!.deferred = true;
  assert.equal(phaseStatus(roadmap.phases[1]!), 'deferred');
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
