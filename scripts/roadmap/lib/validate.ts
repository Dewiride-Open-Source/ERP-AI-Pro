import { DATE_PATTERN, ID_PATTERN, STATUSES, isDone, phaseStatus, type Roadmap, type SubPhase } from './model.ts';

export type Issue = { level: 'error' | 'warning'; message: string };

const BARE_LABEL = /(?<![`\w])P\d{2}(?:\.\d{1,2})?(?![`\w])/;

export type ValidateOptions = { allowMultipleWip?: boolean; today?: string };

export function validate(roadmap: Roadmap, options: ValidateOptions = {}): Issue[] {
  const issues: Issue[] = [];
  const error = (message: string) => issues.push({ level: 'error', message });
  const warning = (message: string) => issues.push({ level: 'warning', message });

  if (roadmap.version !== 1) error(`version must be 1, got ${String(roadmap.version)}`);
  if (!DATE_PATTERN.test(roadmap.updatedOn)) error(`updatedOn must be an ISO date, got ${roadmap.updatedOn}`);
  if (!Array.isArray(roadmap.milestones) || roadmap.milestones.length === 0) error('milestones must be a non-empty array');
  if (!Array.isArray(roadmap.phases) || roadmap.phases.length === 0) error('phases must be a non-empty array');
  if (issues.length > 0) return issues;

  const milestoneIds = new Set<string>();
  for (const milestone of roadmap.milestones) {
    if (!ID_PATTERN.test(milestone.id)) error(`milestone id "${milestone.id}" is not a slug`);
    if (milestoneIds.has(milestone.id)) error(`duplicate milestone id "${milestone.id}"`);
    milestoneIds.add(milestone.id);
    if (!milestone.title?.trim()) error(`milestone "${milestone.id}" has no title`);
  }

  const allIds = new Set<string>();
  for (const phase of roadmap.phases) {
    allIds.add(phase.id);
    for (const sub of phase.subPhases ?? []) allIds.add(sub.id);
  }

  const seen = new Set<string>();
  const titles = new Set<string>();
  const inProgress: string[] = [];

  for (const [phaseIndex, phase] of roadmap.phases.entries()) {
    const where = `phase ${phaseIndex + 1} (${phase.id})`;
    if (!ID_PATTERN.test(phase.id)) error(`${where}: id is not a slug`);
    if (seen.has(phase.id)) error(`${where}: duplicate id`);
    seen.add(phase.id);
    if (!phase.title?.trim()) error(`${where}: missing title`);
    if (!phase.goal?.trim()) error(`${where}: missing goal`);
    rejectBareLabels(where, { title: phase.title, goal: phase.goal }, error);
    if (!milestoneIds.has(phase.milestone)) error(`${where}: unknown milestone "${phase.milestone}"`);
    if (!Array.isArray(phase.dependsOn)) error(`${where}: dependsOn must be an array`);
    if (!Array.isArray(phase.subPhases)) error(`${where}: subPhases must be an array`);
    else if (phase.subPhases.length === 0) warning(`${where}: has no sub-phases yet`);
    if (titles.has(phase.title)) warning(`${where}: duplicate title "${phase.title}"`);
    titles.add(phase.title);

    for (const dep of phase.dependsOn ?? []) {
      if (!allIds.has(dep)) error(`${where}: dependsOn references unknown id "${dep}"`);
      if (dep === phase.id) error(`${where}: depends on itself`);
    }

    for (const [subIndex, sub] of (phase.subPhases ?? []).entries()) {
      validateSubPhase(sub, `${where} sub-phase ${subIndex + 1} (${sub.id})`, { seen, allIds, phaseId: phase.id, error, warning, today: options.today });
      if (sub.status === 'in-progress') inProgress.push(sub.id);
    }
  }

  const cycle = findCycle(roadmap);
  if (cycle) error(`dependency cycle: ${cycle.join(' -> ')}`);

  for (const phase of roadmap.phases) {
    if (phaseStatus(phase) !== 'done') continue;
    for (const dep of phase.dependsOn) {
      if (!isDone(roadmap, dep)) error(`phase "${phase.id}" is done but depends on "${dep}" which is not done`);
    }
  }

  if (inProgress.length > 1 && !options.allowMultipleWip) error(`more than one sub-phase is in progress: ${inProgress.join(', ')}`);
  if (roadmap.phases.length > 40) warning(`${roadmap.phases.length} phases; consider splitting by milestone`);

  return issues;
}

type SubPhaseContext = {
  seen: Set<string>;
  allIds: Set<string>;
  phaseId: string;
  error: (message: string) => void;
  warning: (message: string) => void;
  today: string | undefined;
};

function validateSubPhase(sub: SubPhase, where: string, ctx: SubPhaseContext): void {
  const { seen, allIds, phaseId, error, warning, today } = ctx;
  if (!ID_PATTERN.test(sub.id)) error(`${where}: id is not a slug`);
  if (seen.has(sub.id)) error(`${where}: duplicate id`);
  seen.add(sub.id);
  if (!sub.title?.trim()) error(`${where}: missing title`);
  if (!sub.scope?.trim()) error(`${where}: missing scope`);
  rejectBareLabels(where, { title: sub.title, scope: sub.scope, notes: sub.notes, blockedReason: sub.blockedReason, ...Object.fromEntries((sub.acceptance ?? []).map((a, i) => [`acceptance[${i}]`, a])) }, error);
  if (!STATUSES.includes(sub.status)) error(`${where}: invalid status "${String(sub.status)}"`);
  if (!Array.isArray(sub.acceptance)) error(`${where}: acceptance must be an array`);
  if (!Array.isArray(sub.tags)) error(`${where}: tags must be an array`);
  if (!Array.isArray(sub.dependsOn)) error(`${where}: dependsOn must be an array`);
  for (const dep of sub.dependsOn ?? []) {
    if (!allIds.has(dep)) error(`${where}: dependsOn references unknown id "${dep}"`);
    if (dep === sub.id || dep === phaseId) error(`${where}: depends on itself or its own phase`);
  }
  for (const key of ['startedOn', 'completedOn'] as const) {
    const value = sub[key];
    if (value === undefined) continue;
    if (!DATE_PATTERN.test(value)) error(`${where}: ${key} must be an ISO date`);
    else if (today && value > latestAcceptableDate(today)) error(`${where}: ${key} ${value} is in the future`);
  }
  if (sub.status === 'done' && !sub.completedOn) error(`${where}: done without completedOn`);
  if (sub.status === 'in-progress' && !sub.startedOn) error(`${where}: in-progress without startedOn`);
  if (sub.status === 'blocked' && !sub.blockedReason) error(`${where}: blocked without blockedReason`);
  if (sub.status !== 'blocked' && sub.blockedReason) warning(`${where}: blockedReason set but status is ${sub.status}`);
  if (sub.status !== 'done' && sub.completedOn) warning(`${where}: completedOn set but status is ${sub.status}`);
}

function latestAcceptableDate(today: string): string {
  const date = new Date(`${today}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

function rejectBareLabels(where: string, fields: Record<string, string | undefined>, error: (message: string) => void): void {
  for (const [field, text] of Object.entries(fields)) {
    const match = text === undefined ? null : BARE_LABEL.exec(text);
    if (match) error(`${where}: ${field} references "${match[0]}" by positional label; use the stable slug (labels change when phases move)`);
  }
}

function findCycle(roadmap: Roadmap): string[] | undefined {
  const edges = new Map<string, string[]>();
  for (const phase of roadmap.phases) {
    edges.set(phase.id, [...phase.dependsOn]);
    for (const sub of phase.subPhases) edges.set(sub.id, [...sub.dependsOn, phase.id]);
  }
  const state = new Map<string, 'visiting' | 'done'>();
  const stack: string[] = [];
  const visit = (id: string): string[] | undefined => {
    const current = state.get(id);
    if (current === 'done') return undefined;
    if (current === 'visiting') return [...stack.slice(stack.indexOf(id)), id];
    state.set(id, 'visiting');
    stack.push(id);
    for (const dep of edges.get(id) ?? []) {
      const found = visit(dep);
      if (found) return found;
    }
    stack.pop();
    state.set(id, 'done');
    return undefined;
  };
  for (const id of edges.keys()) {
    const found = visit(id);
    if (found) return found;
  }
  return undefined;
}

export function formatIssues(issues: Issue[]): string {
  return issues.map((issue) => `${issue.level === 'error' ? 'ERROR' : 'WARN '} ${issue.message}`).join('\n');
}
