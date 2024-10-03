param storageAccountNameVis string
param location string

resource storageAccountVis 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountNameVis
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}
