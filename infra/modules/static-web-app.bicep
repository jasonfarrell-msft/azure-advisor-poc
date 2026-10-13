param name string
param location string
param tags object
param skuName string = 'Standard'

resource staticWebApp 'Microsoft.Web/staticSites@2025-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {}
}

output staticWebAppId string = staticWebApp.id
output defaultHostname string = staticWebApp.properties.defaultHostname
