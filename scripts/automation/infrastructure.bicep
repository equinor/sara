targetScope = 'subscription'

param environment string
param resourceGroupName string = 'IDA${environment}'
param location string

param storageAccountNameRaw string

param storageAccountNameAnon string

param storageAccountNameVis string

param keyVaultName string
param objectIdFgRobots string

param administratorLogin string
@secure()
param administratorLoginPassword string
param postgresConnectionString string
param serverName string

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module storageAccountRaw 'modules/storage-account-raw.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-raw'
  params: {
    location: location
    storageAccountNameRaw: storageAccountNameRaw
  }
}

module storageAccountAnon 'modules/storage-account-anon.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-anon'
  params: {
    location: location
    storageAccountNameAnon: storageAccountNameAnon
  }
}

module storageAccountVis 'modules/storage-account-visualize.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-sa-vis'
  params: {
    location: location
    storageAccountNameVis: storageAccountNameVis
  }
}

module keyVault 'modules/key-vault.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-kv'
  params: {
    location: location
    keyVaultName: keyVaultName
    objectIdFgRobots: objectIdFgRobots
    secrets: [
      { name: 'administratorLoginPassword', value: administratorLoginPassword }
      { name: 'Database--postgresConnectionString', value: postgresConnectionString }
    ]
  }
}

module postgreSQLFlexibleServer 'modules/db-postgreSQL-flexibleserver.bicep' = {
  scope: resourceGroup
  name: 'infrastructure-db'
  params: {
    location: location
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    serverName: serverName
  }
}
