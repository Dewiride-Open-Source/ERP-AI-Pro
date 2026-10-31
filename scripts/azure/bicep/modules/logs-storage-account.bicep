import { RoleAssignment } from './types.bicep'

@minLength(3)
@maxLength(24)
param name string

param location string

param tags object

@minValue(7)
@maxValue(365)
param deleteRetentionDays int

@minValue(1)
@maxValue(146000)
param immutabilityPeriodDays int

param immutabilityPolicyLocked bool

@minValue(2)
@maxValue(146001)
param deleteAfterDays int

param allowedAddresses string[]

param roleAssignments RoleAssignment[]

var allowedAddressRules = [
  for address in allowedAddresses: {
    value: address
    action: 'Allow'
  }
]

resource account 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    isHnsEnabled: false
    isSftpEnabled: false
    isNfsV3Enabled: false
    isLocalUserEnabled: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      // Azure Monitor and the SQL server's auditing write the records as trusted Microsoft
      // services; the IP rule admits the home, from which the operator reads them.
      bypass: 'AzureServices'
      defaultAction: 'Deny'
      ipRules: allowedAddressRules
      ipv6Rules: []
      virtualNetworkRules: []
      resourceAccessRules: []
    }
    // Azure turns version-level immutability on only when it creates the account. A locked policy
    // keeps its state and only lengthens, so the deployment sends the state the account reports.
    immutableStorageWithVersioning: {
      enabled: true
      immutabilityPolicy: {
        immutabilityPeriodSinceCreationInDays: immutabilityPeriodDays
        // Azure Monitor and SQL auditing append to their blobs, and SQL auditing refuses an
        // immutable destination that does not allow protected append writes.
        allowProtectedAppendWrites: true
        state: immutabilityPolicyLocked ? 'Locked' : 'Unlocked'
      }
    }
  }

  resource blobService 'blobServices' = {
    name: 'default'
    properties: {
      deleteRetentionPolicy: {
        enabled: true
        days: deleteRetentionDays
      }
      containerDeleteRetentionPolicy: {
        enabled: true
        days: deleteRetentionDays
      }
      isVersioningEnabled: true
      changeFeed: {
        enabled: false
      }
    }
  }

  resource lifecycle 'managementPolicies' = {
    name: 'default'
    properties: {
      policy: {
        rules: [
          {
            name: 'delete-records-past-their-period'
            enabled: true
            type: 'Lifecycle'
            definition: {
              filters: {
                blobTypes: [
                  'appendBlob'
                  'blockBlob'
                ]
              }
              actions: {
                baseBlob: {
                  delete: {
                    daysAfterModificationGreaterThan: deleteAfterDays
                  }
                }
                version: {
                  delete: {
                    daysAfterCreationGreaterThan: deleteAfterDays
                  }
                }
              }
            }
          }
        ]
      }
    }
  }
}

resource accountLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'do-not-delete'
  scope: account
  properties: {
    level: 'CanNotDelete'
    notes: 'Blob and container soft delete do not cover deletion of the account itself.'
  }
}

resource accountRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in roleAssignments: {
    name: guid(account.id, assignment.principalId, assignment.roleDefinitionId)
    scope: account
    properties: {
      principalId: assignment.principalId
      principalType: assignment.principalType
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        assignment.roleDefinitionId
      )
    }
  }
]

output blobEndpoint string = account.properties.primaryEndpoints.blob
output id string = account.id
output name string = account.name
