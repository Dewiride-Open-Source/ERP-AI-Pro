import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import {
  allowedLicences,
  isWithinPolicy,
  nugetPackageLicence,
  nuspecLicence,
  reviewedPackages,
  satisfiesPolicy,
  type PackageLicence,
} from '../lib/licence-policy.ts';
import { repoRoot } from '../lib/walk.ts';

const cli = join(repoRoot, 'scripts', 'checks', 'licences.ts');

const npmPackage = (name: string, licence: string): PackageLicence => ({ ecosystem: 'npm', name, version: '1.0.0', licence, expression: licence });

const nuspec = (metadata: string): string =>
  [
    '<?xml version="1.0" encoding="utf-8"?>',
    '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">',
    '  <metadata>',
    '    <id>Contoso.Library</id>',
    '    <version>1.0.0</version>',
    '    <requireLicenseAcceptance>false</requireLicenseAcceptance>',
    `    ${metadata}`,
    '  </metadata>',
    '</package>',
  ].join('\n');

test('an allowed identifier and an OR expression with one allowed licence are accepted', () => {
  for (const licence of allowedLicences) assert.equal(satisfiesPolicy(licence), true, licence);
  for (const expression of [
    'mit',
    'apache-2.0',
    'MIT OR GPL-3.0-only',
    '(GPL-2.0-only OR MIT)',
    'SSPL-1.0 or BSD-3-Clause',
    '(AGPL-3.0-only OR MIT) AND ISC',
    '((MIT))',
  ]) {
    assert.equal(satisfiesPolicy(expression), true, expression);
  }
});

test('an AND expression with a refused licence is refused', () => {
  for (const expression of [
    'MIT AND GPL-3.0-only',
    'MIT and GPL-2.0-only',
    '(MIT OR Apache-2.0) AND AGPL-3.0-only',
    'Apache-2.0 AND (MIT OR ISC) AND SSPL-1.0',
  ]) {
    assert.equal(satisfiesPolicy(expression), false, expression);
  }
});

test('GPL, AGPL, SSPL, BUSL, Elastic and non-commercial licences are refused', () => {
  for (const expression of [
    'GPL-2.0-only',
    'GPL-2.0-or-later',
    'GPL-2.0+',
    'GPL-3.0',
    'GPL-3.0-only',
    'GPL-3.0-or-later',
    'GPL-2.0-only WITH Classpath-exception-2.0',
    'AGPL-1.0-only',
    'AGPL-3.0-only',
    'AGPL-3.0-or-later',
    'SSPL-1.0',
    'BUSL-1.1',
    'Elastic-2.0',
    'CC-BY-NC-4.0',
    'CC-BY-NC-SA-4.0',
    'CC-BY-NC-ND-4.0',
    'PolyForm-Noncommercial-1.0.0',
    'Apache-2.0 WITH Commons-Clause',
    'LGPL-2.0-only',
  ]) {
    assert.equal(satisfiesPolicy(expression), false, expression);
  }
});

test('LGPL, MPL and the sharp expression are accepted', () => {
  for (const expression of [
    'LGPL-2.1-only',
    'LGPL-2.1-or-later',
    'LGPL-2.1+',
    'LGPL-3.0',
    'LGPL-3.0-only',
    'LGPL-3.0-or-later',
    'MPL-2.0',
    'Apache-2.0 AND LGPL-3.0-or-later',
    '(Apache-2.0 AND LGPL-3.0-or-later)',
  ]) {
    assert.equal(satisfiesPolicy(expression), true, expression);
  }
});

test('a free-text licence is refused unless the package is reviewed', () => {
  for (const licence of ['SIL OPEN FONT LICENSE', 'SEE LICENSE IN LICENSE.md', 'UNLICENSED', 'Unknown', '', 'MIT AND', '(MIT', 'MIT)', 'MIT OR OR ISC', 'LicenseRef-Proprietary']) {
    assert.equal(satisfiesPolicy(licence), false, licence);
    assert.equal(isWithinPolicy(npmPackage('left-pad', licence)), false, licence);
  }
  assert.equal(isWithinPolicy(npmPackage('geist', 'SIL OPEN FONT LICENSE')), true);
  assert.equal(isWithinPolicy({ ...npmPackage('geist', 'SIL OPEN FONT LICENSE'), ecosystem: 'nuget' }), false);
  assert.equal(isWithinPolicy(nugetPackageLicence('Microsoft.Data.SqlClient.SNI.runtime', '7.1.0', { kind: 'file', file: 'LICENSE.txt' })), true);
  assert.equal(isWithinPolicy(nugetPackageLicence('microsoft.identity.client.nativeinterop', '0.20.6', { kind: 'file', file: 'LICENSE' })), true);
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' })), false);
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'url', url: 'https://contoso.example/licence' })), false);
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'missing' })), false);
});

test('a nuspec licence is read from an expression, a file and a licence URL', () => {
  const deprecatedUrl = '<licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>';
  assert.deepEqual(nuspecLicence(nuspec('<license type="expression">MIT</license>')), { kind: 'expression', expression: 'MIT' });
  assert.deepEqual(nuspecLicence(nuspec(`<license type="expression" version="1.0.0">(MIT OR Apache-2.0)</license>${deprecatedUrl}`)), {
    kind: 'expression',
    expression: '(MIT OR Apache-2.0)',
  });
  assert.deepEqual(nuspecLicence(nuspec(`<license type="file">LICENSE.txt</license>${deprecatedUrl}`)), { kind: 'file', file: 'LICENSE.txt' });
  assert.deepEqual(nuspecLicence(nuspec('<licenseUrl>https://licenses.nuget.org/MIT</licenseUrl>')), { kind: 'expression', expression: 'MIT' });
  assert.deepEqual(nuspecLicence(nuspec('<licenseUrl>https://licenses.nuget.org/(MIT%20OR%20Apache-2.0)</licenseUrl>')), {
    kind: 'expression',
    expression: '(MIT OR Apache-2.0)',
  });
  assert.deepEqual(nuspecLicence(nuspec('<licenseUrl>https://contoso.example/licence?edition=full&amp;lang=en</licenseUrl>')), {
    kind: 'url',
    url: 'https://contoso.example/licence?edition=full&lang=en',
  });
  assert.deepEqual(nuspecLicence(nuspec('<projectUrl>https://contoso.example</projectUrl>')), { kind: 'missing' });
  assert.equal(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' }).licence, 'licence file LICENSE.txt');
});

test('the command line reports every package outside the policy', (t) => {
  const directory = mkdtempSync(join(tmpdir(), 'licences-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const run = (...args: string[]) => spawnSync(process.execPath, [cli, ...args], { encoding: 'utf8' });

  const listing = join(directory, 'pnpm-licenses.json');
  const pnpmEntry = (name: string, license: string, versions = ['1.0.0']) => ({ name, versions, paths: versions.map(() => join(directory, name)), license });
  const writeListing = (entries: ReturnType<typeof pnpmEntry>[]) => {
    const groups: Record<string, ReturnType<typeof pnpmEntry>[]> = {};
    for (const entry of entries) (groups[entry.license] ??= []).push(entry);
    writeFileSync(listing, JSON.stringify(groups));
  };
  const allowedNpm = [
    pnpmEntry('react', 'MIT', ['19.2.0', '18.3.1']),
    pnpmEntry('@img/sharp-linux-x64', 'Apache-2.0 AND LGPL-3.0-or-later'),
    pnpmEntry('geist', 'SIL OPEN FONT LICENSE'),
  ];
  writeListing(allowedNpm);
  const cleanNpm = run('npm', '--from', listing);
  assert.equal(cleanNpm.status, 0, cleanNpm.stderr);
  assert.match(cleanNpm.stdout, /licences ok: 4 npm packages/);

  writeListing([...allowedNpm, pnpmEntry('copyleft', 'GPL-3.0-only'), pnpmEntry('fonts-elsewhere', 'SIL OPEN FONT LICENSE'), pnpmEntry('closed', 'UNLICENSED')]);
  const failingNpm = run('npm', '--from', listing);
  assert.equal(failingNpm.status, 1);
  assert.match(failingNpm.stderr, /^ {2}closed 1\.0\.0: UNLICENSED$/m);
  assert.match(failingNpm.stderr, /^ {2}copyleft 1\.0\.0: GPL-3\.0-only$/m);
  assert.match(failingNpm.stderr, /^ {2}fonts-elsewhere 1\.0\.0: SIL OPEN FONT LICENSE$/m);
  assert.doesNotMatch(failingNpm.stderr, /react|sharp|geist/);

  const packages = join(directory, 'packages');
  const writeNuspec = (name: string, version: string, metadata: string) => {
    const folder = join(packages, name.toLowerCase(), version);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, `${name.toLowerCase()}.nuspec`), nuspec(metadata));
  };
  const writeLockFile = (host: string, dependencies: Record<string, { type: string; resolved?: string }>) => {
    const folder = join(directory, 'backend', 'Hosts', host, `Dewiride.Erp.Host.${host}`);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, 'packages.lock.json'), JSON.stringify({ version: 2, dependencies: { 'net10.0': dependencies } }));
  };
  writeNuspec('Contoso.Expression', '1.0.0', '<license type="expression">MIT</license>');
  writeNuspec('Contoso.Url', '2.0.0', '<licenseUrl>https://licenses.nuget.org/Apache-2.0</licenseUrl>');
  writeNuspec('Microsoft.Data.SqlClient.SNI.runtime', '7.1.0', '<license type="file">LICENSE.txt</license>');
  writeLockFile('Api', {
    'Contoso.Expression': { type: 'Direct', resolved: '1.0.0' },
    'Contoso.Url': { type: 'Transitive', resolved: '2.0.0' },
    'Microsoft.Data.SqlClient.SNI.runtime': { type: 'CentralTransitive', resolved: '7.1.0' },
    'Dewiride.Erp.Host.Composition': { type: 'Project' },
  });
  writeLockFile('Migrator', { 'Contoso.Expression': { type: 'Transitive', resolved: '1.0.0' } });
  const cleanNuget = run('nuget', '--root', directory, '--packages', packages);
  assert.equal(cleanNuget.status, 0, cleanNuget.stderr);
  assert.match(cleanNuget.stdout, /licences ok: 3 nuget packages/);

  writeNuspec('Contoso.Copyleft', '1.0.0', '<license type="expression">AGPL-3.0-only</license>');
  writeNuspec('Contoso.Native', '1.0.0', '<license type="file">LICENSE.txt</license>');
  writeNuspec('Contoso.Link', '1.0.0', '<licenseUrl>https://contoso.example/licence</licenseUrl>');
  writeLockFile('HealthProbe', {
    'Contoso.Copyleft': { type: 'Direct', resolved: '1.0.0' },
    'Contoso.Native': { type: 'Transitive', resolved: '1.0.0' },
    'Contoso.Link': { type: 'Transitive', resolved: '1.0.0' },
  });
  const failingNuget = run('nuget', '--root', directory, '--packages', packages);
  assert.equal(failingNuget.status, 1);
  assert.match(failingNuget.stderr, /^ {2}Contoso\.Copyleft 1\.0\.0: AGPL-3\.0-only$/m);
  assert.match(failingNuget.stderr, /^ {2}Contoso\.Link 1\.0\.0: licence URL https:\/\/contoso\.example\/licence$/m);
  assert.match(failingNuget.stderr, /^ {2}Contoso\.Native 1\.0\.0: licence file LICENSE\.txt$/m);
  assert.doesNotMatch(failingNuget.stderr, /Contoso\.Expression|Contoso\.Url|SNI/);

  writeLockFile('HealthProbe', { 'Contoso.Absent': { type: 'Direct', resolved: '3.0.0' } });
  const unrestored = run('nuget', '--root', directory, '--packages', packages);
  assert.equal(unrestored.status, 1);
  assert.match(unrestored.stderr, /^ {2}Contoso\.Absent 3\.0\.0$/m);

  assert.equal(run('pip').status, 64);
});

test('the dependency review configuration lists exactly the policy', () => {
  const configuration = readFileSync(join(repoRoot, '.github', 'dependency-review-config.yml'), 'utf8');
  const list = (key: string): string[] => {
    const block = new RegExp(`^${key}:\\r?\\n((?:[ \\t]+- .*(?:\\r?\\n|$))+)`, 'm').exec(configuration)?.[1] ?? '';
    return [...block.matchAll(/^[ \t]+- (.+?)[ \t]*\r?$/gm)].map((match) => match[1] ?? '');
  };
  assert.deepEqual(list('allow-licenses'), [...allowedLicences]);
  assert.deepEqual(
    list('allow-dependencies-licenses'),
    reviewedPackages.map((reviewed) => `pkg:${reviewed.ecosystem}/${reviewed.name}`),
  );
  assert.doesNotMatch(configuration, /^deny-licenses:/m);
  assert.match(readFileSync(join(repoRoot, '.github', 'workflows', 'dependency-review.yml'), 'utf8'), /^ +config-file: \.\/\.github\/dependency-review-config\.yml$/m);
});
