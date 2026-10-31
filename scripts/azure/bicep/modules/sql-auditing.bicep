@minLength(1)
@maxLength(63)
param serverName string

@minLength(36)
@maxLength(36)
param serverPrincipalId string

@minLength(3)
@maxLength(24)
param logsStorageAccountName string

@minLength(36)
@maxLength(36)
param writerRoleDefinitionId string

@minValue(1)
param retentionDays int

// Sign-ins and changes of structure, users and permissions, never statements: a group such as
// BATCH_COMPLETED_GROUP keeps the text of every batch exactly as the client sent it.
var auditActionsAndGroups = [
  'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
  'FAILED_DATABASE_AUTHENTICATION_GROUP'
  'DATABASE_CHANGE_GROUP'
  'DATABASE_OBJECT_CHANGE_GROUP'
  'DATABASE_OBJECT_OWNERSHIP_CHANGE_GROUP'
  'DATABASE_OBJECT_PERMISSION_CHANGE_GROUP'
  'DATABASE_OWNERSHIP_CHANGE_GROUP'
  'DATABASE_PERMISSION_CHANGE_GROUP'
  'DATABASE_PRINCIPAL_CHANGE_GROUP'
  'DATABASE_PRINCIPAL_IMPERSONATION_GROUP'
  'DATABASE_ROLE_MEMBER_CHANGE_GROUP'
  'SCHEMA_OBJECT_CHANGE_GROUP'
  'SCHEMA_OBJECT_OWNERSHIP_CHANGE_GROUP'
  'SCHEMA_OBJECT_PERMISSION_CHANGE_GROUP'
  'USER_CHANGE_PASSWORD_GROUP'
]

resource logsAccount 'Microsoft.Storage/storageAccounts@2026-04-01' existing = {
  name: logsStorageAccountName
}

resource server 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: serverName
}

resource auditWriterRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(logsAccount.id, serverPrincipalId, writerRoleDefinitionId)
  scope: logsAccount
  properties: {
    principalId: serverPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', writerRoleDefinitionId)
  }
}

resource auditing 'Microsoft.Sql/servers/auditingSettings@2025-01-01' = {
  parent: server
  name: 'default'
  properties: {
    state: 'Enabled'
    storageEndpoint: logsAccount.properties.primaryEndpoints.blob
    storageAccountSubscriptionId: subscription().subscriptionId
    // Without an access key the server writes with its managed identity, the only way past the
    // account's firewall, and the role above must exist before auditing is turned on.
    isManagedIdentityInUse: true
    isStorageSecondaryKeyInUse: false
    isAzureMonitorTargetEnabled: false
    retentionDays: retentionDays
    auditActionsAndGroups: auditActionsAndGroups
  }
  dependsOn: [
    auditWriterRole
  ]
}
