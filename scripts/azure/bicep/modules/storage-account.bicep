import { RoleAssignment } from './types.bicep'

@minLength(3)
@maxLength(24)
param name string

param location string

@allowed(['Standard_LRS', 'Standard_ZRS', 'Standard_GRS', 'Standard_GZRS'])
param sku string

param tags object

@minLength(3)
@maxLength(63)
param containerName string

@minValue(7)
@maxValue(365)
param deleteRetentionDays int

param roleAssignments RoleAssignment[]

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
    networkAcls: {
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

    resource container 'containers' = {
      name: containerName
      properties: {
        publicAccess: 'None'
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

resource containerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in roleAssignments: {
    name: guid(account::blobService::container.id, assignment.principalId, assignment.roleDefinitionId)
    scope: account::blobService::container
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
output containerId string = account::blobService::container.id
