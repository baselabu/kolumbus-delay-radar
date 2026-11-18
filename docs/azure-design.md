# Azure deployment design

How Kolumbus Delay Radar runs on Azure Kubernetes Service (AKS), and why it is built this way.
Decided on 30 Oct 2026, before any resource was created; **updated on 5 Nov 2026 to match what was
built**. Changes to the original plan are marked **Update:**, with the reason.

## 1. Goal and constraints

Run the collector, the API and PostgreSQL on a managed Kubernetes cluster, reachable over
HTTPS, built from code in this repository so it can be torn down and rebuilt at any time.

| Constraint | Value |
|---|---|
| Subscription | Azure for Students: **$100 credit** (billed as ~kr 1000), no credit card, spending stops at 0 |
| Region | `swedencentral` (an Azure Policy allows only 5 European regions; Norway East and Germany West Central block small VM sizes for student subscriptions, see below) |
| vCPU quota | **6** regional vCPUs; Bsv2 family allowed, Dv5 family not available |
| Deadlines | Code and report **8 Nov 2026**; video **11 Nov 2026** |
| Data | Cannot be re-collected (Entur only serves current data), so it must never be lost |

Naming follows the Azure convention `<type>-<workload>`: resource group `rg-delay-radar`,
cluster `aks-delay-radar`. AKS also creates a second resource group (`MC_...`) for the node VMs,
disks, load balancer and IPs; AKS manages it, so it is never edited by hand.

**Why Sweden Central:** the first plan was `norwayeast`. `what-if` failed before anything was
created: `Standard_B2ls_v2` is `NotAvailableForSubscription` there. Quota (how many vCPUs) and
SKU restrictions (which VM sizes) are separate limits. A check of all allowed regions with
`az vm list-skus` showed that Sweden Central allows the size, and it is also ~15 % cheaper.

## 2. Architecture (as built)

```
  user (browser / curl)
        │ HTTPS 443  (HTTP 80 → redirect)
        ▼
  Public IP + DNS name  kolumbus-delay-radar.swedencentral.cloudapp.azure.com
        ▼
  Azure Standard Load Balancer                                  (managed by AKS)
        │
 ┌──────┼──────────────── AKS cluster, 2 nodes (B2ls_v2) ───────────────────────────────┐
 │      ▼                                                                               │
 │  Traefik (Gateway API: Gateway + HTTPRoutes)   TLS from Let's Encrypt (cert-manager) │
 │      │ only "/" (demo UI) and "/api/*"                                               │
 │      ▼                                                                               │
 │  Service api ──► API pods ×2, one per node (read-only, also serves the UI)           │
 │                        │ 5432                                                        │
 │                        ▼                                                             │
 │  Service postgres ──► postgres-0 (StatefulSet) ──► PVC ──► Azure Disk 32 GB (Retain) │
 │                        ▲                ▲                                            │
 │        5432 (upserts)  │                │ 03:00 nightly pg_dump                      │
 │  collector ×1 (Recreate)         backup CronJob ─── workload identity ──► Blob Storage
 │        │ HTTPS                                                                       │
 │        ▼                                                                             │
 │   api.entur.io        Prometheus ── scrapes /metrics (api, collector) ── Grafana     │
 └──────────────────────────────────────────────────────────────────────────────────────┘
```

- Every namespace I created uses the **restricted** Pod Security Standard (non-root, no
  privilege escalation, all capabilities dropped, seccomp `RuntimeDefault`).
- **Default-deny NetworkPolicies** (enforced by Cilium) allow only the arrows above.
- Images are built by CI in this repository, pushed to `ghcr.io/baselabu/kolumbus-delay-radar/...`,
  and pinned to the commit SHA in `deploy/k8s/kustomization.yaml`.
- Only Traefik is reachable from the internet. The API, PostgreSQL and Grafana are internal.

## 3. Decisions

| Area | Choice | Alternative rejected | Why |
|---|---|---|---|
| Cluster | AKS **Free tier**, `Standard_B2ls_v2` (2 vCPU, 4 GB). **Update: 2 nodes** instead of 1 (from 2 Nov). | `B2s_v2` (8 GB) | Fits the quota and the credit; the control plane is managed by Azure, so there is no slow-etcd problem like on the course's Talos cluster. One node had 2.73 GiB allocatable with 0.82 GiB requested by AKS system pods. **Update:** Prometheus and Grafana (~0.7–1 GiB) did not fit next to the app, so a second node was added. Its quota: 2 × 2 vCPU + 2 for the upgrade surge = 6 of 6. Bonus: the two API replicas now run on different nodes. |
| Database | **PostgreSQL StatefulSet** with a PVC on Azure Disk | Azure Database for PostgreSQL Flexible Server (~$20/month) | Learning goal: stateful workloads in Kubernetes. The StorageClass uses `reclaimPolicy: Retain`, so deleting the PVC does not delete the data. In production I would choose Flexible Server for managed backups, patching and HA. |
| Backups | **Nightly `pg_dump` to Azure Blob Storage** (Cool tier, 14-day lifecycle). **Update:** built on 1 Nov. | A volume in the cluster; storage account keys | Backups must survive deleting the cluster. The backup pod logs in with **workload identity** (a federated credential for one ServiceAccount), storage keys are disabled, and the role is limited to one container. Restore tested: 1,003,457 rows in 17 s. |
| Images | **GHCR** with an `imagePullSecret`, pinned to the commit SHA. **Update:** moved from `ghcr.io/baselabu/delay-radar` to `ghcr.io/dat515-2026/baselabu-labs` when the project moved into the course repository. | Azure Container Registry + managed identity | Already worked with CI; saves setup time. ACR would remove the long-lived token (see risks). |
| Infrastructure as code | **Bicep** (`infra/`), run by `infra/deploy.sh` | Terraform; clicking in the portal | Built into `az`, no state file to protect, Azure is the source of truth. `what-if` previews changes. |
| Traffic in + HTTPS | **Traefik** with **Gateway API**, **cert-manager** + Let's Encrypt | AKS application routing add-on (NGINX); the add-on's Istio/Gateway API mode | ingress-nginx was retired in March 2026 and the add-on's NGINX support ends Nov 2026; the Istio mode is preview and too heavy for 4 GB. Traefik is light and Gateway API is the current standard. `externalTrafficPolicy: Local` keeps the client IP for the per-IP rate limit. |
| Network | **Default-deny NetworkPolicies** with Cilium. **Update:** added on 1 Nov. | Open pod network | A compromised pod can reach only what it needs. Tested before and after: an unknown pod reached PostgreSQL and the internet before, and neither after. |
| Monitoring | **kube-prometheus-stack** (Prometheus + Grafana), kept lean; metrics from both apps via OpenTelemetry. **Update:** added on 2–4 Nov. | Azure Monitor / Container Insights | Standard, free, and the dashboard is code in git. Alertmanager, node-exporter (needs host access, not allowed by "restricted") and the AKS-managed control-plane targets are turned off. Grafana is reachable only through `kubectl port-forward`. |
| Secrets | Plain **Kubernetes Secrets**, generated with `openssl rand`, never in git | Azure Key Vault + CSI driver | Enough for a short-lived cluster; AKS encrypts Secrets at rest. |

## 4. Cost

Retail prices for `swedencentral` from the Azure price API on 30 Oct 2026 (730 hours = 1 month):

| Item | $/month |
|---|---|
| AKS control plane (Free tier) | 0.00 |
| Node VMs, 2 × `Standard_B2ls_v2` (**Update:** was 1) | 63.08 |
| Node OS disks, 2 × 32 GB Premium SSD (P4) | 11.62 |
| Standard Load Balancer | 18.25 |
| 2 public IPs (outbound + ingress) | 7.30 |
| PostgreSQL data disk, 32 GB Standard SSD (E4) | 2.40 |
| Prometheus disk 8 GB, backups in Blob (~40 MB × 14) | ~1.50 |
| **Total** | **≈ 104** (≈ $3.40/day) |

**Measured:** kr 77.77 for 1–4 Nov, mostly with one node (about kr 19 per day). With two nodes the
estimate is about kr 35 per day, well within the credit until the teardown.

**Budget:** on the **subscription**, not on `rg-delay-radar`, because the VMs, disks and load
balancer are billed in the AKS-managed `MC_` group: a resource-group budget would miss ~95 % of
the cost. It sends e-mails at 50 % and 80 % of actual spend and when the forecast passes 100 %.
**Update:** the amount is in the **billing currency (NOK)**. It was first set to "50", meant as
dollars, which made it kr 50. The alerts fired after 3 days and revealed the mistake (and proved
that the alerting works). It is now **kr 500**.

## 5. Accepted risks

| Risk | Effect | What I do about it |
|---|---|---|
| **PostgreSQL is a single instance** (**Update:** this replaces "single node") | If its node fails, the API is "not ready" until the disk re-attaches on the other node (a few minutes), and the collector misses those polls. | Accepted. The Azure Disk is stored 3 times in the datacenter and re-attaches; the API itself survives a node failure (2 replicas on different nodes). Planned upgrades add a temporary surge node. |
| **Database inside the cluster** | A broken volume or a mistake could lose data. | **Done:** nightly backup to Blob Storage, restore tested; a manual dump before risky changes and before teardown. |
| **Memory** | Pods can be evicted or OOM-killed if usage grows. | Requests and limits on every pod, set from measurements (`kubectl top`, the Grafana dashboard); the second node gave headroom. |
| **Long-lived pull token** | If the GHCR token leaks, someone can download my images. | A classic PAT with **only** `read:packages` that expires after 30 days, typed in with `read -s`, never stored in git or shell history. |
| **Two collectors at Entur** | They share Entur's rate limit if they use the same client name (HTTP 429). | The AKS collector has its own `Collector__ClientName`. **Done:** the VM collector was stopped on 31 Oct, after the AKS collector was saving data. |
| **Let's Encrypt rate limits** | Too many failed certificate requests block new certificates for hours. | Staging issuer first, then production. **Done.** |
| **Credit runs out** | Azure stops all resources at 0. | Budget alert (kr 500), and teardown after the course. |

## 6. Teardown and rebuild

```bash
./deploy/teardown.sh     # asks for confirmation, saves a final dump to ~, deletes the resource group and the budget
```

Then revoke the GHCR pull token on GitHub, and a few days later check *Cost Management* to confirm
that nothing is still billed.

Rebuilding is three scripts: `./infra/deploy.sh` → `./deploy/install.sh` → `./deploy/verify.sh`,
plus `pg_restore` of the final dump (see `report.md`).
