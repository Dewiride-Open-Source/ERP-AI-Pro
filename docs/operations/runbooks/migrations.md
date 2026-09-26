# Runbook: database migrations in production

The migrator applies schema changes and seeds reference data before a new API version starts. Design: [ADR-0021](../../adr/0021-migrator-program-and-seeding.md); developer guide: [migrations and seeding](../../guides/migrations.md).

## What runs

The compose `migrator` service runs the API image with `dotnet /app/migrator/Dewiride.Erp.Host.Migrator.dll migrate`. It signs in to Azure as the migrator service principal (`MIGRATOR_AZURE_CLIENT_ID`, certificate secret `erp-migrator-client.pem`), reads App Configuration under the `production` label, and connects to Azure SQL with `Erp:Platform:Database:MigratorConnectionString`, the contained user that holds the schema rights. The `api` service starts only after the migrator exits with 0.

## Before a deployment

1. Review the SQL of every new migration: `node scripts/ef/ef.ts script --context <key> --from <last deployed migration> --idempotent --output review.sql`.
2. Confirm that the Azure SQL point-in-time restore window covers the deployment time.
3. Optionally list what will run: `docker compose -f compose.yaml -f compose.production.yaml run --rm migrator status` (exit code 3 means migrations are pending).

## Deploying

`docker compose -f compose.yaml -f compose.production.yaml up -d --wait` runs the migrator, waits for it, then starts or replaces the API and the web app. The migrator's log names every schema it brought up to date and every seeder it ran: `docker compose -f compose.yaml -f compose.production.yaml logs migrator`.

## When the migrator fails

The API is not replaced: `api` depends on a successful migrator run, so the previous API version keeps serving (after a first deployment, nothing starts).

| Symptom (exit code) | Cause | Action |
|---|---|---|
| 2, "The migrator configuration is invalid" | a missing or invalid setting, usually the connection string reference | fix the App Configuration value or the Key Vault reference, then run the deployment again |
| 1 with a SQL error | a migration failed; EF Core rolls back the migration in progress, earlier migrations stay applied | fix the migration in a new release; never edit an applied migration |
| 1, "cancelled" | the container was stopped mid-run; the migration in progress rolled back | run the deployment again |
| 1 with a login or permission error | the migrator user lost its schema rights or the certificate expired | restore the rights or renew the certificate ([secrets runbook](secrets.md)), then run again |

A second migrator that starts while one is running waits for EF Core's database lock and then finds nothing left to apply.

## Recovering from a bad migration

1. Stop traffic to the API if the schema change broke it.
2. Restore the database to the point in time before the deployment (Azure SQL point-in-time restore to a new database, then swap the connection string reference), or ship a new migration that reverses the change.
3. Deploy the previous image digest only together with a database that matches it; an older API against a newer schema is not supported.
