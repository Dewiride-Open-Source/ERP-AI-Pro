import { DATE_PATTERN, EDITIONS, ID_PATTERN, STATUSES, isDoneFor, isEdition, isEnterpriseOnly, locate, phaseStatus, subPhasesAwaitedBy, type Edition, type Roadmap, type SubPhase } from './model.ts';

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
  rejectCommunityOnEnterprise(roadmap, error);

  for (const phase of roadmap.phases) {
    if (phaseStatus(phase) !== 'done') continue;
    const editions = new Set(phase.subPhases.filter((s) => s.status === 'done').map((s) => s.edition));
    for (const dep of phase.dependsOn) {
      if ([...editions].some((edition) => !isDoneFor(roadmap, dep, edition))) error(`phase "${phase.id}" is done but depends on "${dep}" which is not done`);
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
  if (!isEdition(sub.edition)) error(`${where}: edition must be ${EDITIONS.join(' or ')}, got "${String(sub.edition)}"`);
  if (!Array.isArray(sub.acceptance)) error(`${where}: acceptance must be an array`);
  if (!Array.isArray(sub.tags)) error(`${where}: tags must be an array`);
  if (!Array.isArray(sub.dependsOn)) error(`${where}: dependsOn must be an array`);
  for (const dep of sub.dependsOn ?? []) {
    if (!allIds.has(dep)) error(`${where}: dependsOn references unknown id "${dep}"`);
    if (dep === sub.id || dep === phaseId) error(`${where}: depends on itself or its own phase`);
  }
  for (const key of ['startedOn', 'completedOn', 'editionConfirmedOn'] as const) {
    const value = sub[key];
    if (value === undefined) continue;
    if (!DATE_PATTERN.test(value)) error(`${where}: ${key} must be an ISO date`);
    else if (today && value > latestAcceptableDate(today)) error(`${where}: ${key} ${value} is in the future`);
  }
  if (sub.status === 'done' && !sub.completedOn) error(`${where}: done without completedOn`);
  if (sub.status === 'in-progress' && !sub.startedOn) error(`${where}: in-progress without startedOn`);
  if ((sub.status === 'in-progress' || sub.status === 'done') && !sub.editionConfirmedOn) {
    error(`${where}: ${sub.status} without editionConfirmedOn; the owner confirms the edition before work starts`);
  }
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

function rejectCommunityOnEnterprise(roadmap: Roadmap, error: (message: string) => void): void {
  for (const phase of roadmap.phases) {
    const community = phase.subPhases.filter((s) => s.edition === 'community');
    if (community.length > 0) {
      for (const dep of phase.dependsOn.filter((d) => isEnterpriseOnly(roadmap, d))) {
        error(`phase "${phase.id}" holds Community sub-phases but depends on "${dep}", which is Enterprise work only`);
      }
    }
    for (const sub of community) {
      for (const dep of sub.dependsOn.filter((d) => isEnterpriseOnly(roadmap, d))) {
        error(`sub-phase "${sub.id}" is Community work but depends on "${dep}", which is Enterprise work only`);
      }
    }
  }
}

type GraphNode = { id: string; edges: string[] };

function dependencyGraph(roadmap: Roadmap): Map<string, GraphNode> {
  const graph = new Map<string, GraphNode>();
  const doneNodes = (deps: string[], edition: Edition): string[] =>
    deps.flatMap((dep) => {
      const found = locate(roadmap, dep);
      if (!found) return [];
      return [found.subPhase === undefined ? `done:${dep}:${edition}` : dep];
    });
  for (const phase of roadmap.phases) {
    for (const sub of phase.subPhases) graph.set(sub.id, { id: sub.id, edges: [...doneNodes(sub.dependsOn, sub.edition), `start:${phase.id}:${sub.edition}`] });
  }
  for (const phase of roadmap.phases) {
    for (const edition of EDITIONS) {
      const start = `start:${phase.id}:${edition}`;
      const awaited = subPhasesAwaitedBy(phase, edition).filter((s) => s.status !== 'deferred').map((s) => s.id);
      graph.set(start, { id: phase.id, edges: doneNodes(phase.dependsOn, edition) });
      graph.set(`done:${phase.id}:${edition}`, { id: phase.id, edges: awaited.length > 0 ? awaited : [start] });
    }
  }
  return graph;
}

function findCycle(roadmap: Roadmap): string[] | undefined {
  const graph = dependencyGraph(roadmap);
  const state = new Map<string, 'visiting' | 'done'>();
  const stack: string[] = [];
  const visit = (node: string): string[] | undefined => {
    const current = state.get(node);
    if (current === 'done') return undefined;
    if (current === 'visiting') return [...stack.slice(stack.indexOf(node)), node];
    state.set(node, 'visiting');
    stack.push(node);
    for (const next of graph.get(node)?.edges ?? []) {
      const found = visit(next);
      if (found) return found;
    }
    stack.pop();
    state.set(node, 'done');
    return undefined;
  };
  for (const node of graph.keys()) {
    const found = visit(node);
    if (found) return plainCycle(found.map((n) => graph.get(n)!.id));
  }
  return undefined;
}

function plainCycle(ids: string[]): string[] {
  const path = ids.slice(0, -1).filter((id, index, all) => index === 0 || id !== all[index - 1]);
  if (path.length > 1 && path.at(-1) === path[0]) path.pop();
  return [...path, path[0]!];
}

export function formatIssues(issues: Issue[]): string {
  return issues.map((issue) => `${issue.level === 'error' ? 'ERROR' : 'WARN '} ${issue.message}`).join('\n');
}
