using 'infrastructure.bicep'
param environment = 'sarastaging'
param resourceGroupName = '${environment}'

param location = 'northeurope'
param objectIdFgRobots = '5ac08731-48dd-4499-9151-7bf6b8ab8eac'

param objectIdEnterpriseApplication = '7b6946f3-eefd-4036-acd2-d8ba2782e9eb' // ObjectID enterprise application sara-staging

param managedIdentityName = '${environment}-mi'

param keyVaultName = '${environment}-kv'

param storageAccountNameAnon = '${environment}storeanon'

param storageAccountNameRaw = '${environment}storeraw'

param storageAccountNameVis = '${environment}storevis'

param thermalReadingStorageAccount = '${environment}thermalref'

param principalId = 'bf81095d-e13d-481d-a4e8-a5c17faad398' //aurora-aks-kubelet-shared staging environment
param roleDefinitionId = 'f1a07417-d97a-45cb-824c-7a7467783830' // azure built-in role for managed identity operator

// Grant Flotilla (FlotillaBackendAuthStaging) role assignment as "Storage Blob Data Reader" to storageanon account
param roleDefinitionIDFlotillaApp = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1' // Storage Blob Data Reader
param principalIdFlotillaApp = 'e7c92357-387d-4c11-a88f-638828a5d4dd' // ObjectID enterprise application FlotillaBackendAuthStaging
