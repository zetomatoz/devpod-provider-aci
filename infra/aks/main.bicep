@description('Azure region for the AKS cluster.')
param location string = resourceGroup().location

@description('AKS cluster name.')
param clusterName string

@description('Optional Kubernetes version. Leave empty to let AKS choose the default supported version.')
param kubernetesVersion string = ''

@description('DNS prefix for the cluster API server endpoint.')
param dnsPrefix string = clusterName

@description('AKS SKU tier.')
@allowed([
  'Free'
  'Standard'
])
param skuTier string = 'Free'

@description('Admin username for Linux worker nodes.')
param linuxAdminUsername string = 'azureuser'

@description('SSH public key for Linux worker nodes.')
@secure()
param sshPublicKey string

@description('System node pool name.')
param systemPoolName string = 'system'

@description('VM size for the system node pool.')
param nodeVmSize string = 'Standard_D4s_v3'

@description('Initial node count for the system node pool.')
@minValue(1)
param nodeCount int = 1

@description('Minimum autoscaler node count.')
@minValue(1)
param minNodeCount int = 1

@description('Maximum autoscaler node count.')
@minValue(1)
param maxNodeCount int = 3

@description('OS disk size in GiB for worker nodes.')
@minValue(30)
param osDiskSizeGb int = 128

@description('Maximum pods per node.')
@minValue(30)
param maxPods int = 110

@description('Service CIDR for the cluster.')
param serviceCidr string = '10.0.0.0/16'

@description('DNS service IP inside the service CIDR range.')
param dnsServiceIp string = '10.0.0.10'

@description('Pod CIDR used by Azure CNI Overlay.')
param podCidr string = '192.168.0.0/16'

@description('Cluster auto-upgrade channel.')
@allowed([
  'patch'
  'stable'
  'rapid'
  'node-image'
  'none'
])
param upgradeChannel string = 'patch'

@description('Node OS image auto-upgrade channel.')
@allowed([
  'NodeImage'
  'SecurityPatch'
  'Unmanaged'
  'None'
])
param nodeOsUpgradeChannel string = 'NodeImage'

@description('Optional tags applied to the AKS resource.')
param tags object = {}

var clusterTags = union(tags, {
  workload: 'devpod'
  managedBy: 'bicep'
  environment: 'dev'
})

resource aks 'Microsoft.ContainerService/managedClusters@2025-02-01' = {
  name: clusterName
  location: location
  tags: clusterTags
  sku: {
    name: 'Base'
    tier: skuTier
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: dnsPrefix
    kubernetesVersion: empty(kubernetesVersion) ? null : kubernetesVersion
    enableRBAC: true
    linuxProfile: {
      adminUsername: linuxAdminUsername
      ssh: {
        publicKeys: [
          {
            keyData: sshPublicKey
          }
        ]
      }
    }
    agentPoolProfiles: [
      {
        name: systemPoolName
        mode: 'System'
        count: nodeCount
        vmSize: nodeVmSize
        type: 'VirtualMachineScaleSets'
        osType: 'Linux'
        osSKU: 'Ubuntu'
        osDiskSizeGB: osDiskSizeGb
        maxPods: maxPods
        enableAutoScaling: true
        minCount: minNodeCount
        maxCount: maxNodeCount
        upgradeSettings: {
          maxSurge: '33%'
        }
      }
    ]
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    autoUpgradeProfile: {
      upgradeChannel: upgradeChannel
      nodeOSUpgradeChannel: nodeOsUpgradeChannel
    }
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'
      serviceCidr: serviceCidr
      dnsServiceIP: dnsServiceIp
      podCidr: podCidr
    }
    storageProfile: {
      blobCSIDriver: {
        enabled: true
      }
      diskCSIDriver: {
        enabled: true
      }
      fileCSIDriver: {
        enabled: true
      }
      snapshotController: {
        enabled: true
      }
    }
  }
}

output clusterName string = aks.name
output clusterResourceId string = aks.id
output clusterFqdn string = aks.properties.fqdn
