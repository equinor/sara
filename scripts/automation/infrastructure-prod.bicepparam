using 'infrastructure.bicep'
param environment = 'saraprod'
param resourceGroupName = '${environment}'

param location = 'northeurope'
param objectIdFgRobots = '5ac08731-48dd-4499-9151-7bf6b8ab8eac'

param objectIdEnterpriseApplication = '49613d59-1f36-4835-8dc4-caff1591c8e9' // ObjectID enterprise application sara-prod

param keyVaultName = '${environment}-kv'

param storageAccountNameAnon = '${environment}storeanon'
param storageAccountNameRaw = '${environment}storeraw'
param storageAccountNameVis = '${environment}storevis'
param storageAccountNameThermalRef = '${environment}thermalref'
param storageAccountNameTimeseries = '${environment}storetime'

// Grant Flotilla (FlotillaBackendAuthProd) role assignment as "Storage Blob Data Reader" to storageanon account
param principalIdFlotillaApp = '17c50841-e5d6-4eae-a74b-98d0d2e0b592' // ObjectID enterprise application FlotillaBackendAuthProd
