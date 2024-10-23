param location string
param managedIdentityName string

@description('Specifies the role definition ID used in the role assignment.')
param roleDefinitionID string

@description('Specifies the principal ID assigned to the role.') //object ID
param principalId string

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
  tags: {}
}

var roleAssignmentName = guid(principalId, roleDefinitionID, resourceGroup().id)
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleAssignmentName
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionID)
    principalId: principalId
  }
}

output name string = roleAssignment.name
output resourceGroupName string = resourceGroup().name
output resourceId string = roleAssignment.id
output objectIdManagedIdentity string = managedIdentity.properties.principalId
