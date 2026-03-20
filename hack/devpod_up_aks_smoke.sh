#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

subscription_id="${AKS_SUBSCRIPTION_ID:-${AZURE_SUBSCRIPTION_ID:-}}"
resource_group="${AKS_RESOURCE_GROUP:-}"
cluster_name="${AKS_NAME:-devpod-aks}"
kubeconfig_path="${AKS_KUBECONFIG:-/tmp/devpod-aks-kubeconfig}"
devpod_home="${DEVPOD_HOME:-/tmp/devpod-aks-home}"
provider_name="${DEVPOD_PROVIDER_NAME:-devpod-aks}"
workspace_id="${DEVPOD_WORKSPACE_ID:-aks-smoke}"
workspace_path="${DEVPOD_WORKSPACE_PATH:-${repo_root}/samples/aks-smoke}"
kubernetes_namespace="${KUBERNETES_NAMESPACE:-devpod-workspaces}"
kubernetes_context="${KUBERNETES_CONTEXT:-${cluster_name}}"
storage_class="${STORAGE_CLASS:-managed-csi}"
pod_timeout="${POD_TIMEOUT:-20m}"

if [[ -z "${subscription_id}" ]]; then
  echo "AKS_SUBSCRIPTION_ID or AZURE_SUBSCRIPTION_ID must be set." >&2
  exit 1
fi

if [[ -z "${resource_group}" ]]; then
  if [[ -n "${AZURE_RESOURCE_GROUP:-}" ]]; then
    echo "AKS_RESOURCE_GROUP must be set explicitly. Refusing to reuse AZURE_RESOURCE_GROUP=${AZURE_RESOURCE_GROUP} because the smoke workflow expects dedicated AKS resources." >&2
  else
    echo "AKS_RESOURCE_GROUP must be set." >&2
  fi
  exit 1
fi

if [[ ! -d "${workspace_path}" ]]; then
  echo "Workspace path does not exist: ${workspace_path}" >&2
  exit 1
fi

echo "Using subscription: ${subscription_id}"
echo "Using resource group: ${resource_group}"
echo "Using cluster: ${cluster_name}"
echo "Using kubeconfig: ${kubeconfig_path}"
echo "Using DevPod home: ${devpod_home}"
echo "Using DevPod provider entry: ${provider_name}"

az account set --subscription "${subscription_id}"
az aks get-credentials \
  --resource-group "${resource_group}" \
  --name "${cluster_name}" \
  --file "${kubeconfig_path}" \
  --overwrite-existing \
  --output none

export DEVPOD_HOME="${devpod_home}"
export KUBECONFIG="${kubeconfig_path}"

if ! devpod provider options "${provider_name}" >/dev/null 2>&1; then
  devpod provider add kubernetes --name "${provider_name}" --use
fi

devpod provider set-options "${provider_name}" \
  -o "KUBERNETES_CONFIG=${kubeconfig_path}" \
  -o "KUBERNETES_CONTEXT=${kubernetes_context}" \
  -o "KUBERNETES_NAMESPACE=${kubernetes_namespace}" \
  -o "STORAGE_CLASS=${storage_class}" \
  -o "POD_TIMEOUT=${pod_timeout}"

devpod up "${workspace_path}" \
  --provider "${provider_name}" \
  --id "${workspace_id}" \
  --ide none

echo
echo "Smoke workspace is available."
echo "Use one of these commands next:"
echo "  DEVPOD_HOME=${devpod_home} devpod list"
echo "  DEVPOD_HOME=${devpod_home} devpod ssh ${workspace_id}"
