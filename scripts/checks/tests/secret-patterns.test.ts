import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { decodeText, isPlaceholder, isScannable, scanText, textEncoding } from '../lib/secret-patterns.ts';
import { repoRoot } from '../lib/walk.ts';

const cli = join(repoRoot, 'scripts', 'checks', 'secret-patterns.ts');
const marker = '-----';
const privateKey = `${marker}BEGIN RSA PRIVATE KEY${marker}`;
const clientSecret = ['abc', '8Q~', 'k9F2mXp4Qw1Rt7Yu3Io5Pl8Zx2Cv6Bn0Mm'].join('');
const connectionString = ['Endpoint=https://appcs-x.azconfig.io;', 'Id=abc-l1-s0:xyz;', 'Secret=', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78St90='].join('');
const storageKey = ['AccountKey=', 'A'.repeat(43), 'b1'.repeat(22), '=='].join('');
const githubToken = ['ghp_', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78'].join('');
const jwt = ['eyJhbGciOiJIUzI1NiJ9', 'eyJzdWIiOiIxMjM0NTY3ODkwIn0', 'SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c'].join('.');
const bearer = ['Authorization: Bearer ', 'Ab12Cd34Ef56Gh78Ij90Kl12'].join('');
const derBlob = ['MII', 'B'.repeat(50), '1'.repeat(50)].join('');
const strongValue = 'Tr0ub4dor&3xtraLong!';
const password = ['Password=', strongValue].join('');

const names = (content: string) => scanText('file.txt', content).map((finding) => finding.name);

test('every secret shape is reported with its file, line and reason', () => {
  const content = [privateKey, clientSecret, connectionString, storageKey, githubToken, jwt, bearer, derBlob, password].join('\n');
  const findings = scanText('config/app.json', content);
  assert.deepEqual(
    findings.map((f) => [f.line, f.name]),
    [
      [1, 'private-key'],
      [2, 'entra-client-secret'],
      [3, 'appconfig-connection-string'],
      [4, 'azure-storage-key'],
      [5, 'github-token'],
      [6, 'jwt'],
      [7, 'authorization-header'],
      [8, 'base64-der-blob'],
      [9, 'credential-assignment'],
    ],
  );
  assert.ok(findings.every((f) => f.path === 'config/app.json' && f.reason.length > 0));
});

test('every alternation of a pattern is reported', () => {
  const cases: ReadonlyArray<[string, string]> = [
    [`${marker}BEGIN PRIVATE KEY${marker}`, 'private-key'],
    [`${marker}BEGIN EC PRIVATE KEY${marker}`, 'private-key'],
    [`${marker}BEGIN DSA PRIVATE KEY${marker}`, 'private-key'],
    [`${marker}BEGIN OPENSSH PRIVATE KEY${marker}`, 'private-key'],
    [`${marker}BEGIN ENCRYPTED PRIVATE KEY${marker}`, 'private-key'],
    [`${marker}BEGIN PGP PRIVATE KEY BLOCK${marker}`, 'private-key'],
    [['abc', '7Q~', 'k9F2mXp4Qw1Rt7Yu3Io5Pl8Zx2Cv6Bn'].join(''), 'entra-client-secret'],
    [['AZURE_CLIENT_SECRET=', '~ab', '8Q~', 'k9F2mXp4Qw1Rt7Yu3Io5Pl8Zx2Cv6Bn0M~'].join(''), 'entra-client-secret'],
    [['value: "', '.ab', '8Q~', 'k9F2mXp4Qw1Rt7Yu3Io5Pl8Zx2Cv6Bn0Mm', '"'].join(''), 'entra-client-secret'],
    [['gho_', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78'].join(''), 'github-token'],
    [['ghs_', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78'].join(''), 'github-token'],
    [['github_pat_', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78St90Uv12Wx34Yz56Ab78Cd90Ef12Gh34Ij56Kl'].join(''), 'github-token'],
    [['Authorization: Basic ', 'c2E6VHIwdWI0ZG9yJjN4dHJhTG9uZyE='].join(''), 'authorization-header'],
    [['client_secret: ', 'Ab12Cd34Ef56Gh78'].join(''), 'credential-assignment'],
    [['clientSecret = "', 'Ab12Cd34Ef56Gh78', '"'].join(''), 'credential-assignment'],
    [['api-key: ', 'Ab12Cd34Ef56Gh78'].join(''), 'credential-assignment'],
    [['access_key=', 'Ab12Cd34Ef56Gh78'].join(''), 'credential-assignment'],
    [['secret_key=', 'Ab12Cd34Ef56Gh78'].join(''), 'credential-assignment'],
    [['pwd=', 'Ab12Cd34'].join(''), 'credential-assignment'],
    [['AZURE_CLIENT_SECRET=', 'Ab12Cd34Ef56Gh78Ij90Kl12Mn34'].join(''), 'credential-assignment'],
    [['MSSQL_SA_PASSWORD: ', strongValue].join(''), 'credential-assignment'],
    [['OPENAI_API_KEY=', 'sk-proj-Ab12Cd34Ef56Gh78Ij90'].join(''), 'credential-assignment'],
    [["sqlPassword = '", strongValue, "'"].join(''), 'credential-assignment'],
    [['"Password": "', strongValue, '"'].join(''), 'credential-assignment'],
    [['  "ClientSecret": "', 'Ab12Cd34Ef56Gh78Ij90', '",'].join(''), 'credential-assignment'],
    [["'api_key': '", 'sk-live-4eC39HqLyjWDarjtT1zdp7dc', "'"].join(''), 'credential-assignment'],
    [['Password=', 'Password2024!xyz'].join(''), 'credential-assignment'],
    [['client_secret=', 'MySecret2024value'].join(''), 'credential-assignment'],
    [['Password=', 'Ab1!Cd2@'].join(''), 'credential-assignment'],
  ];
  for (const [line, expected] of cases) {
    assert.ok(names(line).includes(expected), line);
  }
});

test('an excerpt never exceeds eight characters of the value', () => {
  const [finding] = scanText('x', connectionString);
  assert.equal(finding?.excerpt, 'Ab12Cd34…');
  assert.ok(!finding?.excerpt.includes('Ef56'));
});

test('placeholders, interpolations and documented examples are not reported', () => {
  for (const line of [
    'Password=<your-password>',
    'Password=CHANGE_ME',
    'Password=${SQL_PASSWORD}',
    'password: {{ secrets.db }}',
    'Password=%DB_PASSWORD%',
    'password: z.string().min(8)',
    'client_secret: $CLIENT_SECRET',
    'Password=xxxxxxxxxxxxxxxx',
    'Password=your-strong-password',
    'api_key = "example-api-key-value"',
    'Server=tcp:x,1433;Database=ErpAiPro;TrustServerCertificate=True',
    'Endpoint=https://appcs-x.azconfig.io;Id=<id>;Secret=<secret>',
    'AccountKey=${STORAGE_KEY}',
    'AZURE_CLIENT_SECRET=${AZURE_CLIENT_SECRET}',
    'MSSQL_SA_PASSWORD: ${{ secrets.MSSQL_SA_PASSWORD }}',
    'SA_PASSWORD=<your-sa-password>',
    '"Password": "<your-password>"',
    '"Password": "${SQL_PASSWORD}"',
    '"ClientSecret": ""',
    '"SecretRefreshInterval": "01:00:00"',
    'Password = options.Password',
    'password: PASSWORD_1',
  ]) {
    assert.deepEqual(names(line), [], line);
  }
});

test('identifiers that are not secrets are not reported', () => {
  for (const line of [
    "keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'",
    `sha512-${'a1'.repeat(44)}`,
    'integrity: sha512-Ab12Cd34Ef56Gh78Ij90Kl12Mn34Op56Qr78St90Uv12Wx34Yz56Ab78Cd90Ef12Gh34Ij56Kl78Mn90Op12Qr34St56==',
    'Erp--Platform--Identity--ClientCertificate',
    'https://kv-erp-ai-pro-dev.vault.azure.net/secrets/Erp--Platform--Identity--ClientCertificate',
    ['token=', 'AbCdEfGhIjKlMnOpQr', '8Q~', 'k9F2mXp4Qw1Rt7Yu3Io5Pl8Zx2Cv6Bn0Mm', 'StUvWx'].join(''),
  ]) {
    assert.deepEqual(names(line), [], line);
  }
});

test('isPlaceholder recognises each placeholder rule on values that carry a digit', () => {
  for (const value of [
    '<token-1>',
    '${TOKEN_1}',
    '{{ token1 }}',
    '%TOKEN1%',
    '$TOKEN1',
    'CHANGE_ME-1',
    'CHANGEME-1',
    'PLACEHOLDER-1',
    'REDACTED-1',
    'example-1',
    'secret-1',
    'password-1',
    'your-key-1',
    'your_key_1',
    'xxx-1',
    '111111111111',
    'letters-only-value',
    'z.string().min(8)',
    'options.Get(1)',
  ]) {
    assert.equal(isPlaceholder(value), true, value);
  }
  for (const value of ['Ab12Cd34Ef56Gh78', strongValue, 'Password2024!xyz', 'MySecret2024value', 'Get(1)']) {
    assert.equal(isPlaceholder(value), false, value);
  }
});

test('binary files, lockfiles and the generated roadmap are not scanned', () => {
  const text = new TextEncoder().encode('plain text');
  assert.equal(isScannable('docs/guide.md', text), true);
  assert.equal(isScannable('frontend/pnpm-lock.yaml', text), false);
  assert.equal(isScannable('backend/Hosts/Api/Dewiride.Erp.Host.Api/packages.lock.json', text), false);
  assert.equal(isScannable('docs/roadmap/ROADMAP.md', text), false);
  assert.equal(isScannable('frontend/apps/web/public/icon.png', text), false);
  assert.equal(isScannable('infra/compose/secrets/cert.pfx', text), false);
  assert.equal(isScannable('some/file.bin', new Uint8Array([1, 2, 0, 3])), false);
});

test('UTF-16 text is detected, decoded and scanned', () => {
  const withBom = Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(`${privateKey}\n`, 'utf16le')]);
  const withoutBom = Buffer.from(`image: x\n${privateKey}\n`, 'utf16le');
  const bigEndian = Buffer.from(Buffer.from(`${privateKey}\n`, 'utf16le')).swap16();
  assert.equal(textEncoding(withBom), 'utf16le');
  assert.equal(textEncoding(withoutBom), 'utf16le');
  assert.equal(textEncoding(bigEndian), 'utf16be');
  assert.equal(textEncoding(new TextEncoder().encode('ascii')), 'utf8');
  assert.equal(isScannable('config.txt', withBom), true);
  assert.deepEqual(scanText('a', decodeText(withBom, 'utf16le')).map((f) => f.line), [1]);
  assert.deepEqual(scanText('b', decodeText(withoutBom, 'utf16le')).map((f) => f.line), [2]);
  assert.deepEqual(scanText('c', decodeText(bigEndian, 'utf16be')).map((f) => f.line), [1]);
});

test('the command line scans tracked and new files, skips git-ignored ones and reports findings by line', (t) => {
  const directory = mkdtempSync(join(tmpdir(), 'secret-patterns-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const git = (...args: string[]) => execFileSync('git', ['-C', directory, ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(join(directory, '.gitignore'), 'ignored.env\nsecrets/\n');
  writeFileSync(join(directory, 'clean.md'), 'Password=<your-password>\n');
  writeFileSync(join(directory, 'pnpm-lock.yaml'), `${privateKey}\n`);
  git('add', '.');
  git('-c', 'user.email=t@example.test', '-c', 'user.name=t', 'commit', '-q', '-m', 'clean');

  const run = () => spawnSync(process.execPath, [cli, '--root', directory], { encoding: 'utf8' });
  const clean = run();
  assert.equal(clean.status, 0, clean.stderr);
  assert.match(clean.stdout, /secret patterns ok: 2 files scanned/);

  writeFileSync(join(directory, 'ignored.env'), `${privateKey}\n`);
  mkdirSync(join(directory, 'secrets'));
  writeFileSync(join(directory, 'secrets', 'runtime.pem'), `${privateKey}\n`);
  const stillClean = run();
  assert.equal(stillClean.status, 0, stillClean.stderr);

  writeFileSync(join(directory, 'new-file.yaml'), `image: x\n${privateKey}\n`);
  writeFileSync(join(directory, 'wide.txt'), Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(`${password}\n`, 'utf16le')]));
  const failing = run();
  assert.equal(failing.status, 1);
  assert.match(failing.stderr, /new-file\.yaml:2: private key material — private-key/);
  assert.match(failing.stderr, /wide\.txt:1: credential value assigned in text — credential-assignment Tr0ub4do…/);
});
