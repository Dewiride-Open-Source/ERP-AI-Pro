# Runbook: database migrations in production

The migrator applies schema changes and seeds reference data before a new API version starts. Design: [ADR-0021](../../adr/0021-migrator-program-and-seeding.md); developer guide: [migrations and seeding](../../guides/migrations.md).

## What runs

The compose `migrator` service runs the API image with `dotnet /app/migrator/Dewiride.Erp.Host.Migrator.dll migrate`, which applies every pending migration, runs the seeders and then deletes what is past its retention: security events older than `Erp:Platform:Auditing:SecurityEventRetention` and change records older than `Erp:Platform:Auditing:ChangeRecordRetentionYears` financial years ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). It signs in to Azure as the migrator service principal (`MIGRATOR_AZURE_CLIENT_ID`, certificate secret `erp-migrator-client.pem`), reads App Configuration under the `production` label, and connects to Azure SQL with `Erp:Platform:Database:MigratorConnectionString`, the contained user that holds the schema rights, the database permission `CREATE ROLE` and `CONTROL` on the schema `audit`, never `db_owner`; the API's own user is a member of `erp_audit_append_only` and cannot delete from `audit`, so only the migrator does. The service never pulls (`pull_policy: never`): it uses whatever api image the host already has for `IMAGE_TAG`.

`docker compose up` alone is not a safe deployment: it recreates a changed `api` container, stopping and removing the running one, before it waits for the migrator (`condition: service_completed_successfully` is checked only when containers start). So the migrator always runs on its own first, and the stack is rolled only after it succeeded.

## Before a deployment

1. Review the SQL of every new migration: `node scripts/ef/ef.ts script --context <key> --from <last deployed migration> --idempotent --output review.sql`.
2. Confirm that the Azure SQL point-in-time restore window covers the deployment time.

## Deploying

With `IMAGE_TAG` in `.env` set to the new release, from `infra/compose` on the host (`-f compose.yaml -f compose.production.yaml` on every command):

1. `docker compose ... pull api web` fetches the new images; the migrator runs from the api image.
2. Optionally `docker compose ... run --rm migrator status` lists what will run (exit code 3 means migrations are pending).
3. `docker compose ... run --rm migrator` runs `migrate` once and returns its exit code. Stop here if it is not 0: nothing has been replaced, so the previous API keeps serving.
4. `docker compose ... up -d --remove-orphans --wait` rolls the api and web containers. The migrator runs again as the api's dependency, finds nothing to apply and exits 0.

The migrator's output names every schema it brought up to date, every seeder it ran and what the retention sweeps deleted; it is printed by step 3 and kept in the container log.

## Retention between deployments

`migrate` deletes what is past its retention only when a deployment runs it, so between deployments a security event can outlive its year. The command `retain` runs the retention sweeps alone, applying no migration and running no seeder, so a daily run never moves the schema ahead of the running API (a daily `migrate` would, once a newer image had been pulled and before the stack was rolled):

```bash
docker compose -f compose.yaml -f compose.production.yaml run --rm migrator retain
```

It exits 0 when every sweep completed, 1 when a sweep failed (after running the others) or on cancellation, and 2 when its configuration is invalid, so a daily run notices a failed sweep; `migrate` only logs it. `first-deployment-host-bootstrap-runbook-and-scripts` schedules it once a day; until then, run it by hand after a long gap between deployments. A sweep that fails is logged at `Error` and the others still run; the next run deletes what the failed one left.

## When the migrator fails

| Symptom (exit code) | Cause | Action |
|---|---|---|
| 2, "The migrator configuration is invalid" | a setting, credential or configuration source the migrator could not load: a missing or invalid value, an unresolvable Key Vault reference, a missing or expired certificate, an unreachable App Configuration store | fix the setting, reference or certificate named in the message, then repeat step 3 |
| 1 with a SQL error | a migration or a seeder failed; EF Core rolls back the migration in progress, earlier migrations stay applied | fix the migration or seeder in a new release; never edit an applied migration |
| 1, "cancelled" | the container was stopped mid-run; the migration in progress rolled back | repeat step 3 |
| 1 with a login or permission error | the migrator user lost its schema rights, or lacks `CREATE ROLE` or `CONTROL` on the schema `audit` (the migration `AddChangeRecords` creates the role `erp_audit_append_only` and denies it on that schema) | restore the rights (first-deployment database runbook), then repeat step 3 |
| 0 with an `Error` log of a retention sweep | a deletion past retention failed; the migrations and seeders are done | continue the deployment; fix the cause named in the log, and the next `migrate` or `retain` deletes what is due |
| 1 from `retain`, with an `Error` log of a retention sweep | a deletion past retention failed; the other sweeps ran | fix the cause named in the log; the next `retain` deletes what is due |

A second migrator that starts while one is running waits for EF Core's database lock and then finds nothing left to apply.

## Recovering from a bad migration

1. Stop traffic to the API if the schema change broke it.
2. Restore the database to the point in time before the deployment (Azure SQL point-in-time restore to a new database, then swap the connection string reference), or ship a new migration that reverses the change.
3. Deploy the previous image digest only together with a database that matches it; an older API against a newer schema is not supported.
