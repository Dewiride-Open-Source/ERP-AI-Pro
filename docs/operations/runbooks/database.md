# Runbook: the production database

The production database is Azure SQL at the Basic tier in Central India, reached with Microsoft Entra ID only. Design: [ADR-0050](../../adr/0050-the-production-database-azure-sql-basic-with-microsoft-entra-only-users.md); the scripts are described in the [Azure bootstrap](../azure-bootstrap.md).

## What exists

- The server `sql-erp-ai-pro` (`ERP_AZURE_SQL_SERVER_NAME`) with the database `ErpAiPro` (`ERP_AZURE_SQL_DATABASE_NAME`): Basic, 5 DTU, at most 2 GB, 7 days of point-in-time restore, backups geo-redundant to South India. A `CanNotDelete` lock, `do-not-delete`, keeps the server and its database from being deleted.
- Microsoft Entra-only authentication, with the operator who ran `provision.sh` as the Microsoft Entra admin. No SQL login exists.
- One firewall rule, `home`, for the home's current public IPv4 address. Nothing else reaches the server.
- Two contained users, named after the server identities' registrations:
  - `ERP-AI-Pro Runtime (production)`, the api's: `db_datareader`, `db_datawriter` and `erp_audit_append_only`, so it may only insert into `audit`;
  - `ERP-AI-Pro Migrator (production)`, the migrator's: `db_ddladmin`, `db_datareader`, `db_datawriter` and `CREATE ROLE`, owning the schemas its migrations create.
- The connection strings `Erp--Platform--Database--ConnectionString` and `Erp--Platform--Database--MigratorConnectionString` in `kv-erp-ai-pro-prod`, referenced under the `production` label. They hold no credential.

## Setting it up

From the repository root on the operator's computer, at home, signed in with `az login` as the operator. The [Go sqlcmd](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-download-install) must be installed (Windows: `winget install sqlcmd`, which installs into `C:\Program Files\SqlCmd`, where the scripts find it next to the ODBC sqlcmd that SQL Server installs). Run each command with `--dry-run` first.

1. `bash scripts/azure/entra.sh` creates the migrator's registration and its certificate.
2. `bash scripts/azure/provision.sh` creates the server, the database and the lock, grants the migrator its roles and writes both connection strings.
3. `bash scripts/azure/database.sh --allow-current-address` creates the firewall rule for the address Azure sees this computer connect from. The rule takes up to five minutes to apply; the next step waits for it.
4. `bash scripts/azure/seed.sh --label production` writes the production references to both connection strings.
5. `bash scripts/azure/database.sh` creates both users and their roles. It reports that the migrations have not run yet.
6. Run the first migration (next section).
7. `bash scripts/azure/database.sh` again adds the runtime's user to `erp_audit_append_only` and proves the rights of both users.
8. `bash scripts/azure/verify.sh --database`, `bash scripts/azure/verify.sh --entra` and `bash scripts/azure/verify.sh` read everything back.

## First migration

The migrator must create the schemas and the role `erp_audit_append_only` as its own identity, so that it owns them and can see the role ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). On an empty database, before the server runs any deployment, it runs once from the operator's computer with the environment `compose.production.yaml` gives the `migrator` service:

```bash
bash scripts/azure/export-client-certificate.sh migrator
(
  export DOTNET_ENVIRONMENT=Production ERP_ENVIRONMENT=production \
    APPCONFIG_ENDPOINT=https://appcs-erp-ai-pro.azconfig.io AZURE_TOKEN_CREDENTIALS=EnvironmentCredential \
    AZURE_TENANT_ID=<the tenant id> AZURE_CLIENT_ID=<MIGRATOR_CLIENT_ID, printed by entra.sh> \
    AZURE_CLIENT_CERTIFICATE_PATH=scripts/azure/out/erp-migrator-client.pem
  dotnet run --project backend/Hosts/Migrator/Dewiride.Erp.Host.Migrator --no-launch-profile -- status
  dotnet run --project backend/Hosts/Migrator/Dewiride.Erp.Host.Migrator --no-launch-profile -- migrate
)
shred -u scripts/azure/out/erp-migrator-client.pem
```

The parentheses keep the variables from reaching a later command. `status` lists every migration as pending and exits with 3; `migrate` applies them, runs the seeders and the retention sweeps and exits with 0. The migrator reads its connection string through its own roles on the store and the vault, so the run also proves them. The certificate goes to the server later, together with the deployment's images, because from then on `erp-ai-pro-retain.timer` runs `retain` every night ([host setup runbook](host-setup.md)).

## When the home's address changes

The internet provider changes the home's public address from time to time. The api then cannot reach the database: `/healthz/ready` fails, and its log shows error 40615 naming the new address (`Client with IP address '<address>' is not allowed to access the server`). From the operator's computer at home:

```bash
bash scripts/azure/database.sh --allow-current-address
```

It reads the refused address from Azure and moves the rule `home` to it; the change applies within five minutes, and the api recovers on its own. Without the script: in the Azure portal, open the SQL server `sql-erp-ai-pro`, then **Security** → **Networking**, change the rule `home` to the address shown under **Add your client IPv4 address**, and save. The Azure CLI does the same with `az sql server firewall-rule update --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name home --start-ip-address <address> --end-ip-address <address>`.

Never add a rule for a range or turn on **Allow Azure services and resources to access this server**: the first admits other customers of the provider, the second every Azure resource. `database.sh` and `verify.sh --database` fail while another rule exists; the lock keeps it until it is removed by hand (`az lock delete --name do-not-delete --resource-group rg-erp-ai-pro --resource-type Microsoft.Sql/servers --resource-name sql-erp-ai-pro`, then `az sql server firewall-rule delete`, then `bash scripts/azure/provision.sh` to lock the server again).

## Size

Basic holds at most 2 GB, and writes fail once the data reaches it. Read the size whenever the database is worked on:

```bash
az sql db list-usages --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name ErpAiPro --output table
```

The row `database_size` shows the bytes in use against the limit of 2,147,483,648; the freshly migrated database used about 21 MB. Past about 1.5 GB, plan the move to a larger tier.

## Moving to a larger tier

Standard S0 (10 DTU, 250 GB) is the next step and keeps the same features; the move takes minutes and drops open connections once:

```bash
az sql db update --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name ErpAiPro --service-objective S0 --max-size 250GB
```

Then change the database's `sku` and `maxSizeBytes` in `scripts/azure/bicep/modules/sql-database.bicep` in the same change, or the next `provision.sh` moves it back. Check the price in the Azure pricing calculator first.

## Restoring

Point-in-time restore creates a new database from any moment of the last 7 days; restoring over the live one is not possible. A restore may need a larger tier while it runs. The procedure, and longer retention, belong to the backups runbook of the first deployment.

## The fallback: SQL Server on the host

If Azure SQL stops fitting, the database can run as SQL Server Express in a container on the server, which stays within its 10 GB and needs no firewall rule. That changes where the data lives, how it is backed up and the rule that no SQL Server container runs outside CI, so it needs a decision of its own before any work starts.
