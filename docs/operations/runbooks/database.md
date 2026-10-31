# Runbook: the production database

The production database is Azure SQL at the Basic tier in Central India, reached with Microsoft Entra ID only. Design: [ADR-0050](../../adr/0050-the-production-database-azure-sql-basic-with-microsoft-entra-only-users.md); the scripts are described in the [Azure bootstrap](../azure-bootstrap.md).

## What exists

- The server `sql-erp-ai-pro` (`ERP_AZURE_SQL_SERVER_NAME`) with the database `ErpAiPro` (`ERP_AZURE_SQL_DATABASE_NAME`): Basic, 5 DTU, at most 2 GB, 7 days of point-in-time restore, long-term copies (each weekly full backup kept 5 weeks, the first full backup of each month 12 months), backups geo-redundant to South India. A `CanNotDelete` lock, `do-not-delete`, keeps the server and its database from being deleted through Azure (not from `DROP DATABASE` in SQL, see "Restoring").
- Microsoft Entra-only authentication, with the operator who ran `provision.sh` as the Microsoft Entra admin. No SQL login exists.
- One firewall rule, `home`, for the home's current public IPv4 address, which `home-address.sh` keeps together with the rules of the production and backup storage accounts ([home address runbook](home-address.md)). No other server rule, database rule, virtual network rule or private endpoint reaches the database.
- Two contained users, named after the server identities' registrations:
  - `ERP-AI-Pro Runtime (production)`, the api's: `db_datareader`, `db_datawriter` and `erp_audit_append_only`, so it may only insert into `audit`;
  - `ERP-AI-Pro Migrator (production)`, the migrator's: `db_ddladmin`, `db_datareader`, `db_datawriter` and `CREATE ROLE`, owning the schemas its migrations create.
- The connection strings `Erp--Platform--Database--ConnectionString` and `Erp--Platform--Database--MigratorConnectionString` in `kv-erp-ai-pro-prod`, referenced under the `production` label. They hold no credential. The migrator may read these two secrets of the vault and no other.
- The client certificates of both identities in `kv-erp-ai-pro-ops`, which only the operator reads.

## Setting it up

From the repository root on the operator's computer, on the home connection without a VPN, signed in with `az login` as the operator. The [Go sqlcmd](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-download-install) must be installed (Windows: `winget install sqlcmd`, which installs into `C:\Program Files\SqlCmd`, where the scripts find it next to the ODBC sqlcmd that SQL Server installs). Run `entra.sh`, `provision.sh`, `seed.sh`, `home-address.sh` and `database.sh` with `--dry-run` first; `verify.sh` and `export-client-certificate.sh` only read and take no `--dry-run`.

1. `bash scripts/azure/provision.sh` creates the server, the database, the lock and the operations vault and writes both connection strings.
2. `bash scripts/azure/entra.sh` creates the migrator's registration and its certificate in the operations vault.
3. `bash scripts/azure/provision.sh` again grants the migrator its role on the store and its roles on the two connection strings, now that both exist.
4. `bash scripts/azure/home-address.sh` creates the firewall rule for the address Azure sees this computer connect from and points the rules of the production and backup storage accounts at the same address ([home address runbook](home-address.md)). The database's rule takes up to five minutes to apply; step 7 waits for it.
5. Create `Erp--Platform--Attachments--EncryptionKey` in `kv-erp-ai-pro-prod` once, with the block of the [secrets runbook](secrets.md) section 5f (`vault=kv-erp-ai-pro-prod`, `prefix=prod`, `user_secrets=false`), never from the development value: the next step stops while it is missing.
6. `bash scripts/azure/seed.sh --label production` writes the production Key Vault references: the sign-in certificate, both connection strings and the attachments key.
7. `bash scripts/azure/database.sh` creates both users and the migrator's roles. It reports that the migrations have not run yet, and gives the runtime's user no role until they have.
8. Run the first migration (next section).
9. `bash scripts/azure/database.sh` again adds the runtime's user to `erp_audit_append_only`, then to the data roles, and proves the rights of both users.
10. `bash scripts/azure/verify.sh --database`, `bash scripts/azure/verify.sh --entra` and `bash scripts/azure/verify.sh` read everything back.

The operator's computer and the server share the home's public address, so the rule admits both. A computer that reaches the internet another way (a VPN, a phone's hotspot, a remote or cloud desktop) is seen at another address, and `home-address.sh` run there would move the three rules away from the home. Once the api runs on the server, its readiness check (`/healthz/ready`, whose `database:*` entries open the database) proves the rule from the server's side.

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

The internet provider changes the home's public address from time to time. The api then cannot reach the database: `/healthz/ready` fails, and its log shows error 40615 naming the new address (`Client with IP address '<address>' is not allowed to access the server`). The production and backup storage accounts refuse the same address, so one command moves the three rules: `bash scripts/azure/home-address.sh`, from a computer on the home network without a VPN, as the [home address runbook](home-address.md) describes, together with the way to do it without the script and the rules that must never be added.

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

Azure backs the database up by itself: a full backup every week, a differential every day and the transaction log about every 10 minutes, stored geo-redundantly in Central India and South India. Two policies of `sql-database.bicep` decide how far back a restore reaches, and `bash scripts/azure/verify.sh --database` reads both back:

- **Point-in-time restore**, 7 days, Basic's maximum and included in its price: any moment between the earliest restore point and the newest log backup, which trails the present by minutes.
- **Long-term copies** (owner decision of 2026-10-30): each weekly full backup kept 5 weeks and the first full backup of each month 12 months, with no yearly copy. Azure chooses when they are taken, and the first can take up to 7 days to appear after the policy is set. They outlive the deletion of the database and of the server, and they are billed by the storage they use (the "SQL Database - LTR Backup Storage" meter, ₹5.4714 per GB-month in Central India on 2026-10-30).

Every restore creates a new database on the server; restoring over the live one is not possible, and a restore may need a larger tier while it runs. Putting a restored database in place of `ErpAiPro` (renaming the two), the rehearsal and its record are in the [restore runbook](restore.md). A restored database starts with Azure's default backup settings and no tags: once it carries the name `ErpAiPro`, `bash scripts/azure/provision.sh` gives it this template's tier, tags and both retention policies again, and `verify.sh --database` proves them.

List the long-term copies, newest last, from any computer signed in as the operator (they are management data, which the firewall does not guard):

```bash
az sql db ltr-backup list --location centralindia --server sql-erp-ai-pro --database ErpAiPro --query '[].{name: name, taken: backupTime, expires: backupExpirationTime}' --output table
```

Restore one to a new database on the same server, with the live database's settings, naming the copy by its resource id (`--query '[].id'` on the list):

```bash
az sql db ltr-backup restore --backup-id "<the copy's id>" --dest-database ErpAiPro_ltr_<yyyymmdd> --dest-server sql-erp-ai-pro --dest-resource-group rg-erp-ai-pro --service-objective Basic --backup-storage-redundancy Geo --zone-redundant false
```

The lock stops the deletion of the server, which would take every point-in-time backup with it, and of the database through Azure. It does not stop `DROP DATABASE` in SQL, which here only the Microsoft Entra admin may run and no script issues. A dropped database is restored on the same server to its deletion time within the 7 days: the server's **Deleted databases** page in the Azure portal, or `az sql db restore --deleted-time`. Its long-term copies, and those of a deleted server, stay restorable until each expires: `az sql db ltr-backup list --location centralindia --database-state Deleted` lists them, and restoring one to another server works as above.

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
