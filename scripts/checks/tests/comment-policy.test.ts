import assert from "node:assert/strict";
import { test } from "node:test";

import { commentLines, isScanned, policyFindings } from "../lib/comment-policy.ts";

const history = "comments describe the current code, not its history";
const taskMarker = "use a roadmap note instead of a task marker";

const reasonsOf = (path: string, source: string) => policyFindings(path, source).map(({ line, reason }) => `${line}: ${reason}`);

test("a slash and an asterisk inside a script string, template or regex open no comment", () => {
  const source = [
    'const routeImports = "Route files import only @/shared/*, @/features/registry";',
    "const pattern = `src/**/*.${extension}`;",
    "const nested = `${'/*'} and ${\"//\"}`;",
    "const slashes = /\\/*/;",
    'const modules = ["next/image", "next/legacy/image"];',
  ].join("\n");
  assert.deepEqual(commentLines("frontend/packages/config/eslint/feature-boundaries.mjs", source), []);
  assert.deepEqual(reasonsOf("frontend/packages/config/eslint/feature-boundaries.mjs", source), []);
});

test("a script block comment and a script line comment, on a line of their own or after code, are read and checked", () => {
  const source = ['const glob = "@/shared/*";', "/* The legacy loader", "   stays here. */", "const value = 1; // TODO remove", "// It no longer reads the file."].join("\n");
  assert.deepEqual(commentLines("scripts/checks/lib/example.ts", source), [
    { line: 2, text: "/* The legacy loader" },
    { line: 3, text: "   stays here. */" },
    { line: 4, text: "// TODO remove" },
    { line: 5, text: "// It no longer reads the file." },
  ]);
  assert.deepEqual(reasonsOf("scripts/checks/lib/example.ts", source), [`2: ${history}`, `4: ${taskMarker}`, `5: ${history}`]);
});

test("C# strings of every form and character literals open no comment", () => {
  const source = [
    'var origin = "https://*.example.com";',
    'var path = @"C:\\temp\\// not a comment ""quoted /*""";',
    'var raw = """',
    "  // inside a raw string, deprecated /* here",
    '  """;',
    'var interpolated = $"{(flag ? "/*" : "//")} {{braces}} done";',
    "var quote = '\"'; var slash = '/';",
  ].join("\n");
  assert.deepEqual(commentLines("backend/Example.cs", source), []);
  assert.deepEqual(reasonsOf("backend/Example.cs", source), []);
});

test("C# documentation, line and block comments are read and checked", () => {
  const source = ["/// <summary>The XXX value.</summary>", 'var name = "a"; // previously b', "/* Updated to the new rule. */", "var plain = 1;"].join("\r\n");
  assert.deepEqual(reasonsOf("backend/Example.cs", source), [`1: ${taskMarker}`, `2: ${history}`, `3: ${history}`]);
  assert.deepEqual(commentLines("backend/Example.cs", source).at(1), { line: 2, text: "// previously b" });
});

test("Bicep strings, multi-line strings and interpolation holes open no comment, while its comments are checked", () => {
  const allowed = ["param endpoint string = 'https://example.com/*'", "var text = '''", "  // not a comment, legacy", "'''", "var joined = '${concat('//', 'x')}/*'"].join("\n");
  assert.deepEqual(commentLines("scripts/azure/bicep/main.bicep", allowed), []);
  assert.deepEqual(reasonsOf("scripts/azure/bicep/main.bicep", "// Deprecated by Azure.\nvar x = 1 /* HACK */"), [`1: ${history}`, `2: ${taskMarker}`]);
});

test("CSS strings open no comment, its block comments are checked and a double slash is no comment", () => {
  assert.deepEqual(commentLines("frontend/packages/ui/src/styles/globals.css", '@source "../../../apps/web/src/**/*.{ts,tsx}";\na { background: url(//example.com/x.png); }'), []);
  assert.deepEqual(reasonsOf("frontend/packages/ui/src/styles/globals.css", "/* Legacy colours. */\na { color: red; }"), [`1: ${history}`]);
});

test("hash comment languages read whole comment lines only", () => {
  assert.deepEqual(reasonsOf(".github/workflows/example.yml", "# TODO: pin the action\nrun: echo '# legacy'\n  #region setup"), [`1: ${taskMarker}`, "3: regions are not used"]);
  assert.deepEqual(reasonsOf("infra/host/apt/52erp-ai-pro-unattended-upgrades.conf", "// Deprecated origin.\n# Kept: FIXME\nUnattended-Upgrade::Origins-Pattern {};"), [`1: ${history}`, `2: ${taskMarker}`]);
});

test("only the languages with a comment syntax are scanned", () => {
  for (const path of ["a.cs", "a.ts", "a.tsx", "a.mjs", "a.js", "a.bicep", "a.css", "a.yml", "a.sh", "infra/docker/api.Dockerfile", "infra/proxy/Caddyfile", "a.service", "a.conf"]) {
    assert.equal(isScanned(path), true, path);
  }
  for (const path of ["a.json", "a.md", "COPYING", "a.txt"]) assert.equal(isScanned(path), false, path);
});
