targetScope = 'subscription'

@description('Environment name used for tagging and deployment metadata.')
@minLength(3)
@maxLength(25)
param environmentName string

@description('Optional six-character lowercase suffix used for deterministic resource naming. The deployment script derives this from the subscription and environment.')
@minLength(0)
@maxLength(6)
param nameSuffix string = ''

@description('Azure region for regional resources.')
param location string = 'eastus2'

@description('Azure region for Static Web Apps.')
param staticWebAppLocation string = 'centralus'

@description('Session identifier used for traceability.')
param sessionId string = deployment().name

@description('Display name of the person or automation performing deployment.')
param deployedBy string = 'azure-advisor-deploy'

@description('RFC3339 timestamp for deployment metadata.')
param createdAt string = utcNow('u')

var uniqueSuffix = empty(nameSuffix)
  ? take(uniqueString(subscription().id, environmentName), 6)
  : nameSuffix

var resourceGroupName = 'rg-${environmentName}-${uniqueSuffix}'
var swaName = 'swa-${environmentName}-${uniqueSuffix}'
var managedEnvironmentName = 'cae-${environmentName}-${uniqueSuffix}'
var containerAppName = 'ca-${environmentName}-${uniqueSuffix}'
var acrName = take(toLower(replace('cr${environmentName}${uniqueSuffix}', '-', '')), 50)
var vnetName = 'vnet-${environmentName}-${uniqueSuffix}'
var privateEndpointName = 'pe-acr-${environmentName}-${uniqueSuffix}'

@description('Private DNS zone name for ACR private link.')
param privateDnsZoneName string = 'privatelink.azurecr.io'

var userAssignedIdentityName = 'id-${environmentName}-${uniqueSuffix}'

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

@description('Optional Azure AI Foundry account endpoint used for recommendation enrichment. Leave empty for deterministic explanations only.')
param foundryEndpoint string = ''

@description('Optional resource group containing an existing Azure AI Foundry account.')
param foundryResourceGroupName string = ''

@description('Optional existing Azure AI Foundry account name.')
param foundryAccountName string = ''

@description('Azure AI Foundry model deployment name.')
param foundryDeploymentName string = 'gpt-5.4-mini'

@description('Optional Azure AI Foundry project name shown in the UI.')
param foundryProjectName string = ''

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

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'staticWebApp'
  scope: resourceGroup
  params: {
    name: swaName
    location: staticWebAppLocation
    tags: baseTags
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
    foundryEndpoint: foundryEndpoint
    foundryDeploymentName: foundryDeploymentName
    foundryProjectName: foundryProjectName
    staticWebAppClientId: staticWebApp.outputs.defaultHostname
    corsAllowedOrigin: empty(allowedFrontendOrigin)
      ? 'https://${staticWebApp.outputs.defaultHostname}'
      : allowedFrontendOrigin
  }
}

module linkedBackend 'modules/linked-backend.bicep' = {
  name: 'linkedBackend'
  scope: resourceGroup
  params: {
    staticWebAppId: staticWebApp.outputs.staticWebAppId
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

module foundryRoleAssignment 'modules/foundry-role-assignment.bicep' = if (!empty(foundryAccountName) && !empty(foundryResourceGroupName) && !empty(foundryEndpoint)) {
  name: 'foundryRoleAssignment'
  scope: az.resourceGroup(foundryResourceGroupName)
  params: {
    foundryAccountName: foundryAccountName
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

output deployedResourceGroupName string = resourceGroupName
output deployedStaticWebAppName string = swaName
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
output deployedContainerAppName string = containerAppName
output containerAppFqdn string = containerApp.outputs.fqdn
output deployedAcrName string = acrName
output managedIdentityPrincipalId string = identity.outputs.principalId
