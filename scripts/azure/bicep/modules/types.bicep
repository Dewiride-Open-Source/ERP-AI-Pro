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
