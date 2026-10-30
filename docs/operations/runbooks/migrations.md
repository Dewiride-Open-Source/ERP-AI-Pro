# Runbook: database migrations in production

The migrator applies schema changes and seeds reference data before a new API version starts. Design: [ADR-0021](../../adr/0021-migrator-program-and-seeding.md); developer guide: [migrations and seeding](../../guides/migrations.md).

## What runs

The compose `migrator` service runs the API image with `dotnet /app/migrator/Dewiride.Erp.Host.Migrator.dll migrate`, which applies every pending migration, runs the seeders and then deletes what is past its retention: security events older than `Erp:Platform:Auditing:SecurityEventRetention` and change records older than `Erp:Platform:Auditing:ChangeRecordRetentionYears` financial years ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). It signs in to Azure as the migrator service principal (`MIGRATOR_AZURE_CLIENT_ID`, certificate secret `erp-migrator-client.pem`), reads App Configuration under the `production` label, and connects to Azure SQL with `Erp:Platform:Database:MigratorConnectionString`, the contained user that holds the schema rights, the database permission `CREATE ROLE` and `CONTROL` on the schema `audit`, never `db_owner`; the API's own user is a member of `erp_audit_append_only` and cannot delete from `audit`, so only the migrator does. The service never pulls (`pull_policy: never`): it runs the api image of the release that `.env` names (`IMAGE_TAG` and `API_IMAGE_DIGEST`), or of the release `deploy.sh` is deploying, which `deploy.sh` pulled by digest.

An empty database is migrated once from the operator's computer instead, as the migrator's own identity, before the server runs any deployment ([database runbook](database.md), "First migration").

`docker compose up` alone is not a safe deployment: it recreates a changed `api` container, stopping and removing the running one, before it waits for the migrator (`condition: service_completed_successfully` is checked only when containers start), and without `--no-deps` it runs the migrator, its seeders and sweeps included, a second time. So `deploy.sh` runs the migrator on its own first and rolls the stack with `--no-deps` only after it succeeded.

## Before a deployment

1. Review the SQL of every new migration: `node scripts/ef/ef.ts script --context <key> --from <last deployed migration> --idempotent --output review.sql`.
2. Confirm that the Azure SQL point-in-time restore window covers the deployment time.

## Deploying

`bash scripts/deploy/deploy.sh <tag>`, run as the deploy account `erp-ai-pro`, deploys a release ([deploy runbook](deploy.md)). Around the database it runs the release's own migrator twice, from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose` with `-f compose.yaml -f compose.production.yaml`, before any container changes:

1. `run --rm -T migrator status`. Exit 4, a database that holds migrations this release does not know, stops the deployment; exit 3 means the release's migrations will be applied; any other code but 0 stops it.
2. `run --rm -T migrator migrate`. A failure stops the deployment: nothing has been replaced, so the previous API keeps serving.

Only then does it roll the api, web and proxy containers with `up --detach --wait --no-deps`, which runs no migrator. The migrator's output names every schema it brought up to date, every seeder it ran and what the retention sweeps deleted; `deploy.sh` prints it, and the container log keeps it.

## Retention between deployments

`migrate` deletes what is past its retention only when a deployment runs it, so between deployments a security event can outlive its year. The command `retain` runs the retention sweeps alone, applying no migration and running no seeder, so a daily run never moves the schema ahead of the running API (a daily `migrate` would, once a newer image had been pulled and before the stack was rolled):

```bash
flock /srv/erp-ai-pro/deploy.lock docker compose -f compose.yaml -f compose.production.yaml run --rm migrator retain
```

It exits 0 when every sweep completed, 1 when a sweep failed (after running the others) or on cancellation, 2 when its configuration is invalid, and 4, deleting nothing, when the database holds migrations the installed release does not know ("When the migrator fails" below), so a daily run notices a failed sweep; `migrate` only logs it. On the host, `erp-ai-pro-retain.timer` runs it every night at 01:00 India time through `erp-ai-pro-retain.service`, under the deployment lock as the command above takes it. The service keeps its output and exit code in the journal, is skipped until `.env` and the migrator's certificate are in place, and fails until the first deployment has written the release into `.env` ([host setup runbook](host-setup.md), "At boot, every minute and every night"). A sweep that fails is logged at `Error` and the others still run; the next run deletes what the failed one left.

## When the migrator fails

| Symptom (exit code) | Cause | Action |
|---|---|---|
| 2, "The migrator configuration is invalid" | a setting, credential or configuration source the migrator could not load: a missing or invalid value, an unresolvable Key Vault reference, a missing or expired certificate, an unreachable App Configuration store | fix the setting, reference or certificate named in the message, then run `deploy.sh` again |
| 1 with a SQL error | a migration or a seeder failed; EF Core rolls back the migration in progress, earlier migrations stay applied | fix the migration or seeder in a new release; never edit an applied migration |
| 1, "cancelled" | the container was stopped mid-run; the migration in progress rolled back | run `deploy.sh` again |
| 1 with error 40615, `Client with IP address '<address>' is not allowed to access the server` | the home's public address changed and the firewall rule still names the old one | move the rules with `bash scripts/azure/home-address.sh` ([home address runbook](home-address.md)), then run `deploy.sh` again |
| 1 with another login or permission error | the migrator user lost its schema rights, or lacks `CREATE ROLE` or `CONTROL` on the schema `audit` (the migration `AddChangeRecords` creates the role `erp_audit_append_only` and denies it on that schema), or the database no longer knows the migrator's identity | run `bash scripts/azure/database.sh` from the operator's computer, which restores the rights or names what it refuses ([database runbook](database.md), sections "Setting it up" and "Recovery"), then run `deploy.sh` again |
| 4, "The database is ahead of this release, so the `<command>` command did not run", after one line per schema naming the migrations this release does not know | the database holds migrations of a later release: an older release was started against it, or a rollback was attempted across a migration; nothing ran, and the nightly `retain` exits 4 the same way while that release is installed | deploy the release that applied those migrations, or a later one ([deploy runbook](deploy.md)); never edit the history tables |
| 0 with an `Error` log of a retention sweep | a deletion past retention failed; the migrations and seeders are done | continue the deployment; fix the cause named in the log, and the next `migrate` or `retain` deletes what is due |
| 1 from `retain`, with an `Error` log of a retention sweep | a deletion past retention failed; the other sweeps ran | fix the cause named in the log; the next `retain` deletes what is due |

A second migrator that starts while one is running waits for EF Core's database lock and then finds nothing left to apply.

## Recovering from a bad migration

1. Stop traffic to the API if the schema change broke it.
2. Restore the database to the point in time before the deployment (Azure SQL point-in-time restore to a new database, which then takes the live one's place: `provision.sh` writes both connection strings from `ERP_AZURE_SQL_DATABASE_NAME`, `first-deployment-backups-and-restore-drill`), or ship a new migration that reverses the change.
3. Deploy the previous image digest only together with a database that matches it; an older API against a newer schema is not supported.
