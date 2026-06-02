param location string
param keyVaultName string
param objectIdFgRobots string
param objectIdEnterpriseApplication string

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: keyVaultName
  location: location
  properties: {
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    tenantId: tenant().tenantId
    accessPolicies: []
    sku: {
      name: 'standard'
      family: 'A'
    }
  }
}

resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2024-04-01-preview' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: keyVault.properties.tenantId
        objectId: objectIdFgRobots
        permissions: {
          keys: [
            'list'
            'create'
          ]
          secrets: [
            'set'
            'get'
            'list'
          ]
        }
      }
      {
        tenantId: keyVault.properties.tenantId
        objectId: objectIdEnterpriseApplication
        permissions: {
          keys: []
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}
