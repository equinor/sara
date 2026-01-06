param thermalReadingStorageAccount string
param location string

resource storageAccountThermalRef 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: thermalReadingStorageAccount
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {}
}
