# Records runbook

The records the company keeps for a year inside India, where each one lives, how long it stays, and how a time range is handed over when CERT-In or a data principal asks. The law is the CERT-In Directions of 28 April 2022, direction (iv): the logs of every ICT system kept securely for a rolling 180 days within Indian jurisdiction. The Digital Personal Data Protection Rules, 2025, rules 6(1)(e) and 8(3), keep the logs of each processing for at least a year ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). The design is [ADR-0058](../../adr/0058-records-kept-a-year-inside-india.md).

## What is kept

| Record | What it holds | Where | Kept |
|---|---|---|---|
| Change history | every business change: who, when, from which address, the values before and after | the database, `audit.ChangeRecords`, read on the page `/platform/audit` and its export | 8 financial years (`Erp:Platform:Auditing:ChangeRecordRetentionYears`) |
| Security events | sign-ins, refused and failed sign-ins, sign-outs, refused bearer tokens, exports of the change history | the database, `audit.SecurityEvents` | one year (`Erp:Platform:Auditing:SecurityEventRetention`) |
| The server's journal | every line of the migrator, api, web and proxy (`CONTAINER_TAG=erp-ai-pro-<service>`; the proxy's access log, the API's JSON lines) and of the host's own services (the backup, the journal copy, `retain`, `heal`, SSH, the updates) | the server, `/var/log/journal` | up to 400 days, at most 24 GB ([host setup runbook](host-setup.md)) |
| The journal's nightly copies | one sealed file of JSON lines per India day | `host-journal` in `sterpaiprobackup` | write-once for 365 days, deleted after 366 |
| The database's audit | sign-ins to the database and changes of its structure, users and permissions, never a statement | `sqldbauditlogs` in `sterpaiprologs` | 366 days, write-once for 365 |
| Azure's own records | the vaults' `AuditEvent`, blob reads, writes and deletes of the development, production and backup accounts, the configuration store's `Audit` and `HttpRequest`, the subscription's activity log, Microsoft Entra ID's audit and sign-in logs | the `insights-logs-*` and `insights-activity-logs` containers of `sterpaiprologs` | write-once for 365 days, deleted after 366 |

Which Azure source sends which category to which container, what each costs and how the logs account is locked are in the [Azure bootstrap](../azure-bootstrap.md), section 5g.

## Handing a time range over

Collect each record that covers the range into one folder, compress it and hand it over. Note in the folder what was asked, by whom and when.

1. **The server's journal**, for a range within its last thirteen months. On the server, as the administrator, whose group `adm` reads the whole journal, write one JSON object per line into a file only the administrator reads. (`journalctl` run as the deploy account shows only that account's own entries; it reads the containers' lines through Docker, and being root through Docker it could reach the rest.) The server keeps UTC, and `--until` includes its own time, so the India days 1 to 7 November 2026 are:

   ```bash
   umask 077
   journalctl --since "2026-10-31 18:30:00 UTC" --until "2026-11-07 18:29:59.999999 UTC" --output=json --all > journal-2026-11-01-to-07.json
   ```

   `--all` keeps every field whole: without it the JSON output writes a field of 4096 bytes or more, such as an error line with its stack trace, as `null`. `journalctl CONTAINER_TAG=erp-ai-pro-proxy --since …` narrows it to one service, and `journalctl -u erp-ai-pro-backup.service --since …` to one of the host's.
2. **The journal's nightly copies**, for a range the server no longer holds or after the server is lost. Export the backup recipient's key on the operator's computer and copy it to the server ([restore runbook](restore.md), "The server's own files", steps 1 and 2). Then, as `erp-ai-pro`, list the copies and open the days, at most 31 at a time:

   ```bash
   bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/journal.sh --list
   bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/journal.sh --fetch 2026-11-01 --through 2026-11-07 --key ~/erp-backup-recipient-key.pem
   ```

   The fetch checks each day's copy against the length and MD5 the container keeps, refuses a copy made before its day ended and any entry outside its day, and writes each day's JSON lines to `journal-<yyyy-mm-dd>.json` in a new folder only `erp-ai-pro` reads, `/srv/erp-ai-pro/journal-<first>-to-<last>` unless `--output` names another. It shreds the key once the last day is written. When it stops early, it keeps the key, names the days already written, and prints the command that shreds the key.
3. **Azure's records and the database's audit**, from the operator's computer on the home network, signed in as the operator, who reads the logs account. List a container, then download what the range needs:

   ```bash
   az storage container list --account-name sterpaiprologs --auth-mode login --query "[].name" --output tsv
   az storage blob list --account-name sterpaiprologs --container-name insights-activity-logs --auth-mode login --query "[].name" --output tsv
   mkdir -p activity-log
   az storage blob download-batch --account-name sterpaiprologs --source insights-activity-logs --destination activity-log --pattern "*/y=2026/m=10/d=31/*" --auth-mode login
   az storage blob download-batch --account-name sterpaiprologs --source insights-activity-logs --destination activity-log --pattern "*/y=2026/m=11/*" --auth-mode login
   ```

   The resource logs sit in hourly blobs `…/y=<year>/m=<month>/d=<day>/h=<hour>/m=00/PT1H.json`, one JSON record per line. Their names use UTC, and an India day starts at 18:30 UTC the day before, so a range of India days also needs the UTC day before its first: here 31 October for the days from 1 November. The download needs its folder to exist. The database's audit sits in `.xel` files in `sqldbauditlogs`, in folders by server, database and date ([SQL Database audit log format](https://learn.microsoft.com/azure/azure-sql/database/audit-log-format)), which SQL Server Management Studio opens, or the database reads with `sys.fn_get_audit_file`.
4. **The security events**, from the operator's computer on the home network, signed in with `az login` as the database's Microsoft Entra admin, through the Go sqlcmd ([database runbook](database.md)). The query reads the same India days as step 1 and names each kind as `SecurityEventKind` does:

   ```bash
   sqlcmd -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d ErpAiPro --authentication-method ActiveDirectoryAzCli -N true -b -W -s "," -o security-events-2026-11-01-to-07.csv -Q "SET NOCOUNT ON; SELECT OccurredAt, CASE Kind WHEN 1 THEN 'SignedIn' WHEN 2 THEN 'SignInFailed' WHEN 3 THEN 'SignedOut' WHEN 4 THEN 'FrontChannelSignedOut' WHEN 5 THEN 'FrontChannelSignOutRefused' WHEN 6 THEN 'BearerTokenRefused' WHEN 7 THEN 'SignInRefused' WHEN 8 THEN 'ChangeHistoryExported' END AS Kind, Detail, ActorObjectId, ClientApplicationId, ClientAddress, CorrelationId FROM audit.SecurityEvents WHERE OccurredAt >= '2026-10-31T18:30:00+00:00' AND OccurredAt < '2026-11-07T18:30:00+00:00' ORDER BY OccurredAt"
   ```
5. **The change history**, for the ERP's own records of every business change. On `/platform/audit`, set the date range to the requested days first, then export: the export holds what the page's filters select, at most 100,000 lines of CSV, and is itself recorded as a security event.

## Checks

- **After a change to the logs' settings**: `bash scripts/azure/verify.sh` reads back every setting, role and policy of the logs account and of `host-journal`. A new diagnostic setting's first records arrive within about 90 minutes.
- **After a deployment that changed the server's logging**, as the administrator: `journalctl CONTAINER_TAG=erp-ai-pro-api --since -10min` shows the API's lines, and `journalctl -u erp-ai-pro-journal.service --since yesterday` shows the night's copy.
- **A year and a day after the first records**: the lifecycle rule must have deleted what passed 366 days. The documentation of lifecycle management and of version-level immutability disagree on whether it acts on blob versions whose policy has expired. List a container's oldest blob (`az storage blob list --account-name sterpaiprologs --container-name insights-activity-logs --auth-mode login --include v --query "min_by([], &properties.creationTime).properties.creationTime"`). If it is older than 366 days and 7, the records are kept longer than the year, and the rule must be revisited.
