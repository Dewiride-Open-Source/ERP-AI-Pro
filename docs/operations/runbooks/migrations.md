# Runbook: database migrations in production

The migrator applies schema changes and seeds reference data before a new API version starts. Design: [ADR-0021](../../adr/0021-migrator-program-and-seeding.md); developer guide: [migrations and seeding](../../guides/migrations.md).

## What runs

The compose `migrator` service runs the API image with `dotnet /app/migrator/Dewiride.Erp.Host.Migrator.dll migrate`. It signs in to Azure as the migrator service principal (`MIGRATOR_AZURE_CLIENT_ID`, certificate secret `erp-migrator-client.pem`), reads App Configuration under the `production` label, and connects to Azure SQL with `Erp:Platform:Database:MigratorConnectionString`, the contained user that holds the schema rights. The service never pulls (`pull_policy: never`): it uses whatever api image the host already has for `IMAGE_TAG`.

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

The migrator's output names every schema it brought up to date and every seeder it ran; it is printed by step 3 and kept in the container log.

## When the migrator fails

| Symptom (exit code) | Cause | Action |
|---|---|---|
| 2, "The migrator configuration is invalid" | a setting, credential or configuration source the migrator could not load: a missing or invalid value, an unresolvable Key Vault reference, a missing or expired certificate, an unreachable App Configuration store | fix the setting, reference or certificate named in the message, then repeat step 3 |
| 1 with a SQL error | a migration or a seeder failed; EF Core rolls back the migration in progress, earlier migrations stay applied | fix the migration or seeder in a new release; never edit an applied migration |
| 1, "cancelled" | the container was stopped mid-run; the migration in progress rolled back | repeat step 3 |
| 1 with a login or permission error | the migrator user lost its schema rights | restore the rights (first-deployment database runbook), then repeat step 3 |

A second migrator that starts while one is running waits for EF Core's database lock and then finds nothing left to apply.

## Recovering from a bad migration

1. Stop traffic to the API if the schema change broke it.
2. Restore the database to the point in time before the deployment (Azure SQL point-in-time restore to a new database, then swap the connection string reference), or ship a new migration that reverses the change.
3. Deploy the previous image digest only together with a database that matches it; an older API against a newer schema is not supported.
