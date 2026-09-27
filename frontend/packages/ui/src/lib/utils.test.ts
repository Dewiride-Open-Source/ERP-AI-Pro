import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import { typeRoles as lintedTypeRoles } from "@dewiride/erp-config/eslint/type-roles";

import { cn, typeRoles } from "./utils.ts";

function tokenNames(file: string, namespace: string): string[] {
  const css = readFileSync(new URL(`../styles/tokens/${file}`, import.meta.url), "utf8");
  const declaration = new RegExp(`^\\s*--${namespace}-([a-z0-9]+(?:-[a-z0-9]+)*):`, "gm");

  return [...css.matchAll(declaration)].map((match) => match[1] ?? "");
}

const declaredRoles = tokenNames("typography.css", "text");

test("TypeRoles_TypographyTokensCnAndLintRule_NameTheSameRoles", () => {
  assert.deepEqual([...typeRoles].sort(), [...declaredRoles].sort());
  assert.deepEqual([...lintedTypeRoles].sort(), [...declaredRoles].sort());
});

test("cn_TypeRoleAfterASize_ReplacesTheSize", () => {
  for (const role of declaredRoles) {
    assert.equal(cn("font-medium text-base", `text-${role}`), `font-medium text-${role}`);
  }
});

test("cn_SizeAfterATypeRole_ReplacesTheRole", () => {
  for (const role of declaredRoles) {
    assert.equal(cn(`text-${role}`, "text-sm"), "text-sm");
  }
});

test("cn_TypeRoleBesideATextColour_KeepsBoth", () => {
  for (const role of declaredRoles) {
    assert.equal(cn(`text-${role} text-primary uppercase`), `text-${role} text-primary uppercase`);
  }
});

test("cn_NamedSpacingTokens_MergeWithTheNumericScale", () => {
  const names = tokenNames("layout.css", "spacing");
  assert.ok(names.length > 0);
  for (const name of names) {
    assert.equal(cn(`px-${name}`, "px-4"), "px-4");
    assert.equal(cn("gap-4", `gap-${name}`), `gap-${name}`);
    assert.equal(cn(`h-${name}`, "h-auto"), "h-auto");
  }
});

test("cn_NamedContainerTokens_MergeWithTheMaxWidthScale", () => {
  const names = tokenNames("layout.css", "container");
  assert.ok(names.length > 0);
  for (const name of names) {
    assert.equal(cn(`max-w-${name}`, "max-w-6xl"), "max-w-6xl");
    assert.equal(cn("max-w-6xl", `max-w-${name}`), `max-w-${name}`);
  }
});

test("cn_NamedEasingTokens_MergeWithTheEasingScale", () => {
  const names = tokenNames("motion.css", "ease");
  assert.ok(names.length > 0);
  for (const name of names) {
    assert.equal(cn(`ease-${name}`, "ease-in-out"), "ease-in-out");
    assert.equal(cn("ease-linear", `ease-${name}`), `ease-${name}`);
  }
});
