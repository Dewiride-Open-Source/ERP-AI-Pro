import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { repoRoot, walk } from "./lib/walk.ts";

const MINIMUM_LINE_COVERAGE = 80;
const resultsRoot = join(repoRoot, "backend", "artifacts", "TestResults");
const measuredLayers = /[\\/]backend[\\/]Modules[\\/][^\\/]+[\\/][^\\/]+[\\/]Module[\\/].*[\\/](Domain|Application)[\\/]/;
const classPattern = /<class [^>]*filename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g;
const linePattern = /<line number="(\d+)" hits="(\d+)"/g;

const hitsByFile = new Map<string, Map<number, number>>();

if (!existsSync(resultsRoot)) {
  console.error(`coverage gate: ${resultsRoot} does not exist; run the backend tests with --coverage first`);
  process.exit(1);
}

for (const report of walk(resultsRoot, (f) => f.endsWith(".cobertura.xml"))) {
  const xml = readFileSync(report, "utf8");
  for (const [, filename, body] of xml.matchAll(classPattern)) {
    if (!filename || !body || !measuredLayers.test(filename)) continue;
    const lines = hitsByFile.get(filename) ?? new Map<number, number>();
    for (const [, number, hits] of body.matchAll(linePattern)) {
      const line = Number(number);
      lines.set(line, Math.max(lines.get(line) ?? 0, Number(hits)));
    }
    hitsByFile.set(filename, lines);
  }
}

let valid = 0;
let covered = 0;
const byModule = new Map<string, { valid: number; covered: number }>();

for (const [filename, lines] of hitsByFile) {
  const module = /[\\/]Modules[\\/]([^\\/]+)[\\/]([^\\/]+)[\\/]/.exec(filename);
  const key = module ? `${module[1]}/${module[2]}` : "unknown";
  const entry = byModule.get(key) ?? { valid: 0, covered: 0 };
  for (const hits of lines.values()) {
    entry.valid += 1;
    valid += 1;
    if (hits > 0) {
      entry.covered += 1;
      covered += 1;
    }
  }
  byModule.set(key, entry);
}

if (valid === 0) {
  console.log("coverage gate: no Domain or Application lines were measured");
  process.exit(0);
}

const percentage = (covered / valid) * 100;
for (const [module, entry] of [...byModule.entries()].sort(([a], [b]) => a.localeCompare(b))) {
  console.log(`  ${module.padEnd(32)} ${((entry.covered / entry.valid) * 100).toFixed(1).padStart(6)}%  (${entry.covered}/${entry.valid})`);
}

if (percentage < MINIMUM_LINE_COVERAGE) {
  console.error(`coverage gate failed: Domain + Application line coverage is ${percentage.toFixed(1)}%, below ${MINIMUM_LINE_COVERAGE}%`);
  process.exit(1);
}

console.log(`coverage gate ok: Domain + Application line coverage is ${percentage.toFixed(1)}% (${covered}/${valid})`);
