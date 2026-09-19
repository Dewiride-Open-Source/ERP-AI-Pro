import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const STATUSES = ['planned', 'in-progress', 'blocked', 'deferred', 'done'] as const;
export type Status = (typeof STATUSES)[number];

export type SubPhase = {
  id: string;
  title: string;
  scope: string;
  status: Status;
  acceptance: string[];
  tags: string[];
  dependsOn: string[];
  startedOn?: string;
  completedOn?: string;
  blockedReason?: string;
  notes?: string;
};

export type Phase = {
  id: string;
  title: string;
  goal: string;
  milestone: string;
  dependsOn: string[];
  deferred?: boolean;
  subPhases: SubPhase[];
};

export type Milestone = {
  id: string;
  title: string;
  description: string;
};

export type Roadmap = {
  $schema: string;
  version: number;
  updatedOn: string;
  milestones: Milestone[];
  phases: Phase[];
};

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

export const paths = {
  repoRoot,
  roadmapJson: resolve(repoRoot, 'docs', 'roadmap', 'roadmap.json'),
  roadmapMarkdown: resolve(repoRoot, 'docs', 'roadmap', 'ROADMAP.md'),
  schemaJson: resolve(repoRoot, 'docs', 'roadmap', 'roadmap.schema.json'),
};

export const ID_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
export const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export function loadRoadmap(file: string = paths.roadmapJson): Roadmap {
  return JSON.parse(readFileSync(file, 'utf8')) as Roadmap;
}

export function saveRoadmap(roadmap: Roadmap, file: string = paths.roadmapJson): void {
  writeFileSync(file, serialize(roadmap), 'utf8');
}

export function serialize(roadmap: Roadmap): string {
  const canonical = {
    $schema: roadmap.$schema,
    version: roadmap.version,
    updatedOn: roadmap.updatedOn,
    milestones: roadmap.milestones.map((m) => ({ id: m.id, title: m.title, description: m.description })),
    phases: roadmap.phases.map(canonicalPhase),
  };
  return `${JSON.stringify(canonical, null, 2)}\n`;
}

function canonicalPhase(phase: Phase) {
  return {
    id: phase.id,
    title: phase.title,
    goal: phase.goal,
    milestone: phase.milestone,
    dependsOn: phase.dependsOn,
    ...(phase.deferred ? { deferred: true } : {}),
    subPhases: phase.subPhases.map(canonicalSubPhase),
  };
}

function canonicalSubPhase(sub: SubPhase) {
  return {
    id: sub.id,
    title: sub.title,
    scope: sub.scope,
    status: sub.status,
    acceptance: sub.acceptance,
    tags: sub.tags,
    dependsOn: sub.dependsOn,
    ...(sub.startedOn ? { startedOn: sub.startedOn } : {}),
    ...(sub.completedOn ? { completedOn: sub.completedOn } : {}),
    ...(sub.blockedReason ? { blockedReason: sub.blockedReason } : {}),
    ...(sub.notes ? { notes: sub.notes } : {}),
  };
}

export function phaseLabel(index: number): string {
  return `P${String(index + 1).padStart(2, '0')}`;
}

export function subPhaseLabel(phaseIndex: number, subIndex: number): string {
  return `${phaseLabel(phaseIndex)}.${subIndex + 1}`;
}

export function phaseStatus(phase: Phase): Status {
  if (phase.deferred) return 'deferred';
  const statuses = phase.subPhases.map((s) => s.status);
  if (statuses.length === 0) return 'planned';
  const active = statuses.filter((s) => s !== 'deferred');
  if (active.length === 0) return 'deferred';
  if (active.every((s) => s === 'done')) return 'done';
  if (statuses.some((s) => s === 'in-progress' || s === 'done')) return 'in-progress';
  if (statuses.some((s) => s === 'blocked')) return 'blocked';
  return 'planned';
}

export type Located = {
  phase: Phase;
  phaseIndex: number;
  subPhase?: SubPhase;
  subIndex?: number;
};

export function locate(roadmap: Roadmap, id: string): Located | undefined {
  for (const [phaseIndex, phase] of roadmap.phases.entries()) {
    if (phase.id === id) return { phase, phaseIndex };
    for (const [subIndex, subPhase] of phase.subPhases.entries()) {
      if (subPhase.id === id) return { phase, phaseIndex, subPhase, subIndex };
    }
  }
  return undefined;
}

export function labelOf(roadmap: Roadmap, id: string): string | undefined {
  const found = locate(roadmap, id);
  if (!found) return undefined;
  return found.subPhase === undefined ? phaseLabel(found.phaseIndex) : subPhaseLabel(found.phaseIndex, found.subIndex!);
}

export function resolveId(roadmap: Roadmap, idOrLabel: string): string | undefined {
  if (locate(roadmap, idOrLabel)) return idOrLabel;
  const match = /^P(\d{1,2})(?:\.(\d{1,2}))?$/i.exec(idOrLabel);
  if (!match) return undefined;
  const phase = roadmap.phases[Number(match[1]) - 1];
  if (!phase) return undefined;
  if (match[2] === undefined) return phase.id;
  return phase.subPhases[Number(match[2]) - 1]?.id;
}

export function isDone(roadmap: Roadmap, id: string): boolean {
  const found = locate(roadmap, id);
  if (!found) return false;
  return found.subPhase === undefined ? phaseStatus(found.phase) === 'done' : found.subPhase.status === 'done';
}

export function today(): string {
  const now = new Date();
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}
