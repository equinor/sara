using 'infrastructure.bicep'
param environment = 'YourEnvName'
param resourceGroupName = 'IDA${environment}'

param location = 'northeurope'
param objectIdFgRobots = '5ac08731-48dd-4499-9151-7bf6b8ab8eac'

param keyVaultName = 'idakv-${environment}'

param administratorLogin = 'idapostgresqlserver_${environment}'
param administratorLoginPassword = ''

param serverName = 'idaserver${environment}'
param postgresConnectionString = ''

param storageAccountNameAnon = 'storageanon1${environment}'

param storageAccountNameRaw = 'storageraw1${environment}'

param storageAccountNameVis = 'storagevis1${environment}'
