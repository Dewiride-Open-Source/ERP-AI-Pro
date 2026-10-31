import { RoleAssignment } from './types.bicep'

@minLength(5)
@maxLength(50)
param name string

param location string

@allowed(['Free', 'Developer', 'Standard', 'Premium'])
param sku string

param tags object

param roleAssignments RoleAssignment[]

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

output endpoint string = store.properties.endpoint
output id string = store.id
