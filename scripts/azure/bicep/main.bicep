targetScope = 'resourceGroup'

import { RoleAssignment, SecretRoleAssignment } from './modules/types.bicep'

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

@minLength(3)
@maxLength(24)
param operationsKeyVaultName string

@minLength(3)
@maxLength(24)
param developmentStorageAccountName string

@minLength(3)
@maxLength(63)
param attachmentsContainerName string = 'attachments'

@minValue(7)
@maxValue(365)
param developmentStorageDeleteRetentionDays int = 7

@minLength(3)
@maxLength(24)
param productionStorageAccountName string

@minValue(7)
@maxValue(365)
param productionStorageDeleteRetentionDays int = 90

param productionStorageHomeAddress string = ''

@minLength(3)
@maxLength(24)
param backupStorageAccountName string

@minLength(3)
@maxLength(63)
param backupContainerName string = 'host-backups'

@minValue(7)
@maxValue(365)
param backupStorageDeleteRetentionDays int = 7

@minValue(1)
@maxValue(146000)
param backupImmutabilityPeriodDays int = 90

param backupImmutabilityPolicyLocked bool = false

@minLength(3)
@maxLength(63)
param backupJournalContainerName string = 'host-journal'

@minValue(1)
@maxValue(146000)
param backupJournalImmutabilityPeriodDays int = 365

param backupJournalImmutabilityPolicyLocked bool = false

param backupStorageHomeAddress string = ''

@minLength(3)
@maxLength(24)
param logsStorageAccountName string

@minValue(7)
@maxValue(365)
param logsStorageDeleteRetentionDays int = 7

@minValue(1)
@maxValue(146000)
param logsImmutabilityPeriodDays int = 365

param logsImmutabilityPolicyLocked bool = false

param logsStorageHomeAddress string = ''

@minLength(1)
@maxLength(63)
param productionSqlServerName string

@minLength(1)
@maxLength(128)
param productionSqlDatabaseName string

@minLength(36)
@maxLength(36)
param operatorPrincipalId string

@minLength(1)
param operatorLogin string

param developersGroupPrincipalId string = ''

param runtimePrincipalId string = ''

param migratorPrincipalId string = ''

param migratorSecretNames string = ''

param backupPrincipalId string = ''

param tags object = {
  project: 'erp-ai-pro'
  managedBy: 'scripts/azure'
}

var roleDefinitionIds = {
  appConfigurationDataReader: '516239f1-63e1-4d78-a4de-a74fb236a071'
  appConfigurationDataOwner: '5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
  keyVaultCryptoServiceEncryptionUser: 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
  keyVaultCryptoOfficer: '14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
  keyVaultCertificatesOfficer: 'a4417e6f-fecd-4de8-b567-7b0420556985'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageBlobDataReader: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
}

// A day past the write-once period: SQL auditing refuses a retention that does not exceed the
// storage policy's period, and the lifecycle rule then deletes nothing the policy still keeps.
var logsDeleteAfterDays = logsImmutabilityPeriodDays + 1

var recordsStorageAccountId = resourceId('Microsoft.Storage/storageAccounts', logsStorageAccountName)

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
      ],
  empty(migratorPrincipalId)
    ? []
    : [
        {
          principalId: migratorPrincipalId
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
          roleDefinitionId: roleDefinitionIds.keyVaultCryptoServiceEncryptionUser
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
          roleDefinitionId: roleDefinitionIds.keyVaultCryptoServiceEncryptionUser
        }
      ]
)

var productionKeyVaultSecretRoleAssignments SecretRoleAssignment[] = [
  for secretName in (empty(migratorPrincipalId) || empty(migratorSecretNames) ? [] : split(migratorSecretNames, ',')): {
    secretName: secretName
    principalId: migratorPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: roleDefinitionIds.keyVaultSecretsUser
  }
]

var operationsKeyVaultRoleAssignments RoleAssignment[] = [
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
]

var developmentStorageRoleAssignments RoleAssignment[] = concat(
  [
    {
      principalId: operatorPrincipalId
      principalType: 'User'
      roleDefinitionId: roleDefinitionIds.storageBlobDataContributor
    }
  ],
  empty(developersGroupPrincipalId)
    ? []
    : [
        {
          principalId: developersGroupPrincipalId
          principalType: 'Group'
          roleDefinitionId: roleDefinitionIds.storageBlobDataContributor
        }
      ]
)

var productionStorageRoleAssignments RoleAssignment[] = empty(runtimePrincipalId)
  ? []
  : [
      {
        principalId: runtimePrincipalId
        principalType: 'ServicePrincipal'
        roleDefinitionId: roleDefinitionIds.storageBlobDataContributor
      }
    ]

var backupStorageRoleAssignments RoleAssignment[] = empty(backupPrincipalId)
  ? []
  : [
      {
        principalId: backupPrincipalId
        principalType: 'ServicePrincipal'
        roleDefinitionId: roleDefinitionIds.storageBlobDataContributor
      }
    ]

var logsStorageRoleAssignments RoleAssignment[] = [
  {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: roleDefinitionIds.storageBlobDataReader
  }
]

module logsStorageAccount 'modules/logs-storage-account.bicep' = {
  name: 'storage-account-logs'
  params: {
    name: logsStorageAccountName
    location: location
    tags: tags
    deleteRetentionDays: logsStorageDeleteRetentionDays
    immutabilityPeriodDays: logsImmutabilityPeriodDays
    immutabilityPolicyLocked: logsImmutabilityPolicyLocked
    deleteAfterDays: logsDeleteAfterDays
    allowedAddresses: empty(logsStorageHomeAddress) ? [] : [logsStorageHomeAddress]
    roleAssignments: logsStorageRoleAssignments
  }
}

module configurationStore 'modules/configuration-store.bicep' = {
  name: 'configuration-store'
  params: {
    name: configurationStoreName
    location: location
    sku: configurationStoreSku
    tags: tags
    roleAssignments: configurationStoreRoleAssignments
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module developmentKeyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-development'
  params: {
    name: developmentKeyVaultName
    location: location
    tags: tags
    roleAssignments: developmentKeyVaultRoleAssignments
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module productionKeyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-production'
  params: {
    name: productionKeyVaultName
    location: location
    tags: tags
    roleAssignments: productionKeyVaultRoleAssignments
    secretRoleAssignments: productionKeyVaultSecretRoleAssignments
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module operationsKeyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-operations'
  params: {
    name: operationsKeyVaultName
    location: location
    tags: tags
    roleAssignments: operationsKeyVaultRoleAssignments
    createDataProtectionKey: false
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module developmentStorageAccount 'modules/storage-account.bicep' = {
  name: 'storage-account-development'
  params: {
    name: developmentStorageAccountName
    location: location
    sku: 'Standard_LRS'
    tags: tags
    containers: [
      {
        name: attachmentsContainerName
        immutabilityPeriodDays: 0
        immutabilityPolicyLocked: false
        roleAssignments: developmentStorageRoleAssignments
      }
    ]
    deleteRetentionDays: developmentStorageDeleteRetentionDays
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module productionStorageAccount 'modules/storage-account.bicep' = {
  name: 'storage-account-production'
  params: {
    name: productionStorageAccountName
    location: location
    sku: 'Standard_GRS'
    tags: tags
    containers: [
      {
        name: attachmentsContainerName
        immutabilityPeriodDays: 0
        immutabilityPolicyLocked: false
        roleAssignments: productionStorageRoleAssignments
      }
    ]
    deleteRetentionDays: productionStorageDeleteRetentionDays
    restrictToAllowedAddresses: true
    allowedAddresses: empty(productionStorageHomeAddress) ? [] : [productionStorageHomeAddress]
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module backupStorageAccount 'modules/storage-account.bicep' = {
  name: 'storage-account-backup'
  params: {
    name: backupStorageAccountName
    location: location
    sku: 'Standard_GRS'
    tags: tags
    containers: [
      {
        name: backupContainerName
        immutabilityPeriodDays: backupImmutabilityPeriodDays
        immutabilityPolicyLocked: backupImmutabilityPolicyLocked
        roleAssignments: backupStorageRoleAssignments
      }
      {
        name: backupJournalContainerName
        immutabilityPeriodDays: backupJournalImmutabilityPeriodDays
        immutabilityPolicyLocked: backupJournalImmutabilityPolicyLocked
        roleAssignments: backupStorageRoleAssignments
      }
    ]
    deleteRetentionDays: backupStorageDeleteRetentionDays
    restrictToAllowedAddresses: true
    allowedAddresses: empty(backupStorageHomeAddress) ? [] : [backupStorageHomeAddress]
    recordsStorageAccountId: recordsStorageAccountId
  }
  dependsOn: [
    logsStorageAccount
  ]
}

module productionSqlDatabase 'modules/sql-database.bicep' = {
  name: 'sql-database-production'
  params: {
    serverName: productionSqlServerName
    databaseName: productionSqlDatabaseName
    location: location
    tags: tags
    administratorObjectId: operatorPrincipalId
    administratorLogin: operatorLogin
  }
}

module productionSqlAuditing 'modules/sql-auditing.bicep' = {
  name: 'sql-auditing-production'
  params: {
    serverName: productionSqlServerName
    serverPrincipalId: productionSqlDatabase.outputs.principalId
    logsStorageAccountName: logsStorageAccount.outputs.name
    writerRoleDefinitionId: roleDefinitionIds.storageBlobDataContributor
    retentionDays: logsDeleteAfterDays
  }
}

output configurationStoreEndpoint string = configurationStore.outputs.endpoint
output configurationStoreId string = configurationStore.outputs.id
output developmentKeyVaultUri string = developmentKeyVault.outputs.uri
output productionKeyVaultUri string = productionKeyVault.outputs.uri
output operationsKeyVaultUri string = operationsKeyVault.outputs.uri
output developmentDataProtectionKeyUri string = developmentKeyVault.outputs.dataProtectionKeyUri
output productionDataProtectionKeyUri string = productionKeyVault.outputs.dataProtectionKeyUri
output developmentAttachmentsBlobEndpoint string = developmentStorageAccount.outputs.blobEndpoint
output developmentStorageAccountId string = developmentStorageAccount.outputs.id
output developmentAttachmentsContainerId string = developmentStorageAccount.outputs.containerIds[attachmentsContainerName]
output productionAttachmentsBlobEndpoint string = productionStorageAccount.outputs.blobEndpoint
output productionStorageAccountId string = productionStorageAccount.outputs.id
output productionAttachmentsContainerId string = productionStorageAccount.outputs.containerIds[attachmentsContainerName]
output backupBlobEndpoint string = backupStorageAccount.outputs.blobEndpoint
output backupStorageAccountId string = backupStorageAccount.outputs.id
output backupContainerId string = backupStorageAccount.outputs.containerIds[backupContainerName]
output backupJournalContainerId string = backupStorageAccount.outputs.containerIds[backupJournalContainerName]
output logsBlobEndpoint string = logsStorageAccount.outputs.blobEndpoint
output logsStorageAccountId string = logsStorageAccount.outputs.id
output productionSqlServerFqdn string = productionSqlDatabase.outputs.fullyQualifiedDomainName
