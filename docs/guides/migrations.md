# Migrations and seeding

How to change the database schema, apply it, check it and seed reference data. The decision is [ADR-0021](../adr/0021-migrator-program-and-seeding.md); the conventions every context follows are in [persistence](../architecture/persistence.md); running the migrator in production is in the [migrations runbook](../operations/runbooks/migrations.md).

## The tooling: `scripts/ef/ef.ts`

Every command runs from the repository root and wraps the pinned `dotnet ef` local tool (`cd backend && dotnet tool restore` once) with the API host as startup project. Contexts are discovered from the source tree: every class deriving from `ModuleDbContext` in a `Persistence` folder under `backend/Modules` or `backend/BuildingBlocks`, keyed by its name without `DbContext` in kebab case.

| Task | Command |
|---|---|
| List the contexts and their keys | `node scripts/ef/ef.ts list` |
| Add a migration | `node scripts/ef/ef.ts add <Name> --context <key>` (written to the context project's `Persistence/Migrations`) |
| Apply migrations to your database | `node scripts/ef/ef.ts update --all` or `--context <key>` (`--migration <Name>` to go to a specific one) |
| Check for model changes no migration captures | `node scripts/ef/ef.ts pending --all` (CI and `verify.ts` run it with `--no-build`) |
| Script SQL for review | `node scripts/ef/ef.ts script --context <key> --idempotent --output <file>` (`--from <A>`, `--to <B>`) |

`--no-build` and `--configuration <Debug|Release>` pass through. Design time is always offline: the API host reads the connection string from `dotnet user-secrets` (`Erp:Platform:Database:ConnectionString`), never from App Configuration. `--connection <string>` for `update` hands the string to `dotnet ef` through the `Erp__Platform__Database__ConnectionString` environment variable of the child process, so it never appears in a process list or in the echoed command; user secrets remain the everyday source. A relative `--output` for `script` is relative to the directory you run the command from.

## Writing a migration

1. Change the entities or their configurations.
2. `node scripts/ef/ef.ts add <Name> --context <key>`; name it for the change (`AddInvoiceDueDate`).
3. Read the generated migration and its SQL (`script --idempotent`). It must run unchanged on SQL Server 2025 and Azure SQL: no `USE`, no cross-database names, no SQL Agent, no server-level permissions.
4. A change that renames or drops a column transforms the data in the same migration; there are no compatibility columns.
5. `node scripts/ef/ef.ts update --context <key>` and `node scripts/ef/ef.ts pending --all` locally; the integration tests migrate a fresh database with every migration.

## Applying migrations

| Where | How |
|---|---|
| Your machine | `node scripts/ef/ef.ts update --all`, or the migrator: `cd backend && dotnet run --project Hosts/Migrator/Dewiride.Erp.Host.Migrator` (launch profile `migrate`; `--launch-profile status` lists what is pending) |
| Local containers | the compose `migrator` service runs before the `api` service on every `up`, signing in as the `erp_local_migrator` SQL login ([local development](local-development.md)) |
| Tests | the `SqlTestDatabase` assembly fixture migrates and seeds a fresh database per test process through the same composition |
| CI | `e2e.yml` runs the migrator; `docker-build.yml` runs the compose stack, whose migrator container migrates the CI SQL Server |
| Production | `docker compose run --rm migrator` on its own, then `up` to roll the API only after it exited 0 ([runbook](../operations/runbooks/migrations.md)) |

The migrator program (`Hosts/Migrator/Dewiride.Erp.Host.Migrator`) applies every context in catalogue order and then runs the seeders. Commands and exit codes:

| Command | Exit code |
|---|---|
| `migrate` | 0 when every schema is migrated and every seeder ran; 1 on a failure or cancellation; 2 when the configuration cannot be loaded or is invalid (for example no connection string, an unconvertible value, an unresolvable Key Vault reference) |
| `status` | 0 when nothing is pending; 3 when a schema has pending migrations |
| anything else | 64 with the usage line |

It uses `Erp:Platform:Database:MigratorConnectionString` when set, otherwise `Erp:Platform:Database:ConnectionString`. EF Core holds a database-wide lock while migrating, so two migrators never run at once, and each migration runs in its own transaction.

## Seeding reference data

A module seeds data it cannot work without (states, units, default settings) through a seeder:

```csharp
internal sealed class IndianStatesSeeder(TaxDbContext context) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // insert the states that are missing by their code; never update or delete existing rows
    }
}

// in the module's AddServices
builder.Services.AddSeeder<IndianStatesSeeder>(order: 100);
```

- A seeder runs on every migrator run and in every test database, so it must leave the same rows however often it runs: insert what is missing by its natural key, never duplicate, never overwrite what a person changed.
- Seeders run after every migration, ordered by `order` and then by type name, each in its own scope. Give a seeder that depends on another module's data a higher order.
- Every seeder ships with a test that runs it twice and compares the rows (`SeedingTests` shows the pattern).
- Statutory rates and thresholds are effective-dated parameter records with their government source, never literals in a seeder.
