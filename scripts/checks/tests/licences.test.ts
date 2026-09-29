import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import {
  allowedLicences,
  assetsFilePackages,
  isReviewed,
  isWithinPolicy,
  nugetPackageLicence,
  nuspecLicence,
  policyFinding,
  reviewedPackages,
  satisfiesPolicy,
  toolManifestPackages,
  type PackageLicence,
  type ReviewedPackage,
} from '../lib/licence-policy.ts';
import { repoRoot } from '../lib/walk.ts';

const cli = join(repoRoot, 'scripts', 'checks', 'licences.ts');

const npmPackage = (name: string, licence: string): PackageLicence => ({
  ecosystem: 'npm',
  name,
  version: '1.0.0',
  licence,
  expression: licence,
  evidence: { kind: 'declared', licence },
});

const sha256 = (content: string): string => createHash('sha256').update(content).digest('hex');

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
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' }, Buffer.from('CONTOSO TERMS'))), false);
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'url', url: 'https://contoso.example/licence' })), false);
  assert.equal(isWithinPolicy(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'missing' })), false);
});

test('a reviewed package passes only while its licence evidence equals the pinned evidence', () => {
  const terms = 'CONTOSO NATIVE LICENSE TERMS\n';
  const reviewed: ReviewedPackage[] = [
    { ecosystem: 'npm', name: 'contoso-fonts', evidence: { kind: 'declared', licence: 'CONTOSO FONT LICENSE' }, reason: 'CONTOSO-FONT.txt read' },
    { ecosystem: 'nuget', name: 'Contoso.Native', evidence: { kind: 'file', sha256: sha256(terms) }, reason: 'LICENSE.txt read' },
    { ecosystem: 'nuget', name: 'Contoso.Linked', evidence: { kind: 'url', url: 'https://contoso.example/licence' }, reason: 'the linked page read' },
  ];
  const native = (content?: string) =>
    nugetPackageLicence('Contoso.Native', '2.0.0', { kind: 'file', file: 'LICENSE.txt' }, content === undefined ? undefined : Buffer.from(content));
  const linked = (url: string) => nugetPackageLicence('Contoso.Linked', '1.0.0', { kind: 'url', url });

  for (const entry of [
    npmPackage('contoso-fonts', 'CONTOSO FONT LICENSE'),
    native(terms),
    { ...native(terms), name: 'contoso.native' },
    linked('https://contoso.example/licence'),
  ]) {
    assert.equal(isReviewed(entry, reviewed), true, entry.licence);
    assert.equal(policyFinding(entry, reviewed), undefined, entry.licence);
  }

  const changed = [
    npmPackage('contoso-fonts', 'CONTOSO FONT LICENSE 2.0'),
    npmPackage('Contoso-Fonts', 'CONTOSO FONT LICENSE'),
    native('CONTOSO NATIVE LICENSE TERMS, REVISED\n'),
    native(),
    linked('https://contoso.example/licence/v2'),
    { ...linked('https://contoso.example/licence'), evidence: { kind: 'declared' as const, licence: 'https://contoso.example/licence' } },
  ];
  for (const entry of changed) {
    assert.equal(isReviewed(entry, reviewed), false, entry.licence);
    assert.equal(isWithinPolicy(entry, reviewed), false, entry.licence);
  }
  assert.equal(
    policyFinding(changed[0]!, reviewed),
    'CONTOSO FONT LICENSE 2.0 (licence changed since it was reviewed as declared licence "CONTOSO FONT LICENSE"; read it again and update reviewedPackages)',
  );
  assert.equal(policyFinding(changed[1]!, reviewed), 'CONTOSO FONT LICENSE');
  assert.equal(
    policyFinding(changed[2]!, reviewed),
    `licence file LICENSE.txt, SHA-256 ${sha256('CONTOSO NATIVE LICENSE TERMS, REVISED\n')} (licence changed since it was reviewed as licence file SHA-256 ${sha256(terms)}; read it again and update reviewedPackages)`,
  );
  assert.match(policyFinding(changed[3]!, reviewed) ?? '', /^licence file LICENSE\.txt, missing from the package \(licence changed since/);
  assert.equal(
    policyFinding(changed[4]!, reviewed),
    'licence URL https://contoso.example/licence/v2 (licence changed since it was reviewed as licence URL https://contoso.example/licence; read it again and update reviewedPackages)',
  );

  assert.equal(policyFinding(npmPackage('contoso-fonts', 'MIT'), reviewed), undefined);
  assert.equal(policyFinding(npmPackage('left-pad', 'CONTOSO FONT LICENSE'), reviewed), 'CONTOSO FONT LICENSE');
});

test('every reviewed package pins the evidence that was read', () => {
  for (const review of reviewedPackages) {
    const label = `${review.ecosystem}/${review.name}`;
    assert.ok(review.reason.length > 0, label);
    switch (review.evidence.kind) {
      case 'declared':
        assert.ok(review.evidence.licence.length > 0, label);
        assert.equal(satisfiesPolicy(review.evidence.licence), false, label);
        break;
      case 'file':
        assert.equal(review.ecosystem, 'nuget', label);
        assert.match(review.evidence.sha256, /^[0-9a-f]{64}$/, label);
        break;
      case 'url':
        assert.equal(review.ecosystem, 'nuget', label);
        assert.match(review.evidence.url, /^https:\/\//, label);
        assert.doesNotMatch(review.evidence.url, /^https:\/\/licenses\.nuget\.org\//, label);
        break;
    }
  }
});

test('restored packages are read from an assets file and local tools from the tools manifest', () => {
  const assets = JSON.stringify({
    version: 3,
    libraries: {
      'Azure.Core/1.60.0': { sha512: 'AA==', type: 'package', path: 'azure.core/1.60.0', files: ['azure.core.nuspec'] },
      'xunit.v3.mtp-v2/4.0.1': { type: 'package', path: 'xunit.v3.mtp-v2/4.0.1' },
      'Dewiride.Erp.Testing/1.0.0': { type: 'project', path: '../../Tests/Shared/Dewiride.Erp.Testing/Dewiride.Erp.Testing.csproj' },
    },
  });
  assert.deepEqual(assetsFilePackages(assets), [
    { name: 'Azure.Core', version: '1.60.0' },
    { name: 'xunit.v3.mtp-v2', version: '4.0.1' },
  ]);
  assert.deepEqual(assetsFilePackages(JSON.stringify({ version: 3 })), []);
  assert.throws(
    () => assetsFilePackages(JSON.stringify({ version: 3, libraries: { 'Contoso.Broken': { type: 'package' } } })),
    /library "Contoso\.Broken" is not <name>\/<version>/,
  );

  const manifest = JSON.stringify({
    version: 1,
    isRoot: true,
    tools: {
      'dotnet-ef': { version: '10.0.12', commands: ['dotnet-ef'], rollForward: false },
      'microsoft.openapi.kiota': { version: '1.35.0', commands: ['kiota'], rollForward: false },
    },
  });
  assert.deepEqual(toolManifestPackages(manifest), [
    { name: 'dotnet-ef', version: '10.0.12' },
    { name: 'microsoft.openapi.kiota', version: '1.35.0' },
  ]);
  assert.deepEqual(toolManifestPackages(JSON.stringify({ version: 1, isRoot: true })), []);
  assert.throws(() => toolManifestPackages(JSON.stringify({ version: 1, tools: { 'contoso.tool': { commands: ['contoso'] } } })), /tool "contoso\.tool" has no version/);
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
  assert.deepEqual(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' }, Buffer.from('CONTOSO TERMS')), {
    ecosystem: 'nuget',
    name: 'Contoso.Native',
    version: '1.0.0',
    licence: `licence file LICENSE.txt, SHA-256 ${sha256('CONTOSO TERMS')}`,
    expression: undefined,
    evidence: { kind: 'file', sha256: sha256('CONTOSO TERMS') },
  });
  assert.equal(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' }).licence, 'licence file LICENSE.txt, missing from the package');
  assert.equal(nugetPackageLicence('Contoso.Native', '1.0.0', { kind: 'file', file: 'LICENSE.txt' }).evidence, undefined);
  assert.deepEqual(nugetPackageLicence('Contoso.Url', '1.0.0', { kind: 'expression', expression: 'MIT' }).evidence, { kind: 'declared', licence: 'MIT' });
  assert.deepEqual(nugetPackageLicence('Contoso.Link', '1.0.0', { kind: 'url', url: 'https://contoso.example/licence' }).evidence, {
    kind: 'url',
    url: 'https://contoso.example/licence',
  });
  assert.equal(nugetPackageLicence('Contoso.Silent', '1.0.0', { kind: 'missing' }).evidence, undefined);
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

  writeListing([
    ...allowedNpm,
    pnpmEntry('copyleft', 'GPL-3.0-only'),
    pnpmEntry('fonts-elsewhere', 'SIL OPEN FONT LICENSE'),
    pnpmEntry('closed', 'UNLICENSED'),
    pnpmEntry('geist', 'SIL OPEN FONT LICENSE 2.0', ['2.0.0']),
  ]);
  const failingNpm = run('npm', '--from', listing);
  assert.equal(failingNpm.status, 1);
  assert.match(failingNpm.stderr, /^ {2}closed 1\.0\.0: UNLICENSED$/m);
  assert.match(failingNpm.stderr, /^ {2}copyleft 1\.0\.0: GPL-3\.0-only$/m);
  assert.match(failingNpm.stderr, /^ {2}fonts-elsewhere 1\.0\.0: SIL OPEN FONT LICENSE$/m);
  assert.match(
    failingNpm.stderr,
    /^ {2}geist 2\.0\.0: SIL OPEN FONT LICENSE 2\.0 \(licence changed since it was reviewed as declared licence "SIL OPEN FONT LICENSE"; read it again and update reviewedPackages\)$/m,
  );
  assert.doesNotMatch(failingNpm.stderr, /react|sharp|geist 1\.0\.0/);

  const packages = join(directory, 'packages');
  const writePackageFile = (name: string, version: string, file: string, content: string) => {
    const folder = join(packages, name.toLowerCase(), version);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, file), content);
  };
  const writeNuspec = (name: string, version: string, metadata: string, file = `${name.toLowerCase()}.nuspec`) =>
    writePackageFile(name, version, file, nuspec(metadata));
  const writeAssets = (project: string, libraries: Record<string, { type: string }>) => {
    const folder = join(directory, 'backend', 'artifacts', 'obj', project);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, 'project.assets.json'), JSON.stringify({ version: 3, targets: { 'net10.0': {} }, libraries }));
  };
  const writeTools = (tools: Record<string, string>) => {
    const folder = join(directory, 'backend', '.config');
    mkdirSync(folder, { recursive: true });
    const entries = Object.fromEntries(Object.entries(tools).map(([id, version]) => [id, { version, commands: [id], rollForward: false }]));
    writeFileSync(join(folder, 'dotnet-tools.json'), JSON.stringify({ version: 1, isRoot: true, tools: entries }));
  };
  const nugetRun = () => run('nuget', '--root', directory, '--packages', packages);

  const unrestored = nugetRun();
  assert.equal(unrestored.status, 1);
  assert.match(unrestored.stderr, /^no project\.assets\.json under .+; run "dotnet restore --locked-mode" in backend first$/m);

  writeNuspec('Contoso.Expression', '1.0.0', '<license type="expression">MIT</license>');
  writeNuspec('Contoso.Url', '2.0.0', '<licenseUrl>https://licenses.nuget.org/Apache-2.0</licenseUrl>');
  writeNuspec('Contoso.Testing', '1.0.0', '<license type="expression">Apache-2.0</license>');
  writeNuspec('Contoso.Tool', '1.0.0', '<license type="expression">MIT</license>', 'Contoso.Tool.nuspec');
  writeAssets('Dewiride.Erp.Host.Api', {
    'Contoso.Expression/1.0.0': { type: 'package' },
    'Contoso.Url/2.0.0': { type: 'package' },
    'Dewiride.Erp.Host.Composition/1.0.0': { type: 'project' },
  });
  writeAssets('Dewiride.Erp.ArchitectureTests', { 'contoso.expression/1.0.0': { type: 'package' }, 'Contoso.Testing/1.0.0': { type: 'package' } });
  const withoutManifest = nugetRun();
  assert.equal(withoutManifest.status, 1);
  assert.match(withoutManifest.stderr, /dotnet-tools\.json is missing$/m);

  writeTools({ 'contoso.tool': '1.0.0' });
  const cleanNuget = nugetRun();
  assert.equal(cleanNuget.status, 0, cleanNuget.stderr);
  assert.match(cleanNuget.stdout, /licences ok: 4 nuget packages/);

  const pinnedSni = reviewedPackages.find((review) => review.name === 'Microsoft.Data.SqlClient.SNI.runtime')?.evidence;
  if (pinnedSni?.kind !== 'file') assert.fail('Microsoft.Data.SqlClient.SNI.runtime pins its licence file');
  writeNuspec('Contoso.Copyleft', '1.0.0', '<license type="expression">AGPL-3.0-only</license>');
  writeNuspec('Contoso.Native', '1.0.0', '<license type="file">LICENSE.txt</license>');
  writePackageFile('Contoso.Native', '1.0.0', 'license.TXT', 'CONTOSO NATIVE TERMS');
  writeNuspec('Contoso.Unpacked', '1.0.0', '<license type="file">LICENSE.txt</license>');
  writeNuspec('Contoso.Link', '1.0.0', '<licenseUrl>https://contoso.example/licence</licenseUrl>');
  writeNuspec('Microsoft.Data.SqlClient.SNI.runtime', '7.1.0', '<license type="file">LICENSE.txt</license>');
  writePackageFile('Microsoft.Data.SqlClient.SNI.runtime', '7.1.0', 'LICENSE.txt', 'REVISED SNI TERMS');
  writeNuspec('Contoso.CopyleftTool', '2.0.0', '<license type="expression">GPL-3.0-only</license>');
  writeAssets('Dewiride.Erp.Host.HealthProbe', {
    'Contoso.Copyleft/1.0.0': { type: 'package' },
    'Contoso.Native/1.0.0': { type: 'package' },
    'Contoso.Unpacked/1.0.0': { type: 'package' },
    'Contoso.Link/1.0.0': { type: 'package' },
    'Microsoft.Data.SqlClient.SNI.runtime/7.1.0': { type: 'package' },
  });
  writeTools({ 'contoso.tool': '1.0.0', 'contoso.copylefttool': '2.0.0' });
  const failingNuget = nugetRun();
  assert.equal(failingNuget.status, 1);
  const reported = failingNuget.stderr.split(/\r?\n/);
  for (const line of [
    '  Contoso.Copyleft 1.0.0: AGPL-3.0-only',
    '  contoso.copylefttool 2.0.0: GPL-3.0-only',
    '  Contoso.Link 1.0.0: licence URL https://contoso.example/licence',
    `  Contoso.Native 1.0.0: licence file LICENSE.txt, SHA-256 ${sha256('CONTOSO NATIVE TERMS')}`,
    '  Contoso.Unpacked 1.0.0: licence file LICENSE.txt, missing from the package',
    `  Microsoft.Data.SqlClient.SNI.runtime 7.1.0: licence file LICENSE.txt, SHA-256 ${sha256('REVISED SNI TERMS')} (licence changed since it was reviewed as licence file SHA-256 ${pinnedSni.sha256}; read it again and update reviewedPackages)`,
  ]) {
    assert.ok(reported.includes(line), `${line}\n${failingNuget.stderr}`);
  }
  assert.doesNotMatch(failingNuget.stderr, /Contoso\.Expression|Contoso\.Url|Contoso\.Testing|contoso\.tool /);

  writeAssets('Dewiride.Erp.Host.HealthProbe', { 'Contoso.Absent/3.0.0': { type: 'package' } });
  writeTools({ 'contoso.tool': '1.0.0', 'contoso.absenttool': '4.0.0' });
  const missing = nugetRun();
  assert.equal(missing.status, 1);
  assert.match(missing.stderr, /run "dotnet tool restore" and "dotnet restore --locked-mode" in backend first/);
  assert.match(missing.stderr, /^ {2}Contoso\.Absent 3\.0\.0$/m);
  assert.match(missing.stderr, /^ {2}contoso\.absenttool 4\.0\.0$/m);

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
