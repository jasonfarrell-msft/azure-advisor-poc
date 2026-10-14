param name string
param location string
param tags object
param publicNetworkAccessEnabled bool = false
param bootstrapIpAddress string = ''

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Premium'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: publicNetworkAccessEnabled ? 'Enabled' : 'Disabled'
    anonymousPullEnabled: false
    networkRuleBypassOptions: publicNetworkAccessEnabled ? 'AzureServices' : 'None'
    networkRuleSet: {
      defaultAction: publicNetworkAccessEnabled && empty(bootstrapIpAddress) ? 'Allow' : 'Deny'
      ipRules: publicNetworkAccessEnabled && !empty(bootstrapIpAddress)
        ? [
            {
              action: 'Allow'
              value: bootstrapIpAddress
            }
          ]
        : []
    }
  }
}

output acrId string = registry.id
output loginServer string = registry.properties.loginServer
