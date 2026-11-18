// Nightly pg_dump target: a private blob container, written by the backup pod through
// workload identity (no keys or passwords anywhere).

param location string

@description('OIDC issuer URL of the AKS cluster (who signs the pod tokens)')
param oidcIssuerUrl string

@description('Kubernetes ServiceAccount allowed to log in as the backup identity')
param serviceAccountSubject string = 'system:serviceaccount:delay-radar:backup'

@description('Object ID of the user who may read the backups (restore)')
param adminObjectId string

param retentionDays int = 14

// Built-in role "Storage Blob Data Contributor"
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Globally unique name, stable for this resource group (3-24 lowercase letters/digits)
resource storage 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: 'stradar${uniqueString(resourceGroup().id)}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Cool'               // written once a night, rarely read: cheaper per GB
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false     // no anonymous access, ever
    allowSharedKeyAccess: false      // no account keys: only Entra ID logins (identity + RBAC)
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: storage
  name: 'default'
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: blobService
  name: 'backups'
}

// Azure deletes old dumps by itself, so storage never grows without limit
resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2025-06-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'delete-old-backups'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['backups/']
            }
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: retentionDays
                }
              }
            }
          }
        }
      ]
    }
  }
}

// The Azure identity the backup pod becomes
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-delay-radar-backup'
  location: location
}

// The trust: tokens from this AKS cluster, for exactly this ServiceAccount, may log in as the identity
resource federation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: identity
  name: 'aks-backup-serviceaccount'
  properties: {
    issuer: oidcIssuerUrl
    subject: serviceAccountSubject
    audiences: ['api://AzureADTokenExchange']
  }
}

// The permission: write to this one container only (not the whole storage account)
resource backupWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(container.id, identity.id, blobDataContributorRoleId)
  scope: container
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Me: list and download backups for a restore (Owner alone cannot read blob data)
resource adminReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(container.id, adminObjectId, blobDataContributorRoleId)
  scope: container
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: adminObjectId
    principalType: 'User'
  }
}

output storageAccountName string = storage.name
output backupClientId string = identity.properties.clientId
