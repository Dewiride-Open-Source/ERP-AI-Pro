# Runbook: the home's address

The production database, the production storage account and the backup storage account answer only the home's public IPv4 address: the server at home reaches all three through it, and so does the operator's computer on the home network. One command keeps the three rules on that address. Design: [ADR-0050](../../adr/0050-the-production-database-azure-sql-basic-with-microsoft-entra-only-users.md) for the database, [ADR-0051](../../adr/0051-the-production-attachments-storage-geo-redundant-and-reachable-from-the-homes-address-alone.md) for the attachments' storage account, [ADR-0056](../../adr/0056-the-servers-own-files-backed-up-nightly-sealed-to-an-operator-key-and-the-restore-drill.md) and the [Azure bootstrap](../azure-bootstrap.md), section 5f, for the backups' storage account.

## What admits the home

- The firewall rule `home` of the SQL server `sql-erp-ai-pro`, whose start and end are both the home's address. No other server rule, database rule, virtual network rule or private endpoint exists, and **Allow Azure services and resources to access this server** is off.
- The one IP rule of the storage account `sterpaiproprod`, which holds the attachments, behind the default action Deny, with no exception for trusted Azure services, no IPv6, virtual network or resource instance rule, no approved private endpoint connection and no network security perimeter.
- The one IP rule of the storage account `sterpaiprobackup`, which holds the server's nightly backups, with the same settings.

`provision.sh` keeps both storage accounts' rules on the address of the database's rule `home`, which it reads before every deployment, and `verify.sh --database` and `verify.sh` check all three. Only IPv4 counts: the SQL server has IPv6 off, and each storage account keeps its IPv6 rule list empty and publishes no IPv6 endpoint, so the server's IPv6 address plays no part.

From any other address, the operator's included, the database refuses the connection with error 40615 and each storage account answers 403 `AuthorizationFailure` to every request for a file. The Azure portal still shows the three resources and their settings, which these rules do not guard.

## When the home's address changes

The internet provider changes the home's public address from time to time. Until the rules move:

- the api's readiness check (`/healthz/ready`) answers 503, and its log shows error 40615 naming the new address (`Client with IP address '<address>' is not allowed to access the server`);
- the api's log also shows the check `storage:attachments` failing with 403 `AuthorizationFailure`, and every upload and download of an attachment fails;
- a migrator run stops with exit code 1 and the same error 40615 ([migrations runbook](migrations.md));
- the nightly backup cannot store its archive, and its journal shows the same 403 `AuthorizationFailure` ([restore runbook](restore.md));
- the three rules still admit the previous address, which the provider may already have given to another customer. Whoever holds it would still need a token of one of the server's identities and would read only encrypted files, but move the rules as soon as the address changes.

From a computer on the home network, without a VPN, signed in with `az login` as the operator, with the [Go sqlcmd](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-download-install) installed:

```bash
bash scripts/azure/home-address.sh --dry-run
bash scripts/azure/home-address.sh
```

Before it changes anything, the script fails while the server holds a rule besides `home` or either storage account admits more than one address (section "Never"); a rule `home` that became a range is narrowed. It then learns this computer's address from Azure, from the database when it answers and from error 40615 when its firewall refuses, and points the three rules at it, announcing each rule it moves. The database's rule takes up to five minutes to apply and each storage account's a few minutes; the api recovers on its own, and the next night's backup stores its archive again. Then read them back:

```bash
bash scripts/azure/verify.sh --database
bash scripts/azure/verify.sh
```

The script needs the operator's rights on the subscription, which change the server's firewall rule and the accounts' network rules, and does not need the database's Microsoft Entra admin: when the database refuses another sign-in, its firewall has already admitted this computer, and the script takes the address the rule `home` admits.

When the computer at hand cannot reach Azure from the home's address (it is away from home, or its VPN cannot be turned off), name the address instead. The 40615 line in the api's log on the server and the router's status page both show it; the script then runs from any computer, without the Go sqlcmd:

```bash
bash scripts/azure/home-address.sh --address <the home's new public address>
```

A computer that reaches the internet another way (a VPN, a phone's hotspot, a remote or cloud desktop) is seen at another address, and `home-address.sh` run there without `--address` would move the three rules away from the home; the dry run names the address first. The script refuses a private, shared or reserved address, such as a local address of the home network, because Azure sees the home only by its public address.

## Without the script

The database, in the Azure portal: open the SQL server `sql-erp-ai-pro`, then **Security** → **Networking**, set both the start and the end address of the rule `home` to the new address, and save. Never use **Add your client IPv4 address**, which adds a second rule, and never change the start address alone, which turns the rule into a range. With the Azure CLI:

```bash
az sql server firewall-rule update --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name home --start-ip-address <new address> --end-ip-address <new address>
```

Each storage account, in the Azure portal: open the storage account `sterpaiproprod`, then **Security + networking** → **Networking**, and under **Resource settings: Virtual networks, IP addresses, and exceptions** select **View**. Enter the new address in the box marked *IPv4 address or CIDR*, delete the old address with the delete icon next to it, and select **Save**; then do the same on `sterpaiprobackup`. With the Azure CLI, add the new address before removing the old one, so the server is never left without one:

```bash
az storage account network-rule add --resource-group rg-erp-ai-pro --account-name sterpaiproprod --ip-address <new address>
az storage account network-rule remove --resource-group rg-erp-ai-pro --account-name sterpaiproprod --ip-address <old address>
az storage account network-rule add --resource-group rg-erp-ai-pro --account-name sterpaiprobackup --ip-address <new address>
az storage account network-rule remove --resource-group rg-erp-ai-pro --account-name sterpaiprobackup --ip-address <old address>
```

## Never

- Never add a rule for a range or for a second address on any of the three resources: a provider gives a range to many customers.
- Never turn on **Allow Azure services and resources to access this server** on the SQL server, nor the exception for trusted Azure services on either storage account, and never add a virtual network rule, a resource instance rule, an IPv6 rule or a private endpoint to any of them, publish a storage account's IPv6 endpoints or add it to a network security perimeter: each admits more than the home.
- `home-address.sh`, `database.sh` and `verify.sh --database` fail while the server holds a rule besides `home`, and `home-address.sh` narrows a rule `home` that became a range. The server's lock keeps another rule until it is removed by hand: `az lock delete --name do-not-delete --resource-group rg-erp-ai-pro --resource-type Microsoft.Sql/servers --resource-name sql-erp-ai-pro`, then `az sql server firewall-rule delete --resource-group rg-erp-ai-pro --server sql-erp-ai-pro --name <rule>`, then `bash scripts/azure/provision.sh` to lock the server again.
- `home-address.sh` and `verify.sh` fail while either storage account admits more than one public address, and each failure names the account and its remedy:
  - a public network access other than **Enabled**, a default action other than Deny, a trusted-service exception, a second IP rule, or an IPv6, virtual network or resource instance rule: `bash scripts/azure/provision.sh` puts the account's own settings back to the template, the address of the database's rule `home` alone, and its what-if lists the change first;
  - a private endpoint connection, which the failure names: reject it, an update the account's lock allows, with `az storage account private-endpoint-connection reject --account-name <account> --resource-group rg-erp-ai-pro --name <connection>`. `home-address.sh` stops only on an approved one and reports a pending request, which admits nobody; `verify.sh` fails on both;
  - a published IPv6 endpoint (`verify.sh` alone): `az storage account update --name <account> --resource-group rg-erp-ai-pro --publish-ipv6-endpoint false`;
  - a network security perimeter: remove the account on the perimeter's **Associated resources** page in the Azure portal.
