@description('User name for administrator login')
param administratorLogin string

@description('Password for administrator password login. Required on creation time regardless of authentication method configuration')
@secure()
param administratorLoginPassword string
param location string
param serverName string
param serverEdition string = 'GeneralPurpose'
param skuSizeGB int = 256
param dbInstanceType string = 'Standard_D4ds_v4'
param availabilityZone string = '1'
param version string = '14'

resource serverName_resource 'Microsoft.DBforPostgreSQL/flexibleServers@2021-06-01' = {
  name: serverName
  location: location
  sku: {
    name: dbInstanceType
    tier: serverEdition
  }
  properties: {
    version: version
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    highAvailability: {
      mode: 'Disabled'
    }
    storage: {
      storageSizeGB: skuSizeGB
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    availabilityZone: availabilityZone
  }
}
