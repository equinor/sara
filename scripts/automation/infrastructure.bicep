targetScope = 'subscription'

param environment string
param resourceGroupName string
param location string
param deploymentId string = newGuid()

param storageAccountNameRaw string
param storageAccountNameVis string
param storageAccountNameThermalRef string
param storageAccountNameTimeseries string

param keyVaultName string
param objectIdFgRobots string

param objectIdEnterpriseApplication string

param principalIdFlotillaApp string

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module storageAccountRaw 'modules/storage-account-raw.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-raw-${deploymentId}'
  params: {
    location: location
    storageAccountNameRaw: storageAccountNameRaw
  }
}


module storageAccountVis 'modules/storage-account-visualize.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-vis-${deploymentId}'
  params: {
    location: location
    storageAccountNameVis: storageAccountNameVis
  }
}

module storageAccountThermal 'modules/storage-account-thermal-ref.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-thermal-${deploymentId}'
  params: {
    location: location
    storageAccountNameThermalRef: storageAccountNameThermalRef
  }
}

module storageAccountTimeseries 'modules/storage-account-timeseries.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-time-${deploymentId}'
  params: {
    location: location
    storageAccountNameTimeseries: storageAccountNameTimeseries
    objectIdEnterpriseApplication: objectIdEnterpriseApplication
  }
}

module keyVault 'modules/key-vault.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-kv-${deploymentId}'
  params: {
    location: location
    keyVaultName: keyVaultName
    objectIdFgRobots: objectIdFgRobots
    objectIdEnterpriseApplication: objectIdEnterpriseApplication
  }
}
