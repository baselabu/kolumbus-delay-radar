#!/usr/bin/env bash
# Removes everything from Azure after taking a final backup to this laptop.
# Irreversible: the database disk and all backups in Blob Storage are deleted with the resource group.
set -euo pipefail
cd "$(dirname "$0")/.."

RESOURCE_GROUP=rg-delay-radar
NS=delay-radar
FINAL_DUMP="$HOME/radar-final-$(date +%F).dump"   # outside the repo: no data or binaries in git

read -rp "This deletes $RESOURCE_GROUP and all its data. Type the resource group name to continue: " answer
[ "$answer" = "$RESOURCE_GROUP" ] || { echo "Aborted."; exit 1; }

echo "==> Final backup to $FINAL_DUMP"
kubectl -n "$NS" exec postgres-0 -- pg_dump -U radar -d radar -Fc > "$FINAL_DUMP"
ls -lh "$FINAL_DUMP"

echo "==> Deleting $RESOURCE_GROUP (cluster, node group MC_*, disks, IPs, storage); takes ~10 min"
az group delete --name "$RESOURCE_GROUP" --yes

echo "==> Deleting the budget (it lives on the subscription, not in the resource group)"
az consumption budget delete --budget-name budget-delay-radar

echo "==> Removing the cluster from ~/.kube/config"
kubectl config delete-context aks-delay-radar || true
kubectl config delete-cluster aks-delay-radar || true

echo "==> Done. Manual steps left:"
echo "  - revoke the GHCR pull token on GitHub (Settings > Developer settings > Personal access tokens)"
echo "  - check Cost Management in a few days: nothing should still be billed"
