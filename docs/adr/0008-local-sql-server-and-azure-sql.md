# ADR-0008: Owner's SQL Server for development, Azure SQL in production, one schema per module

Status: accepted
Date: 2026-09-19

## Context

The owner runs their own SQL Server instance (managed with SQL Server Management Studio) and has ruled out a SQL Server container for local development. Production is Azure SQL. EF Core 10 provides `UseSqlServer` and `UseAzureSql`; Microsoft recommends testing against a real SQL Server rather than in-memory providers, and applying migrations as a deployment step rather than at application startup.

## Decision

- Local development and local integration tests use the owner's SQL Server instance. The connection string is a secret in the local-dev Key Vault; integration tests read `ERP_TEST_SQL_CONNECTION` and create a fresh database per test run. No compose file starts SQL Server.
- CI will use a `mcr.microsoft.com/mssql/server:2025-latest` service container from the backend-platform phase because GitHub-hosted runners have no other SQL Server.
- Production uses Azure SQL with `Authentication="Active Directory Default"` and contained Entra users: the API principal has data roles, the migrator principal has schema rights. The tier is chosen in the first-deployment phase (the free serverless offer auto-pauses and exhausts under always-on health probes).
- One database, one schema per module (`<domain>_<module>`), migrations history table inside the module schema, migrations owned by the module. Provider selection by configuration: `UseSqlServer(cs, o => o.UseCompatibilityLevel(170).EnableRetryOnFailure())` locally, `UseAzureSql(cs)` in production. Migration SQL must run on both engines.
- Migrations are never applied at startup. Locally `dotnet ef database update`; in production self-contained migration bundles run by the one-shot migrator container before the API starts.

## Consequences

- No Docker dependency for the database on developer machines; SQL Server 2025 compatibility level 170 must be available on the owner's instance for JSON-typed columns (verified in the backend platform phase).
- `Microsoft.Data.SqlClient` 7.x requires `Microsoft.Data.SqlClient.Extensions.Azure` for Entra authentication; the backend platform phase adds it when the provider is introduced.
