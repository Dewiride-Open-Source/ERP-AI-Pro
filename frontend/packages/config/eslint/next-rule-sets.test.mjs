import assert from "node:assert/strict";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

import { Linter } from "eslint";

import { nextConfig } from "./next.mjs";
import { reactLibraryConfig } from "./react-library.mjs";

const presets = {
  nextConfig: {
    config: nextConfig,
    root: fileURLToPath(new URL("../../../apps/web/", import.meta.url)),
    file: "src/shared/layout/sample.tsx",
  },
  reactLibraryConfig: {
    config: reactLibraryConfig,
    root: fileURLToPath(new URL("../../ui/", import.meta.url)),
    file: "src/components/sample/sample.tsx",
  },
};

const cases = [
  {
    condition: "ListItemWithoutKey",
    ruleId: "react/jsx-key",
    code: "export function List({ items }: { items: string[] }) {\n  return <ul>{items.map((item) => <li>{item}</li>)}</ul>;\n}",
  },
  {
    condition: "ImageWithoutAlternativeText",
    ruleId: "jsx-a11y/alt-text",
    code: 'export function Logo() {\n  return <img src="/logo.png" />;\n}',
  },
  {
    condition: "AnonymousDefaultExport",
    ruleId: "import/no-anonymous-default-export",
    code: "export default () => null;",
  },
  {
    condition: "HookCalledConditionally",
    ruleId: "react-hooks/rules-of-hooks",
    code: 'import { useState } from "react";\n\nexport function Toggle({ shown }: { shown: boolean }) {\n  if (shown) useState(false);\n  return null;\n}',
  },
];

for (const [presetName, { config, root, file }] of Object.entries(presets)) {
  const linter = new Linter({ cwd: root });

  for (const { condition, ruleId, code } of cases) {
    test(`${presetName}_${condition}_IsReported`, () => {
      const messages = linter.verify(code, config, { filename: join(root, file) });
      const unlinted = messages.filter((message) => message.fatal === true || message.ruleId === null);

      assert.deepEqual(unlinted, [], `${file} must parse and match a configuration object`);
      assert.ok(
        messages.some((message) => message.ruleId === ruleId),
        `${ruleId} is not reported:\n${messages.map((message) => `${message.line}: ${message.ruleId} ${message.message}`).join("\n")}`,
      );
    });
  }
}
