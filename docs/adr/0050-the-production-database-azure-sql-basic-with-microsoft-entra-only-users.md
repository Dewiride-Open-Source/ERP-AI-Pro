# ADR-0050: The production database: Azure SQL Basic with Microsoft Entra-only users

Status: accepted
Date: 2026-10-19

## Context

- **What existed.**
  - Production uses Azure SQL with `Authentication="Active Directory Default"` and contained Microsoft Entra users, the API's with data roles and the migrator's with schema rights; the tier was left to the first deployment (ADR-0008).
  - The migrator runs as an identity of its own: `compose.production.yaml` gives the `migrator` service `MIGRATOR_AZURE_CLIENT_ID` and the file secret `erp-migrator-client.pem`, and `Erp:Platform:Database:MigratorConnectionString` replaces the API's connection string for it (ADR-0021).
  - The migration `AddChangeRecords` creates the role `erp_audit_append_only` and denies it `UPDATE`, `DELETE` and `ALTER` on the schema `audit`. The API's user joins it, so the running API may only insert there. The migrator needs `CREATE ROLE` and `CONTROL` on `audit`, never `db_owner`, and a user-defined role is visible only to holders of `ALTER ANY ROLE` and to its members, so the migrator should create the role itself (ADR-0043).
  - The logs of every ICT system stay in India for 180 days (CERT-In Directions of 28.04.2022, direction (iv); ADR-0038), and the home connection's public address changes (ADR-0048).
  - `scripts/azure` held one app registration per server identity only for the API (`ERP-AI-Pro Runtime (production)`), its certificate exported by `export-runtime-certificate.sh`.
- **Owner decisions.**
  - 2026-10-18: Azure SQL at the Basic tier, chosen over SQL Server Express on the server and over the free offer. When the home's public address changes, the owner updates the firewall rule by hand; no updater.
  - 2026-10-19: the Azure changes are made behind a dry run; the Microsoft Entra admin's SQL runs through the Go sqlcmd signed in with the Azure CLI; no alert on the database's size.
- **What the services document.**
  - A Basic database has 5 DTU, at most 2 GB, 30 concurrent workers and 300 sessions, on standard storage, and a restore may need a larger tier for the time it runs (Azure SQL, "Resource limits for single databases using the DTU purchasing model").
  - The Microsoft retail price list gives Basic in Central India ₹17.6716 a day (read 2026-10-19), about ₹540 a month.
  - "In the DTU model ... there's no extra charge for PITR backup storage for default retention of seven days"; Basic keeps 1 to 7 days. New databases store backups geo-redundantly, replicated to the paired region (Azure SQL, "Automated backups"), and Central India's pair is South India.
  - Server-level IP firewall rules take up to five minutes to apply, and "Allow Azure services and resources to access this server" admits connections from any Azure resource, other customers' included (Azure SQL, "IP firewall rules"). A refused client gets error 40615, which names its address.
  - The Microsoft Entra admin is "the only user who can initially create other Microsoft Entra users"; `CREATE USER [name] FROM EXTERNAL PROVIDER` resolves the display name, the SID of a service principal's user is its application (client) id, and `WITH OBJECT_ID` is "a troubleshooting repair item" for display names that repeat (Azure SQL, "Microsoft Entra service principals with Azure SQL" and its tutorial; `CREATE USER`).
  - The ODBC sqlcmd that SQL Server installs signs in to Microsoft Entra ID interactively only through a window of its own; the Go sqlcmd's `--authentication-method ActiveDirectoryAzCli` reuses the Azure CLI's sign-in ("Authenticate with Microsoft Entra ID in sqlcmd").
  - The `administrators` block of a server sets Entra-only authentication when the server is created; later updates of that property go through `servers/azureADOnlyAuthentications`. `isIPv6Enabled` turns the server's IPv6 support on or off (template reference `Microsoft.Sql/servers` 2025-01-01).
  - A `CanNotDelete` lock lets authorized users "read and modify a resource, but they can't delete it", and child resources inherit it (Azure Resource Manager, "Lock your resources").

## Decision

- **The server and the database.** `scripts/azure/bicep/modules/sql-database.bicep`, deployed by `provision.sh` in the resource group's region, Central India:
  - the server `sql-erp-ai-pro` (`ERP_AZURE_SQL_SERVER_NAME`): Microsoft Entra-only authentication, set by `administrators` and converged by `azureADOnlyAuthentications/Default`; the operator who runs the script, a member user of the tenant, as its Microsoft Entra admin; TLS 1.2 at least; the public endpoint on and IPv6 off; a `CanNotDelete` lock `do-not-delete` on the server, which covers its database;
  - the database `ErpAiPro` (`ERP_AZURE_SQL_DATABASE_NAME`): Basic, 5 DTU, 2 GB, collation `SQL_Latin1_General_CP1_CI_AS`, geo-redundant backups (South India, still in India), 7 days of point-in-time restore;
  - `provision.sh` checks that a new server name is free before the what-if.
- **The firewall.** The template creates no rule, so a deployment never resets the address. `database.sh` keeps the server's one rule, `home`, admitting one public IPv4 address:
  - `--allow-current-address` connects, and when the firewall refuses, takes the address error 40615 names; `--allow-address <IPv4>` takes it from the operator; a private, shared or reserved address is refused;
  - there is never a rule for Azure services or a range; another rule fails `database.sh` and `verify.sh --database`, and the lock keeps it until it is removed by hand;
  - the owner moves the rule when the home's address changes (`docs/operations/runbooks/database.md`).
- **The migrator's identity.**
  - `entra.sh` converges `ERP-AI-Pro Migrator (production)` (`ERP_AZURE_APP_MIGRATOR_NAME`) like the runtime registration: single tenant, no redirect URI, no secret, a Key Vault certificate `Erp--Platform--Identity--MigratorClientCertificate` (PEM) in `kv-erp-ai-pro-prod` as its only credential.
  - `provision.sh` grants it App Configuration Data Reader on the store and Key Vault Secrets User on the production vault, and no crypto role, because the migrator never opens the key ring.
  - `--rotate-server-certificate <runtime|migrator>` and `--prune-old-credentials <...|runtime|migrator>` serve both server identities, and `export-client-certificate.sh <runtime|migrator>` exports either certificate for the server.
- **The connection strings.** `provision.sh` writes `Server=tcp:<server host>,1433;Database=ErpAiPro;Authentication="Active Directory Default";Encrypt=True;TrustServerCertificate=False;Connect Timeout=30` into both `Erp--Platform--Database--ConnectionString` and `Erp--Platform--Database--MigratorConnectionString` of the production vault when they differ, through a file, since `az` under Git Bash runs through a batch file that would mangle the double quotes. The string holds no credential: each container signs in as the identity its environment names. `infra/appconfig/key-vault-references.json` references the first under both labels and the second under `production` only.
- **The users.** `database.sh` runs `scripts/azure/sql/database-users.sql` as the Microsoft Entra admin through the Go sqlcmd (`ERP_AZURE_SQLCMD`, else the one on `PATH`, else `C:\Program Files\SqlCmd`), with `--authentication-method ActiveDirectoryAzCli`:
  - it creates each contained user `FROM EXTERNAL PROVIDER` by its registration's display name and refuses one whose type is not `E` or whose SID is not its app's client id;
  - the runtime's user gets `db_datareader` and `db_datawriter`, the migrator's `db_ddladmin`, `db_datareader`, `db_datawriter` and `CREATE ROLE`;
  - it refuses the runtime's user in `db_owner`, `db_securityadmin`, `db_ddladmin` or `db_accessadmin` and the migrator's in `db_owner`, and removes nothing itself;
  - once the migrations have created `audit` and `erp_audit_append_only`: `CONTROL` on `audit` for the migrator unless it owns the schema, the runtime's user into the role, and, under `EXECUTE AS USER`, the proof that the runtime's user may insert into `audit` and may not update, delete or alter there or alter any role, and that the migrator's holds `CONTROL` on `audit` and `CREATE ROLE`;
  - `--dry-run` prints every change and still checks what exists.
- **The first migration.** The operator runs the migrator once from their computer as the migrator's own identity, with its exported certificate, so the migrator creates every schema and the role and owns them; then `database.sh` again. Later migrations run on the server (`docs/operations/runbooks/migrations.md`).
- **The checks.** `verify.sh --database` reads back the server, the firewall, the database, its restore policy, both secrets (without showing a differing value) and, through a dry run of the users script, the users and their rights; the default run checks the migrator's role assignments and `--entra` its registration.
- **No size alert** (owner decision of 2026-10-19). The database runbook measures the size against the 2 GB cap whenever the database is worked on.

## Options rejected

- **The free serverless offer.** A serverless database consumes compute while connected, and the API's pool, health checks and nightly work keep it connected, so the monthly free amount would run out within days.
- **SQL Server Express in a container on the server.** Recommended as cheaper and closer, and declined by the owner (2026-10-18). It would also go against the rule that no SQL Server container runs outside CI, so it would need a decision of its own; the database runbook keeps it as the fallback.
- **The rule "Allow Azure services".** It admits every Azure resource of every customer.
- **An automatic updater of the firewall rule.** Declined by the owner; Azure reports the new address, and the runbook moves the rule in one command.
- **The ODBC sqlcmd's interactive sign-in, and SQL pasted into the portal's query editor.** A window to complete on every run, or steps outside any script; the owner chose the Go sqlcmd.
- **`CREATE USER ... WITH OBJECT_ID`.** Meant for repeated display names; the SID check already tells a stale user apart.
- **A lock on the database alone.** Whether a lock on a child resource stops the deletion of its parent is undocumented, while a lock on the server covers both and still lets the firewall rule be updated.
- **Pre-creating `erp_audit_append_only` as the admin.** The migrator could then miss the role in its existence check (ADR-0043).
- **An alert at 80 % of the 2 GB.** Declined by the owner (about ₹9 a month).

## Consequences

- The database costs ₹17.67 a day; backups within the 7 days add nothing.
- The data is capped at 2 GB and the work at 30 concurrent workers; a restore may need a larger tier while it runs, and moving up is one command (`docs/operations/runbooks/database.md`).
- Only the home's current public address reaches the database. When it changes, the API's readiness fails until the owner moves the rule, and error 40615 in the API's log names the new address. The firewall is the second gate: an internet provider may give one public address to many customers, so every connection also needs a token of a user the database knows.
- The operator is the only Microsoft Entra admin; a second admin, or a group as admin, needs a decision of its own.
- The migrator's certificate reaches the server only together with the deployment's images, because from then on `erp-ai-pro-retain.timer` runs `retain` every night (ADR-0049).
- `database.sh` and `verify.sh --database` need the Go sqlcmd and a computer the firewall admits; the operator's computer and the server share the home's public address.
