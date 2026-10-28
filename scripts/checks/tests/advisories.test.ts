import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import {
  advisoryFindings,
  directDependency,
  pnpmAuditReport,
  reviewedAdvisories,
  severities,
  type Advisory,
  type ReviewedAdvisory,
} from '../lib/advisory-policy.ts';
import { repoRoot } from '../lib/walk.ts';

const cli = join(repoRoot, 'scripts', 'checks', 'advisories.ts');

const bracesPaths = [
  'apps__web>eslint-config-next>@next/eslint-plugin-next>fast-glob>micromatch>braces',
  'packages__ui>shadcn>fast-glob>micromatch>braces',
];

const pnpmAdvisory = (overrides: Record<string, unknown> = {}) => ({
  findings: [{ version: '3.0.3', paths: bracesPaths, dev: false, optional: false, bundled: false }],
  id: 1240992,
  title: 'braces vulnerable to stack-exhaustion denial of service through deeply nested patterns',
  module_name: 'braces',
  vulnerable_versions: '<=3.0.3',
  patched_versions: null,
  patched_versions_unpublished: true,
  severity: 'high',
  cwe: 'CWE-674',
  github_advisory_id: 'GHSA-vfj7-8cjw-p6xm',
  url: 'https://github.com/advisories/GHSA-vfj7-8cjw-p6xm',
  ...overrides,
});

const pnpmReport = (...advisories: ReturnType<typeof pnpmAdvisory>[]) =>
  JSON.stringify({
    advisories: Object.fromEntries(advisories.map((advisory, index) => [String(index + 1), advisory])),
    metadata: {
      vulnerabilities: { info: 0, low: 0, moderate: 0, high: advisories.length, critical: 0 },
      dependencies: 450,
      devDependencies: 239,
      optionalDependencies: 103,
      totalDependencies: 769,
    },
  });

const advisory = (overrides: Partial<Advisory> = {}): Advisory => ({
  id: 'GHSA-vfj7-8cjw-p6xm',
  name: 'braces',
  severity: 'high',
  title: 'braces vulnerable to stack-exhaustion denial of service through deeply nested patterns',
  url: 'https://github.com/advisories/GHSA-vfj7-8cjw-p6xm',
  vulnerableVersions: '<=3.0.3',
  patchedVersions: undefined,
  versions: ['3.0.3'],
  paths: bracesPaths,
  ...overrides,
});

const braces: ReviewedAdvisory = { id: 'GHSA-vfj7-8cjw-p6xm', name: 'braces', through: ['eslint-config-next', 'shadcn'], reason: 'reviewed' };

test('a pnpm report is read into advisories with their installed versions, paths and published fix', () => {
  const report = pnpmAuditReport(
    pnpmReport(
      pnpmAdvisory(),
      pnpmAdvisory({
        module_name: 'sharp',
        severity: 'critical',
        github_advisory_id: 'GHSA-wq5f-xc86-pv6w',
        url: 'https://github.com/advisories/GHSA-wq5f-xc86-pv6w',
        patched_versions: '>=0.35.5',
        patched_versions_unpublished: undefined,
        findings: [
          { version: '0.35.4', paths: ['apps__web>next>sharp'] },
          { version: '0.34.5', paths: ['packages__ui>sharp'] },
        ],
      }),
    ),
  );

  assert.equal(report.packageCount, 769);
  assert.deepEqual(report.advisories, [
    advisory(),
    advisory({
      id: 'GHSA-wq5f-xc86-pv6w',
      name: 'sharp',
      severity: 'critical',
      url: 'https://github.com/advisories/GHSA-wq5f-xc86-pv6w',
      patchedVersions: '>=0.35.5',
      versions: ['0.34.5', '0.35.4'],
      paths: ['apps__web>next>sharp', 'packages__ui>sharp'],
    }),
  ]);
});

test('a report without advisories or metadata, or with an unknown severity, is refused', () => {
  assert.throws(() => pnpmAuditReport('{"error":{"code":"ERR_PNPM_AUDIT_BAD_RESPONSE"}}'), /no advisories and metadata/);
  assert.throws(() => pnpmAuditReport('null'), /no advisories and metadata/);
  assert.throws(() => pnpmAuditReport(JSON.stringify({ advisories: {} })), /no advisories and metadata/);
  assert.throws(() => pnpmAuditReport(pnpmReport(pnpmAdvisory({ severity: 'severe' }))), /unknown severity "severe"/);
  assert.deepEqual(pnpmAuditReport(pnpmReport()).advisories, []);
});

test('an advisory without a GitHub advisory id is known by its URL', () => {
  const [entry] = pnpmAuditReport(pnpmReport(pnpmAdvisory({ github_advisory_id: '', url: 'https://www.npmjs.com/advisories/1' }))).advisories;

  assert.equal(entry?.id, 'https://www.npmjs.com/advisories/1');
});

test('an unreviewed high or critical advisory fails, naming the upgrade when a fixed version is published', () => {
  const findings = advisoryFindings(
    [
      advisory(),
      advisory({
        id: 'GHSA-wq5f-xc86-pv6w',
        name: 'sharp',
        severity: 'critical',
        title: 'sharp issue',
        url: 'https://github.com/advisories/GHSA-wq5f-xc86-pv6w',
        patchedVersions: '>=0.35.5',
        versions: ['0.35.4'],
        paths: ['apps__web>next>sharp'],
      }),
    ],
    [],
  );

  assert.deepEqual(findings, [
    'braces 3.0.3: GHSA-vfj7-8cjw-p6xm (high) braces vulnerable to stack-exhaustion denial of service through deeply nested patterns; no fixed version is published, so remove the dependency or review it into reviewedAdvisories (https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)',
    'sharp 0.35.4: GHSA-wq5f-xc86-pv6w (critical) sharp issue; upgrade to >=0.35.5 (https://github.com/advisories/GHSA-wq5f-xc86-pv6w)',
  ]);
});

test('info, low and moderate advisories never fail', () => {
  const findings = advisoryFindings(
    ['info', 'low', 'moderate'].map((severity) => advisory({ id: `GHSA-${severity}`, severity: severity as Advisory['severity'], patchedVersions: '>=9.0.0' })),
    [],
  );

  assert.deepEqual(findings, []);
  assert.deepEqual(severities, ['info', 'low', 'moderate', 'high', 'critical']);
});

test('a reviewed advisory passes while no fixed version is published and every path goes through a dependency its review names', () => {
  assert.deepEqual(advisoryFindings([advisory()], [braces]), []);
});

test('a reviewed advisory fails once a fixed version is published', () => {
  assert.deepEqual(advisoryFindings([advisory({ patchedVersions: '>=3.0.4' })], [braces]), [
    'braces 3.0.3: GHSA-vfj7-8cjw-p6xm (high) braces vulnerable to stack-exhaustion denial of service through deeply nested patterns; >=3.0.4 is published, so upgrade and remove its reviewedAdvisories entry',
  ]);
});

test('a reviewed advisory fails when it is reached through a dependency its review does not name', () => {
  const findings = advisoryFindings([advisory({ paths: [...bracesPaths, 'apps__web>next-sitemap>globby>fast-glob>micromatch>braces'] })], [braces]);

  assert.deepEqual(findings, [
    'braces 3.0.3: GHSA-vfj7-8cjw-p6xm (high) braces vulnerable to stack-exhaustion denial of service through deeply nested patterns; reached through apps__web>next-sitemap>globby>fast-glob>micromatch>braces, which its review does not name',
  ]);
});

test('a reviewed advisory that is no longer reported as high or critical fails as stale', () => {
  const expected = ['braces GHSA-vfj7-8cjw-p6xm: no longer reported as high or critical, so remove its reviewedAdvisories entry'];

  assert.deepEqual(advisoryFindings([], [braces]), expected);
  assert.deepEqual(advisoryFindings([advisory({ severity: 'moderate' })], [braces]), expected);
});

test('the direct dependency of a path is the package after the workspace project', () => {
  assert.equal(directDependency('packages__ui>shadcn>@shadcn/registry>fast-glob>micromatch>braces'), 'shadcn');
  assert.equal(directDependency('apps__web>@next/eslint-plugin-next>fast-glob'), '@next/eslint-plugin-next');
  assert.equal(directDependency('apps__web>braces'), 'braces');
  assert.equal(directDependency('braces'), 'braces');
});

test('every reviewed advisory names a GitHub advisory id, its package, the dependencies it is reached through and a reason, once', () => {
  for (const review of reviewedAdvisories) {
    assert.match(review.id, /^GHSA(-[23456789cfghjmpqrvwx]{4}){3}$/, review.id);
    assert.match(review.name, /^(@[a-z0-9._-]+\/)?[a-z0-9._-]+$/, review.id);
    assert.ok(review.through.length > 0 && review.through.every((name) => name.length > 0), review.id);
    assert.ok(review.reason.length > 40, review.id);
  }
  assert.equal(new Set(reviewedAdvisories.map((review) => review.id)).size, reviewedAdvisories.length);
});

test('the command line fails on every finding of a saved report and passes a clean one', (t) => {
  const directory = mkdtempSync(join(tmpdir(), 'advisories-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const report = join(directory, 'pnpm-audit.json');
  const run = () => spawnSync(process.execPath, [cli, '--from', report], { encoding: 'utf8' });

  writeFileSync(
    report,
    pnpmReport(
      pnpmAdvisory({
        severity: 'moderate',
        module_name: 'postcss',
        github_advisory_id: 'GHSA-7fh5-64p2-3v2j',
        url: 'https://github.com/advisories/GHSA-7fh5-64p2-3v2j',
        title: 'postcss issue',
        findings: [{ version: '8.5.28', paths: ['apps__web>postcss'] }],
      }),
    ),
  );
  const clean = run();
  assert.equal(clean.status, 0, clean.stderr);
  assert.match(clean.stdout, /^ {2}below high, not blocking: postcss 8\.5\.28: GHSA-7fh5-64p2-3v2j \(moderate\) postcss issue$/m);
  assert.match(clean.stdout, /advisories ok: 769 npm packages audited, \d+ reviewed advisor(?:y|ies) without a published fix/);

  writeFileSync(
    report,
    pnpmReport(
      pnpmAdvisory(),
      pnpmAdvisory({
        severity: 'critical',
        module_name: 'next',
        github_advisory_id: 'GHSA-673q-mjm6-r5xr',
        url: 'https://github.com/advisories/GHSA-673q-mjm6-r5xr',
        title: 'next issue',
        patched_versions: '>=16.4.1',
        findings: [{ version: '16.4.0', paths: ['apps__web>next'] }],
      }),
    ),
  );
  const failing = run();
  assert.equal(failing.status, 1);
  assert.match(failing.stderr, /^npm advisories outside the advisory policy/m);
  assert.match(
    failing.stderr,
    /^ {2}braces 3\.0\.3: GHSA-vfj7-8cjw-p6xm \(high\) .*; no fixed version is published, so remove the dependency or review it into reviewedAdvisories \(https:\/\/github\.com\/advisories\/GHSA-vfj7-8cjw-p6xm\)$/m,
  );
  assert.match(
    failing.stderr,
    /^ {2}next 16\.4\.0: GHSA-673q-mjm6-r5xr \(critical\) next issue; upgrade to >=16\.4\.1 \(https:\/\/github\.com\/advisories\/GHSA-673q-mjm6-r5xr\)$/m,
  );

  writeFileSync(report, 'not json');
  const broken = run();
  assert.equal(broken.status, 1);
  assert.match(broken.stderr, /^pnpm audit --json printed no report/m);
});
