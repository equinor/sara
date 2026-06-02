using 'infrastructure.bicep'
param environment = 'sarastaging'
param resourceGroupName = '${environment}'

param location = 'northeurope'
param objectIdFgRobots = '5ac08731-48dd-4499-9151-7bf6b8ab8eac'

param objectIdEnterpriseApplication = '7b6946f3-eefd-4036-acd2-d8ba2782e9eb' // ObjectID enterprise application sara-staging

param keyVaultName = '${environment}-kv'

param storageAccountNameAnon = '${environment}storeanon'
param storageAccountNameRaw = '${environment}storeraw'
param storageAccountNameVis = '${environment}storevis'
param storageAccountNameThermalRef = '${environment}thermalref'
param storageAccountNameTimeseries = '${environment}storetime'

// Grant Flotilla (FlotillaBackendAuthStaging) role assignment as "Storage Blob Data Reader" to storageanon account
param principalIdFlotillaApp = 'e7c92357-387d-4c11-a88f-638828a5d4dd' // ObjectID enterprise application FlotillaBackendAuthStaging
