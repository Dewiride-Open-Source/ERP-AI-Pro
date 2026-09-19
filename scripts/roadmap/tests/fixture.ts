import type { Roadmap } from '../lib/model.ts';

export function sampleRoadmap(): Roadmap {
  return {
    $schema: './roadmap.schema.json',
    version: 1,
    updatedOn: '2026-09-19',
    milestones: [
      { id: 'm0-ready', title: 'M0 Ready', description: 'Skeleton in place' },
      { id: 'm1-live', title: 'M1 Live', description: 'First feature live' },
    ],
    phases: [
      {
        id: 'foundation',
        title: 'Foundation',
        goal: 'Create the skeleton',
        milestone: 'm0-ready',
        dependsOn: [],
        subPhases: [
          {
            id: 'foundation-governance',
            title: 'Governance',
            scope: 'Root files',
            status: 'done',
            acceptance: ['LICENSE exists'],
            tags: [],
            dependsOn: [],
            startedOn: '2026-09-18',
            completedOn: '2026-09-19',
          },
          {
            id: 'foundation-tooling',
            title: 'Tooling',
            scope: 'Roadmap CLI',
            status: 'planned',
            acceptance: [],
            tags: [],
            dependsOn: [],
          },
        ],
      },
      {
        id: 'authentication',
        title: 'Authentication',
        goal: 'Sign in with Entra ID',
        milestone: 'm1-live',
        dependsOn: ['foundation'],
        subPhases: [
          {
            id: 'authentication-oidc',
            title: 'OIDC sign-in',
            scope: 'Cookie BFF',
            status: 'planned',
            acceptance: [],
            tags: [],
            dependsOn: [],
          },
          {
            id: 'authentication-ai-helper',
            title: 'AI: helper',
            scope: 'Assistant',
            status: 'planned',
            acceptance: [],
            tags: ['ai'],
            dependsOn: ['authentication-oidc'],
          },
        ],
      },
    ],
  };
}
