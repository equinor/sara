param location string
param keyVaultName string
param objectIdFgRobots string
param secrets array

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
    ]
  }
}

resource keyVaultSecret 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = [
  for secret in secrets: {
    name: secret.name
    parent: keyVault
    properties: {
      value: secret.value
    }
  }
]
