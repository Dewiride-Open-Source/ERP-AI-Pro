# ADR-0054: Failed SQL spans rely on the SqlClient instrumentation to keep SQL Server's message out

Status: accepted
Date: 2026-10-28

## Context

- **What existed.** [ADR-0020](0020-observability-baseline.md) keeps SQL Server's error message out of failed SqlClient spans. SQL Server quotes the offending value in some messages, such as a duplicate key or a truncated string, so a PAN or a bank number could reach a trace. `OpenTelemetry.Instrumentation.SqlClient` copied that message into the span's status description, and `SqlErrorStatusProcessor`, registered before the instrumentation and before any exporter, cleared the description.
- **What changed upstream.** `OpenTelemetry.Instrumentation.SqlClient` 1.20.0 (2026-10-15) "fixed exception messages being exported as the span status description on .NET. The message is still recorded when `RecordException` is enabled" ([changelog](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.SqlClient-1.20.0/src/OpenTelemetry.Instrumentation.SqlClient/CHANGELOG.md)). The dependency round of 2026-10-28 adopts 1.20.1. A failed command's span now has the status `Error` without a description, and `error.type` and `db.response.status_code` still name the failure.

## Decision

- **No processor.** `SqlErrorStatusProcessor` is deleted, and `AddErpDatabaseTelemetry` registers the SqlClient instrumentation alone.
- **`RecordException` stays off.** With it on, the instrumentation records the exception as an `exception` event, whose message carries the quoted value.
- **One test holds the rule.** `DatabaseTelemetryTests.ExecuteNonQuery_DuplicateKeyTheServerQuotes_ExportsAnErrorSpanWithoutTheQuotedValue` inserts a PAN-shaped value twice. It expects an error span with no status description and `db.response.status_code` 2627, and it finds the value in neither the status, a tag nor an event.
- **Earlier decisions this changes.** ADR-0020's "SQL telemetry lives with persistence" no longer adds `SqlErrorStatusProcessor`. The rest of that decision stands: command text without parameter values, and `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS` never set.

## Consequences

- The guard is now the instrumentation's own behaviour. Turning `RecordException` on, or a later instrumentation release that records the message again, fails the test before such a release can merge.
- `docs/guides/observability.md`, section "SQL and personal data", describes the rule.
