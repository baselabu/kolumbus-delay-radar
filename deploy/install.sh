#!/usr/bin/env bash
# STEP 2 of 3: installs everything inside the AKS cluster created by ./infra/deploy.sh.
#
#   In this order:
#     1. check that the backup settings in git match Azure
#     2. Gateway API     - teaches Kubernetes the "front door" objects (Gateway, HTTPRoute)
#     3. Traefik         - the front door itself: public IP and the address *.cloudapp.azure.com
#     4. cert-manager    - gets and renews the HTTPS certificate from Let's Encrypt
#     5. Prometheus + Grafana - monitoring
#     6. passwords (Secrets) - created only if they don't exist yet
#     7. my app: PostgreSQL, collector, API, firewall rules, backup job, dashboard
#
#   Safe to run again: things that already exist are left alone or updated, never recreated.
#   Not included: loading old data into the database (pg_restore), see report.md.
#
#   Next: ./deploy/verify.sh

set -euo pipefail               # stop at the first command that fails
cd "$(dirname "$0")/.."         # run from the repo root, wherever the script is started from

RESOURCE_GROUP=rg-delay-radar
NS=delay-radar                  # the namespace my app lives in
GHCR_USER=${GHCR_USER:-baselabu}  # GitHub user that owns the image pull token

# Pinned versions: a re-run installs exactly what was tested
GATEWAY_API_VERSION=v1.6.3      # the Gateway API version Traefik 3.7 is built against
TRAEFIK_CHART_VERSION=42.2.0
CERT_MANAGER_VERSION=v1.21.3
KPS_CHART_VERSION=92.17.0       # kube-prometheus-stack = Prometheus + Grafana

# Create a namespace (if missing) with the strict "restricted" Pod Security rules:
# pods must run as non-root, without extra privileges.
restricted_namespace() {
  kubectl create namespace "$1" --dry-run=client -o yaml | kubectl apply -f -   # "create if missing"
  kubectl label namespace "$1" --overwrite \
    pod-security.kubernetes.io/enforce=restricted \
    pod-security.kubernetes.io/warn=restricted
}

# 1. backup.yaml contains two IDs that Bicep created (the backup identity and the storage name).
#    After a rebuild they change; with old values the nightly backup would fail without anyone noticing.
echo "==> 1. Checking the backup settings in deploy/k8s/backup.yaml"
client_id=$(az identity show -g "$RESOURCE_GROUP" -n id-delay-radar-backup --query clientId -o tsv)
storage=$(az storage account list -g "$RESOURCE_GROUP" --query "[?starts_with(name,'stradar')].name | [0]" -o tsv)
for value in "$client_id" "$storage"; do
  if ! grep -q "$value" deploy/k8s/backup.yaml; then
    echo "ERROR: deploy/k8s/backup.yaml is out of date. Set:"
    echo "  azure.workload.identity/client-id: $client_id"
    echo "  STORAGE_ACCOUNT: $storage"
    exit 1
  fi
done

# 2. Gateway API: new object types (Gateway, HTTPRoute). Must exist before Traefik and my app use them.
echo "==> 2. Gateway API $GATEWAY_API_VERSION"
kubectl apply --server-side -f \
  "https://github.com/kubernetes-sigs/gateway-api/releases/download/$GATEWAY_API_VERSION/standard-install.yaml"

# 3. Traefik: receives all traffic from the internet and sends it on according to my HTTPRoutes.
#    `helm upgrade --install` = install the first time, update afterwards.
echo "==> 3. Traefik $TRAEFIK_CHART_VERSION"
helm repo add traefik https://traefik.github.io/charts --force-update >/dev/null
restricted_namespace traefik
helm upgrade --install traefik traefik/traefik \
  --version "$TRAEFIK_CHART_VERSION" --namespace traefik \
  --values deploy/platform/traefik-values.yaml --wait

# 4. cert-manager + the Let's Encrypt "accounts" (ClusterIssuers).
#    --server-side=false: AKS adds its own safety rule to every webhook, and Helm 4's default
#    (server-side apply) refuses to overwrite it. The classic method leaves AKS's rule alone.
#    --wait: cert-manager's webhook must be running before the ClusterIssuers can be applied.
echo "==> 4. cert-manager $CERT_MANAGER_VERSION"
helm repo add jetstack https://charts.jetstack.io --force-update >/dev/null
restricted_namespace cert-manager
helm upgrade --install cert-manager jetstack/cert-manager \
  --version "$CERT_MANAGER_VERSION" --namespace cert-manager --server-side=false \
  --values deploy/platform/cert-manager-values.yaml --wait
kubectl apply -f deploy/platform/cluster-issuer.yaml

# 5. Prometheus + Grafana. Installed before my app, because my app's PodMonitors
#    ("Prometheus, scrape these pods") are an object type that this chart adds.
echo "==> 5. Prometheus + Grafana $KPS_CHART_VERSION"
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts --force-update >/dev/null
restricted_namespace monitoring
if ! kubectl -n monitoring get secret grafana-admin >/dev/null 2>&1; then
  kubectl -n monitoring create secret generic grafana-admin \
    --from-literal=admin-user=admin --from-literal=admin-password="$(openssl rand -hex 16)"
fi
helm upgrade --install kps prometheus-community/kube-prometheus-stack \
  --version "$KPS_CHART_VERSION" --namespace monitoring --server-side=false \
  --values deploy/platform/monitoring-values.yaml --wait --timeout 10m

# 6. Passwords, created ONLY if missing. A new database password on a re-run would lock the
#    app out of the existing database, because PostgreSQL keeps the password it started with.
echo "==> 6. Secrets"
kubectl apply -f deploy/k8s/namespace.yaml          # the Secrets need the namespace to exist
if ! kubectl -n "$NS" get secret postgres-auth >/dev/null 2>&1; then
  kubectl -n "$NS" create secret generic postgres-auth \
    --from-literal=password="$(openssl rand -hex 24)"    # random, never typed, never in git
fi
if ! kubectl -n "$NS" get secret ghcr-pull >/dev/null 2>&1; then
  read -rsp "GHCR token (read:packages): " token; echo   # -s: not shown on screen, not saved in history
  kubectl -n "$NS" create secret docker-registry ghcr-pull \
    --docker-server=ghcr.io --docker-username="$GHCR_USER" --docker-password="$token"
fi

# 7. My app: everything in deploy/k8s/ (listed in kustomization.yaml). Then wait until it runs.
echo "==> 7. Application"
kubectl apply -k deploy/k8s
kubectl -n "$NS" rollout status statefulset/postgres --timeout=5m
kubectl -n "$NS" rollout status deployment/collector --timeout=3m
kubectl -n "$NS" rollout status deployment/api --timeout=3m

echo "==> Done. Next: ./deploy/verify.sh"
