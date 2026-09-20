@export()
type RoleAssignment = {
  principalId: string
  principalType: 'User' | 'Group' | 'ServicePrincipal'
  roleDefinitionId: string
}
