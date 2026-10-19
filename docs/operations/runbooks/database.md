# Runbook: the production database

The production database is Azure SQL at the Basic tier in Central India, reached with Microsoft Entra ID only. Design: [ADR-0050](../../adr/0050-the-production-database-azure-sql-basic-with-microsoft-entra-only-users.md); the scripts are described in the [Azure bootstrap](../azure-bootstrap.md).

## What exists

- The server `sql-erp-ai-pro` (`ERP_AZURE_SQL_SERVER_NAME`) with the database `ErpAiPro` (`ERP_AZURE_SQL_DATABASE_NAME`): Basic, 5 DTU, at most 2 GB, 7 days of point-in-time restore, backups geo-redundant to South India. A `CanNotDelete` lock, `do-not-delete`, keeps the server and its database from being deleted through Azure (not from `DROP DATABASE` in SQL, see "Restoring").
- Microsoft Entra-only authentication, with the operator who ran `provision.sh` as the Microsoft Entra admin. No SQL login exists.
- One firewall rule, `home`, for the home's current public IPv4 address. No other server rule, database rule, virtual network rule or private endpoint reaches the database.
- Two contained users, named after the server identities' registrations:
  - `ERP-AI-Pro Runtime (production)`, the api's: `db_datareader`, `db_datawriter` and `erp_audit_append_only`, so it may only insert into `audit`;
  - `ERP-AI-Pro Migrator (production)`, the migrator's: `db_ddladmin`, `db_datareader`, `db_datawriter` and `CREATE ROLE`, owning the schemas its migrations create.
- The connection strings `Erp--Platform--Database--ConnectionString` and `Erp--Platform--Database--MigratorConnectionString` in `kv-erp-ai-pro-prod`, referenced under the `production` label. They hold no credential. The migrator may read these two secrets of the vault and no other.
- The client certificates of both identities in `kv-erp-ai-pro-ops`, which only the operator reads.

## Setting it up

From the repository root on the operator's computer, on the home connection without a VPN, signed in with `az login` as the operator. The [Go sqlcmd](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-download-install) must be installed (Windows: `winget install sqlcmd`, which installs into `C:\Program Files\SqlCmd`, where the scripts find it next to the ODBC sqlcmd that SQL Server installs). Run `entra.sh`, `provision.sh`, `seed.sh` and `database.sh` with `--dry-run` first; `verify.sh` and `export-client-certificate.sh` only read and take no `--dry-run`.

1. `bash scripts/azure/provision.sh` creates the server, the database, the lock and the operations vault and writes both connection strings.
2. `bash scripts/azure/entra.sh` creates the migrator's registration and its certificate in the operations vault.
3. `bash scripts/azure/provision.sh` again grants the migrator its role on the store and its roles on the two connection strings, now that both exist.
4. `bash scripts/azure/database.sh --allow-current-address` creates the firewall rule for the address Azure sees this computer connect from. The rule takes up to five minutes to apply; step 6 waits for it.
5. `bash scripts/azure/seed.sh --label production` writes the production references to both connection strings.
6. `bash scripts/azure/database.sh` creates both users and the migrator's roles. It reports that the migrations have not run yet, and gives the runtime's user no role until they have.
7. Run the first migration (next section).
8. `bash scripts/azure/database.sh` again adds the runtime's user to `erp_audit_append_only`, then to the data roles, and proves the rights of both users.
9. `bash scripts/azure/verify.sh --database`, `bash scripts/azure/verify.sh --entra` and `bash scripts/azure/verify.sh` read everything back.

The operator's computer and the server share the home's public address, so the rule admits both. A computer that reaches the internet another way (a VPN, a phone's hotspot, a remote or cloud desktop) is seen at another address, and `--allow-current-address` run there would move the rule away from the home. Once the api runs on the server, its readiness check (`/healthz/ready`, whose `database:*` entries open the database) proves the rule from the server's side.

## First migration

The migrator must create the schemas and the role `erp_audit_append_only` as its own identity, so that it owns them and can see the role ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). On an empty database, before the server runs any deployment, it runs once from the operator's computer with the environment `compose.production.yaml` gives the `migrator` service:

```bash
bash scripts/azure/export-client-certificate.sh migrator
(
  export DOTNET_ENVIRONMENT=Production ERP_ENVIRONMENT=production \
    APPCONFIG_ENDPOINT=https://appcs-erp-ai-pro.azconfig.io AZURE_TOKEN_CREDENTIALS=EnvironmentCredential \
    AZURE_TENANT_ID=<the tenant id> AZURE_CLIENT_ID=<the MIGRATOR_AZURE_CLIENT_ID line entra.sh prints> \
    AZURE_CLIENT_CERTIFICATE_PATH=scripts/azure/out/erp-migrator-client.pem
  dotnet run --project backend/Hosts/Migrator/Dewiride.Erp.Host.Migrator --no-launch-profile -- status
  dotnet run --project backend/Hosts/Migrator/Dewiride.Erp.Host.Migrator --no-launch-profile -- migrate
)
shred -u scripts/azure/out/erp-migrator-client.pem
```

The parentheses keep the variables from reaching a later command. `status` lists every migration as pending and exits with 3; `migrate` applies them, runs the seeders and the retention sweeps and exits with 0. The migrator reads its two connection strings through its own roles on the store and on those two secrets, and opens no other Key Vault reference, so the run proves both. A role granted in the last few minutes may not apply yet: the run then stops with `The migrator configuration is invalid`, a Key Vault refusal and exit code 2; wait ten minutes and run it again.

`shred -u` is the Linux and Git Bash command; on macOS use `rm -P`. On Windows `shred` cannot reach copies the file system keeps elsewhere, so keep the file inside your user profile on a volume without shadow copies, as the export script says. The certificate goes to the server later, together with the deployment's images, because from then on `erp-ai-pro-retain.timer` runs `retain` every night ([host setup runbook](host-setup.md)).

## When the home's address changes

The internet provider changes the home's public address from time to time. The api then cannot reach the database: `/healthz/ready` fails, and its log shows error 40615 naming the new address (`Client with IP address '<address>' is not allowed to access the server`). From a computer on the home connection, without a VPN:

```bash
bash scripts/azure/database.sh --allow-current-address
```

It learns this computer's address from Azure and moves the rule `home` to it, saying so; the change applies within five minutes, and the api recovers on its own. When no computer on the home connection is at hand, take the address from the api's log line and run, from anywhere:

```bash
bash scripts/azure/database.sh --allow-address <the address in the 40615 line>
```

Without the script: in the Azure portal, open the SQL server `sql-erp-ai-pro`, then **Security** → **Networking**, set both the start and the end address of the rule `home` to the new address, and save. Never use **Add your client IPv4 address**, which adds a second rule, and never change the start address alone, which turns the rule into a range. The Azure CLI does the same with `az sql server firewall-rule update --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name home --start-ip-address <address> --end-ip-address <address>`.

Never add a rule for a range or turn on **Allow Azure services and resources to access this server**: the first admits other customers of the provider, the second every Azure resource. Every mode of `database.sh`, and `verify.sh --database`, fails while another rule exists, and `--allow-current-address` narrows a `home` rule that became a range. The lock keeps another rule until it is removed by hand (`az lock delete --name do-not-delete --resource-group rg-erp-ai-pro --resource-type Microsoft.Sql/servers --resource-name sql-erp-ai-pro`, then `az sql server firewall-rule delete`, then `bash scripts/azure/provision.sh` to lock the server again).

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

In the same change, set the database's `sku` and `maxSizeBytes` in `scripts/azure/bicep/modules/sql-database.bicep`, or the next `provision.sh` moves it back, and `SQL_DATABASE_SKU`, `SQL_DATABASE_CAPACITY` and `SQL_DATABASE_MAX_SIZE_BYTES` in `scripts/azure/verify.sh`, or `verify.sh --database` fails. Check the price in the Azure pricing calculator first.

## Restoring

Point-in-time restore creates a new database from any moment of the last 7 days; restoring over the live one is not possible. A restore may need a larger tier while it runs. The procedure, and longer retention, belong to the backups runbook of `first-deployment-backups-and-restore-drill`.

The lock stops the deletion of the server, which would take every backup with it, and of the database through Azure. It does not stop `DROP DATABASE` in SQL, which here only the Microsoft Entra admin may run and no script issues. A dropped database is restored on the same server to its deletion time within the 7 days: the server's **Deleted databases** page in the Azure portal, or `az sql db restore --deleted-time`.

## Recovery

A registration deleted in Microsoft Entra ID takes its service principal with it, and the database refuses its identity from then on. Within 30 days both come back with their client id, which the database user's SID matches:

1. In the Microsoft Entra admin center, open **Entra ID** → **App registrations** → **Deleted applications**, select the registration and choose **Restore app registration**, which restores its service principal too.
2. `bash scripts/azure/entra.sh` and `bash scripts/azure/provision.sh` converge its credential and its roles; `bash scripts/azure/verify.sh --entra` and `bash scripts/azure/verify.sh` read them back.
3. `bash scripts/azure/verify.sh --database` proves that the database knows the identity again.

After the 30 days, `entra.sh` creates a new registration under the same name with a new client id, and `database.sh` refuses its user:

- **The runtime.** Its user owns nothing, so it is dropped and created anew. As the Microsoft Entra admin, through the Go sqlcmd (on macOS and Linux the `sqlcmd` on `PATH`):

  ```bash
  "/c/Program Files/SqlCmd/sqlcmd.exe" -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d ErpAiPro --authentication-method ActiveDirectoryAzCli -N true -Q "DROP USER [ERP-AI-Pro Runtime (production)];"
  ```

  Then `bash scripts/azure/database.sh` creates the user with its roles, `bash scripts/azure/provision.sh` grants the new service principal its roles, and the server receives the new `AZURE_CLIENT_ID` and certificate ([host setup runbook](host-setup.md)).
- **The migrator.** Its user owns every schema and `erp_audit_append_only`, so it cannot be dropped, and moving that ownership to a new user resets the permissions the schemas carry, the append-only denial included. Migrations and the nightly `retain` stop until a decision of its own settles the way back; `database.sh` names this section meanwhile.

## The fallback: SQL Server on the host

If Azure SQL stops fitting, the database can run as SQL Server Express in a container on the server, which stays within its 10 GB and needs no firewall rule. That changes where the data lives, how it is backed up and the rule that no SQL Server container runs outside CI, so it needs a decision of its own before any work starts.
