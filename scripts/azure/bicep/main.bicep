targetScope = 'resourceGroup'

import { RoleAssignment } from './modules/types.bicep'

param location string = resourceGroup().location

@minLength(5)
@maxLength(50)
param configurationStoreName string

@allowed(['Free', 'Developer', 'Standard', 'Premium'])
param configurationStoreSku string = 'Free'

@minLength(3)
@maxLength(24)
param developmentKeyVaultName string

@minLength(3)
@maxLength(24)
param productionKeyVaultName string

@minLength(36)
@maxLength(36)
param operatorPrincipalId string

param developersGroupPrincipalId string = ''

param runtimePrincipalId string = ''

param tags object = {
  project: 'erp-ai-pro'
  managedBy: 'scripts/azure'
}

var roleDefinitionIds = {
  appConfigurationDataReader: '516239f1-63e1-4d78-a4de-a74fb236a071'
  appConfigurationDataOwner: '5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
  keyVaultCryptoUser: '12338af0-0e69-4776-bea7-57ae8d297424'
  keyVaultCryptoOfficer: '14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
  keyVaultCertificatesOfficer: 'a4417e6f-fecd-4de8-b567-7b0420556985'
}

var configurationStoreRoleAssignments RoleAssignment[] = concat(
  [
    {
      principalId: operatorPrincipalId
      principalType: 'User'
      roleDefinitionId: roleDefinitionIds.appConfigurationDataOwner
    }
  ],
  empty(developersGroupPrincipalId)
    ? []
    : [
        {
          principalId: developersGroupPrincipalId
          principalType: 'Group'
          roleDefinitionId: roleDefinitionIds.appConfigurationDataReader
        }
      ],
  empty(runtimePrincipalId)
    ? []
    : [
        {
          principalId: runtimePrincipalId
          principalType: 'ServicePrincipal'
          roleDefinitionId: roleDefinitionIds.appConfigurationDataReader
        }
      ]
)

// Certificate User is deliberately absent: sign-in certificates are read through the
// secrets/get operation, which Secrets User already grants.
var operatorVaultRoleAssignments RoleAssignment[] = [
  {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: roleDefinitionIds.keyVaultSecretsOfficer
  }
  {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: roleDefinitionIds.keyVaultCertificatesOfficer
  }
  {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: roleDefinitionIds.keyVaultCryptoOfficer
  }
]

var developmentKeyVaultRoleAssignments RoleAssignment[] = concat(
  operatorVaultRoleAssignments,
  empty(developersGroupPrincipalId)
    ? []
    : [
        {
          principalId: developersGroupPrincipalId
          principalType: 'Group'
          roleDefinitionId: roleDefinitionIds.keyVaultSecretsUser
        }
        {
          principalId: developersGroupPrincipalId
          principalType: 'Group'
          roleDefinitionId: roleDefinitionIds.keyVaultCryptoUser
        }
      ]
)

var productionKeyVaultRoleAssignments RoleAssignment[] = concat(
  operatorVaultRoleAssignments,
  empty(runtimePrincipalId)
    ? []
    : [
        {
          principalId: runtimePrincipalId
          principalType: 'ServicePrincipal'
          roleDefinitionId: roleDefinitionIds.keyVaultSecretsUser
        }
        {
          principalId: runtimePrincipalId
          principalType: 'ServicePrincipal'
          roleDefinitionId: roleDefinitionIds.keyVaultCryptoUser
        }
      ]
)

module configurationStore 'modules/configuration-store.bicep' = {
  name: 'configuration-store'
  params: {
    name: configurationStoreName
    location: location
    sku: configurationStoreSku
    tags: tags
    roleAssignments: configurationStoreRoleAssignments
  }
}

module developmentKeyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-development'
  params: {
    name: developmentKeyVaultName
    location: location
    tags: tags
    roleAssignments: developmentKeyVaultRoleAssignments
  }
}

module productionKeyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-production'
  params: {
    name: productionKeyVaultName
    location: location
    tags: tags
    roleAssignments: productionKeyVaultRoleAssignments
  }
}

output configurationStoreEndpoint string = configurationStore.outputs.endpoint
output configurationStoreId string = configurationStore.outputs.id
output developmentKeyVaultUri string = developmentKeyVault.outputs.uri
output productionKeyVaultUri string = productionKeyVault.outputs.uri
