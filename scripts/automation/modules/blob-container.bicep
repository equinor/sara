param storageAccountName string = 'YourStorageAccountNameHere'
param containerName string = 'YourContainerNameHere'

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccountName}/default/${containerName}'
  properties: {
    publicAccess: 'None'
  }
}
