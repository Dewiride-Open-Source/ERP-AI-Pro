# AI capabilities

AI is a capability of every module, not a separate product. The rules below apply from the AI platform phase onwards; until then no module contains AI code.

## Where AI lives

| Layer | Location | Responsibility |
|---|---|---|
| Pipeline | `backend/BuildingBlocks/Ai` | builds the single `IChatClient` (Azure OpenAI through `Microsoft.Extensions.AI`: function invocation, OpenTelemetry with sensitive data off, logging, optional distributed cache), `PromptLoader` for embedded `*.prompt.md` files, structured-output helpers, usage metering and audit abstractions, PII redaction |
| Platform module | `backend/Modules/Ai/*` | assistant sessions and streaming, prompt catalogue, tool registry aggregated from module contracts, usage and cost ledger, evaluation datasets |
| Domain AI | `backend/Modules/<Domain>/<Module>/<Feature>/Ai/<Capability>/` | one folder per capability: command or query + handler + prompt file; uses `IChatClient` only |
| Web | `features/<domain>/<module>/<feature>/ai/` and `features/ai/` | review forms for AI suggestions, the assistant panel, per-field confidence display |

## Rules

- Feature code depends on `IChatClient` and `BuildingBlocks.Ai` only; provider SDKs appear in `BuildingBlocks.Ai` alone.
- Structured extraction uses `GetResponseAsync<T>()` with a JSON-schema DTO; tools use `AIFunctionFactory.Create` over permission-checked application services.
- Every AI output is a proposal: it lands in a review state with per-field confidence and a human confirms before anything is posted. AI never computes statutory amounts; deterministic calculators do.
- Every capability has its own feature flag `Erp.Modules.<Domain>.<Module>.<Capability>` and its own audit trail entry (prompt version, model, tokens, decision).
- Data minimisation: send only the fields the capability needs; PAN, Aadhaar, bank numbers and salary components are excluded unless the owner approved that capability explicitly.
- Prompts are versioned with the code (`*.prompt.md` next to the handler) and covered by snapshot tests; an offline evaluation suite runs on a sample in CI.
- Provider, region and deployment names are configuration (`Erp:Ai:*`), never code; credentials are Key Vault references.
