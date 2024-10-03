param storageAccountNameAnon string
param location string

resource storageAccountAnon 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountNameAnon
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}
