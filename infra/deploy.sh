#!/usr/bin/env bash
# STEP 1 of 3: creates the Azure resources and connects kubectl to the new cluster.
#
#   What it creates (all described in infra/*.bicep):
#     - the AKS cluster (2 nodes), with Entra ID login instead of an admin password
#     - a storage account for the nightly database backups, and the identity the backup uses
#     - a budget that e-mails me before the student credit runs out
#
#   Costs money: about kr 35 per day while it exists. Remove everything with ./deploy/teardown.sh
#   Safe to run again: Bicep only changes what differs from the files.
#   Before the first run: `az login`, and put your own e-mail and object ID in infra/main.bicepparam.
#
#   Next: ./deploy/install.sh

set -euo pipefail               # stop at the first command that fails
cd "$(dirname "$0")/.."         # run from the repo root, wherever the script is started from

RESOURCE_GROUP=rg-delay-radar
CLUSTER=aks-delay-radar
LOCATION=${LOCATION:-swedencentral}   # student subscriptions may not use small VMs in every region

# 1. A resource group is a folder in Azure; everything else is created inside it.
echo "==> Resource group $RESOURCE_GROUP in $LOCATION"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

# 2. Create or update everything in infra/main.bicep. Prints the backup storage name and identity ID.
#    (To preview changes first: az deployment group what-if -g rg-delay-radar -f infra/main.bicep -p infra/main.bicepparam)
echo "==> Bicep: AKS, backup storage, identity, budget (first run takes ~5-8 min)"
az deployment group create -g "$RESOURCE_GROUP" --name "delay-radar-$(date +%Y%m%d-%H%M)" \
  -f infra/main.bicep -p infra/main.bicepparam --query properties.outputs -o json

# 3. Give kubectl the cluster's address, and log in with my Azure account (kubelogin) instead of a password.
echo "==> Connecting kubectl"
az aks get-credentials -g "$RESOURCE_GROUP" -n "$CLUSTER" --overwrite-existing
kubelogin convert-kubeconfig -l azurecli
kubectl get nodes               # proves the login works: both nodes should be Ready

echo "==> Done. Next: ./deploy/install.sh"
