import assert from "node:assert/strict";
import { test } from "node:test";

import type { FileSystemEntry } from "../../checks/lib/image-contents.ts";
import { type Component, sortedComponents } from "../lib/inventory.ts";
import { TextTable } from "../lib/licence-texts.ts";
import { differences, noticesDocument, renderJson, renderText } from "../lib/render.ts";
import { inventoryProblems, observedImage, spdxCreated, spdxDocument } from "../lib/spdx.ts";

const copyright = "Copyright (c) 2026 Dewiride Technologies Private Limited.";

function inventory() {
  const texts = new TextTable();
  const mit = texts.add("MIT", "MIT License\n\nPermission is hereby granted.");
  const notice = texts.add("NOTICE of b 2.0.0", "B\nCopyright 2020 B Authors");
  const component = (overrides: Partial<Component> & Pick<Component, "purl" | "kind" | "name" | "version">): Component => ({
    licence: "MIT",
    copyright: [],
    texts: [],
    notices: [],
    inImage: [],
    source: `https://example.com/${overrides.name}`,
    ...overrides,
  });
  const components = sortedComponents([
    component({ purl: "pkg:deb/debian/zlib1g@1.2?arch=amd64&distro=debian-12", kind: "deb", name: "zlib1g", version: "1.2", licence: "NOASSERTION", inImage: ["/usr/share/doc/zlib1g/copyright"] }),
    component({ purl: "pkg:npm/b@2.0.0", kind: "npm", name: "b", version: "2.0.0", texts: [mit], notices: [notice], copyright: ["Copyright (c) 2020 B"] }),
    component({ purl: "pkg:npm/a@1.0.0", kind: "npm", name: "a", version: "1.0.0", texts: [mit], supplier: "Person: A" }),
    component({ purl: "pkg:generic/node@24.21.0", kind: "runtime", name: "Node.js", version: "24.21.0", inImage: ["/usr/local/LICENSE"], detail: "the runtime" }),
  ]);
  return { image: "web" as const, base: "node@sha256:abc", system: "Debian GNU/Linux 12 (bookworm)", components, texts };
}

test("the notices list the components by kind and name and number each shared text once", () => {
  const text = renderText(inventory(), copyright);
  assert.match(text, /^Third-party notices of the ERP-AI-Pro web image\n={47}\n/);
  assert.ok(text.indexOf("Runtimes and tools of the base image (1)") < text.indexOf("npm packages of the web app (2)"));
  assert.ok(text.indexOf("\na 1.0.0\n") < text.indexOf("\nb 2.0.0\n"));
  assert.match(text, /\nb 2\.0\.0\n {2}Licence: MIT\n {2}Copyright \(c\) 2020 B\n {2}Licence text: \[1\]\n {2}Notices: \[2\]\n/);
  assert.match(text, /\nzlib1g 1\.2\n {2}Licence: not declared in a form an SPDX expression can state; see its copyright file\n/);
  assert.equal(text.match(/^\[1\] MIT$/gm)?.length, 1);
  assert.match(text, /\[2\] NOTICE of b 2\.0\.0\n-+\n\nB\nCopyright 2020 B Authors\n$/);
  assert.match(text, new RegExp(copyright.replaceAll(".", "\\.").replaceAll("(", "\\(").replaceAll(")", "\\)")));
});

test("the notices document keys every component by its package URL with its SPDX licence and text numbers", () => {
  const document = noticesDocument(inventory(), copyright);
  assert.deepEqual(Object.keys(document.components), ["pkg:generic/node@24.21.0", "pkg:npm/a@1.0.0", "pkg:npm/b@2.0.0", "pkg:deb/debian/zlib1g@1.2?arch=amd64&distro=debian-12"]);
  assert.deepEqual(document.components["pkg:npm/b@2.0.0"], { kind: "npm", name: "b", version: "2.0.0", licence: "MIT", spdx: "MIT", copyright: ["Copyright (c) 2020 B"], texts: [1], notices: [2], inImage: [], source: "https://example.com/b" });
  assert.equal(document.components["pkg:deb/debian/zlib1g@1.2?arch=amd64&distro=debian-12"]?.spdx, "NOASSERTION");
  assert.deepEqual(Object.keys(document.texts), ["1", "2"]);
  assert.ok(renderJson(document).endsWith("}\n"));
});

test("differences names the first lines where the committed notices and the generated ones part", () => {
  assert.deepEqual(differences("a\nb\n", "a\nb\n"), []);
  assert.deepEqual(differences("a\nb", "a\nc\nd"), ['line 2 holds "b", the generator writes "c"', 'line 3 holds the end of the file, the generator writes "d"']);
});

const entry = (path: string, text: string): [string, FileSystemEntry] => [path, { path, type: "0", size: text.length, linkName: "", layer: 0, content: new TextEncoder().encode(text) }];

test("the image's own package databases, dependency files and manifests must match its notices", () => {
  const document = noticesDocument(inventory(), copyright);
  const files = new Map([
    entry("var/lib/dpkg/status", "Package: zlib1g\nVersion: 1.2\nArchitecture: amd64\n"),
    entry("app/node_modules/.pnpm/a@1.0.0/node_modules/a/package.json", '{"name":"a","version":"1.0.0"}'),
    entry("app/node_modules/.pnpm/b@2.0.0/node_modules/b/package.json", '{"name":"b","version":"2.0.0"}'),
    entry("app/node_modules/.pnpm/b@2.0.0/node_modules/b/dist/compiled/x/package.json", '{"name":"x","version":"9.9.9"}'),
    entry("app/node_modules/@dewiride/erp-ui/package.json", '{"name":"@dewiride/erp-ui","version":"0.1.0"}'),
  ]);
  assert.deepEqual(inventoryProblems("web", document, observedImage(files, { NODE_VERSION: "24.21.0" })), []);
  const drifted = new Map([...files, entry("var/lib/dpkg/status", "Package: zlib1g\nVersion: 1.3\nArchitecture: amd64\n"), entry("app/node_modules/.pnpm/c@3.0.0/node_modules/c/package.json", '{"name":"c","version":"3.0.0"}')]);
  assert.deepEqual(inventoryProblems("web", document, observedImage(drifted, { NODE_VERSION: "24.22.0" })), [
    "the image holds the dpkg package zlib1g 1.3, which its notices do not list",
    "the notices list the dpkg package zlib1g 1.2, which the image does not hold",
    "the image holds the npm package c@3.0.0, which its notices do not list",
    "the image sets NODE_VERSION=24.22.0, but the notices list Node.js 24.21.0",
  ]);
  assert.deepEqual(inventoryProblems("api", document, observedImage(files, {})), ["/usr/share/doc/erp-ai-pro/third-party-notices.json describes the web image, not api"]);
});

test("the SPDX document describes the image and contains one package per component", () => {
  const spdx = spdxDocument({ image: "web", version: "0.1.0", created: "2026-10-28T04:30:15.123456789Z", configDigest: `sha256:${"c".repeat(64)}`, licence: "LGPL-3.0-only", document: noticesDocument(inventory(), copyright) }) as {
    spdxVersion: string;
    SPDXID: string;
    documentNamespace: string;
    creationInfo: { created: string; creators: string[] };
    packages: { SPDXID: string; name: string; licenseDeclared: string; copyrightText: string; downloadLocation: string; externalRefs?: { referenceLocator: string }[] }[];
    relationships: { spdxElementId: string; relationshipType: string; relatedSpdxElement: string }[];
  };
  assert.equal(spdx.spdxVersion, "SPDX-2.3");
  assert.equal(spdx.SPDXID, "SPDXRef-DOCUMENT");
  assert.equal(spdx.documentNamespace, `https://github.com/Dewiride-Open-Source/ERP-AI-Pro/spdx/web/0.1.0/${"c".repeat(64)}`);
  assert.deepEqual(spdx.creationInfo, { created: "2026-10-28T04:30:15Z", creators: ["Organization: Dewiride Technologies Private Limited", "Tool: erp-ai-pro-notices"] });
  assert.equal(spdx.packages.length, 5);
  assert.deepEqual([spdx.packages[0]?.SPDXID, spdx.packages[0]?.downloadLocation, spdx.packages[0]?.copyrightText], ["SPDXRef-Image", "https://github.com/Dewiride-Open-Source/ERP-AI-Pro/releases/tag/v0.1.0", copyright]);
  const zlib = spdx.packages.find((item) => item.name === "zlib1g");
  assert.deepEqual([zlib?.licenseDeclared, zlib?.copyrightText, zlib?.externalRefs?.[0]?.referenceLocator], ["NOASSERTION", "NOASSERTION", "pkg:deb/debian/zlib1g@1.2?arch=amd64&distro=debian-12"]);
  assert.equal(new Set(spdx.packages.map((item) => item.SPDXID)).size, 5);
  assert.deepEqual(spdx.relationships[0], { spdxElementId: "SPDXRef-DOCUMENT", relationshipType: "DESCRIBES", relatedSpdxElement: "SPDXRef-Image" });
  assert.equal(spdx.relationships.filter((relationship) => relationship.relationshipType === "CONTAINS").length, 4);
  assert.equal(spdxDocument({ image: "web", version: "local", created: "2026-10-28T04:30:15Z", configDigest: "sha256:0", licence: "LGPL-3.0-only", document: noticesDocument(inventory(), copyright) }).packages instanceof Array, true);
  assert.throws(() => spdxCreated("yesterday"), /is not a time/);
});
