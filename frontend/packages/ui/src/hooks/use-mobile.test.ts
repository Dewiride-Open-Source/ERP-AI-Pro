import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { test } from "node:test";

const stylesheets = new URL("../styles/", import.meta.url);

function declaredValue(css: string, name: string): string | undefined {
  return new RegExp(`--${name}:\\s*([^;]+);`).exec(css)?.[1]?.trim();
}

function mdBreakpoint(): string | undefined {
  const own = readdirSync(stylesheets, { recursive: true, encoding: "utf8" })
    .filter((file) => file.endsWith(".css"))
    .map((file) => declaredValue(readFileSync(new URL(file, stylesheets), "utf8"), "breakpoint-md"))
    .find((value) => value !== undefined);
  return (
    own ??
    declaredValue(
      readFileSync(new URL(import.meta.resolve("tailwindcss/theme.css")), "utf8"),
      "breakpoint-md",
    )
  );
}

test("useIsMobile_MediaQuery_EndsWhereTheMdBreakpointBegins", () => {
  const hook = readFileSync(new URL("./use-mobile.ts", import.meta.url), "utf8");
  const query = /const mobileQuery = "([^"]+)";/.exec(hook)?.[1];
  const md = mdBreakpoint();

  assert.ok(md, "Tailwind's theme declares --breakpoint-md");
  assert.equal(query, `(width < ${md})`);
});
