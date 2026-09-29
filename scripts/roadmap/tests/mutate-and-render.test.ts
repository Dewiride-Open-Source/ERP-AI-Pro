import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { locate, serialize, type Roadmap } from '../lib/model.ts';
import { RoadmapError, addPhase, addSubPhase, block, defer, done, move, note, resume, setAcceptance, setDependsOn, setEdition, start } from '../lib/mutate.ts';
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

  addSubPhase(roadmap, { phase: 'P01', id: 'foundation-docs', title: 'Docs', scope: 'Write docs', edition: 'community', after: 'foundation-governance', tags: ['docs'] }, DATE);
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

test('edition applies to a sub-phase or to every sub-phase of a phase and --confirm stamps the date', () => {
  const roadmap = sampleRoadmap();
  const changed = setEdition(roadmap, 'enterprise', ['P02'], true, DATE);
  assert.deepEqual(
    changed.map((s) => s.id),
    ['authentication-oidc', 'authentication-ai-helper'],
  );
  for (const sub of roadmap.phases[1]!.subPhases) {
    assert.equal(sub.edition, 'enterprise');
    assert.equal(sub.editionConfirmedOn, DATE);
  }
  assert.equal(roadmap.updatedOn, DATE);

  const single = setEdition(roadmap, 'community', ['authentication-oidc', 'authentication', 'P02.1'], true, '2026-09-21');
  assert.deepEqual(
    single.map((s) => s.id),
    ['authentication-oidc', 'authentication-ai-helper'],
  );
  setEdition(roadmap, 'enterprise', ['P02.2'], true, '2026-09-21');
  assert.equal(roadmap.phases[1]!.subPhases[0]!.edition, 'community');
  assert.equal(roadmap.phases[1]!.subPhases[1]!.edition, 'enterprise');
  assert.equal(roadmap.phases[1]!.subPhases[1]!.editionConfirmedOn, '2026-09-21');
  assert.equal(roadmap.phases[0]!.subPhases[0]!.editionConfirmedOn, '2026-09-19');
  assert.deepEqual(validate(roadmap, { today: '2026-09-21' }).filter((i) => i.level === 'error'), []);
});

test('edition without --confirm records a recommendation and clears the confirmation', () => {
  const roadmap = sampleRoadmap();
  const [sub] = setEdition(roadmap, 'enterprise', ['foundation-tooling'], false, DATE);
  assert.equal(sub!.edition, 'enterprise');
  assert.equal('editionConfirmedOn' in sub!, false);
  assert.equal(roadmap.updatedOn, DATE);

  setEdition(roadmap, 'community', ['foundation-governance'], false, DATE);
  assert.ok(
    validate(roadmap, { today: DATE }).some((i) => i.level === 'error' && i.message.includes('(foundation-governance): done without editionConfirmedOn')),
  );
});

test('edition refuses an unknown edition, an unknown id and a phase without sub-phases', () => {
  const roadmap = sampleRoadmap();
  assert.throws(() => setEdition(roadmap, 'premium', ['foundation'], true, DATE), /unknown edition "premium"; use community or enterprise/);
  assert.throws(() => setEdition(roadmap, 'Community', ['foundation'], true, DATE), RoadmapError);
  assert.throws(() => setEdition(roadmap, 'community', [], true, DATE), RoadmapError);
  assert.throws(() => setEdition(roadmap, 'enterprise', ['foundation', 'nope'], true, DATE), /unknown id or label "nope"/);
  addPhase(roadmap, { id: 'clients', title: 'Clients', goal: 'Client master', milestone: 'm1-live' }, DATE);
  assert.throws(() => setEdition(roadmap, 'enterprise', ['foundation', 'clients'], true, DATE), /phase "clients" has no sub-phases/);
  for (const sub of roadmap.phases[0]!.subPhases) {
    assert.equal(sub.edition, 'community');
    assert.equal(sub.editionConfirmedOn, '2026-09-19');
  }
});

test('start and done refuse a sub-phase whose edition is not confirmed', () => {
  const roadmap = sampleRoadmap();
  const refusal = /"authentication-ai-helper" has the enterprise edition recommended but not confirmed; ask the owner, then run: edition enterprise authentication-ai-helper --confirm/;
  assert.throws(() => start(roadmap, 'P02.2', DATE), refusal);
  assert.throws(() => done(roadmap, 'authentication-ai-helper', DATE), refusal);
  assert.equal(roadmap.phases[1]!.subPhases[1]!.status, 'planned');
  assert.equal(roadmap.updatedOn, '2026-09-19');

  setEdition(roadmap, 'enterprise', ['authentication-ai-helper'], true, DATE);
  assert.equal(start(roadmap, 'authentication-ai-helper', DATE).status, 'in-progress');
  setEdition(roadmap, 'enterprise', ['authentication-ai-helper'], false, DATE);
  assert.throws(() => done(roadmap, 'authentication-ai-helper', DATE), refusal);
});

test('add-sub-phase records the edition and confirms it only when asked', () => {
  const roadmap = sampleRoadmap();
  const recommended = addSubPhase(roadmap, { phase: 'authentication', id: 'authentication-mfa', title: 'MFA', scope: 'Second factor', edition: 'enterprise' }, DATE);
  assert.equal(recommended.edition, 'enterprise');
  assert.equal('editionConfirmedOn' in recommended, false);
  const confirmed = addSubPhase(roadmap, { phase: 'P01', id: 'foundation-docs', title: 'Docs', scope: 'Write docs', edition: 'community', confirm: true }, DATE);
  assert.equal(confirmed.edition, 'community');
  assert.equal(confirmed.editionConfirmedOn, DATE);
  assert.throws(
    () => addSubPhase(roadmap, { phase: 'P01', id: 'foundation-extra', title: 'Extra', scope: 'More', edition: 'premium' }, DATE),
    /unknown edition "premium"/,
  );
  assert.equal(locate(roadmap, 'foundation-extra'), undefined);
  assert.deepEqual(validate(roadmap, { today: DATE }).filter((i) => i.level === 'error'), []);
});

test('depends-on replaces the dependencies of phases and sub-phases and resolves labels', () => {
  const roadmap = sampleRoadmap();
  const items = setDependsOn(roadmap, ['P02.2'], ['P01.2', 'authentication-oidc', 'foundation-tooling'], DATE);
  assert.deepEqual(
    items.map((i) => i.id),
    ['authentication-ai-helper'],
  );
  assert.deepEqual(roadmap.phases[1]!.subPhases[1]!.dependsOn, ['foundation-tooling', 'authentication-oidc']);
  assert.equal(roadmap.updatedOn, DATE);

  setDependsOn(roadmap, ['authentication', 'P01.2'], ['foundation-governance'], DATE);
  assert.deepEqual(roadmap.phases[1]!.dependsOn, ['foundation-governance']);
  assert.deepEqual(roadmap.phases[0]!.subPhases[1]!.dependsOn, ['foundation-governance']);
  roadmap.phases[1]!.dependsOn.push('foundation-tooling');
  assert.deepEqual(roadmap.phases[0]!.subPhases[1]!.dependsOn, ['foundation-governance']);

  setDependsOn(roadmap, ['P02'], [], DATE);
  assert.deepEqual(roadmap.phases[1]!.dependsOn, []);
  assert.deepEqual(validate(roadmap, { today: DATE }).filter((i) => i.level === 'error'), []);
});

test('depends-on refuses unknown items and unknown dependencies', () => {
  const roadmap = sampleRoadmap();
  assert.throws(() => setDependsOn(roadmap, ['nope'], ['foundation'], DATE), /unknown id or label "nope"/);
  assert.throws(() => setDependsOn(roadmap, ['authentication'], ['foundation-tooling', 'P09'], DATE), /unknown dependency "P09"/);
  assert.throws(() => setDependsOn(roadmap, [], ['foundation'], DATE), RoadmapError);
  assert.deepEqual(roadmap.phases[1]!.dependsOn, ['foundation']);
  assert.equal(roadmap.updatedOn, '2026-09-19');
});

test('render shows the edition of every phase and sub-phase and what awaits confirmation', () => {
  const roadmap = sampleRoadmap();
  addPhase(roadmap, { id: 'clients', title: 'Clients', goal: 'Client master', milestone: 'm1-live' }, DATE);
  const markdown = render(roadmap);
  assert.ok(markdown.includes("Editions: 3 Community · 1 Enterprise · 1 awaiting the owner's confirmation"));
  assert.ok(markdown.includes('Edition: Community (this repository, LGPL-3.0-only) · Enterprise (built in the private Enterprise repository) · "recommended" until the owner confirms'));
  assert.ok(markdown.includes('| Label | Phase | Milestone | Edition | Status | Done |\n|---|---|---|---|---|---|'));
  assert.ok(markdown.includes('| P01 | [Foundation](#phase-foundation) | M0 Ready | Community | ⏳ in progress | 1/2 |'));
  assert.ok(markdown.includes('| P02 | [Authentication](#phase-authentication) | M1 Live | Mixed | planned | 0/2 |'));
  assert.ok(markdown.includes('| P03 | [Clients](#phase-clients) | M1 Live | — | planned | 0/0 |'));
  assert.ok(markdown.includes('Id `authentication` · Milestone: M1 Live · Edition: Mixed · Status: planned · Depends on: `foundation`'));
  assert.ok(markdown.includes('Id `clients` · Milestone: M1 Live · Edition: — · Status: planned · Depends on: none'));
  assert.ok(markdown.includes('- **P01.2** Tooling (`foundation-tooling`) · Community (confirmed 2026-09-19) — Roadmap CLI'));
  assert.ok(markdown.includes('  - LICENSE exists\n  - Edition: Community (confirmed 2026-09-19)\n'));
  assert.ok(markdown.includes('  Assistant\n  - Edition: Enterprise (recommended)\n  - Depends on: `authentication-oidc`\n  - Tags: ai'));

  start(roadmap, 'foundation-tooling', DATE);
  assert.ok(render(roadmap).includes('- ⏳ **P01.2** Tooling (`foundation-tooling`) · Community (confirmed 2026-09-19) — in progress since 2026-09-20'));
  done(roadmap, 'foundation-tooling', DATE);
  done(roadmap, 'authentication-oidc', DATE);
  assert.ok(render(roadmap).includes('- **P02.2** AI: helper (`authentication-ai-helper`) · Enterprise (recommended) — Assistant'));
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

test('the CLI records editions, refuses an unconfirmed start and fills a roadmap without editions in one call', () => {
  const dir = mkdtempSync(join(tmpdir(), 'roadmap-'));
  const jsonFile = join(dir, 'roadmap.json');
  const bare = sampleRoadmap();
  for (const sub of bare.phases.flatMap((p) => p.subPhases)) {
    delete (sub as { edition?: string }).edition;
    delete sub.editionConfirmedOn;
  }
  writeFileSync(jsonFile, serialize(bare));
  const cli = resolve(fileURLToPath(import.meta.url), '..', '..', 'roadmap.ts');
  const run = (...args: string[]) => execFileSync(process.execPath, [cli, ...args, '--file', jsonFile], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  const saved = () => JSON.parse(readFileSync(jsonFile, 'utf8')) as Roadmap;

  assert.throws(() => run('check'), /edition must be community or enterprise, got "undefined"/);
  assert.throws(() => run('start', 'P01.2', '--date', DATE), /"foundation-tooling" has no edition; ask the owner, then run: edition community\|enterprise foundation-tooling --confirm/);
  assert.match(run('edition', 'community', 'foundation', 'authentication', '--confirm', '--date', DATE), /confirmed the community edition for 4 sub-phase\(s\)/);
  assert.match(run('check', '--date', DATE), /fresh/);
  assert.ok(saved().phases.flatMap((p) => p.subPhases).every((s) => s.edition === 'community' && s.editionConfirmedOn === DATE));

  assert.match(run('edition', 'enterprise', 'P02.2', '--date', DATE), /recommended the enterprise edition for 1 sub-phase\(s\)/);
  assert.equal(saved().phases[1]!.subPhases[1]!.editionConfirmedOn, undefined);
  assert.throws(() => run('start', 'P02.2', '--date', DATE), /"authentication-ai-helper" has the enterprise edition recommended but not confirmed/);
  assert.throws(() => run('edition', 'premium', 'P02.2', '--date', DATE), /unknown edition "premium"/);

  const addAudit = ['add-sub-phase', '--phase', 'P01', '--id', 'foundation-audit', '--title', 'Audit', '--scope', 'Audit trail', '--date', DATE];
  assert.throws(() => run(...addAudit), /--edition is required/);
  run(...addAudit, '--edition', 'enterprise');
  assert.equal(saved().phases[0]!.subPhases[2]!.edition, 'enterprise');
  assert.equal(saved().phases[0]!.subPhases[2]!.editionConfirmedOn, undefined);
  const next = run('next', '--date', DATE);
  assert.ok(next.includes('P01.2  foundation-tooling  [community, confirmed 2026-09-20]'));
  assert.ok(next.includes('P01.3  foundation-audit  [enterprise, recommended: ask the owner, then edition enterprise foundation-audit --confirm]'));

  assert.match(run('depends-on', 'P01.3', 'P02', '--on', 'P01.2,foundation-governance', '--date', DATE), /dependencies set for P01\.3 \(foundation-audit\), P02 \(authentication\)/);
  assert.deepEqual(saved().phases[0]!.subPhases[2]!.dependsOn, ['foundation-tooling', 'foundation-governance']);
  assert.deepEqual(saved().phases[1]!.dependsOn, ['foundation-tooling', 'foundation-governance']);
  run('depends-on', 'P01.3', '--on', '', '--date', DATE);
  assert.deepEqual(saved().phases[0]!.subPhases[2]!.dependsOn, []);
  assert.throws(() => run('depends-on', 'P01.3', '--date', DATE), /--on is required/);
  assert.match(run('check', '--date', DATE), /fresh/);
});
