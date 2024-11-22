param storageAccountNameAnon string
param location string

@description('Specifies the role definition ID used in the role assignment.')
param roleDefinitionIDFlotillaApp string

@description('Specifies the principal ID assigned to the role.') //objectID enterprise application
param principalIdFlotillaApp string

resource storageAccountAnon 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountNameAnon
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}

var roleAssignmentNameStorageAccount = guid(principalIdFlotillaApp, roleDefinitionIDFlotillaApp, resourceGroup().id)
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleAssignmentNameStorageAccount
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionIDFlotillaApp)
    principalId: principalIdFlotillaApp
  }
}
