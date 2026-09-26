import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import {
  flatten,
  readFeatureFlags,
  readKeyVaultReferences,
  readSeedData,
  referenceLabels,
  repoRoot,
  validateSeedData,
  type SeedData,
} from '../../azure/lib/seed-files.ts';

const cli = join(repoRoot, 'scripts', 'azure', 'lib', 'seed-files.ts');
const seedDirectory = join(repoRoot, 'infra', 'appconfig');

const wellFormed = (): SeedData => ({
  defaults: new Map([
    ['Erp:Platform:Host:ApplicationName', 'ERP-AI-Pro'],
    ['Erp:Platform:Identity:Instance', 'https://login.microsoftonline.com/'],
  ]),
  labelled: {
    'local-dev': new Map([['Erp:Platform:Host:ApplicationName', 'ERP-AI-Pro (local-dev)']]),
    production: new Map(),
  },
  flags: [{ id: 'Erp.Modules.Platform.SystemInfo', enabled: { 'local-dev': true, production: true } }],
  references: [{ key: 'Erp:Platform:Identity:ClientCertificate', secret: 'Erp--Platform--Identity--ClientCertificate' }],
});

const run = (...args: string[]): { status: number; stdout: string } => {
  try {
    return { status: 0, stdout: execFileSync(process.execPath, [cli, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }) };
  } catch (error) {
    const failure = error as { status: number; stdout: string };
    return { status: failure.status, stdout: failure.stdout };
  }
};

test('the seed files under infra/appconfig pass validation', () => {
  assert.deepEqual(validateSeedData(readSeedData()), []);
});

test('a well-formed seed has no problems', () => {
  assert.deepEqual(validateSeedData(wellFormed()), []);
});

test('flatten joins nested objects with a colon and accepts only string leaves', () => {
  assert.deepEqual(
    [...flatten({ Erp: { Platform: { Host: { ApplicationName: 'x', Port: '8080' } } } })],
    [
      ['Erp:Platform:Host:ApplicationName', 'x'],
      ['Erp:Platform:Host:Port', '8080'],
    ],
  );
  assert.throws(() => flatten({ Erp: { Platform: { Host: { Port: 8080 } } } }), /must be a JSON string \(write 8080 as "8080"\)/);
  assert.throws(() => flatten({ Erp: { Platform: { Host: { Enabled: true } } } }), /must be a JSON string/);
  assert.throws(() => flatten({ Erp: { Platform: { Host: { KnownNetworks: ['10.0.0.0/8'] } } } }), /arrays are host-local/);
  assert.throws(() => flatten({ Erp: { Platform: { Host: { ApplicationName: null } } } }), /must be a JSON string/);
  assert.throws(() => flatten({ 'Erp:Platform': { Host: 'x' } }), /must not contain ':'/);
  assert.throws(() => flatten([]), /arrays are host-local/);
  assert.throws(() => flatten('text'), /must hold an object/);
});

test('a key outside the Erp:<Domain>:<Module>:<Setting> shape is reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:ApplicationName', 'short');
  data.defaults.set('Erp:platform:Host:Name', 'lowercase');
  data.defaults.set('Erp:Platform:Host:Nested:Deep', 'too deep');
  const problems = validateSeedData(data);
  assert.equal(problems.length, 3);
  assert.ok(problems.every((p) => p.startsWith('defaults.json: key ')));
});

test('a value carrying a store, vault, SQL host, connection string, credential or GUID is reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Host:Store', 'https://appcs-x.azconfig.io');
  data.defaults.set('Erp:Platform:Host:Vault', 'https://kv-x.vault.azure.net/');
  data.defaults.set('Erp:Platform:Host:Sql', 'erp-sql.database.windows.net');
  data.defaults.set('Erp:Platform:Host:Store2', 'Endpoint=https://x;Id=a');
  data.defaults.set('Erp:Platform:Host:Db', 'Server=tcp:x,1433;Database=ErpAiPro');
  data.defaults.set('Erp:Platform:Host:Login', 'User Id=erp;Password=<sql-password>');
  data.defaults.set('Erp:Platform:Host:Tenant', '00000000-0000-0000-0000-000000000000');
  const problems = validateSeedData(data);
  assert.equal(problems.length, 7);
  assert.ok(problems.every((p) => p.includes('environment identifiers and credentials stay out of the repository')));
});

test('a value carrying a blob endpoint is reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Attachments:Endpoint', 'https://sterpaiprodev.blob.core.windows.net/');
  data.labelled['local-dev'].set('Erp:Platform:Host:ApplicationName', 'STERPAIPRODEV.BLOB.CORE.WINDOWS.NET');
  assert.deepEqual(validateSeedData(data), [
    "defaults.json: value of 'Erp:Platform:Attachments:Endpoint' carries a blob endpoint; environment identifiers and credentials stay out of the repository",
    "local-dev.json: value of 'Erp:Platform:Host:ApplicationName' carries a blob endpoint; environment identifiers and credentials stay out of the repository",
  ]);
});

test('a shared access signature in a query string is reported as a credential', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Host:Report', 'https://files.example.test/report.pdf?sv=2026-06-06&se=2026-10-01T00%3A00%3A00Z&sp=r&sig=abc');
  data.defaults.set('Erp:Platform:Host:Download', 'https://files.example.test/report.pdf?sig=abc');
  data.defaults.set('Erp:Platform:Host:Callback', 'https://files.example.test/hook?user=erp&token=abc');
  data.defaults.set('Erp:Platform:Host:Search', 'https://files.example.test/search?q=invoice&page=2');
  const problems = validateSeedData(data);
  assert.deepEqual(problems, [
    "defaults.json: value of 'Erp:Platform:Host:Report' carries a credential; environment identifiers and credentials stay out of the repository",
    "defaults.json: value of 'Erp:Platform:Host:Download' carries a credential; environment identifiers and credentials stay out of the repository",
    "defaults.json: value of 'Erp:Platform:Host:Callback' carries a credential; environment identifiers and credentials stay out of the repository",
  ]);
});

test('a secret-like key seeded as a plain value is reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Mail:Password', 'x');
  data.defaults.set('Erp:Platform:Identity:ClientSecret', 'x');
  data.defaults.set('Erp:Platform:Database:ConnectionString', 'x');
  data.defaults.set('Erp:Finance:Gst:ApiKey', 'x');
  const problems = validateSeedData(data);
  assert.equal(problems.length, 4);
  assert.ok(problems.every((p) => p.includes('names a secret; seed it as a Key Vault reference')));
});

test('an encryption key or a list of retired encryption keys seeded as a plain value is reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Attachments:EncryptionKey', 'dev-1:placeholder');
  data.labelled['local-dev'].set('Erp:Platform:Attachments:EncryptionKey', 'dev-2:placeholder');
  data.defaults.set('Erp:Platform:Attachments:RetiredEncryptionKeys', 'dev-0:placeholder');
  data.defaults.set('Erp:Platform:Attachments:EncryptionKeyRotation', 'manual');
  assert.deepEqual(validateSeedData(data), [
    "defaults.json: key 'Erp:Platform:Attachments:EncryptionKey' names a secret; seed it as a Key Vault reference, never as a plain value",
    "defaults.json: key 'Erp:Platform:Attachments:RetiredEncryptionKeys' names a secret; seed it as a Key Vault reference, never as a plain value",
    "local-dev.json: key 'Erp:Platform:Attachments:EncryptionKey' names a secret; seed it as a Key Vault reference, never as a plain value",
  ]);
});

test('the attachments emulator host is host-local and never seeded', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Attachments:EmulatorHost', 'azurite');
  assert.deepEqual(validateSeedData(data), [
    "defaults.json: key 'Erp:Platform:Attachments:EmulatorHost' is host-local and must not be seeded",
  ]);
});

test('provision.sh-owned keys are never seeded, neither as a value nor as a reference', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Attachments:BlobServiceUri', 'https://storage.example.test/');
  data.labelled['local-dev'].set('Erp:Platform:Attachments:BlobServiceUri', 'https://storage.example.test/dev/');
  data.references.push({ key: 'Erp:Platform:Attachments:BlobServiceUri', secret: 'Erp--Platform--Attachments--BlobServiceUri', labels: ['production'] });
  const problems = validateSeedData(data);
  assert.deepEqual(
    problems.filter((p) => p.includes('written by scripts/azure/provision.sh')),
    [
      "defaults.json: key 'Erp:Platform:Attachments:BlobServiceUri' is written by scripts/azure/provision.sh and must not be seeded",
      "local-dev.json: key 'Erp:Platform:Attachments:BlobServiceUri' is written by scripts/azure/provision.sh and must not be seeded",
      "key-vault-references.json: key 'Erp:Platform:Attachments:BlobServiceUri' is written by scripts/azure/provision.sh and must not be seeded",
    ],
  );
});

test('bootstrap-only, host-local and entra.sh-owned keys are never seeded', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Configuration:RefreshInterval', '00:05:00');
  data.defaults.set('Erp:Platform:Host:KnownNetworks', '10.0.0.0/8');
  data.defaults.set('Erp:Platform:Identity:TenantId', 'tenant');
  data.labelled.production.set('Erp:Platform:Identity:ClientId', 'client');
  data.references.push({ key: 'Erp:Platform:Identity:ClientId', secret: 'Erp--Platform--Identity--ClientId' });
  const problems = validateSeedData(data);
  assert.ok(problems.some((p) => p.includes('bootstrap-only')));
  assert.ok(problems.some((p) => p.includes('host-local')));
  assert.equal(problems.filter((p) => p.includes('written by scripts/azure/entra.sh')).length, 3);
});

test('an empty value and a value with a control character are reported', () => {
  const data = wellFormed();
  data.defaults.set('Erp:Platform:Host:Empty', '   ');
  data.defaults.set('Erp:Platform:Host:Multiline', 'line one\nline two');
  data.defaults.set('Erp:Platform:Host:Tabbed', 'a\tb');
  const problems = validateSeedData(data);
  assert.ok(problems.some((p) => p.includes("value of 'Erp:Platform:Host:Empty' is empty")));
  assert.equal(problems.filter((p) => p.includes('contains a control character')).length, 2);
});

test('a labelled override needs an unlabelled default', () => {
  const data = wellFormed();
  data.labelled.production.set('Erp:Platform:Host:AllowedHosts', 'erp.example.test');
  assert.deepEqual(validateSeedData(data), [
    "production.json: 'Erp:Platform:Host:AllowedHosts' overrides a key that defaults.json does not define",
  ]);
});

test('a flag must have an Erp.Modules id and appear once', () => {
  const data = wellFormed();
  data.flags.push({ id: 'Erp.Modules.Platform.SystemInfo', enabled: { 'local-dev': true, production: false } });
  data.flags.push({ id: 'Erp:Modules:Finance:Sales', enabled: { 'local-dev': true, production: true } });
  const problems = validateSeedData(data);
  assert.deepEqual(problems, [
    "feature-flags.json: flag 'Erp.Modules.Platform.SystemInfo' is listed twice",
    "feature-flags.json: flag id 'Erp:Modules:Finance:Sales' is not Erp.Modules.<Domain>.<Module>[.<Capability>]",
  ]);
});

test('a flag must declare exactly the local-dev and production labels as booleans', () => {
  const data = wellFormed();
  data.flags.push({ id: 'Erp.Modules.Finance.Sales', enabled: { 'local-dev': true } as never });
  data.flags.push({ id: 'Erp.Modules.Finance.Purchases', enabled: { 'local-dev': true, production: true, staging: true } as never });
  data.flags.push({ id: 'Erp.Modules.Finance.Payroll', enabled: { 'local-dev': 1, production: 'no' } as never });
  assert.deepEqual(validateSeedData(data), [
    "feature-flags.json: flag 'Erp.Modules.Finance.Sales' must declare exactly the labels local-dev and production",
    "feature-flags.json: flag 'Erp.Modules.Finance.Sales' needs a boolean for 'production'",
    "feature-flags.json: flag 'Erp.Modules.Finance.Purchases' must declare exactly the labels local-dev and production",
    "feature-flags.json: flag 'Erp.Modules.Finance.Payroll' needs a boolean for 'local-dev'",
    "feature-flags.json: flag 'Erp.Modules.Finance.Payroll' needs a boolean for 'production'",
  ]);
});

test('a Key Vault reference needs an Erp key, an Erp-- secret name, one listing and no plain value for the same key', () => {
  const data = wellFormed();
  data.references.push({ key: 'Erp:Platform:Host:ApplicationName', secret: 'Erp--Platform--Host--ApplicationName' });
  data.references.push({ key: 'Platform:Identity:Certificate', secret: 'Platform-Identity-Certificate' });
  data.references.push({ key: 'Erp:Platform:Identity:ClientCertificate', secret: 'Erp--Platform--Identity--ClientCertificateOld' });
  const problems = validateSeedData(data);
  assert.ok(problems.some((p) => p.includes("'Erp:Platform:Host:ApplicationName' is also a plain value in defaults.json")));
  assert.ok(problems.some((p) => p.includes("key 'Platform:Identity:Certificate' is not Erp:")));
  assert.ok(problems.some((p) => p.includes("secret 'Platform-Identity-Certificate' is not Erp--")));
  assert.ok(problems.some((p) => p.includes("key 'Erp:Platform:Identity:ClientCertificate' is listed twice")));
});

test('a Key Vault reference may restrict itself to one label', () => {
  const data = wellFormed();
  data.references.push({ key: 'Erp:Platform:Database:ConnectionString', secret: 'Erp--Platform--Database--ConnectionString', labels: ['local-dev'] });
  data.references.push({ key: 'Erp:Platform:Mail:Password', secret: 'Erp--Platform--Mail--Password', labels: ['production', 'local-dev'] });
  assert.deepEqual(validateSeedData(data), []);
  assert.deepEqual(referenceLabels(data.references[0]!), ['local-dev', 'production']);
  assert.deepEqual(referenceLabels(data.references[1]!), ['local-dev']);
});

test('a Key Vault reference with an unknown, empty, duplicated or non-array labels value is reported', () => {
  const data = wellFormed();
  data.references.push({ key: 'Erp:Platform:Database:ConnectionString', secret: 'Erp--Platform--Database--ConnectionString', labels: ['staging'] as never });
  data.references.push({ key: 'Erp:Platform:Mail:Password', secret: 'Erp--Platform--Mail--Password', labels: [] });
  data.references.push({ key: 'Erp:Finance:Gst:ApiKey', secret: 'Erp--Finance--Gst--ApiKey', labels: ['local-dev', 'local-dev'] });
  data.references.push({ key: 'Erp:Finance:Gst:Password', secret: 'Erp--Finance--Gst--Password', labels: 'local-dev' as never });
  assert.deepEqual(validateSeedData(data), [
    "key-vault-references.json: labels of 'Erp:Platform:Database:ConnectionString' must list local-dev and/or production",
    "key-vault-references.json: labels of 'Erp:Platform:Mail:Password' must list local-dev and/or production",
    "key-vault-references.json: labels of 'Erp:Finance:Gst:ApiKey' must list local-dev and/or production",
    "key-vault-references.json: labels of 'Erp:Finance:Gst:Password' must list local-dev and/or production",
  ]);
});

test('the flag and reference readers reject a document of the wrong shape', () => {
  const directory = mkdtempSync(join(tmpdir(), 'appconfig-seed-'));
  const flags = join(directory, 'feature-flags.json');
  const references = join(directory, 'key-vault-references.json');
  writeFileSync(flags, '{}');
  writeFileSync(references, '{ "key": "Erp:Platform:Identity:ClientCertificate" }');
  assert.throws(() => readFeatureFlags(flags), /expected \{ "flags": \[\.\.\.\] \}/);
  assert.throws(() => readKeyVaultReferences(references), /expected an array of \{ key, secret, labels\? \}/);
});

test('the command line prints tab-separated rows and validates a directory', () => {
  const settings = run('settings', join(seedDirectory, 'defaults.json'));
  assert.equal(settings.status, 0);
  assert.ok(settings.stdout.split('\n').includes('Erp:Platform:Host:ApplicationName\tERP-AI-Pro'));
  const flags = run('flags', join(seedDirectory, 'feature-flags.json'));
  assert.equal(flags.status, 0);
  assert.deepEqual(flags.stdout.trim().split('\n'), [
    'Erp.Modules.Platform.SystemInfo\tlocal-dev\ttrue',
    'Erp.Modules.Platform.SystemInfo\tproduction\ttrue',
    'Erp.Modules.Platform.Attachments\tlocal-dev\ttrue',
    'Erp.Modules.Platform.Attachments\tproduction\ttrue',
  ]);
  const references = run('references', join(seedDirectory, 'key-vault-references.json'));
  assert.equal(references.status, 0);
  assert.deepEqual(references.stdout.trim().split('\n'), [
    'Erp:Platform:Identity:ClientCertificate\tErp--Platform--Identity--ClientCertificate\tlocal-dev,production',
    'Erp:Platform:Database:ConnectionString\tErp--Platform--Database--ConnectionString\tlocal-dev',
    'Erp:Platform:Attachments:EncryptionKey\tErp--Platform--Attachments--EncryptionKey\tlocal-dev',
  ]);
  assert.equal(run('validate', seedDirectory).status, 0);

  const broken = mkdtempSync(join(tmpdir(), 'appconfig-seed-'));
  writeFileSync(join(broken, 'defaults.json'), '{ "Erp": { "Platform": { "Host": { "ApplicationName": "x" } } } }');
  writeFileSync(join(broken, 'local-dev.json'), '{ "Erp": { "Platform": { "Host": { "Other": "y" } } } }');
  writeFileSync(join(broken, 'production.json'), '{}');
  writeFileSync(join(broken, 'feature-flags.json'), '{ "flags": [] }');
  writeFileSync(join(broken, 'key-vault-references.json'), '[]');
  assert.equal(run('validate', broken).status, 1);
  assert.equal(run('unknown').status, 1);
});
