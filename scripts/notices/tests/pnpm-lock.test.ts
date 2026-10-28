import assert from "node:assert/strict";
import { test } from "node:test";

import { isPlatformSpecific, parseLockfileDocuments, productionClosure, readLockfile, splitPackageKey } from "../lib/pnpm-lock.ts";

const lockfile = `---
lockfileVersion: '9.0'

importers:

  .:
    packageManagerDependencies:
      pnpm:
        specifier: 12.4.2
        version: 12.4.2

packages:

  pnpm@12.4.2:
    resolution: {integrity: sha512-pm}

---
lockfileVersion: '9.0'

importers:

  apps/web:
    dependencies:
      '@dewiride/erp-ui':
        specifier: workspace:*
        version: link:../../packages/ui
      next:
        specifier: 'catalog:'
        version: 16.4.1(react@19.3.1)
      react:
        specifier: 'catalog:'
        version: 19.3.1
    devDependencies:
      typescript:
        specifier: 'catalog:'
        version: 6.0.3

  packages/ui:
    dependencies:
      string-width-cjs:
        specifier: npm:string-width@4.2.3
        version: string-width@4.2.3
    devDependencies:
      eslint:
        specifier: 'catalog:'
        version: 10.0.0

packages:

  '@next/swc-linux-x64-gnu@16.4.1':
    resolution: {integrity: sha512-a}
    engines: {node: '>= 10'}
    cpu: [x64]
    os: [linux]
    libc: [glibc]

  '@playwright/test@1.64.0':
    resolution: {integrity: sha512-b}

  eslint@10.0.0:
    resolution: {integrity: sha512-c}

  next@16.4.1:
    resolution: {integrity: sha512-d}
    peerDependencies:
      '@playwright/test': ^1.51.1
      react: ^19.0.0
    peerDependenciesMeta:
      '@playwright/test':
        optional: true

  react@19.3.1:
    resolution: {integrity: sha512-e}

  sharp@0.35.5:
    resolution: {integrity: sha512-f}

  string-width@4.2.3:
    resolution: {integrity: sha512-g}

  systeminformation@5.33.11:
    resolution: {integrity: sha512-h}
    os: [darwin, linux, win32]

  typescript@6.0.3:
    resolution: {integrity: sha512-i}

  'it''s-quoted@1.0.0':
    resolution: {integrity: sha512-j}

snapshots:

  '@next/swc-linux-x64-gnu@16.4.1':
    optional: true

  '@playwright/test@1.64.0': {}

  eslint@10.0.0: {}

  next@16.4.1(react@19.3.1):
    dependencies:
      react: 19.3.1
      systeminformation: 5.33.11
    optionalDependencies:
      '@next/swc-linux-x64-gnu': 16.4.1
      '@playwright/test': 1.64.0
      sharp: 0.35.5
    transitivePeerDependencies:
      - '@babel/core'
      - babel-plugin-macros

  react@19.3.1:
    dependencies:
      'it''s-quoted': 1.0.0

  sharp@0.35.5: {}

  string-width@4.2.3: {}

  systeminformation@5.33.11: {}

  typescript@6.0.3: {}

  'it''s-quoted@1.0.0': {}
`;

test("parseLockfileDocuments reads every document with its mappings, flow values, sequences and quoted keys", () => {
  const [first, second] = parseLockfileDocuments(lockfile);
  assert.deepEqual(first?.lockfileVersion, "9.0");
  const packages = second?.packages as Record<string, Record<string, unknown>>;
  assert.deepEqual(packages["@next/swc-linux-x64-gnu@16.4.1"], { resolution: { integrity: "sha512-a" }, engines: { node: ">= 10" }, cpu: ["x64"], os: ["linux"], libc: ["glibc"] });
  assert.deepEqual((second?.snapshots as Record<string, Record<string, unknown>>)["next@16.4.1(react@19.3.1)"]?.transitivePeerDependencies, ["@babel/core", "babel-plugin-macros"]);
  assert.ok("it's-quoted@1.0.0" in packages);
});

test("the production closure follows workspace links, dependencies and real optional dependencies only", () => {
  const closure = productionClosure(readLockfile(lockfile, "apps/web"), "apps/web", { excluded: (name) => name === "sharp" });
  assert.deepEqual(
    closure.map(({ key }) => key),
    ["it's-quoted@1.0.0", "next@16.4.1", "react@19.3.1", "string-width@4.2.3", "systeminformation@5.33.11"],
  );
});

test("a package that installs only on some platforms is platform-specific, one for every platform is not", () => {
  assert.equal(isPlatformSpecific({ cpu: ["x64"], os: ["linux"] }), true);
  assert.equal(isPlatformSpecific({ libc: ["glibc"] }), true);
  assert.equal(isPlatformSpecific({ os: ["darwin"] }), true);
  assert.equal(isPlatformSpecific({ os: ["!win32"] }), true);
  assert.equal(isPlatformSpecific({ os: ["darwin", "linux", "win32"] }), false);
  assert.equal(isPlatformSpecific({ os: ["!android"] }), false);
  assert.equal(isPlatformSpecific({}), false);
});

test("splitPackageKey separates scoped names, versions and peer suffixes, and refuses a key without a version", () => {
  assert.deepEqual(splitPackageKey("@floating-ui/react-dom@2.1.9(react@19.3.1)(react-dom@19.3.1(react@19.3.1))"), { name: "@floating-ui/react-dom", version: "2.1.9" });
  assert.deepEqual(splitPackageKey("react@19.3.1"), { name: "react", version: "19.3.1" });
  assert.throws(() => splitPackageKey("@scope/name"), /is not <name>@<version>/);
});

test("readLockfile refuses a lockfile without the importer and a closure that names a package the lockfile lacks", () => {
  assert.throws(() => readLockfile(lockfile, "apps/api"), /has no importer apps\/api/);
  const broken = lockfile.replace("  react@19.3.1:\n    resolution: {integrity: sha512-e}\n", "");
  assert.throws(() => productionClosure(readLockfile(broken, "apps/web"), "apps/web", { excluded: () => false }), /has no package entry react@19\.3\.1/);
});
