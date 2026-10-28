import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";

import { repoRoot } from "../../checks/lib/walk.ts";
import { finalBaseImage, ImageFiles, type ImageFile, osRelease } from "../lib/container-files.ts";
import { nugetComponent, publishedHosts, shippedPackages } from "../lib/dotnet.ts";
import { canonicalBody, TextTable } from "../lib/licence-texts.ts";
import { declaredLicence, npmComponent, packageFolder, pnpmFolders } from "../lib/npm.ts";

function withTree(files: Record<string, string>, check: (root: string) => void): void {
  const root = mkdtempSync(join(tmpdir(), "erp-notices-test-"));
  try {
    for (const [path, content] of Object.entries(files)) {
      mkdirSync(dirname(join(root, path)), { recursive: true });
      writeFileSync(join(root, path), content);
    }
    check(root);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

test("shippedPackages lists the packages whose runtime, native, runtime-target or resource assets a host publishes", () => {
  const deps = {
    targets: {
      ".NETCoreApp,Version=v10.0": {
        "Dewiride.Erp.Host.Api/1.0.0": { runtime: { "Dewiride.Erp.Host.Api.dll": {} } },
        "Azure.Core/1.61.0": { runtime: { "lib/net8.0/Azure.Core.dll": {} } },
        "Microsoft.Data.SqlClient.SNI.runtime/7.1.0": { runtimeTargets: { "runtimes/win-x64/native/Microsoft.Data.SqlClient.SNI.dll": {} } },
        "Humanizer.Core/2.14.1": { resources: { "lib/net6.0/de/Humanizer.resources.dll": {} } },
        "Microsoft.Identity.Client.NativeInterop/0.20.6": { runtime: { "lib/net8.0/_._": {} }, runtimeTargets: { "runtimes/osx-x64/native/_._": {} } },
        "System.Composition/9.0.0": {},
      },
    },
    libraries: {
      "Dewiride.Erp.Host.Api/1.0.0": { type: "project" },
      "Azure.Core/1.61.0": { type: "package" },
      "Microsoft.Data.SqlClient.SNI.runtime/7.1.0": { type: "package" },
      "Humanizer.Core/2.14.1": { type: "package" },
      "Microsoft.Identity.Client.NativeInterop/0.20.6": { type: "package" },
      "System.Composition/9.0.0": { type: "package" },
    },
  };
  assert.deepEqual(shippedPackages(JSON.stringify(deps)), [
    { name: "Azure.Core", version: "1.61.0" },
    { name: "Microsoft.Data.SqlClient.SNI.runtime", version: "7.1.0" },
    { name: "Humanizer.Core", version: "2.14.1" },
  ]);
});

test("the published hosts are the three that api.Dockerfile publishes, into the folders it copies", () => {
  const dockerfile = readFileSync(join(repoRoot, "infra", "docker", "api.Dockerfile"), "utf8");
  for (const host of publishedHosts) {
    assert.match(dockerfile, new RegExp(`dotnet publish ${host.project.replaceAll(".", "\\.")}\\s*\\\\\\s*\\n[^\\n]*--output ${host.publishFolder}\\b`), host.project);
    assert.match(dockerfile, new RegExp(`COPY --from=build --chown=app:app ${host.publishFolder}/ \\./${host.imageFolder === "/app" ? "" : host.imageFolder.slice("/app/".length).replace(/$/, "/")}\\n`), host.imageFolder);
  }
  assert.equal((dockerfile.match(/dotnet publish /g) ?? []).length, publishedHosts.length);
});

test("a NuGet package declaring an expression gets its canonical text, its nuspec copyright and its notices", () => {
  withTree(
    {
      "polly.core/8.8.0/polly.core.nuspec": '<package><metadata><id>Polly.Core</id><authors>Michael Wolfenden, App vNext</authors><license type="expression">BSD-3-Clause</license><copyright>Copyright (c) 2015-2026, App vNext</copyright><repository type="git" url="https://github.com/App-vNext/Polly.git" commit="9ad5ae9b" /></metadata></package>',
      "polly.core/8.8.0/THIRD-PARTY-NOTICES.TXT": "Polly bundles no third-party code.",
      "apache.arrow/22.1.0/apache.arrow.nuspec": '<package><metadata><id>Apache.Arrow</id><license type="file">LICENSE.txt</license><copyright>Copyright 2016-2025 The Apache Software Foundation</copyright></metadata></package>',
      "apache.arrow/22.1.0/LICENSE.txt": `${canonicalBody("Apache-2.0")}\n\nThis product includes FlatBuffers.\nCopyright 2014 Google Inc.`,
    },
    (root) => {
      const table = new TextTable();
      const polly = nugetComponent(root, { name: "Polly.Core", version: "8.8.0" }, table);
      assert.deepEqual(
        [polly.purl, polly.licence, polly.copyright, polly.texts.length, polly.notices.length, polly.supplier, polly.detail, polly.source],
        ["pkg:nuget/Polly.Core@8.8.0", "BSD-3-Clause", ["Copyright (c) 2015-2026, App vNext"], 1, 1, "Organization: Michael Wolfenden, App vNext", "built from https://github.com/App-vNext/Polly/tree/9ad5ae9b", "https://api.nuget.org/v3-flatcontainer/polly.core/8.8.0/polly.core.8.8.0.nupkg"],
      );
      const arrow = nugetComponent(root, { name: "Apache.Arrow", version: "22.1.0" }, table);
      assert.deepEqual([arrow.licence, arrow.copyright], ["NOASSERTION", ["Copyright 2016-2025 The Apache Software Foundation", "Copyright 2014 Google Inc."]]);
      assert.match(table.get(arrow.texts[0] as string)?.text ?? "", /This product includes FlatBuffers\./);
      assert.throws(() => nugetComponent(root, { name: "Missing", version: "1.0.0" }, table), /Missing 1\.0\.0 is missing from/);
    },
  );
});

test("an npm package's licence files give its texts and copyright, and a package without one gets the canonical text", () => {
  withTree(
    {
      ".pnpm/@scope+long-name@2.1._0123456789abcdef0123456789abcdef/node_modules/@scope/long-name/package.json": '{"name":"@scope/long-name","version":"2.1.9","license":"MIT","author":"Jane Doe <jane@example.com> (https://example.com)"}',
      ".pnpm/@scope+long-name@2.1._0123456789abcdef0123456789abcdef/node_modules/@scope/long-name/LICENSE": `MIT License\n\nCopyright (c) 2021 Jane Doe\n\n${canonicalBody("MIT").replace(/^MIT License\n\n/, "")}`,
      ".pnpm/@scope+long-name@2.1._0123456789abcdef0123456789abcdef/node_modules/@scope/long-name/dist/compiled/cookie/package.json": '{"name":"cookie"}',
      ".pnpm/@scope+long-name@2.1._0123456789abcdef0123456789abcdef/node_modules/@scope/long-name/dist/compiled/cookie/LICENSE": "(The MIT License)\n\nCopyright (c) 2012-2014 Roman Shtylman",
      ".pnpm/bare@1.0.0/node_modules/bare/package.json": '{"name":"bare","version":"1.0.0","license":"Apache-2.0"}',
      ".pnpm/bare@1.0.0/node_modules/bare/NOTICE": "Bare\nCopyright 2020 The Bare Authors",
      ".pnpm/bare@1.0.0/node_modules/bare/LICENSE-3rdparty.csv": "Component,Origin,License\nx,y,MIT",
    },
    (root) => {
      const folders = pnpmFolders(join(root, ".pnpm"));
      const table = new TextTable();
      const scoped = npmComponent(packageFolder(folders, "@scope/long-name", "2.1.9"), table);
      assert.deepEqual(
        [scoped.purl, scoped.licence, scoped.copyright, scoped.texts.length, scoped.notices.length, scoped.supplier, scoped.source],
        ["pkg:npm/%40scope/long-name@2.1.9", "MIT", ["Copyright (c) 2021 Jane Doe"], 1, 1, "Person: Jane Doe (jane@example.com)", "https://registry.npmjs.org/@scope/long-name/-/long-name-2.1.9.tgz"],
      );
      assert.equal(table.get(scoped.notices[0] as string)?.title, "cookie, bundled in @scope/long-name");
      const bare = npmComponent(packageFolder(folders, "bare", "1.0.0"), table);
      assert.equal(table.get(bare.texts[0] as string)?.title, "Apache-2.0");
      assert.equal(bare.notices.length, 2);
      assert.throws(() => packageFolder(folders, "bare", "2.0.0"), /bare 2\.0\.0 is not installed/);
    },
  );
});

test("declaredLicence reads the licence field in its string, object and legacy array forms", () => {
  assert.equal(declaredLicence({ license: "MIT" }), "MIT");
  assert.equal(declaredLicence({ license: { type: "ISC" } }), "ISC");
  assert.equal(declaredLicence({ licenses: [{ type: "MIT" }, "Apache-2.0"] }), "(MIT OR Apache-2.0)");
  assert.equal(declaredLicence({}), undefined);
});

test("finalBaseImage follows stage names to the digest-pinned image of the last stage", () => {
  assert.equal(finalBaseImage("FROM a@sha256:" + "1".repeat(64) + " AS base\nRUN x\nFROM base AS runtime\n"), "a@sha256:" + "1".repeat(64));
  assert.equal(finalBaseImage(readFileSync(join(repoRoot, "infra", "docker", "web.Dockerfile"), "utf8")).startsWith("docker.io/library/node:24-slim@sha256:"), true);
  assert.throws(() => finalBaseImage("FROM node:24-slim\n"), /not pinned by digest/);
  assert.throws(() => finalBaseImage("RUN x\n"), /has no FROM line/);
});

test("image files resolve directory and file links and read the operating system release", () => {
  const entry = (path: string, value: string | { link: string }): [string, ImageFile] => [path, typeof value === "string" ? { path, type: "0", linkName: "", content: new TextEncoder().encode(value) } : { path, type: "2", linkName: value.link, content: undefined }];
  const files = new ImageFiles(
    new Map([
      entry("etc/os-release", { link: "../usr/lib/os-release" }),
      entry("usr/lib/os-release", 'ID=ubuntu\nVERSION_ID="24.04"\nPRETTY_NAME="Ubuntu 24.04.4 LTS"\n'),
      entry("usr/share/doc/libgcc-s1", { link: "gcc-14-base" }),
      entry("usr/share/doc/gcc-14-base/copyright", "GCC"),
      entry("loop", { link: "loop" }),
    ]),
  );
  assert.equal(files.text("usr/share/doc/libgcc-s1/copyright"), "GCC");
  assert.equal(files.resolve("/usr/share/doc/libgcc-s1/copyright"), "usr/share/doc/gcc-14-base/copyright");
  assert.deepEqual(osRelease(files), { ID: "ubuntu", VERSION_ID: "24.04", PRETTY_NAME: "Ubuntu 24.04.4 LTS" });
  assert.throws(() => files.resolve("loop/x"), /chain of more than 40 symbolic links/);
});
