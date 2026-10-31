# Go-live checklist

The steps that put the ERP into use on the production server for the first time, in order, each with the check that proves it, and the record of the go-live. The production setup it completes is [ADR-0057](../../adr/0057-the-production-topology.md). Steps marked "operator's computer" run there from the repository root, signed in with `az login` as the operator, at home without a VPN; "the server" steps run there as the account they name.

## Before it

- [ ] **The release.** The newest release is published, its three packages are public and its images verified, and main's checks passed ([deploy runbook](deploy.md), "Cutting a release" and "The first release").
- [ ] **The address.** The router reserves the server's address for its wired interface (a DHCP reservation for its MAC address), and that address is the one people will open, `https://<address>`. It stays out of every file of this repository: it lives in `scripts/azure/params.env` and the server's `.env`, both git-ignored.
- [ ] **A quiet window** of about three hours, away from 01:00 to 03:30 India time (the nightly retention run, the backup and the update restart), with everyone told the ERP is not open yet.

## The sign-in for the address (operator's computer)

1. Set `ERP_AZURE_PRODUCTION_WEB_ORIGIN=https://<address>` in `scripts/azure/params.env`.
2. `bash scripts/azure/entra.sh --dry-run`, then `bash scripts/azure/entra.sh`. It gives the production sign-in registration its redirect URIs and its front-channel logout URL `https://<address>/api/auth/signout-oidc`, and writes `Erp:Platform:Identity:WebOrigin` and `Erp:Platform:Host:AllowedHosts` (`<address>;api;127.0.0.1`) under the `production` label.
3. `bash scripts/azure/verify.sh --entra` and `bash scripts/azure/verify.sh --labels` pass.

## The server (the administrator)

4. Clone the release into the administrator's home and run the bootstrap from it, a dry run first, with the backup's public certificate exported on the operator's computer (`bash scripts/azure/export-client-certificate.sh backup-recipient`) and copied over ([host setup runbook](host-setup.md), "Running the bootstrap" and "The backup's certificates"):

   ```bash
   git clone --branch v<release> https://github.com/Dewiride-Open-Source/ERP-AI-Pro.git
   sudo bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --dry-run --site-address <address> --authorized-keys /srv/erp-ai-pro/.ssh/authorized_keys --backup-recipient ~/erp-backup-recipient.crt
   sudo bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --site-address <address> --authorized-keys /srv/erp-ai-pro/.ssh/authorized_keys --backup-recipient ~/erp-backup-recipient.crt
   ```

5. Place the three client certificates, the runtime's, the migrator's and the backup's ([host setup runbook](host-setup.md), "The client certificates" and "The backup's certificates"), and shred every copy outside `infra/compose/secrets`.
6. As `erp-ai-pro`, fill the go-live values of `.env`: `APPCONFIG_ENDPOINT`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `MIGRATOR_AZURE_CLIENT_ID`, `BACKUP_BLOB_ENDPOINT`, `BACKUP_CONTAINER` and `BACKUP_AZURE_CLIENT_ID` ([host setup runbook](host-setup.md), "The `.env` values"). The bootstrap wrote `ERP_SITE_ADDRESS`; the release lines stay for `deploy.sh`.

## The first deployment (`erp-ai-pro`)

7. Move the checkout to the release by hand once, then deploy it ([deploy runbook](deploy.md), "The first deployment"). It ends with `deployed v<release> (commit <commit>): the API answers Healthy`.
8. `docker compose -f compose.yaml -f compose.production.yaml ps`, in the compose folder, shows `migrator` exited 0 and `api`, `web` and `proxy` running and healthy.

## Each device

9. Every computer and phone that opens the ERP trusts the proxy's root once, after comparing its fingerprint ([host setup runbook](host-setup.md), "Trusting the proxy's root on each device").

## Proving it

10. **Sign-in through the proxy** (the owner, with their own account, owner decision of 2026-10-19): open `https://<address>` in a private window on a device that trusts the root, sign in, see the start page, open a page, sign out. The browser shows no certificate warning, and the sign-out asks no account question.
11. **Single sign-out**: signed in to the ERP, sign out of Outlook on the web in the same browser; the ERP's next request in that browser goes to the sign-in page, while a session on another device stays signed in (ADR-0036).
12. **Attachments from the server**: upload a file on the attachments page, download it and delete it; the API's readiness, read through the proxy container, answers `Healthy` (`docker compose -f compose.yaml -f compose.production.yaml exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready`, as `deploy.sh` reads it).
13. **The production smoke** (operator's computer, which trusts the root as step 9 says, with the frontend installed and Playwright's browsers too, `pnpm e2e:install` in `frontend`): from `frontend/e2e`, naming the root's file of step 9,

    ```bash
    E2E_PRODUCTION_URL=https://<address> NODE_EXTRA_CA_CERTS=<path to erp-proxy-root.crt> pnpm exec playwright test --project=production
    ```

    passes its five tests: the sign-in page with no certificate warning, the web app's and the API's headers without the proxy's own, the API's refusal of a visitor, a Microsoft sign-in start that comes back to the address, and the redirect from HTTP. The browser trusts the root through the computer's store, while the runner's own requests go through Node, which trusts only its own list and the file `NODE_EXTRA_CA_CERTS` names: without it they fail with `unable to get local issuer certificate`.
14. **The first backup**: `sudo systemctl start erp-ai-pro-backup.service` returns, `journalctl -u erp-ai-pro-backup.service --since today` shows every step and ends with `backed up the server's files to host-backups/…`.
15. **The first restore rehearsal**, with its measured times in the record of the [restore runbook](restore.md), section "The rehearsal".
16. **Lock the write-once policy** of `host-backups` once the rehearsal succeeded ([Azure bootstrap](../azure-bootstrap.md), section 5f); `bash scripts/azure/verify.sh` then reports it locked.

## Standing conditions

- **The sign-in limit.** Each sign-in path admits 60 requests per address per minute (`Erp:Platform:RateLimiting:SignInPermitLimit`), and everyone behind the office's one public address shares that allowance; a larger office raises it in App Configuration ([auth threat model](../../security/auth-threat-model.md)).
- **The address's domain.** People open an IP address, which shares no registrable domain with any other host. Once the ERP has a public name, that name shares its registrable domain with no host the company does not control, because the `__Secure-` correlation and nonce cookies, unlike the `__Host-` session cookie, can be planted by such a host ([auth threat model](../../security/auth-threat-model.md)).

## Record

| Date | Release | Operator | Steps done | Issues and follow-ups |
|---|---|---|---|---|
| 2026-10-31 | v0.1.0 | The owner, with Claude | 1 to 10 and 12, without the backup's parts of 4 to 6, which v0.1.0 does not carry. The owner signed in on a Mac through the proxy, opened People and Attachments, uploaded, downloaded and deleted a file, and signed out. On the server a request to the api naming another host answered 400 `request.host-not-allowed`, and one naming `api` 401 `request.unauthenticated`. Of step 13, the three tests that send requests passed from the operator's computer, a Windows computer whose store did not trust the root yet. | The proxy's root was trusted on the Mac in the login keychain with **Always Trust**. Double-clicking the file adds it untrusted. Windows' `curl` (Schannel) also needs `--ssl-no-revoke`, because the proxy's authority publishes no revocation data. Step 11 follows at the owner's next sign-out of Outlook on the web in a signed-in browser, the two browser tests of step 13 once the operator's computer trusts the root, and steps 14 to 16 with the release that carries the backups, after the bootstrap runs again from it with `--backup-recipient`. |
