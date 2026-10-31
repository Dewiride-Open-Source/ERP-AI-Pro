@export()
type RoleAssignment = {
  principalId: string
  principalType: 'User' | 'Group' | 'ServicePrincipal'
  roleDefinitionId: string
}

@export()
type SecretRoleAssignment = {
  secretName: string
  principalId: string
  principalType: 'User' | 'Group' | 'ServicePrincipal'
  roleDefinitionId: string
}

@export()
type StorageContainer = {
  @minLength(3)
  @maxLength(63)
  name: string

  @minValue(0)
  @maxValue(146000)
  immutabilityPeriodDays: int

  immutabilityPolicyLocked: bool

  roleAssignments: RoleAssignment[]
}

@export()
var recordsDiagnosticSettingName = 'erp-ai-pro-records'
