param foundryAccountName string
param managedIdentityPrincipalId string

var cognitiveServicesOpenAiUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
)

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName
}

resource foundryInferenceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, managedIdentityPrincipalId, cognitiveServicesOpenAiUserRoleDefinitionId)
  scope: foundryAccount
  properties: {
    roleDefinitionId: cognitiveServicesOpenAiUserRoleDefinitionId
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
