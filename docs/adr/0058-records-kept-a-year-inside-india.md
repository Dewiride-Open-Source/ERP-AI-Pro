# ADR-0058: Records kept a year inside India

Status: accepted
Date: 2026-10-31

## Context

- The Digital Personal Data Protection Rules, 2025 (G.S.R. 846(E) of 13 November 2025), rules 6(1)(e) and 8(3), keep the logs of each processing for at least a year, and the CERT-In Directions of 28 April 2022, direction (iv), keep the logs of every ICT system securely for a rolling 180 days within Indian jurisdiction (both verified in [ADR-0043](0043-change-records-and-the-append-only-audit-schema.md) and [ADR-0038](0038-sign-in-limit-security-events-and-the-audit-schema.md)).
- Until now the containers logged with `json-file`, 20 MB × 5 per container, so their lines rotated away within days. Azure keeps its activity log 90 days and Microsoft Entra ID its sign-in and audit logs 30 days (Microsoft Entra ID P1 or P2), the vaults, the storage accounts and the configuration store record no request without a diagnostic setting, and the database's auditing is off.
- The ERP serves the owner's own small company, the owner its only user, with no external user and no other tenant. The owner declined every recurring cost and every copy of the records (owner decisions of 2026-10-31).

## Decision

- **The server's journal holds the stack's lines for a year.** The migrator, api, web and proxy log through Docker's `journald` driver, each under its own tag (`erp-ai-pro-<service>`), and Docker's default driver is `journald` too. A journald drop-in keeps the journal persistent and compressed, up to 24 GB in at most 400 files with 20 GB of the disk left free, and deletes a file 400 days after its first entry (`MaxRetentionSec=400d`), so every entry stays more than a year. The file count is set because journald on Ubuntu 24.04 caps a file at 128 MB and keeps 100, which would stop the journal near 12.8 GB. `docker.service`, through which every container logs, may write 100,000 lines per 30 seconds instead of journald's 10,000, so a busy minute drops nothing.
  - Outside Development the API writes one JSON object per line with its scopes (`AddJsonConsole`, UTC timestamps in ISO 8601), so a line can be filtered by its correlation id.
  - The bootstrap installs all of it ([ADR-0049](0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md)) and proves the limits in effect. It recognises and replaces the `daemon.json` an earlier bootstrap wrote, which replaces ADR-0049's rotated `json-file` logs, and refuses an operator's own file that keeps a log option `journald` refuses.
  - The administrator reads all of it with `journalctl` (the group `adm`), exporting JSON with `--all` so that no field of 4096 bytes or more is written as `null`. `journalctl` run as `erp-ai-pro` shows only that account's own entries, but `docker compose logs` gives it the containers' lines, and being root through Docker it could reach the rest.
- **The ERP's own records stay in the database**: the change records for 8 financial years (ADR-0043) and the security events for one year (ADR-0038).
- **Azure's own records stay where Azure keeps them**, as long as it keeps them: the activity log 90 days, Entra ID's sign-in and audit logs 30 days.
- **Handing records over** to CERT-In or a data principal is the [records runbook](../operations/runbooks/records.md): which record holds what, where, for how long, and how a time range is read out.

## Consequences

- **It costs nothing**: the journal takes at most 24 GB of the server's disk, which has more than 200 GB free.
- **Azure's own records fall short of direction (iv).** The activity log's 90 days and Entra's 30 are shorter than the 180 the direction asks. The vaults', storage accounts' and configuration store's requests and the database's own sign-ins and changes are recorded nowhere. The owner accepted this on 2026-10-31 for a company of one user. When CERT-In asks, the records runbook exports what Azure still holds.
- **The journal lives on the server alone.** Losing the server or its disk loses it, and an attacker with root on the server can change it. The nightly backup holds the server's own files, not its journal ([ADR-0056](0056-the-servers-own-files-backed-up-nightly-sealed-to-an-operator-key-and-the-restore-drill.md)).
- **The containers move to `journald` at the next deployment**, because a container keeps the driver it was created with.
- **Failed database sign-ins** with an Entra account never reach the database, because Entra refuses them first. They appear in Entra's sign-in logs, for 30 days.

## Alternatives considered

- **A logs account in Central India**, one copy, Shared Key off, write-once for a year. It would receive the database's auditing of sign-ins and changes of structure, users and permissions, the vaults', storage accounts' and configuration store's resource logs, the activity log and Entra ID's logs. Estimated at well under ₹100 a month, the owner declined it as too costly for one user (2026-10-31).
- **A sealed copy of each day of the journal**, uploaded every night to a write-once container of the backup account: declined by the owner, with every other copy of the records (2026-10-31).
- **Two copies of the logs** (`Standard_GRS`): the owner chose one, before declining the account itself.
- **Every statement in the database's audit** (`BATCH_COMPLETED_GROUP`): it would copy every value the ERP sends, people's names and emails among them, into a second place for a year.
- **Data collection rules** with a managed identity: in preview, and supporting none of these sources. **A Log Analytics workspace**: its records can be purged and are not write-once, and ingestion and retention are billed. **Event Hubs with Capture**: the hub needs a key, and it costs more.
- **journald's Forward Secure Sealing**: free, but it needs `journalctl --setup-keys`, a verification key kept off the server and a regular `journalctl --verify` by the operator, which one user does not run.
