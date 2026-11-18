# Kolumbus Delay Radar

Which buses in Rogaland are late right now, and which lines are least punctual at rush hour?

Kolumbus Delay Radar collects real-time bus data for Kolumbus from Entur's open
[SIRI-ET API](https://developer.entur.org/) every 30 seconds, stores every stop passage in
PostgreSQL, and serves punctuality statistics through a REST API and a small web UI. It runs
on Azure Kubernetes Service (AKS), built entirely from code in this repository.

**Live demo (until 30 November 2026):** https://kolumbus-delay-radar.swedencentral.cloudapp.azure.com

## What it does

| Endpoint | Answers |
|---|---|
| `GET /api/live/late` | Buses that are more than 3 minutes late right now |
| `GET /api/lines/{line}/punctuality` | Median and p90 delay for one line |
| `GET /api/lines/ranking?days=7&period=rush` | Lines ranked by punctuality, at rush hour or all day |
| `GET /api/stops` | All stops with a name |
| `GET /api/stops/{stopRef}/delays` | Delay profile of one stop, hour by hour |

The web UI at `/` calls the same public API.

## Architecture

```
  browser ──HTTPS──► Azure Load Balancer ──► Traefik (Gateway API, Let's Encrypt via cert-manager)
                                                 │  only "/" and "/api/*"
                                                 ▼
                                   API ×2 (one per node, read-only)
                                                 │
  api.entur.io ◄──HTTPS── collector ×1 ──► PostgreSQL (StatefulSet, Azure Disk) ◄── nightly backup CronJob
                                                                                        │ workload identity
                                                                                        ▼
                                                                              Azure Blob Storage
  Prometheus scrapes /metrics from the API and the collector ──► Grafana dashboard (internal)
```

- **Collector** (.NET worker): polls Entur every 30 s and upserts on (operating date, trip,
  stop order), so a repeated update never duplicates a row and restarts lose nothing.
- **API** (ASP.NET Core): median and p90 instead of averages, because one tracking error
  ("97 min late") moves an average a lot. Rate limited to 60 requests/min per client IP.
- **Infrastructure as code:** Bicep for Azure (AKS, storage, identity, budget), Kustomize
  for the app, Helm for Traefik, cert-manager and kube-prometheus-stack.

## Cloud and security practices

- **Entra ID login** for `kubectl`; local cluster accounts are disabled.
- **Workload identity** for the backup: no keys or passwords, and storage account keys are disabled.
- **Default-deny NetworkPolicies** (Cilium): only the API, the collector and the backup reach
  PostgreSQL, the API cannot reach the internet, and only Prometheus can scrape metrics.
- **The `restricted` Pod Security Standard** in every app namespace: non-root, read-only root
  filesystem, all capabilities dropped.
- **Backups outside the cluster:** a nightly `pg_dump` to Blob Storage (Cool tier, 14-day
  lifecycle). Restore tested: 1,003,457 rows in 17 s.
- **Images pinned to the commit SHA**, built and pushed by GitHub Actions.
- **Metrics and dashboard as code:** OpenTelemetry metrics in both services, and the Grafana
  dashboard is a JSON file in [`deploy/k8s/dashboards/`](deploy/k8s/dashboards).
- **A cost budget** with e-mail alerts on the subscription.

Every decision, the alternative I rejected and the cost are in
[`docs/azure-design.md`](docs/azure-design.md).

## Run it locally

Needs Docker.

```bash
cp .env.example .env          # then set your own POSTGRES_PASSWORD
docker compose up --build     # PostgreSQL, the collector and the API
```

Open http://localhost:8080. The first data arrives within 30 seconds.

## Tests

```bash
dotnet test                   # needs the .NET 10 SDK and Docker (Testcontainers starts PostgreSQL 17)
```

There are 31 tests: the SIRI parser against recorded Entur XML, the database upserts against
real PostgreSQL, the live-delay rules with a fake clock, and the API end to end with
`WebApplicationFactory`.

## Deploy to Azure

Creates billable resources (about kr 35 per day for 2 × `Standard_B2ls_v2`).

```bash
az login
# put your e-mail and Entra object ID in infra/main.bicepparam
./infra/deploy.sh             # Bicep: AKS, backup storage, identity, budget; connects kubectl
./deploy/install.sh           # Gateway API, Traefik, cert-manager, Prometheus/Grafana, secrets, the app
./deploy/verify.sh            # 18 end-to-end checks
./deploy/teardown.sh          # final database dump, then deletes everything
```

For your own deployment, change the DNS label in `deploy/platform/traefik-values.yaml`, the
hostname in `deploy/k8s/gateway.yaml` and `httproute.yaml`, and `Collector__ClientName` in
`deploy/k8s/collector.yaml` (Entur rate-limits per client name).

## Repository layout

```
src/Radar.Collector   polls Entur, parses SIRI-ET, writes to PostgreSQL
src/Radar.Api         REST API and the web UI (wwwroot/index.html)
src/Radar.Data        EF Core model and migrations
tests/Radar.Tests     xUnit, Testcontainers, WebApplicationFactory
infra/                Bicep and deploy.sh
deploy/k8s/           the app: Kustomize manifests, NetworkPolicies, backup CronJob, dashboard
deploy/platform/      Helm values for Traefik, cert-manager and kube-prometheus-stack
docs/                 design decisions, data notes, collector pseudocode
```

## Background

This was my project in DAT515 Cloud Computing at the University of Stavanger (autumn 2026).
It started as a two-person group project, and I finished it alone after my teammate left the
course. It was planned for the university's OpenStack Kubernetes cluster. That cluster's
control-plane disk was too slow for etcd, so I moved the project to AKS. The web UI and the
`/api/stops` endpoint were generated with an AI assistant (Claude) and reviewed by me.

## Data and credits

Real-time data from [Entur](https://entur.no), published as open data under the
[NLOD licence](https://data.norge.no/nlod/en/2.0). This is an independent project, not
affiliated with or endorsed by Kolumbus or Entur.

## License

The code is under the [MIT License](LICENSE). The data belongs to Entur and is under NLOD.
