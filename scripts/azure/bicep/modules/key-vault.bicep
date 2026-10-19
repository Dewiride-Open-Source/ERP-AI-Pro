import { RoleAssignment, SecretRoleAssignment } from './types.bicep'

@minLength(3)
@maxLength(24)
param name string

param location string

param tags object

param roleAssignments RoleAssignment[]

param secretRoleAssignments SecretRoleAssignment[] = []

param createDataProtectionKey bool = true

param dataProtectionKeyName string = 'Erp--Platform--DataProtection--Key'

resource vault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    // Developer machines and the on-premises server reach the vault over the public
    // endpoint; there is no virtual network or private endpoint to restrict to.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }

  resource dataProtectionKey 'keys' = if (createDataProtectionKey) {
    name: dataProtectionKeyName
    properties: {
      kty: 'RSA'
      keySize: 2048
      keyOps: [
        'wrapKey'
        'unwrapKey'
      ]
    }
  }
}

resource assignedSecrets 'Microsoft.KeyVault/vaults/secrets@2025-05-01' existing = [
  for assignment in secretRoleAssignments: {
    parent: vault
    name: assignment.secretName
  }
]

resource vaultRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in roleAssignments: {
    name: guid(vault.id, assignment.principalId, assignment.roleDefinitionId)
    scope: vault
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

resource secretRoleAssignmentResources 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (assignment, index) in secretRoleAssignments: {
    name: guid(assignedSecrets[index].id, assignment.principalId, assignment.roleDefinitionId)
    scope: assignedSecrets[index]
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

output uri string = vault.properties.vaultUri
output id string = vault.id
output dataProtectionKeyUri string = createDataProtectionKey ? vault::dataProtectionKey!.properties.keyUri : ''
