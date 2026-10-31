# Restore runbook

What protects each part of the production ERP, how each is brought back, and the rehearsal that proves it twice a year (owner decisions of 2026-10-30, [ADR-0056](../../adr/0056-the-servers-own-files-backed-up-nightly-sealed-to-an-operator-key-and-the-restore-drill.md)). Commands without a host run on the operator's computer, from the repository root, signed in with `az login` as the operator, at home without a VPN; commands marked "on the server" run on the production server as the account they name. Times are UTC unless they say India time (UTC+05:30).

## Objectives

| Measure | Objective | What decides it |
|---|---|---|
| Time until people work again | 4 hours | the slowest procedure below, a rebuilt server |
| Database work lost | at most 15 minutes | Azure backs the transaction log up about every 10 minutes, so a restore reaches a moment minutes before the loss |
| Attached files lost | none | each file is written once and never overwritten, and soft delete keeps a deleted one for 90 days |
| The server's own files lost | what changed since last night's archive | `erp-ai-pro-backup.timer` runs at 02:00 India time |

Each rehearsal records what it measured against these ("The rehearsal", "Record").

## What protects each part

| Part | Where it lives | What protects it | How it comes back |
|---|---|---|---|
| The database `ErpAiPro` | Azure SQL `sql-erp-ai-pro`, Central India | Azure's own backups on geo-redundant storage (copied to South India): any moment of the last 7 days, each weekly full backup for 5 weeks, the first full backup of each month for 12 months; the server's `CanNotDelete` lock | "The database to a point in time", "A dropped database", "A long-term copy" |
| Attachments | container `attachments` of `sterpaiproprod` | geo-redundant storage (South India); blob and container soft delete, 90 days; the account's lock; each blob is written once under its content's id | "A deleted attachment", "A deleted container" |
| The attachments key and the data-protection key | `kv-erp-ai-pro-prod` | Key Vault soft delete and purge protection, 90 days | `az keyvault secret recover` and `az keyvault key recover`; losing every copy of the attachments key makes every file it wrapped unreadable ([secrets runbook](secrets.md), sections 5e and 5f) |
| The key ring (`erp-ai-pro-data-protection-keys`), the proxy's authority and certificates (`erp-ai-pro-proxy-data`), `.env` and `.env.previous` | the server | the nightly archive in `host-backups` of `sterpaiprobackup`, write-once for 90 days and sealed to `Erp--Platform--Backup--EncryptionCertificate` | "The server's own files" |
| The client certificates of the runtime, the migrator and the backup | `kv-erp-ai-pro-ops` | Key Vault soft delete and purge protection | exported again ([host setup runbook](host-setup.md), "The client certificates" and "The backup's certificates") |
| The settings | App Configuration store `appcs-erp-ai-pro` (Free tier, no soft delete) | the seed files in `infra/appconfig/` and the scripts that write the rest | `provision.sh`, `seed.sh` and `entra.sh` ([Azure bootstrap](../azure-bootstrap.md), section 8) |
| The images | `ghcr.io/dewiride-open-source/erp-ai-pro/{api,web,proxy}`, public | every release's images stay published under the digests `.env` names | `deploy.sh` pulls and verifies them |
| The server itself | the Ubuntu host | `scripts/deploy/bootstrap.sh` and this repository | "A rebuilt server" |

Not covered, and accepted: an outage of the Central India region keeps the ERP down whatever the backups hold, because the App Configuration store (Free tier, no geo-replication) and the vaults (failed over to South India by Microsoft, read-only, after hours) live there; losing the Azure subscription itself loses the database, the attachments, the keys and the backups together.

## The server's own files

`scripts/deploy/restore.sh` puts an archive of `host-backups` back into the two volumes, `.env` and `.env.previous`. It runs on the server as `erp-ai-pro` and opens the archive with the private key of the backup encryption certificate, which the operator exports for the restore alone and which never stays on the server.

1. Export the key:

   ```bash
   bash scripts/azure/export-client-certificate.sh backup-recipient-key
   ```

   It writes `scripts/azure/out/erp-backup-recipient-key.pem` (git-ignored) and prints the certificate's serial number. An archive sealed before the last rotation needs the version it was sealed to (`--version <version>`, [secrets runbook](secrets.md), section 5g); `restore.sh` names the serial number it needs when the key is another version's.
2. Copy it into the deploy account's home on the server with the deploy account's own key (`scp -i ~/.ssh/erp-ai-pro-deploy scripts/azure/out/erp-backup-recipient-key.pem erp-ai-pro@<server>:`), then shred the workstation's copy: `shred -u scripts/azure/out/erp-backup-recipient-key.pem` (`rm -P` on macOS).
3. On the server, as `erp-ai-pro`, list the archives, choose the one to restore by its creation time and check it. Neither command changes anything, so both may run while the stack runs:

   ```bash
   bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/restore.sh --list
   bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/restore.sh --name <archive> --key ~/erp-backup-recipient-key.pem --dry-run
   ```

   `--list` names each archive with the time the storage service created it, newest first, and marks a name whose time lies more than an hour from its creation. After a suspected compromise of the server, choose an archive created before it, never the newest ("Choosing the archive" below). The dry run downloads, opens and checks the archive it would restore and names it, with its creation time, the manifest's release, keys and proxy root fingerprint, what each volume holds now and what a restore would write. It refuses an archive whose manifest names no release, no key and no proxy root: the backup of a server whose volumes were still empty, which restores nothing. Keep the archive's name for the next step.
4. Stop the stack as the administrator (`sudo systemctl stop erp-ai-pro.service`). Then, as `erp-ai-pro`, restore that archive by its name, with `--replace` when a volume still holds files:

   ```bash
   bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/restore.sh --name <yyyy>/<mm>/<yyyymmddThhmmssZ>.tar.p7m --key ~/erp-backup-recipient-key.pem --replace
   ```

   It takes the deployment lock, refuses while a container of the stack runs, checks every file against the manifest before it changes anything, gives each volume's top folder the owner and mode its image gives it (1654:1654 for the key ring, 10001:10001 for the proxy data, both 0700), fills each volume as that owner, writes `.env` and `.env.previous` (0600) as the archive holds them, and shreds the key file.
5. The restored `.env` names the release that ran when the archive was made, and `restore.sh` prints the release the replaced `.env` named.
   - When no deployment or rollback ran after the archive was made, the checkout is at the archive's release: start the stack with `sudo systemctl start erp-ai-pro.service`, which returns once it is healthy. The API's key ring check stops the start when the restored keys cannot be unwrapped or their folder written, so a start proves the key ring, its owner and its mode.
   - When one ran, the checkout is at the release it left running (`git -C /srv/erp-ai-pro/ERP-AI-Pro tag --points-at HEAD` names it): deploy that release ([deploy runbook](deploy.md)), which writes it into `.env`, keeps the archive's release in `.env.previous` and starts the stack. Deploying the archive's release instead stops at the migrator's exit 4 when the later release brought migrations, and otherwise undoes the later deployment or rollback.

After a dry run or a failure the key stays where it was, and `restore.sh` prints the command that removes it: `shred -u ~/erp-backup-recipient-key.pem`. A failure after the first volume names what was restored before it; run the restore again with `--replace` once its cause is fixed.

Choosing the archive:

- `--latest` takes the archive the storage service created last, not the one whose name sorts last, and `--created-before <yyyy-mm-ddThh:mm:ssZ>` the newest one it created before that moment. Whoever holds the backup service principal's certificate can add an archive under any name, though under the write-once policy never replace or delete one, so `--list` and `restore.sh` also report a name whose time lies more than an hour from its creation.
- After a suspected compromise of the server, never take `--latest`. List the archives, choose one created before the compromise could have begun (`--created-before <that moment>` or `--name`), check the creation time its dry run names and the release its manifest names against the release page, then restore it by its name. An archive is sealed to a public certificate, so its seal shows only that whoever made it could write to the container.

What each part's loss costs when no archive can be restored:

- The key ring: everyone is signed out once and nothing else is lost; the API makes a new ring when it starts.
- The proxy data: the proxy makes a new authority at its next start, and every device trusts the new root again ([host setup runbook](host-setup.md), "Trusting the proxy's root on each device").
- `.env`: its values are rebuilt from the [host setup runbook](host-setup.md) ("The `.env` values") and the release page; `.env.previous` serves `rollback.sh` alone.

## The database to a point in time

A restore creates a new database and never overwrites the live one. The restored database takes the live one's place by renaming the two, so no connection string, setting or secret changes.

1. On the server, stop the stack (`sudo systemctl stop erp-ai-pro.service`), so nothing writes while the databases change places. The nightly retention run starts at 01:00 India time; restore outside the half hour after it.
2. Restore to a new name, at a moment of the last 7 days:

   ```bash
   az sql db restore --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name ErpAiPro --dest-name ErpAiPro_restored_<yyyymmdd> --time "<yyyy-mm-ddThh:mm:ss>" --service-objective Basic --backup-storage-redundancy Geo
   ```

   The command returns once the database is online. A time later than the newest log backup is refused; the latest time it accepts is minutes before the present. A restore may need more power than Basic gives: run it at `S3` (`--service-objective S3`) and scale the restored database down to Basic afterwards (`az sql db update --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name <name> --service-objective Basic`); each hour is billed at the highest tier it ran at.
3. Swap the two as the server's Microsoft Entra admin, connected to `master` through the Go sqlcmd, which reuses the Azure CLI sign-in:

   ```bash
   sqlcmd -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d master --authentication-method ActiveDirectoryAzCli -N true -b -Q "ALTER DATABASE [ErpAiPro] MODIFY NAME = [ErpAiPro_replaced_<yyyymmdd>];"
   sqlcmd -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d master --authentication-method ActiveDirectoryAzCli -N true -b -Q "ALTER DATABASE [ErpAiPro_restored_<yyyymmdd>] MODIFY NAME = [ErpAiPro];"
   ```

   A database is renamed only while nothing else is connected to it. The restored database brings its contained users, their roles and the append-only role on `audit` with it; the server's Microsoft Entra admin, its Entra-only sign-in and its firewall rule belong to the server and stay.
4. Give it the template's settings again and prove it:

   ```bash
   bash scripts/azure/provision.sh
   bash scripts/azure/database.sh --dry-run
   bash scripts/azure/verify.sh --database
   ```

   A restored database starts with Azure's default backup settings and no tags; `provision.sh` gives it the tier, the tags and both retention policies, `database.sh --dry-run` proves the users and their rights without changing them, and `verify.sh --database` reads everything back.
5. When the restore went back past a deployment, return to the release that knows exactly the restored schema with `rollback.sh` ([migrations runbook](migrations.md), "Recovering from a bad migration"). Otherwise start the stack: `sudo systemctl start erp-ai-pro.service`.

The replaced database stays, billed as Basic, until the owner drops it, which no script does: `sqlcmd -S tcp:sql-erp-ai-pro.database.windows.net,1433 -d master --authentication-method ActiveDirectoryAzCli -N true -b -Q "DROP DATABASE [ErpAiPro_replaced_<yyyymmdd>];"`. Its backups stay for its retention, so even a dropped database can be restored.

## A dropped database

Azure takes a last log backup before a database is dropped and keeps its point-in-time backups for 7 days. Restore it under its own name, which is free once it is gone: the server's **Deleted databases** page in the Azure portal, or

```bash
az sql db list-deleted --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --query '[].{name: name, deleted: deletionDate}' --output table
az sql db restore --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name ErpAiPro --deleted-time "<the deletion time, yyyy-mm-ddThh:mm:ss>" --dest-name ErpAiPro --service-objective Basic --backup-storage-redundancy Geo
```

Then step 4 of the point-in-time restore, and start the stack. The server's lock stops its deletion through Azure, which would take every point-in-time backup with it; it does not stop `DROP DATABASE` in SQL, which only the Microsoft Entra admin may run.

## A long-term copy

Weekly copies reach 5 weeks back and monthly ones 12 months; they outlive the deletion of the database and of the server. The [database runbook](database.md), section "Restoring", lists them and restores one to a new database on the server, which then takes the live one's place as steps 1, 3, 4 and 5 of "The database to a point in time" show.

## A deleted attachment

Every file is a blob named by its stored content's id, written once and never changed; soft delete keeps a deleted blob for 90 days. Nobody but the runtime service principal holds a data role on `sterpaiproprod`, and the account answers only the home's address, so the restore runs from a computer on the home network with a role granted for its duration ([Azure bootstrap](../azure-bootstrap.md), section 8, "Production storage" and "Deleted blob"):

1. Grant yourself Storage Blob Data Contributor on the account: `MSYS_NO_PATHCONV=1 az role assignment create --assignee <your object id> --role "Storage Blob Data Contributor" --scope <account resource id>`, and allow up to 10 minutes for it to take effect. `MSYS_NO_PATHCONV=1` keeps Git Bash on Windows from rewriting the scope `/subscriptions/…` into a Windows path, which Azure refuses with `MissingSubscription`; the scripts set it for every command (`scripts/azure/lib/common.sh`), and on any other shell it changes nothing.
2. Find the blob: `az storage blob list --account-name sterpaiproprod --container-name attachments --include d --auth-mode login --query "[?deleted].{name: name, deleted: properties.deletedTime}" --output table`. A blob's name is the `Id` of its row in `files.StoredContents`, 32 hexadecimal digits without hyphens. Uploads of identical content share that row and its blob, so while the blob is deleted every attachment of that content answers as not found.
3. Restore it: `az storage blob undelete --account-name sterpaiproprod --container-name attachments --name <blob name> --auth-mode login`. The file downloads again in the ERP.
4. Remove the role with the account's lock lifted, `az lock delete --name do-not-delete --resource-group rg-erp-ai-pro --resource sterpaiproprod --resource-type Microsoft.Storage/storageAccounts` and then `MSYS_NO_PATHCONV=1 az role assignment delete --assignee <your object id> --role "Storage Blob Data Contributor" --scope <account resource id>`; then `bash scripts/azure/provision.sh`, which puts the lock back, and `bash scripts/azure/verify.sh`.

## A deleted container

Container soft delete keeps `attachments` with every blob in it for 90 days, but only while no container of the same name exists, so restore it before `provision.sh` runs, which would create a new empty one. With the temporary role of the previous section: `az storage container list --account-name sterpaiproprod --prefix attachments --include-deleted --auth-mode login` gives the deleted version, and `az storage container restore --account-name sterpaiproprod --name attachments --deleted-version <version> --auth-mode login` restores it. The restore is refused for about half a minute after the deletion; repeat it until it is accepted. Then remove the role as above.

## A rebuilt server

A new or reinstalled server with the old one's address, taking its place:

1. Bootstrap it ([host setup runbook](host-setup.md)) without `--backup-recipient`: it creates the two volumes empty and writes `.env` from `.env.example`. Place the runtime, migrator and backup client certificates as that runbook shows, and not yet the recipient certificate: without it the nightly backup is skipped, so no backup of the empty volumes runs before the restore, by the timer, at boot or by hand. `restore.sh` does not need it.
2. Put `AZURE_TENANT_ID`, `BACKUP_BLOB_ENDPOINT`, `BACKUP_CONTAINER` and `BACKUP_AZURE_CLIENT_ID` into the fresh `.env`; `restore.sh` reads them to reach the archives.
3. When the server leaves through another public address than before, move the four rules first: `bash scripts/azure/home-address.sh` ([home address runbook](home-address.md)).
4. Restore the server's own files before the stack first starts ("The server's own files", steps 1 to 4; the volumes are empty, so no `--replace` is needed), or the API makes a new key ring at its first start. Choose an archive the storage service created before the old server failed, and after a suspected compromise one created before it began ("Choosing the archive").
5. Deploy the release that ran last against the database as it is now, as the first deployment does ([deploy runbook](deploy.md), "The first deployment"): the release the restored `.env` names when no deployment or rollback ran after the archive was made, otherwise the one that did, from the record of that deployment (the release page shows what was published, not what ran). It pulls that release's images by digest and verifies them, finds no migration to apply and starts the stack. Deploying an older release stops at the migrator's exit 4 when a later one brought migrations.
6. Place the recipient certificate with the bootstrap's `--backup-recipient` ([host setup runbook](host-setup.md), "The backup's certificates"); the restored `.env` already holds the backup settings, and the backup runs again from the next night.
7. Every device keeps trusting the proxy's root, which came back with its volume; compare the fingerprint the manifest names with the one the proxy now serves ([proxy runbook](proxy.md), "Exporting the root certificate").

## The rehearsal

A full rehearsal on the production server, twice a year and after any change to the backups, with the ERP closed for about 90 minutes (owner decision of 2026-10-30). It brings the database back from Azure's backups and the server's own files from the archive noted before it, proves that people can sign in and open an old attachment, rehearses a deleted attachment and a deleted container, and then puts everything back as it was. The first rehearsal runs right after the first deployment, as a step of the go-live; once it succeeds, the operator locks the write-once policy of `host-backups` ([Azure bootstrap](../azure-bootstrap.md), section 5f).

### Before it

- Choose a quiet window away from 01:00 to 01:30 India time (the nightly retention run), 02:00 (the backup) and 03:00 (the update restart), and tell everyone the ERP is closed.
- On the operator's computer: `bash scripts/azure/verify.sh --database` passes; note `az sql db show --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name ErpAiPro --query earliestRestoreDate --output tsv` (about 7 days back) and the long-term copies (`az sql db ltr-backup list`, [database runbook](database.md)).
- The archive to restore holds the files the stack runs on: no deployment or rollback ran since last night's backup, or a backup runs at once just before T0 (`sudo systemctl start erp-ai-pro-backup.service`). Note its name (`restore.sh --list` on the server), the proxy root's fingerprint, one attachment uploaded days before, and upload a test attachment for the attachment step: a file no other attachment holds, because attachments of identical content share one blob ("A deleted attachment", step 2).
- Export the recipient key and copy it to the server ("The server's own files", steps 1 and 2).
- Just before the start, make a marker change in the ERP, such as renaming a test department to "Restore rehearsal <date> <hh:mm>", and note its time on the change history page.

### Steps

| Clock | Step | Note |
|---|---|---|
| 0:00 | On the server: `sudo systemctl stop erp-ai-pro.service`. | **T0** |
| 0:05 | Restore the database to T0 under the name `ErpAiPro_drill_<yyyymmdd>` ("The database to a point in time", step 2); if T0 is refused as later than the newest log backup, take the latest time accepted. | the restore point **R**; the request **T1** and the database online **T2** |
| 0:05, meanwhile | On the server: the dry run, then the restore by name, with `--replace`, of the archive noted before T0 ("The server's own files", steps 3 and 4). | done **T3** |
| 0:35 | Swap the databases: `ErpAiPro` becomes `ErpAiPro_live_<yyyymmdd>` and `ErpAiPro_drill_<yyyymmdd>` becomes `ErpAiPro` (step 3); then `database.sh --dry-run` and `verify.sh --database`. The rehearsal copy carries no long-term retention policy, so `verify.sh --database` reports exactly two failures, the weekly and the monthly copies (`got 'PT0S'`), and ends with `[FAIL] 2 check(s) failed`: record them as expected and do not follow their `provision.sh` hint; any other failing line is a real one. Run no `provision.sh` while the rehearsal copy carries the name `ErpAiPro`, until the swap back at 1:20: it would give the copy the template's tags and long-term retention, whose copies would outlive the copy's drop. | **T4** |
| 0:45 | On the server: `sudo systemctl start erp-ai-pro.service`; it returns once the stack is healthy. Compare the proxy root's fingerprint with the noted one. | **T5** |
| 0:50 | On a device that trusts the root, open the ERP without a warning, sign in in a private window, look for the marker on the change history page and download the old attachment. | **T6**, the end of the time until people work again |
| 1:00 | The files, which do not count towards T6: grant the temporary role; delete the test attachment's blob, see the ERP refuse it, undelete it and download it again; create a container `restore-drill`, put a file in it, delete the container, restore it and delete it again; read the regional sync time, `az storage account show --name sterpaiproprod --resource-group rg-erp-ai-pro --expand geoReplicationStats --query geoReplicationStats.lastSyncTime`; remove the role with the lock lifted, and leave the lock off until 1:35. | each restore's duration; the regional sync |
| 1:20 | Put production back: stop the stack; rename `ErpAiPro` to `ErpAiPro_drill_<yyyymmdd>` and `ErpAiPro_live_<yyyymmdd>` to `ErpAiPro`; start the stack and sign in once more; `earliestRestoreDate` is still about 7 days back. The restored volumes stay: they hold the files the stack used before T0 ("Before it"). | **T7** |
| 1:35 | Record (below); `provision.sh`, which puts the production storage account's lock back now that `ErpAiPro` is the original again; then `verify.sh` and `verify.sh --database` pass. | |

Swapping back returns production to the untouched original, so nothing written before T0 is lost; what the rehearsal itself wrote (its sign-ins, cached tokens) exists only in the rehearsal copy. A real incident stops after T6 and keeps the restored database. The rehearsal copy is dropped by the owner, naming it exactly, after a week at most (about ₹124 at Basic for the week).

### What to measure

- **Time until people work again**: T6 − T0, broken down into the database restore (T2 − T1), the server's files (T3 − T0), the swap and its checks (T4 − the later of T2 and T3), the start (T5 − T4) and the proof (T6 − T5).
- **Database work lost**: T0 − the time of the newest change record in the restored copy (whether the marker survived shows it), and T0 − R.
- **The server's own files**: T0 − the creation of the archive restored.
- **Attachments**: none lost within 90 days of a deletion; the regional loss in a failover is now − the sync time read.
- Also: the restore's tier and duration, `earliestRestoreDate` before and after, and every step that failed, with its fix.

## Record

| Date | Operator | T0 (UTC) | R | Database restore | Server's files | Swap and checks | Start | T6 − T0 | Database work lost | Archive restored | Attachments | Issues and follow-ups |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-10-31 | The owner, with Claude | 12:01:54 | 12:01:54, T0 itself, accepted at once | 8 min 5 s at Basic, requested 12:05:47 and online 12:13:52; `earliestRestoreDate` 2026-10-24 12:05 before and 12:50 after | 28 s for the restore itself, done 12:07:09, 5 min 15 s after T0 | 4 min 22 s, done 12:18:14: the copy matched the live database table by table, the swap took 26 s, `database.sh --dry-run` changed nothing and `verify.sh --database` gave exactly the two expected failures | 18 s, up at 12:38:12, 19 min 58 s after T4, which waited for the owner | 38 min 12 s: signed in in a private window at 12:39:23, the marker on the change history page, the file downloaded at 12:40:05 | None: in every table compared the copy held the live database's rows as at T0, the marker of 12:01:07 included; T0 − R is 0 | `2026/10/20261031T120131Z.tar.p7m`, created 12:01:39, 15 s before T0, by a backup run at once; release 0.1.2, the proxy root unchanged | None lost: the blob both test uploads shared was deleted, the ERP refused the file, the undelete brought it back within 5 s and it downloaded again; `restore-drill` came back 38 s after its deletion with its file unchanged; the regional copy was 2 min 48 s behind | The three targets were met. Git Bash on Windows rewrote the role's scope (`MissingSubscription`), fixed with `MSYS_NO_PATHCONV=1` ("A deleted attachment"). The container restore was refused for about 30 s after the deletion ("A deleted container"). The two test uploads were one file and shared one blob, and no attachment from days before existed, the ERP having gone live that day; the next rehearsal uploads a file no other attachment holds ("Before it"). The role took effect a minute after the grant and was held 5 min 36 s. Production was back on the original at 12:52:45 (T7, 50 min 51 s after T0), unchanged in every table compared, and the write-once policy of `host-backups` was locked at 13:05. Azure's resource listing still showed `ErpAiPro_live_20261031` after the swap back, while a read of it answered not found. The owner drops `ErpAiPro_drill_20261031` within the week. |
