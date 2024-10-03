param storageAccountNameRaw string
param location string

resource storageAccountRaw 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountNameRaw
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}
