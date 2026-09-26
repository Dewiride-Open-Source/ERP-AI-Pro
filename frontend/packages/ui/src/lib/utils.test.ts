import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import { typeRoles } from "@dewiride/erp-config/eslint/type-roles";

import { cn } from "./utils.ts";

const typography = readFileSync(new URL("../styles/tokens/typography.css", import.meta.url), "utf8");

const declaredRoles = [...typography.matchAll(/^\s*--text-([a-z]+):/gm)].map((match) => match[1]);

test("cn_TypeRoleAfterASize_ReplacesTheSize", () => {
  for (const role of declaredRoles) {
    assert.equal(cn("font-medium text-base", `text-${role}`), `font-medium text-${role}`);
  }
});

test("cn_SizeAfterATypeRole_ReplacesTheRole", () => {
  assert.equal(cn("text-title", "text-sm"), "text-sm");
});

test("cn_TypeRoleBesideATextColour_KeepsBoth", () => {
  for (const role of declaredRoles) {
    assert.equal(cn(`text-${role} text-primary uppercase`), `text-${role} text-primary uppercase`);
  }
});

test("TypeRoles_TypographyTokensAndLintRule_NameTheSameRoles", () => {
  assert.deepEqual([...declaredRoles].sort(), [...typeRoles].sort());
});
