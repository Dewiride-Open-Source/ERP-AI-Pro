import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { parseArgs } from 'node:util';
import { labelOf, loadRoadmap, locate, paths, resolveId, saveRoadmap, serialize, subPhaseLabel, today, type Roadmap } from './lib/model.ts';
import { RoadmapError, addPhase, addSubPhase, block, defer, done, move, note, resume, setAcceptance, start } from './lib/mutate.ts';
import { nextCandidates, render } from './lib/render.ts';
import { formatIssues, validate } from './lib/validate.ts';

const USAGE = `Usage: node scripts/roadmap/roadmap.ts <command> [options]

Read-only
  build                          validate roadmap.json and write ROADMAP.md
  check [--allow-multiple-wip]   validate, verify canonical formatting and a fresh ROADMAP.md (CI)
  next [--count N]               list the next eligible planned sub-phases
  show <id|label>                print one phase or sub-phase as JSON

Mutations (rewrite roadmap.json canonically, regenerate ROADMAP.md, then run check)
  start <id|label> [--allow-multiple-wip]
  done <id|label>
  block <id|label> --reason "<text>"
  defer <id|label>                (sub-phase or whole phase)
  resume <id|label>               (sub-phase or whole phase)
  note <id|label> "<text>"        (empty text clears the note)
  acceptance <id|label> "<criterion>" ["<criterion>" ...]
  add-phase --id <slug> --title "<t>" --goal "<g>" --milestone <slug> [--after <phase>] [--depends-on a,b]
  add-sub-phase --phase <id> --id <slug> --title "<t>" --scope "<s>" [--after <sub-phase>] [--tags a,b] [--depends-on a,b]
  move <sub-phase> --to <phase> [--at N]

Ids are stable slugs; labels such as P07 or P07.3 are accepted anywhere an id is.
Every mutation stamps today's date unless --date YYYY-MM-DD is given.
--file <path> operates on another roadmap.json; its ROADMAP.md is written beside it.
`;

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    'allow-multiple-wip': { type: 'boolean', default: false },
    count: { type: 'string' },
    reason: { type: 'string' },
    date: { type: 'string' },
    id: { type: 'string' },
    title: { type: 'string' },
    goal: { type: 'string' },
    scope: { type: 'string' },
    milestone: { type: 'string' },
    phase: { type: 'string' },
    after: { type: 'string' },
    to: { type: 'string' },
    at: { type: 'string' },
    tags: { type: 'string' },
    'depends-on': { type: 'string' },
    file: { type: 'string' },
    help: { type: 'boolean', default: false },
  },
});

const [command, ...args] = positionals;
const jsonFile = values.file ?? paths.roadmapJson;
const markdownFile = values.file ? values.file.replace(/roadmap\.json$/, 'ROADMAP.md') : paths.roadmapMarkdown;
const date = values.date ?? today();

function fail(message: string, code = 1): never {
  console.error(message);
  process.exit(code);
}

function list(value: string | undefined): string[] {
  return value ? value.split(',').map((s) => s.trim()).filter(Boolean) : [];
}

function integer(name: 'count' | 'at'): number | undefined {
  const value = values[name];
  if (value === undefined) return undefined;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 1) fail(`--${name} must be a positive integer`);
  return parsed;
}

function required(name: keyof typeof values): string {
  const value = values[name];
  if (typeof value !== 'string' || !value.trim()) fail(`--${name} is required\n\n${USAGE}`);
  return value;
}

function assertValid(roadmap: Roadmap): void {
  const issues = validate(roadmap, { allowMultipleWip: values['allow-multiple-wip'], today: date });
  const errors = issues.filter((i) => i.level === 'error');
  if (issues.length > 0) console.error(formatIssues(issues));
  if (errors.length > 0) fail(`roadmap.json has ${errors.length} error(s)`);
}

function build(roadmap: Roadmap): void {
  assertValid(roadmap);
  saveRoadmap(roadmap, jsonFile);
  writeFileSync(markdownFile, render(roadmap), 'utf8');
  console.log(`wrote ${jsonFile} (canonical) and ${markdownFile}`);
}

function check(roadmap: Roadmap): void {
  assertValid(roadmap);
  const raw = readFileSync(jsonFile, 'utf8');
  if (raw !== serialize(roadmap)) fail('roadmap.json is not in canonical form; run `node scripts/roadmap/roadmap.ts build` and commit the result');
  const expected = render(roadmap);
  const actual = existsSync(markdownFile) ? readFileSync(markdownFile, 'utf8') : '';
  if (actual !== expected) fail('ROADMAP.md is stale; run `node scripts/roadmap/roadmap.ts build` and commit the result');
  console.log('roadmap is valid, canonical and ROADMAP.md is fresh');
}

function commit(roadmap: Roadmap, message: string): void {
  assertValid(roadmap);
  saveRoadmap(roadmap, jsonFile);
  writeFileSync(markdownFile, render(roadmap), 'utf8');
  console.log(message);
}

function describe(roadmap: Roadmap, id: string): string {
  return `${labelOf(roadmap, id)} (${id})`;
}

if (values.help || !command) {
  console.log(USAGE);
  process.exit(command ? 0 : 1);
}

try {
  const roadmap = loadRoadmap(jsonFile);
  switch (command) {
    case 'build':
      build(roadmap);
      break;
    case 'check':
      check(roadmap);
      break;
    case 'next': {
      const count = integer('count') ?? 3;
      for (const [phaseIndex, phase] of roadmap.phases.entries()) {
        for (const [subIndex, sub] of phase.subPhases.entries()) {
          if (sub.status === 'in-progress') console.log(`in progress: ${subPhaseLabel(phaseIndex, subIndex)}  ${sub.id}  (since ${sub.startedOn})`);
        }
      }
      const candidates = nextCandidates(roadmap, count);
      if (candidates.length === 0) console.log('nothing else is eligible');
      for (const c of candidates) console.log(`${c.label}  ${c.subPhase.id}\n  ${c.subPhase.title}\n  ${c.subPhase.scope}`);
      break;
    }
    case 'show': {
      const id = args[0] ?? fail(USAGE);
      const resolved = resolveId(roadmap, id);
      const found = resolved === undefined ? undefined : locate(roadmap, resolved);
      if (!found) fail(`unknown id or label "${id}"`);
      console.log(JSON.stringify(found.subPhase ?? found.phase, null, 2));
      break;
    }
    case 'start': {
      const sub = start(roadmap, args[0] ?? fail(USAGE), date, values['allow-multiple-wip']);
      commit(roadmap, `started ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'done': {
      const sub = done(roadmap, args[0] ?? fail(USAGE), date);
      commit(roadmap, `completed ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'block': {
      const sub = block(roadmap, args[0] ?? fail(USAGE), required('reason'), date);
      commit(roadmap, `blocked ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'defer': {
      const item = defer(roadmap, args[0] ?? fail(USAGE), date);
      commit(roadmap, `deferred ${describe(roadmap, item.id)}`);
      break;
    }
    case 'resume': {
      const item = resume(roadmap, args[0] ?? fail(USAGE), date);
      commit(roadmap, `resumed ${describe(roadmap, item.id)}`);
      break;
    }
    case 'note': {
      const sub = note(roadmap, args[0] ?? fail(USAGE), args[1] ?? '', date);
      commit(roadmap, `noted ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'acceptance': {
      const sub = setAcceptance(roadmap, args[0] ?? fail(USAGE), args.slice(1), date);
      commit(roadmap, `acceptance updated for ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'add-phase': {
      const phase = addPhase(
        roadmap,
        { id: required('id'), title: required('title'), goal: required('goal'), milestone: required('milestone'), dependsOn: list(values['depends-on']), after: values.after },
        date,
      );
      commit(roadmap, `added phase ${describe(roadmap, phase.id)}`);
      break;
    }
    case 'add-sub-phase': {
      const sub = addSubPhase(
        roadmap,
        { phase: required('phase'), id: required('id'), title: required('title'), scope: required('scope'), tags: list(values.tags), dependsOn: list(values['depends-on']), after: values.after },
        date,
      );
      commit(roadmap, `added sub-phase ${describe(roadmap, sub.id)}`);
      break;
    }
    case 'move': {
      const sub = move(roadmap, args[0] ?? fail(USAGE), required('to'), integer('at'), date);
      commit(roadmap, `moved ${describe(roadmap, sub.id)}`);
      break;
    }
    default:
      fail(`unknown command "${command}"\n\n${USAGE}`);
  }
} catch (error) {
  if (error instanceof RoadmapError) fail(error.message);
  throw error;
}
