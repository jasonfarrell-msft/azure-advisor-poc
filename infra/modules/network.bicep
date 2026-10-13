param name string
param location string
param tags object
param addressPrefix string
param infrastructureSubnetName string
param infrastructureSubnetPrefix string
param privateEndpointSubnetName string
param privateEndpointSubnetPrefix string

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2026-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: infrastructureSubnetName
        properties: {
          addressPrefix: infrastructureSubnetPrefix
          delegations: [
            {
              name: 'aca-environment-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

output virtualNetworkId string = virtualNetwork.id
output infrastructureSubnetId string = resourceId(
  'Microsoft.Network/virtualNetworks/subnets',
  name,
  infrastructureSubnetName
)
output privateEndpointSubnetId string = resourceId(
  'Microsoft.Network/virtualNetworks/subnets',
  name,
  privateEndpointSubnetName
)
