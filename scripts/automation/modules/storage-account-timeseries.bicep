param storageAccountNameTimeseries string
param location string
param objectIdEnterpriseApplication string

resource storageAccountAnon 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountNameTimeseries
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}

var roleDefinitionId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1' // Storage Blob Data Reader. See ids here: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
var roleAssignmentNameStorageAccount = guid(objectIdEnterpriseApplication, roleDefinitionId, resourceGroup().id)
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleAssignmentNameStorageAccount
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: objectIdEnterpriseApplication
  }
}
