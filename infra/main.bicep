@description('Azure region for all resources')
param location string = resourceGroup().location

param clusterName string = 'aks-delay-radar' // naming: <type>-<workload>, see docs/azure-design.md

@description('Object ID of the user who gets cluster-admin rights')
param adminObjectId string

@description('E-mail for budget alerts')
param budgetEmail string

// Built-in role "Azure Kubernetes Service RBAC Cluster Admin"
var aksRbacClusterAdminRoleId = 'b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b'

resource aks 'Microsoft.ContainerService/managedClusters@2026-06-01' = {
  name: clusterName
  location: location
  sku: {
    name: 'Base'
    tier: 'Free' // Free: no SLA, no control-plane cost
  }
  identity: {
    type: 'SystemAssigned' // the cluster gets its own identity in Entra ID (no passwords)
  }
  properties: {
    kubernetesVersion: '1.36' // minor only: AKS picks the newest patch
    dnsPrefix: 'delay-radar' // part of the API server's address
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        count: 2
        vmSize: 'Standard_B2ls_v2' // Bsv2 family, 2 vCPU / 4 GiB
        osDiskSizeGB: 32
        osDiskType: 'Managed' // Bsv2 has no local temp disk, so "Ephemeral" is not possible
        osSKU: 'AzureLinux' // Microsoft's small container OS (fewer packages = less to patch)
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      networkDataplane: 'cilium'
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'
    }
    autoUpgradeProfile: {
      upgradeChannel: 'patch' // 1.36.x → newest patch, never a new minor by itself
      nodeOSUpgradeChannel: 'NodeImage' // weekly OS security updates for the node
    }
    aadProfile: {
      managed: true
      enableAzureRBAC: true
    }
    disableLocalAccounts: true
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
  }
}

resource adminRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aks.id, adminObjectId, aksRbacClusterAdminRoleId)
  scope: aks                    // the role applies only to the cluster, not the whole subscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', aksRbacClusterAdminRoleId)
    principalId: adminObjectId
    principalType: 'User'
  }
}

module budget 'budget.bicep' = {
  name: 'budget'
  scope: subscription()
  params: {
    budgetEmail: budgetEmail
    amount: 500 // ~$30 expected until teardown, see docs/azure-design.md
    startDate: '2026-10-01' // fixed: a budget's start date should not move on every deployment
  }
}

module backup 'backup.bicep' = {
  name: 'backup'
  params: {
    location: location
    oidcIssuerUrl: aks.properties.oidcIssuerProfile.issuerURL
    adminObjectId: adminObjectId
  }
}

output backupStorageAccount string = backup.outputs.storageAccountName
output backupClientId string = backup.outputs.backupClientId
