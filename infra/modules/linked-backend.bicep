param staticWebAppId string
param linkName string
param backendResourceId string
param region string
param environmentName string = 'default'

var staticWebAppName = last(split(staticWebAppId, '/'))

resource staticWebApp 'Microsoft.Web/staticSites@2025-05-01' existing = {
  name: staticWebAppName
}

resource staticWebAppEnvironment 'Microsoft.Web/staticSites/builds@2025-05-01' existing = {
  parent: staticWebApp
  name: environmentName
}

resource linkedBackend 'Microsoft.Web/staticSites/builds/linkedBackends@2025-05-01' = {
  parent: staticWebAppEnvironment
  name: linkName
  properties: {
    backendResourceId: backendResourceId
    region: region
  }
}

output linkedBackendId string = linkedBackend.id
