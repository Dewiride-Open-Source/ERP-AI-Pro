# AI capabilities

The AI foundation is part of the Community edition: the `BuildingBlocks.Ai` pipeline, the prompt catalogue, the tool registry, the usage and cost ledger, the evaluation datasets and the safety controls. Every AI feature is an Enterprise module in the private repository `Dewiride-Enterprise/ERP-AI-Pro-Enterprise` ([ADR-0030](../adr/0030-community-and-enterprise-editions.md)): the AI capabilities of a Community module live in its AI companion, the Enterprise module `<Domain>/<Module>Ai` ([Enterprise modules](module-anatomy.md#enterprise-modules)). The rules below apply from the AI platform phase onwards, to the foundation and to every Enterprise AI module; until then no module contains AI code.

## Where AI lives

| Layer | Edition | Location | Responsibility |
|---|---|---|---|
| Pipeline | Community | `backend/BuildingBlocks/Ai` | builds the single `IChatClient` (Azure OpenAI through `Microsoft.Extensions.AI`: function invocation, OpenTelemetry with sensitive data off, logging, optional distributed cache), `PromptLoader` for embedded `*.prompt.md` files, structured-output helpers, usage metering and audit abstractions, PII redaction |
| Platform module | Community | `backend/Modules/Ai/*` | prompt catalogue, tool registry aggregated from module contracts, usage and cost ledger, evaluation datasets and safety checks |
| AI platform features | Enterprise | Enterprise modules of the `Ai` domain | assistant sessions and streaming, the document intelligence pipeline, retrieval and semantic search, agents |
| Domain AI | Enterprise | the Enterprise module `<Domain>/<Module>Ai`: `<Feature>/Ai/<Capability>/` | one folder per capability: command or query + handler + prompt file; uses `IChatClient` only |
| Web | Enterprise | `features/<domain>/<module>/<feature>/ai/<capability>/` in the features of the Enterprise module, created with the capability's first file, and the assistant's modules under `features/ai/<module>/` | review forms for AI suggestions, the assistant panel, per-field confidence display |

## Rules

- Feature code depends on `IChatClient` and `BuildingBlocks.Ai` only; provider SDKs appear in `BuildingBlocks.Ai` alone.
- An Enterprise AI module reaches the Community module it serves only through that module's `Contracts` and integration events ([dependency rules](dependency-rules.md#editions)).
- Structured extraction uses `GetResponseAsync<T>()` with a JSON-schema DTO; tools use `AIFunctionFactory.Create` over permission-checked application services.
- Every AI output is a proposal: it lands in a review state with per-field confidence and a human confirms before anything is posted. AI never computes statutory amounts; deterministic calculators do.
- Every capability has its own feature flag `Erp.Modules.<Domain>.<Module>.<Capability>` and its own audit trail entry (prompt version, model, tokens, decision).
- Data minimisation: send only the fields the capability needs; PAN, Aadhaar, bank numbers and salary components are excluded unless the owner approved that capability explicitly.
- Prompts are versioned with the code (`*.prompt.md` next to the handler) and covered by snapshot tests; an offline evaluation suite runs on a sample in CI.
- Provider, region and deployment names are configuration (`Erp:Ai:*`), never code; credentials are Key Vault references.
