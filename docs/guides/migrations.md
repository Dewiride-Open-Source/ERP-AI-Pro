# Migrations and seeding

How to change the database schema, apply it, check it and seed reference data. The decision is [ADR-0021](../adr/0021-migrator-program-and-seeding.md); the conventions every context follows are in [persistence](../architecture/persistence.md); running the migrator in production is in the [migrations runbook](../operations/runbooks/migrations.md).

## The tooling: `scripts/ef/ef.ts`

Every command runs from the repository root and wraps the pinned `dotnet ef` local tool (`cd backend && dotnet tool restore` once) with the API host as startup project. Contexts are discovered from the source tree: every class deriving from `ModuleDbContext` in a `Persistence` folder under `backend/Modules` or `backend/BuildingBlocks`, keyed by its name without `DbContext` in kebab case. Today `list` prints six:

| Key | Context | Project | Schema |
|---|---|---|---|
| `attachments` | `AttachmentsDbContext` | `BuildingBlocks/Attachments/Dewiride.Erp.BuildingBlocks.Attachments` | `files` (`Attachments`, `StoredContents`, `UploadReservations`, `DownloadLinks`, `DownloadRedemptions`) |
| `auditing` | `AuditingDbContext` | `BuildingBlocks/Auditing/Dewiride.Erp.BuildingBlocks.Auditing` | `audit` (`SecurityEvents`, `ChangeRecords`, and the role `erp_audit_append_only` denied `UPDATE`, `DELETE` and `ALTER` on the schema) |
| `caching` | `CachingDbContext` | `BuildingBlocks/Caching/Dewiride.Erp.BuildingBlocks.Caching` | `platform_caching` (`DistributedCacheEntries`) |
| `idempotency` | `IdempotencyDbContext` | `BuildingBlocks/Idempotency/Dewiride.Erp.BuildingBlocks.Idempotency` | `platform_idempotency` |
| `system-info` | `SystemInfoDbContext` | `Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo` | `platform_system_info` |
| `users` | `UsersDbContext` | `Modules/Identity/Users/Module/Dewiride.Erp.Modules.Identity.Users` | `identity_users` (`Users`, `Departments`, `Designations`, `Positions`, `PersonThemes`) |

The migrator applies them in catalogue order, `platform_caching`, `platform_idempotency`, `audit`, `files`, `platform_system_info`, `identity_users`, which is the order `AddErpPlatform` registers them in (building blocks first, then `Modules.All`). A key comes from the class name and a schema from the context, so the `attachments` key names the `files` schema.

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
4. A change that renames or drops a column transforms the data in the same migration; there are no compatibility columns. A data step runs as `migrationBuilder.Sql(@"EXEC(N'...')")`: the idempotent script wraps each migration in an `IF NOT EXISTS ... BEGIN ... END` batch, SQL Server compiles a whole batch at once and defers names only for missing tables, so a statement reading a column an earlier run dropped would not compile unless it runs as a batch of its own. `AddDepartmentsDesignationsAndPositions` of the users module is the example, and `DesignationConversionTests` its test (`docs/architecture/persistence.md`, section "Test databases").
5. `node scripts/ef/ef.ts update --context <key>` and `node scripts/ef/ef.ts pending --all` locally; the integration tests migrate a fresh database with every migration.

Every context maps `audit.ChangeRecords` for the change records it saves, and every context but `auditing` maps it excluded from migrations ([persistence](../architecture/persistence.md), section "Change records"). The first migration a module adds after that mapping carries it in its model snapshot and produces no operation for it; only the `auditing` context's migrations create or change the table. A migration that changes `ChangeRecords` (`AddChangeRecords` is the first) writes its rights-related statements through `EXEC(N'...')`, as a data step does.

## Applying migrations

| Where | How |
|---|---|
| Your machine | `node scripts/ef/ef.ts update --all`, or the migrator: `cd backend && dotnet run --project Hosts/Migrator/Dewiride.Erp.Host.Migrator` (launch profile `migrate`; `--launch-profile status` lists what is pending) |
| Local containers | the compose `migrator` service runs before the `api` service on every `up`, signing in as the `erp_local_migrator` SQL login (`db_ddladmin`, `db_datareader`, `db_datawriter`, the database permission `CREATE ROLE` and `CONTROL` on the schema `audit`; [local development](local-development.md), section "Containers") |
| Tests | the `SqlTestDatabase` assembly fixture migrates and seeds a fresh database per test process through the same composition |
| CI | `ci-backend.yml`'s test databases and `e2e.yml`'s end-to-end API hosts are migrated and seeded by `SqlTestDatabase` through `TestDatabaseHost`; `docker-build.yml` runs the compose stack, whose migrator container migrates the CI SQL Server |
| Production | `bash scripts/deploy/deploy.sh <tag>` as the deploy account `erp-ai-pro` ([deploy runbook](../operations/runbooks/deploy.md)): it runs the release's migrator on its own, `status` and then `migrate`, and rolls the api, web and proxy with `up --detach --wait --no-deps` only after it exited 0, so the migrator never runs twice ([migrations runbook](../operations/runbooks/migrations.md)); the empty database's first migration runs once from the operator's computer as the migrator's identity ([database runbook](../operations/runbooks/database.md), section "First migration") |

The migrator program (`Hosts/Migrator/Dewiride.Erp.Host.Migrator`) applies every context in catalogue order, then runs the seeders and then the retention sweeps. Before any command does anything, it compares each catalogue context's applied migrations with the ones the release knows (`DatabaseMigrator.GetStatusAsync`, `MigrationStatus`, matching ids without regard to case as EF Core does). When a schema holds a migration the release does not know, the database is ahead of the release: every command logs `Schema <schema> holds <n> migration(s) this release does not know: <ids>` at `Error` and exits 4 without doing anything else ([ADR-0053](../adr/0053-the-release-public-images-a-dependabot-gate-attestations-verified-on-the-server-and-the-deployment-scripts.md)). A schema only a later release knows is not compared, because the older release never touches it. Commands and exit codes:

| Command | Runs | Exit code |
|---|---|---|
| `migrate` | the migrations, the seeders, then the retention sweeps | 0 when every schema is migrated and every seeder ran, even when a retention sweep failed (logged at `Error`); 1 on a failure or cancellation; 2 when the configuration cannot be loaded or is invalid (for example no connection string, an unconvertible value, an unresolvable Key Vault reference); 4 when the database is ahead, applying nothing and running no seeder or sweep |
| `retain` | the retention sweeps alone: no migration, no seeder, so it can run every day without moving the schema ahead of the running API | 0 when every sweep completed; 1 when one failed (after the others ran) or on cancellation; 2 as for `migrate`; 4 when the database is ahead, deleting nothing |
| `status` | nothing; lists the pending migrations of each schema | 0 when nothing is pending; 3 when a schema has pending migrations; 4 when the database is ahead, which comes before 3 |
| anything else | nothing | 64 with the usage line, which lists every exit code |

It uses `Erp:Platform:Database:MigratorConnectionString` when set, otherwise `Erp:Platform:Database:ConnectionString`. EF Core holds a database-wide lock while migrating, so two migrators never run at once, and each migration runs in its own transaction.

## Retention

A retention sweep (`IRetentionSweep`, `BuildingBlocks.Persistence/Retention`, registered with `services.AddRetentionSweep<TSweep>()`) deletes what is past a retention period. `RetentionRunner` runs every sweep in its own scope; a sweep that fails is logged at `Error` with its exception and the others still run, because a deletion that can wait never stops a deployment. Only the migrator runs them, under its own principal, because the API's database user may not delete from `audit`; the test databases run none. The one sweep today is `AuditRetentionSweep` of `BuildingBlocks.Auditing` ([ADR-0043](../adr/0043-change-records-and-the-append-only-audit-schema.md)):

| Table | Deleted | Setting |
|---|---|---|
| `audit.SecurityEvents` | rows older than the retention | `Erp:Platform:Auditing:SecurityEventRetention`, one year |
| `audit.ChangeRecords` | rows made before 1 April (IST) of the financial year that many years before the current one; with 8, the financial year 2035-36 deletes those of 2026-27 and earlier | `Erp:Platform:Auditing:ChangeRecordRetentionYears`, 8 from the store; absent, none is deleted |

Each table is deleted in `DELETE TOP` statements of 1,000 rows (`AuditRetentionSweep.BatchSize`) until one deletes fewer, because one statement holding 5,000 locks on an index escalates to a table lock that would hold back the API's inserts. Rows past their retention therefore leave when the migrator runs, at each deployment and on each `retain` ([runbook](../operations/runbooks/migrations.md), section "Retention between deployments"). A migrator without the store (the local container stack) reads no `ChangeRecordRetentionYears` and keeps every change record.

## The migrator's rights

The migrator's user holds the schema rights (`db_ddladmin` and the data roles, locally as the `erp_local_migrator` login and in production as the migrator service principal's contained user, which also owns the schemas its first migration created) and, for the migration `AddChangeRecords`, the database permission `CREATE ROLE` and `CONTROL` on the schema `audit`, because it creates the role `erp_audit_append_only` and denies it `UPDATE`, `DELETE` and `ALTER` on that schema; the owner of the schema holds `CONTROL` on it without a grant. It never holds `db_owner`. The API's user is a member of the role and so may only insert into `audit`; it is never a member of `db_owner`, `db_securityadmin` or `db_ddladmin`, never holds `ALTER ANY ROLE` and never owns `audit`, because each of those would let it undo the denial or escape it. Developer machines and tests run as `sysadmin`, which no denial binds.

The migrator creates the `files` schema but needs no `Erp:Platform:Attachments` setting: `AddErpAttachments` binds the attachment options without `ValidateOnStart()`, and only the API validates them, when its `AttachmentStorageInitializer` starts ([ADR-0022](../adr/0022-attachments-in-azure-blob-storage.md)). The compose `migrator` service therefore receives neither the storage emulator host nor the encryption key file, and `MigratorApplicationTests.RunAsync_MigrateWithoutAnyAttachmentSetting_SucceedsAndCreatesTheFilesTables` keeps it that way.

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
