param name string
param location string
param tags object
param infrastructureSubnetId string

resource managedEnvironment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: infrastructureSubnetId
      internal: false
    }
  }
}

output managedEnvironmentId string = managedEnvironment.id
