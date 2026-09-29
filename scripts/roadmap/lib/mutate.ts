import { EDITIONS, ID_PATTERN, isEdition, locate, resolveId, type Edition, type Located, type Phase, type Roadmap, type SubPhase } from './model.ts';

export class RoadmapError extends Error {}

function requireItem(roadmap: Roadmap, idOrLabel: string): Located {
  const id = resolveId(roadmap, idOrLabel);
  const found = id === undefined ? undefined : locate(roadmap, id);
  if (!found) throw new RoadmapError(`unknown id or label "${idOrLabel}"`);
  return found;
}

function requireSubPhase(roadmap: Roadmap, idOrLabel: string): SubPhase {
  const found = requireItem(roadmap, idOrLabel);
  if (!found.subPhase) throw new RoadmapError(`"${idOrLabel}" is a phase; status changes apply to sub-phases`);
  return found.subPhase;
}

function requirePhase(roadmap: Roadmap, idOrLabel: string): { phase: Phase; index: number } {
  const id = resolveId(roadmap, idOrLabel);
  const found = id === undefined ? undefined : locate(roadmap, id);
  if (!found || found.subPhase) throw new RoadmapError(`unknown phase "${idOrLabel}"`);
  return { phase: found.phase, index: found.phaseIndex };
}

function requireNewId(roadmap: Roadmap, id: string): void {
  if (!ID_PATTERN.test(id)) throw new RoadmapError(`"${id}" is not a slug (lowercase letters, digits, hyphens)`);
  if (locate(roadmap, id)) throw new RoadmapError(`id "${id}" already exists`);
}

function requireEdition(value: string): Edition {
  if (!isEdition(value)) throw new RoadmapError(`unknown edition "${value}"; use ${EDITIONS.join(' or ')}`);
  return value;
}

function requireConfirmedEdition(sub: SubPhase): void {
  if (sub.editionConfirmedOn) return;
  const recorded = isEdition(sub.edition) ? `has the ${sub.edition} edition recommended but not confirmed` : 'has no edition';
  const edition = isEdition(sub.edition) ? sub.edition : EDITIONS.join('|');
  throw new RoadmapError(`"${sub.id}" ${recorded}; ask the owner, then run: edition ${edition} ${sub.id} --confirm`);
}

export function start(roadmap: Roadmap, idOrLabel: string, date: string, allowMultipleWip = false): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  if (sub.status === 'done') throw new RoadmapError(`"${sub.id}" is already done`);
  requireConfirmedEdition(sub);
  const wip = roadmap.phases.flatMap((p) => p.subPhases).filter((s) => s.status === 'in-progress' && s.id !== sub.id);
  if (wip.length > 0 && !allowMultipleWip) throw new RoadmapError(`"${wip[0]!.id}" is already in progress; finish or block it first`);
  sub.status = 'in-progress';
  sub.startedOn ??= date;
  delete sub.blockedReason;
  roadmap.updatedOn = date;
  return sub;
}

export function done(roadmap: Roadmap, idOrLabel: string, date: string): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  requireConfirmedEdition(sub);
  sub.status = 'done';
  sub.startedOn ??= date;
  sub.completedOn = date;
  delete sub.blockedReason;
  roadmap.updatedOn = date;
  return sub;
}

export function block(roadmap: Roadmap, idOrLabel: string, reason: string, date: string): SubPhase {
  if (!reason.trim()) throw new RoadmapError('a reason is required to block an item');
  const sub = requireSubPhase(roadmap, idOrLabel);
  if (sub.status === 'done') throw new RoadmapError(`"${sub.id}" is already done`);
  sub.status = 'blocked';
  sub.blockedReason = reason.trim();
  roadmap.updatedOn = date;
  return sub;
}

export function defer(roadmap: Roadmap, idOrLabel: string, date: string): SubPhase | Phase {
  const found = requireItem(roadmap, idOrLabel);
  roadmap.updatedOn = date;
  if (!found.subPhase) {
    found.phase.deferred = true;
    return found.phase;
  }
  if (found.subPhase.status === 'done') throw new RoadmapError(`"${found.subPhase.id}" is already done`);
  found.subPhase.status = 'deferred';
  delete found.subPhase.blockedReason;
  return found.subPhase;
}

export function resume(roadmap: Roadmap, idOrLabel: string, date: string): SubPhase | Phase {
  const found = requireItem(roadmap, idOrLabel);
  roadmap.updatedOn = date;
  if (!found.subPhase) {
    delete found.phase.deferred;
    return found.phase;
  }
  if (found.subPhase.status === 'done') throw new RoadmapError(`"${found.subPhase.id}" is already done`);
  found.subPhase.status = 'planned';
  delete found.subPhase.blockedReason;
  return found.subPhase;
}

export function note(roadmap: Roadmap, idOrLabel: string, text: string, date: string): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  if (text.trim()) sub.notes = text.trim();
  else delete sub.notes;
  roadmap.updatedOn = date;
  return sub;
}

export type NewPhase = { id: string; title: string; goal: string; milestone: string; dependsOn?: string[]; after?: string };

export function addPhase(roadmap: Roadmap, input: NewPhase, date: string): Phase {
  requireNewId(roadmap, input.id);
  if (!roadmap.milestones.some((m) => m.id === input.milestone)) throw new RoadmapError(`unknown milestone "${input.milestone}"`);
  const phase: Phase = { id: input.id, title: input.title, goal: input.goal, milestone: input.milestone, dependsOn: input.dependsOn ?? [], subPhases: [] };
  const at = input.after === undefined ? roadmap.phases.length : requirePhase(roadmap, input.after).index + 1;
  roadmap.phases.splice(at, 0, phase);
  roadmap.updatedOn = date;
  return phase;
}

export type NewSubPhase = { phase: string; id: string; title: string; scope: string; edition: string; confirm?: boolean; acceptance?: string[]; tags?: string[]; dependsOn?: string[]; after?: string };

export function addSubPhase(roadmap: Roadmap, input: NewSubPhase, date: string): SubPhase {
  requireNewId(roadmap, input.id);
  const { phase } = requirePhase(roadmap, input.phase);
  const sub: SubPhase = {
    id: input.id,
    title: input.title,
    scope: input.scope,
    status: 'planned',
    edition: requireEdition(input.edition),
    ...(input.confirm ? { editionConfirmedOn: date } : {}),
    acceptance: input.acceptance ?? [],
    tags: input.tags ?? [],
    dependsOn: input.dependsOn ?? [],
  };
  let at = phase.subPhases.length;
  if (input.after !== undefined) {
    const afterId = resolveId(roadmap, input.after);
    const index = phase.subPhases.findIndex((s) => s.id === afterId);
    if (index < 0) throw new RoadmapError(`"${input.after}" is not a sub-phase of "${phase.id}"`);
    at = index + 1;
  }
  phase.subPhases.splice(at, 0, sub);
  roadmap.updatedOn = date;
  return sub;
}

export function move(roadmap: Roadmap, idOrLabel: string, targetPhase: string, position: number | undefined, date: string): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  const source = roadmap.phases.find((p) => p.subPhases.includes(sub))!;
  const { phase: target } = requirePhase(roadmap, targetPhase);
  source.subPhases.splice(source.subPhases.indexOf(sub), 1);
  const at = position === undefined ? target.subPhases.length : Math.max(0, Math.min(position - 1, target.subPhases.length));
  target.subPhases.splice(at, 0, sub);
  roadmap.updatedOn = date;
  return sub;
}

export function setAcceptance(roadmap: Roadmap, idOrLabel: string, acceptance: string[], date: string): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  sub.acceptance = acceptance.map((a) => a.trim()).filter(Boolean);
  roadmap.updatedOn = date;
  return sub;
}

export function setEdition(roadmap: Roadmap, edition: string, idsOrLabels: string[], confirm: boolean, date: string): SubPhase[] {
  const value = requireEdition(edition);
  if (idsOrLabels.length === 0) throw new RoadmapError('name at least one phase or sub-phase');
  const targets = new Set<SubPhase>();
  for (const idOrLabel of idsOrLabels) {
    const found = requireItem(roadmap, idOrLabel);
    if (found.subPhase) targets.add(found.subPhase);
    else if (found.phase.subPhases.length === 0) throw new RoadmapError(`phase "${found.phase.id}" has no sub-phases to take an edition`);
    else found.phase.subPhases.forEach((s) => targets.add(s));
  }
  for (const sub of targets) {
    sub.edition = value;
    if (confirm) sub.editionConfirmedOn = date;
    else delete sub.editionConfirmedOn;
  }
  roadmap.updatedOn = date;
  return [...targets];
}

export function setDependsOn(roadmap: Roadmap, idsOrLabels: string[], deps: string[], date: string): (Phase | SubPhase)[] {
  if (idsOrLabels.length === 0) throw new RoadmapError('name at least one phase or sub-phase');
  const items = idsOrLabels.map((idOrLabel) => {
    const found = requireItem(roadmap, idOrLabel);
    return found.subPhase ?? found.phase;
  });
  const resolved = new Set<string>();
  for (const dep of deps) {
    const id = resolveId(roadmap, dep);
    if (id === undefined) throw new RoadmapError(`unknown dependency "${dep}"`);
    resolved.add(id);
  }
  for (const item of items) item.dependsOn = [...resolved];
  roadmap.updatedOn = date;
  return items;
}
