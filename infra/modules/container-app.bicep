param name string
param location string
param tags object
param managedEnvironmentId string
param userAssignedIdentityId string
param userAssignedIdentityClientId string
param acrLoginServer string
param containerImage string
param cpu string = '0.5'
param memory string = '1Gi'
param containerPort int = 8080
param healthProbePath string = '/api/settings/mode'
param corsAllowedOrigin string

var isPlaceholder = containerImage == 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
var effectivePort = isPlaceholder ? 80 : containerPort
var effectiveHealthProbePath = isPlaceholder ? '/' : healthProbePath

resource containerApp 'Microsoft.App/containerApps@2026-07-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: effectivePort
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: isPlaceholder
        ? []
        : [
            {
              server: acrLoginServer
              identity: userAssignedIdentityId
            }
          ]
      secrets: []
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:${containerPort}'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: corsAllowedOrigin
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: userAssignedIdentityClientId
            }
            {
              name: 'AzureAiFoundry__Endpoint'
              // Hosted Foundry is disabled because the configured instance is outside Central US.
              value: ''
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: effectiveHealthProbePath
                port: effectivePort
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: effectiveHealthProbePath
                port: effectivePort
              }
              initialDelaySeconds: 15
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: effectiveHealthProbePath
                port: effectivePort
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output containerAppId string = containerApp.id
