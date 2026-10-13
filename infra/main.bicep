targetScope = 'subscription'

@description('Environment name used for tagging and deployment metadata.')
param environmentName string

@description('Azure region for regional resources.')
param location string

@description('Session identifier used for traceability.')
param sessionId string

@description('Display name of the person or automation performing deployment.')
param deployedBy string

@description('RFC3339 timestamp for deployment metadata.')
param createdAt string

@description('Resource group name for this deployment.')
param resourceGroupName string = 'rg-azureadvisor-eus201'

@description('Static Web App name.')
param swaName string = 'swa-azure-advisor-dev-46b2'

@description('Resource group containing the existing Static Web App.')
param swaResourceGroupName string = 'rg-azureadvisor-cus01'

@description('Container Apps managed environment name.')
param managedEnvironmentName string = 'cae-azure-advisor-dev-46b2-eus2'

@description('Container App name.')
param containerAppName string = 'ca-azure-advisor-dev-46b2-eus2'

@description('Container Registry name.')
param acrName string = 'crazureadvisordev46b2eus2'

@description('Virtual Network name.')
param vnetName string = 'vnet-azure-advisor-dev-46b2-eus2'

@description('Private Endpoint name for ACR.')
param privateEndpointName string = 'pe-acr-azure-advisor-dev-46b2-eus2'

@description('Private DNS zone name for ACR private link.')
param privateDnsZoneName string = 'privatelink.azurecr.io'

@description('User-assigned managed identity name.')
param userAssignedIdentityName string = 'id-azure-advisor-dev-46b2-eus2'

@description('Address prefix for virtual network.')
param vnetAddressPrefix string = '10.42.0.0/16'

@description('Infrastructure subnet name for Container Apps environment.')
param infrastructureSubnetName string = 'snet-aca-infra'

@description('Address prefix for Container Apps infrastructure subnet.')
param infrastructureSubnetPrefix string = '10.42.0.0/23'

@description('Private endpoint subnet name.')
param privateEndpointSubnetName string = 'snet-private-endpoints'

@description('Address prefix for private endpoint subnet.')
param privateEndpointSubnetPrefix string = '10.42.2.0/24'

@description('Container image to run in Container Apps. Placeholder image is used for phase-1 infrastructure deploys.')
param containerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container Apps CPU allocation, including fractional vCPU values.')
param backendCpu string = '0.5'

@description('Container Apps memory allocation.')
param backendMemory string = '1Gi'

@description('Container app target port.')
param backendContainerPort int = 8080

@description('Static Web App HTTPS origin used in backend CORS configuration. Leave empty to derive from Static Web App default hostname.')
param allowedFrontendOrigin string = ''

@description('Controls Reader role assignment at subscription scope. Keep false until the SWA auth routes and operator role are deployed and verified.')
param enableSubscriptionReaderAssignment bool = false

@description('Temporarily allow ACR public network access for authenticated image bootstrap. Keep disabled by default.')
param acrPublicNetworkAccessEnabled bool = false

@description('Optional client IP/CIDR allowed during an explicitly approved ACR bootstrap window.')
param acrBootstrapIpAddress string = ''

var baseTags = {
  'app-onboard-skill': 'true'
  'app-onboard-session-id': sessionId
  'created-at': createdAt
  'deployed-by': deployedBy
  environment: environmentName
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: baseTags
}

resource swaResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' existing = {
  name: swaResourceGroupName
}

resource existingStaticWebApp 'Microsoft.Web/staticSites@2025-05-01' existing = {
  name: swaName
  scope: swaResourceGroup
}

module network 'modules/network.bicep' = {
  name: 'network'
  scope: resourceGroup
  params: {
    name: vnetName
    location: location
    tags: baseTags
    addressPrefix: vnetAddressPrefix
    infrastructureSubnetName: infrastructureSubnetName
    infrastructureSubnetPrefix: infrastructureSubnetPrefix
    privateEndpointSubnetName: privateEndpointSubnetName
    privateEndpointSubnetPrefix: privateEndpointSubnetPrefix
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  scope: resourceGroup
  params: {
    name: userAssignedIdentityName
    location: location
    tags: baseTags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  scope: resourceGroup
  params: {
    name: acrName
    location: location
    tags: baseTags
    publicNetworkAccessEnabled: acrPublicNetworkAccessEnabled
    bootstrapIpAddress: acrBootstrapIpAddress
  }
}

module privateDns 'modules/private-dns.bicep' = {
  name: 'privateDns'
  scope: resourceGroup
  params: {
    name: privateDnsZoneName
    tags: baseTags
    virtualNetworkId: network.outputs.virtualNetworkId
    virtualNetworkLinkName: 'lnk-acr-vnet'
  }
}

module privateEndpointAcr 'modules/private-endpoint-acr.bicep' = {
  name: 'privateEndpointAcr'
  scope: resourceGroup
  params: {
    name: privateEndpointName
    location: location
    tags: baseTags
    subnetId: network.outputs.privateEndpointSubnetId
    acrResourceId: acr.outputs.acrId
    privateDnsZoneId: privateDns.outputs.privateDnsZoneId
  }
}

module managedEnvironment 'modules/managed-environment.bicep' = {
  name: 'managedEnvironment'
  scope: resourceGroup
  params: {
    name: managedEnvironmentName
    location: location
    tags: baseTags
    infrastructureSubnetId: network.outputs.infrastructureSubnetId
  }
}

module containerApp 'modules/container-app.bicep' = {
  name: 'containerApp'
  scope: resourceGroup
  params: {
    name: containerAppName
    location: location
    tags: baseTags
    managedEnvironmentId: managedEnvironment.outputs.managedEnvironmentId
    userAssignedIdentityId: identity.outputs.identityId
    userAssignedIdentityClientId: identity.outputs.clientId
    acrLoginServer: acr.outputs.loginServer
    containerImage: containerImage
    cpu: backendCpu
    memory: backendMemory
    containerPort: backendContainerPort
    healthProbePath: '/api/settings/mode'
    corsAllowedOrigin: empty(allowedFrontendOrigin)
      ? 'https://${existingStaticWebApp.properties.defaultHostname}'
      : allowedFrontendOrigin
  }
}

module linkedBackend 'modules/linked-backend.bicep' = {
  name: 'linkedBackend'
  scope: swaResourceGroup
  params: {
    staticWebAppName: swaName
    linkName: containerAppName
    backendResourceId: containerApp.outputs.containerAppId
    region: location
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'roleAssignments'
  scope: resourceGroup
  dependsOn: [
    acr
  ]
  params: {
    acrName: acrName
    managedIdentityPrincipalId: identity.outputs.principalId
  }
}

var readerRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'acdd72a7-3385-48ef-bd42-f606fba81ae7'
)

resource subscriptionReaderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableSubscriptionReaderAssignment) {
  name: guid(subscription().id, userAssignedIdentityName, readerRoleDefinitionId)
  scope: subscription()
  dependsOn: [
    linkedBackend
  ]
  properties: {
    roleDefinitionId: readerRoleDefinitionId
    principalId: identity.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}
