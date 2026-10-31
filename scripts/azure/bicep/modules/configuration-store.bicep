import { RoleAssignment, recordsDiagnosticSettingName } from './types.bicep'

@minLength(5)
@maxLength(50)
param name string

param location string

@allowed(['Free', 'Developer', 'Standard', 'Premium'])
param sku string

param tags object

param roleAssignments RoleAssignment[]

param recordsStorageAccountId string

// Soft delete and purge protection exist only on the Standard and Premium tiers; the
// resource provider rejects the properties on Free and Developer stores.
var retentionProperties = contains(['Standard', 'Premium'], sku)
  ? {
      enablePurgeProtection: true
      softDeleteRetentionInDays: 7
    }
  : {}

resource store 'Microsoft.AppConfiguration/configurationStores@2024-06-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    ...retentionProperties
  }
}

resource storeRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for assignment in roleAssignments: {
    name: guid(store.id, assignment.principalId, assignment.roleDefinitionId)
    scope: store
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
resource storeRecords 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: recordsDiagnosticSettingName
  scope: store
  properties: {
    storageAccountId: recordsStorageAccountId
    logs: [
      {
        category: 'Audit'
        enabled: true
      }
      {
        category: 'HttpRequest'
        enabled: true
      }
    ]
  }
}

output endpoint string = store.properties.endpoint
output id string = store.id
