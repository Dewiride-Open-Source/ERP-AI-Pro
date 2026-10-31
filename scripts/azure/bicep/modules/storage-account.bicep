import { StorageContainer, recordsDiagnosticSettingName } from './types.bicep'

@minLength(3)
@maxLength(24)
param name string

param location string

@allowed(['Standard_LRS', 'Standard_ZRS', 'Standard_GRS', 'Standard_GZRS'])
param sku string

param tags object

param containers StorageContainer[]

@minValue(7)
@maxValue(365)
param deleteRetentionDays int

param restrictToAllowedAddresses bool = false

param allowedAddresses string[] = []

param recordsStorageAccountId string

var allowedAddressRules = [
  for address in allowedAddresses: {
    value: address
    action: 'Allow'
  }
]

var containerAssignments = flatten(map(range(0, length(containers)), index => map(containers[index].roleAssignments, assignment => {
  containerIndex: index
  principalId: assignment.principalId
  principalType: assignment.principalType
  roleDefinitionId: assignment.roleDefinitionId
})))

resource account 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: sku
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
    // Developer machines and the on-premises server reach the account over the public
    // endpoint; there is no virtual network or private endpoint to restrict to.
    publicNetworkAccess: 'Enabled'
    networkAcls: restrictToAllowedAddresses
      ? {
          bypass: 'None'
          defaultAction: 'Deny'
          ipRules: allowedAddressRules
          ipv6Rules: []
          virtualNetworkRules: []
          resourceAccessRules: []
        }
      : {
          bypass: 'AzureServices'
          defaultAction: 'Allow'
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
      isVersioningEnabled: false
      changeFeed: {
        enabled: false
      }
    }
  }
}

resource blobContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = [
  for container in containers: {
    parent: account::blobService
    name: container.name
    properties: {
      publicAccess: 'None'
    }
  }
]

resource immutabilityPolicies 'Microsoft.Storage/storageAccounts/blobServices/containers/immutabilityPolicies@2026-04-01' = [
  for (container, index) in containers: if (container.immutabilityPeriodDays > 0 && !container.immutabilityPolicyLocked) {
    parent: blobContainers[index]
    name: 'default'
    properties: {
      immutabilityPeriodSinceCreationInDays: container.immutabilityPeriodDays
      allowProtectedAppendWrites: false
      allowProtectedAppendWritesAll: false
    }
  }
]

resource accountLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'do-not-delete'
  scope: account
  properties: {
    level: 'CanNotDelete'
    notes: 'Blob and container soft delete do not cover deletion of the account itself.'
  }
}

resource containerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in containerAssignments: {
    name: guid(blobContainers[assignment.containerIndex].id, assignment.principalId, assignment.roleDefinitionId)
    scope: blobContainers[assignment.containerIndex]
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

#disable-next-line use-recent-api-versions // the newest version; 2016-09-01, the one stable version the rule accepts, allows a single setting, named service
resource blobServiceRecords 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: recordsDiagnosticSettingName
  scope: account::blobService
  properties: {
    storageAccountId: recordsStorageAccountId
    logs: [
      {
        category: 'StorageRead'
        enabled: true
      }
      {
        category: 'StorageWrite'
        enabled: true
      }
      {
        category: 'StorageDelete'
        enabled: true
      }
    ]
  }
}

output blobEndpoint string = account.properties.primaryEndpoints.blob
output id string = account.id
output containerIds object = toObject(containers, container => container.name, container => resourceId('Microsoft.Storage/storageAccounts/blobServices/containers', name, 'default', container.name))
