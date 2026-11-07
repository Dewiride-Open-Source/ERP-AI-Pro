import { editJson, rowsAfter } from "./backend.ts";
import type { ScaffoldModule } from "./module.ts";
import { insertAt, nextLineStart, preservingShape, sameText, snippet } from "./text.ts";

export type FlagSeed = { readonly id: string; readonly enabled: Readonly<Record<string, unknown>> };

export const seedLabels = ["local-dev", "production"] as const;

const moduleFlagPrefix = "Erp.Modules.";
const flagRow = /^\| `(Erp\.Modules\.[^`]*)` \|/gm;
const modulesTable = /^\| Domain \/ Module \| API group \| Web routes \| Notes \|\n\|[-| ]+\|\n/m;
const modulesRow = /^\| ([^|]+?) \/ ([^|]+?) \|/;
const gatedSentence = /^Every module route group is gated by its module flag/m;
const routesParagraph = /^([A-Za-z0-9]+) \/ ([A-Za-z0-9]+) routes \(/gm;

export function flagSeedsOf(text: string): FlagSeed[] {
  return flagList(JSON.parse(text.replace(/^\uFEFF/, "")) as unknown) as FlagSeed[];
}

// A module flag joins the others, before the platform flags, enabled under every label, so the module is on until the
// owner turns it off in App Configuration.
export function registerFlag(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) =>
    editJson(source, (document) => {
      const flags = flagList(document);
      if (flags.some((flag) => sameText(flag.id, module.featureFlag))) throw new Error(`it already seeds ${module.featureFlag}`);
      const last = flags.findLastIndex((flag) => flag.id.startsWith(moduleFlagPrefix));
      flags.splice(last + 1, 0, { id: module.featureFlag, enabled: Object.fromEntries(seedLabels.map((label) => [label, true])) });
    }),
  );
}

export function flagRowsOf(text: string): string[] {
  return [...text.replaceAll("\r\n", "\n").matchAll(flagRow)].map((match) => match[1] as string);
}

export function registerFlagRow(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const rows = [...source.matchAll(flagRow)];
    const last = rows.at(-1);
    if (last === undefined) throw new Error("it holds no feature flag row | `Erp.Modules.…` | to add the module's after");
    if (rows.some((row) => sameText(row[1] as string, module.featureFlag))) throw new Error(`it already describes ${module.featureFlag}`);
    return insertAt(source, nextLineStart(source, last.index), snippet("configuration-flag-row.md", module.tokens));
  });
}

export function routedModulesOf(text: string): { readonly rows: string[]; readonly routeTables: string[] } {
  const source = text.replaceAll("\r\n", "\n");
  const table = modulesTable.exec(source);
  const rows =
    table === null
      ? []
      : rowsAfter(source, table.index + table[0].length).flatMap((row) => {
          const match = modulesRow.exec(row.text);
          return match === null ? [] : [`${(match[1] as string).trim()}/${(match[2] as string).trim()}`];
        });
  return { rows, routeTables: [...source.matchAll(routesParagraph)].map((match) => `${match[1]}/${match[2]}`) };
}

// The module's row closes the Modules table, and its route table comes last among the route tables, before the rule that
// gates every module's routes.
export function registerRoutes(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const table = modulesTable.exec(source);
    if (table === null) throw new Error("it holds no | Domain / Module | API group | Web routes | Notes | table");
    const gated = gatedSentence.exec(source);
    if (gated === null) throw new Error('it holds no paragraph starting "Every module route group is gated by its module flag" to put the route table before');
    const name = `${module.domain.pascal}/${module.module.pascal}`;
    const { rows, routeTables } = routedModulesOf(source);
    if ([...rows, ...routeTables].some((listed) => sameText(listed, name))) throw new Error(`it already lists ${module.domain.pascal} / ${module.module.pascal}`);
    const rowsEnd = rowsAfter(source, table.index + table[0].length).at(-1);
    if (rowsEnd === undefined) throw new Error("its Modules table has no row to add the module's after");
    const rowAt = nextLineStart(source, rowsEnd.index);
    if (gated.index < rowAt) throw new Error("its rule that gates every module's routes comes before the end of its Modules table");
    const withRoutes = insertAt(source, gated.index, `${snippet("routing-routes.md", module.tokens)}\n`);
    return insertAt(withRoutes, rowAt, snippet("routing-module-row.md", module.tokens));
  });
}

function flagList(document: unknown): FlagSeed[] {
  const flags = (document as { flags?: unknown } | null)?.flags;
  if (!Array.isArray(flags) || !flags.every((flag) => typeof (flag as { id?: unknown } | null)?.id === "string")) {
    throw new Error('it does not hold { "flags": [{ "id": … }] }');
  }

  return flags as FlagSeed[];
}
