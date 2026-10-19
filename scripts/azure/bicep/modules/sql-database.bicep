@minLength(1)
@maxLength(63)
param serverName string

@minLength(1)
@maxLength(128)
param databaseName string

param location string

param tags object

@minLength(36)
@maxLength(36)
param administratorObjectId string

@minLength(1)
param administratorLogin string

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: administratorLogin
      principalType: 'User'
      sid: administratorObjectId
      tenantId: tenant().tenantId
    }
    minimalTlsVersion: '1.2'
    // The home server reaches the database over the internet. Its one firewall rule is kept by
    // scripts/azure/database.sh, never by this template, so a deployment leaves the address as the
    // operator last set it; IPv6 stays off so that IPv4 rule is the only way in.
    publicNetworkAccess: 'Enabled'
    isIPv6Enabled: 'Disabled'
    version: '12.0'
  }

  // The administrators block sets Entra-only authentication when the server is created; later
  // deployments converge it through this resource only.
  resource entraOnlyAuthentication 'azureADOnlyAuthentications' = {
    name: 'Default'
    properties: {
      azureADOnlyAuthentication: true
    }
  }

  resource database 'databases' = {
    name: databaseName
    location: location
    tags: tags
    sku: {
      name: 'Basic'
      tier: 'Basic'
      capacity: 5
    }
    properties: {
      collation: 'SQL_Latin1_General_CP1_CI_AS'
      maxSizeBytes: 2147483648
      // Geo-redundant backups go to the paired region, South India, so they stay in India
      // (CERT-In Directions of 28.04.2022, direction (iv)).
      requestedBackupStorageRedundancy: 'Geo'
      zoneRedundant: false
    }

    resource shortTermRetention 'backupShortTermRetentionPolicies' = {
      name: 'default'
      properties: {
        retentionDays: 7
      }
    }
  }
}

resource serverLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'do-not-delete'
  scope: server
  properties: {
    level: 'CanNotDelete'
    notes: 'Deleting the server deletes the production database with it; a changed firewall rule is an update, which the lock allows.'
  }
}

output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName
output serverId string = server.id
output databaseId string = server::database.id
