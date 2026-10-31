# Records runbook

The records the company keeps, where each one lives, how long it stays, and how a time range is handed over when CERT-In or a data principal asks. The CERT-In Directions of 28 April 2022, direction (iv), keep the logs of every ICT system securely for a rolling 180 days within Indian jurisdiction. The Digital Personal Data Protection Rules, 2025, rules 6(1)(e) and 8(3), keep the logs of each processing for at least a year ([ADR-0043](../../adr/0043-change-records-and-the-append-only-audit-schema.md)). The design is [ADR-0058](../../adr/0058-records-kept-a-year-inside-india.md): the ERP serves the owner's own small company with one user, so everything is kept on the server or where Azure keeps it anyway, at no cost (owner decision of 2026-10-31).

## What is kept

| Record | What it holds | Where | Kept |
|---|---|---|---|
| Change history | every business change: who, when, from which address, the values before and after | the database, `audit.ChangeRecords`, read on the page `/platform/audit` and its export | 8 financial years (`Erp:Platform:Auditing:ChangeRecordRetentionYears`) |
| Security events | sign-ins, refused and failed sign-ins, sign-outs, refused bearer tokens, exports of the change history | the database, `audit.SecurityEvents` | one year (`Erp:Platform:Auditing:SecurityEventRetention`) |
| The server's journal | every line of the migrator, api, web and proxy (`CONTAINER_TAG=erp-ai-pro-<service>`; the proxy's access log, the API's JSON lines) and of the host's own services (the backup, `retain`, `heal`, SSH, the updates) | the server, `/var/log/journal` | up to 400 days, at most 24 GB ([host setup runbook](host-setup.md)) |
| Azure's activity log | every change to the subscription's resources: who, what, when | Azure | 90 days |
| Microsoft Entra ID's sign-in and audit logs | every sign-in to the company's Microsoft accounts, the ERP's included, and every change to the directory | Microsoft Entra ID | 30 days (Microsoft Entra ID P1 or P2, which the tenant holds) |

Nothing else is kept. The vaults, the storage accounts and the configuration store record their requests in no log, and the database's own auditing is off: a logs account in Central India would have kept them a year, and the owner declined its cost and every copy of the records on 2026-10-31. Azure's activity log and Entra's logs therefore stay short of the 180 days of direction (iv); hand them over before they expire.

## Handing a time range over

Collect each record that covers the range into one folder, compress it and hand it over. Note in the folder what was asked, by whom and when.

1. **The server's journal**, for a range within its last thirteen months. On the server, as the administrator, whose group `adm` reads the whole journal, write one JSON object per line into a file only the administrator reads. (`journalctl` run as the deploy account shows only that account's own entries; it reads the containers' lines through Docker, and being root through Docker it could reach the rest.) The server keeps UTC, and `--until` includes its own time, so the India days 1 to 7 November 2026 are:

   ```bash
   umask 077
   journalctl --since "2026-10-31 18:30:00 UTC" --until "2026-11-07 18:29:59.999999 UTC" --output=json --all > journal-2026-11-01-to-07.json
   ```

   `--all` keeps every field whole: without it the JSON output writes a field of 4096 bytes or more, such as an error line with its stack trace, as `null`. `journalctl CONTAINER_TAG=erp-ai-pro-proxy --since …` narrows it to one service, and `journalctl -u erp-ai-pro-backup.service --since …` to one of the host's.
2. **Azure's activity log**, for a range within its last 90 days, from the operator's computer signed in as the operator. The times are UTC, and `--max-events` lifts the command's default of 50:

   ```bash
   az monitor activity-log list --start-time 2026-10-31T18:30:00+00:00 --end-time 2026-11-07T18:30:00+00:00 --max-events 100000 --output json > activity-log-2026-11-01-to-07.json
   ```
3. **Microsoft Entra ID's sign-in and audit logs**, for a range within their last 30 days: in the Microsoft Entra admin center, **Monitoring and health**, then **Sign-in logs** (and **Audit logs**), set the date range and **Download** the JSON.
4. **The security events**, from the operator's computer on the home network, signed in with `az login` as the database's Microsoft Entra admin, through the Go sqlcmd ([database runbook](database.md)). The query reads the same India days as step 1 and names each kind as `SecurityEventKind` does:

   ```bash
   sqlcmd -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d ErpAiPro --authentication-method ActiveDirectoryAzCli -N true -b -W -s "," -o security-events-2026-11-01-to-07.csv -Q "SET NOCOUNT ON; SELECT OccurredAt, CASE Kind WHEN 1 THEN 'SignedIn' WHEN 2 THEN 'SignInFailed' WHEN 3 THEN 'SignedOut' WHEN 4 THEN 'FrontChannelSignedOut' WHEN 5 THEN 'FrontChannelSignOutRefused' WHEN 6 THEN 'BearerTokenRefused' WHEN 7 THEN 'SignInRefused' WHEN 8 THEN 'ChangeHistoryExported' END AS Kind, Detail, ActorObjectId, ClientApplicationId, ClientAddress, CorrelationId FROM audit.SecurityEvents WHERE OccurredAt >= '2026-10-31T18:30:00+00:00' AND OccurredAt < '2026-11-07T18:30:00+00:00' ORDER BY OccurredAt"
   ```
5. **The change history**, for the ERP's own records of every business change. On `/platform/audit`, set the date range to the requested days first, then export: the export holds what the page's filters select, at most 100,000 lines of CSV, and is itself recorded as a security event.

## Checks

- **After a deployment that changed the server's logging**, as the administrator: `journalctl CONTAINER_TAG=erp-ai-pro-api --since -10min` shows the API's lines, and `journalctl --disk-usage` the journal's size.
- **After a bootstrap**: its summary proves the journal's limits in effect, and its runner proves them on fresh Ubuntu 22.04, 24.04 and 26.04 ([host setup runbook](host-setup.md)).
