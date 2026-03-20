#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
template_file="${repo_root}/infra/aks/main.bicep"

subscription_id="${AKS_SUBSCRIPTION_ID:-${AZURE_SUBSCRIPTION_ID:-}}"
resource_group="${AKS_RESOURCE_GROUP:-}"
location="${AKS_LOCATION:-${AZURE_REGION:-}}"
cluster_name="${AKS_NAME:-devpod-aks}"
dns_prefix="${AKS_DNS_PREFIX:-${cluster_name}}"
kubernetes_version="${AKS_KUBERNETES_VERSION:-}"
sku_tier="${AKS_SKU_TIER:-Free}"
linux_admin_username="${AKS_LINUX_ADMIN_USERNAME:-azureuser}"
node_vm_size="${AKS_NODE_VM_SIZE:-Standard_D4s_v3}"
node_count="${AKS_NODE_COUNT:-1}"
min_node_count="${AKS_MIN_NODE_COUNT:-1}"
max_node_count="${AKS_MAX_NODE_COUNT:-3}"
os_disk_size_gb="${AKS_OS_DISK_SIZE_GB:-128}"
max_pods="${AKS_MAX_PODS:-110}"
service_cidr="${AKS_SERVICE_CIDR:-10.0.0.0/16}"
dns_service_ip="${AKS_DNS_SERVICE_IP:-10.0.0.10}"
pod_cidr="${AKS_POD_CIDR:-192.168.0.0/16}"
upgrade_channel="${AKS_UPGRADE_CHANNEL:-patch}"
node_os_upgrade_channel="${AKS_NODE_OS_UPGRADE_CHANNEL:-NodeImage}"

if [[ -z "${subscription_id}" ]]; then
  echo "AKS_SUBSCRIPTION_ID or AZURE_SUBSCRIPTION_ID must be set." >&2
  exit 1
fi

if [[ -z "${resource_group}" ]]; then
  if [[ -n "${AZURE_RESOURCE_GROUP:-}" ]]; then
    echo "AKS_RESOURCE_GROUP must be set explicitly. Refusing to reuse AZURE_RESOURCE_GROUP=${AZURE_RESOURCE_GROUP} because this repo keeps AKS resources isolated and unambiguous." >&2
  else
    echo "AKS_RESOURCE_GROUP must be set." >&2
  fi
  exit 1
fi

if [[ -z "${location}" ]]; then
  echo "AKS_LOCATION or AZURE_REGION must be set." >&2
  exit 1
fi

ssh_public_key="${AKS_SSH_PUBLIC_KEY:-}"
ssh_public_key_file="${AKS_SSH_PUBLIC_KEY_FILE:-}"

if [[ -z "${ssh_public_key}" ]]; then
  if [[ -n "${ssh_public_key_file}" ]]; then
    if [[ ! -f "${ssh_public_key_file}" ]]; then
      echo "AKS_SSH_PUBLIC_KEY_FILE does not exist: ${ssh_public_key_file}" >&2
      exit 1
    fi
    ssh_public_key="$(<"${ssh_public_key_file}")"
  else
    for candidate in "${HOME}/.ssh/id_ed25519.pub" "${HOME}/.ssh/id_rsa.pub"; do
      if [[ -f "${candidate}" ]]; then
        ssh_public_key="$(<"${candidate}")"
        break
      fi
    done
  fi
fi

if [[ -z "${ssh_public_key}" ]]; then
  echo "Set AKS_SSH_PUBLIC_KEY or AKS_SSH_PUBLIC_KEY_FILE, or create ~/.ssh/id_ed25519.pub or ~/.ssh/id_rsa.pub." >&2
  exit 1
fi

echo "Using subscription: ${subscription_id}"
echo "Using resource group: ${resource_group}"
echo "Using location: ${location}"
echo "Deploying AKS cluster: ${cluster_name}"

az account set --subscription "${subscription_id}"
az group create \
  --name "${resource_group}" \
  --location "${location}" \
  --output none

deployment_name="aks-${cluster_name}-$(date +%Y%m%d%H%M%S)"

az deployment group create \
  --name "${deployment_name}" \
  --resource-group "${resource_group}" \
  --template-file "${template_file}" \
  --parameters \
    clusterName="${cluster_name}" \
    location="${location}" \
    dnsPrefix="${dns_prefix}" \
    kubernetesVersion="${kubernetes_version}" \
    skuTier="${sku_tier}" \
    linuxAdminUsername="${linux_admin_username}" \
    sshPublicKey="${ssh_public_key}" \
    nodeVmSize="${node_vm_size}" \
    nodeCount="${node_count}" \
    minNodeCount="${min_node_count}" \
    maxNodeCount="${max_node_count}" \
    osDiskSizeGb="${os_disk_size_gb}" \
    maxPods="${max_pods}" \
    serviceCidr="${service_cidr}" \
    dnsServiceIp="${dns_service_ip}" \
    podCidr="${pod_cidr}" \
    upgradeChannel="${upgrade_channel}" \
    nodeOsUpgradeChannel="${node_os_upgrade_channel}"

echo
echo "AKS deployment complete."
echo "Next:"
echo "  az aks get-credentials --resource-group ${resource_group} --name ${cluster_name} --overwrite-existing"
echo "  kubectl get nodes"
