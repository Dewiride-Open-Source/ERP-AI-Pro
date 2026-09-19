import { ID_PATTERN, locate, resolveId, type Phase, type Roadmap, type SubPhase } from './model.ts';

export class RoadmapError extends Error {}

function requireSubPhase(roadmap: Roadmap, idOrLabel: string): SubPhase {
  const id = resolveId(roadmap, idOrLabel);
  const found = id === undefined ? undefined : locate(roadmap, id);
  if (!found) throw new RoadmapError(`unknown id or label "${idOrLabel}"`);
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

export function start(roadmap: Roadmap, idOrLabel: string, date: string, allowMultipleWip = false): SubPhase {
  const sub = requireSubPhase(roadmap, idOrLabel);
  if (sub.status === 'done') throw new RoadmapError(`"${sub.id}" is already done`);
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
  const id = resolveId(roadmap, idOrLabel);
  const found = id === undefined ? undefined : locate(roadmap, id);
  if (!found) throw new RoadmapError(`unknown id or label "${idOrLabel}"`);
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
  const id = resolveId(roadmap, idOrLabel);
  const found = id === undefined ? undefined : locate(roadmap, id);
  if (!found) throw new RoadmapError(`unknown id or label "${idOrLabel}"`);
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

export type NewSubPhase = { phase: string; id: string; title: string; scope: string; acceptance?: string[]; tags?: string[]; dependsOn?: string[]; after?: string };

export function addSubPhase(roadmap: Roadmap, input: NewSubPhase, date: string): SubPhase {
  requireNewId(roadmap, input.id);
  const { phase } = requirePhase(roadmap, input.phase);
  const sub: SubPhase = {
    id: input.id,
    title: input.title,
    scope: input.scope,
    status: 'planned',
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
