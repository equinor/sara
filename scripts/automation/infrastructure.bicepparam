using 'infrastructure.bicep'

param resourceGroupName = 'IDA-${environment}'

param environment = 'YourEnvName'
param location = 'northeurope'
param objectIdFgRobots = '5ac08731-48dd-4499-9151-7bf6b8ab8eac'

param keyVaultName = 'ida1-${environment}'

param administratorLogin = 'idapostgresqlserver_${environment}'
param administratorLoginPassword = ''
param serverName = 'ida-server-${environment}'

param storageAccountNameAnon = 'storageanon1${environment}'
param storageAccountNameRaw = 'storageraw1${environment}'
param storageAccountNameVis = 'storagevis1${environment}'
