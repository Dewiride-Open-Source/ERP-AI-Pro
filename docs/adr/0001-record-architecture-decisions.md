# ADR-0001: Record architecture decisions

Status: accepted
Date: 2026-09-19

## Context

ERP-AI-Pro is built over many sessions by the owner and Claude. Decisions that are only in chat history or in code comments are lost or contradict the comment policy, which forbids historical commentary in code.

## Decision

Every significant architectural or technology decision is recorded as a numbered Markdown file in `docs/adr/` using `0000-template.md`. An accepted ADR is immutable; a change is a new ADR that supersedes it. Roadmap sub-phases that involve a decision name the ADR they must produce.

## Consequences

- Rationale lives next to the code but outside it, so source files stay free of history.
- Reviewers can check a change against the ADR it claims to implement.
- The ADR index in `docs/README.md` must be updated whenever an ADR is added.
