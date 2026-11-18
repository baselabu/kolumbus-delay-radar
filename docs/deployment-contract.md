# Deployment contract: what the application needs from Kubernetes

> Written on 2026-10-26 as the handoff for the planned UiS Talos deployment, and kept as a record.
> The deployment as built is described in [`azure-design.md`](azure-design.md) and [`../report.md`](../report.md).

This document lists everything the Kubernetes deployment needs to know about the collector and
the API, so they can be deployed without reading the C# code.

## 1. Components and images

| Component | Dockerfile | Base image | Port | Replicas |
|---|---|---|---|---|
| Collector | `src/Radar.Collector/Dockerfile` | `dotnet/runtime:10.0` | none (no HTTP server) | exactly 1 |
| API | `src/Radar.Api/Dockerfile` | `dotnet/aspnet:10.0` | **8080** (`EXPOSE 8080`) | 2 or more |

- Build context for both images is the repository root (they need `src/Radar.Data` too):
  `docker build -f src/Radar.Api/Dockerfile -t radar-api .`
- Both images run as the non-root user `$APP_UID` (UID **1654**), so `runAsNonRoot: true` works.
  `readOnlyRootFilesystem: true` has not been tested yet; try it and check the logs.
- PostgreSQL: `postgres:17`.

## 2. Configuration (environment variables)

All settings can be overridden with environment variables (`__` = nested section). Values in
`appsettings.json` are the defaults; only the connection string has no default.

| Variable | Used by | Value / note |
|---|---|---|
| `ConnectionStrings__Radar` | collector, API | **Secret.** `Host=<db-service>;Port=5432;Database=radar;Username=...;Password=...`. Add `;Gss Encryption Mode=Disable` to avoid a harmless Kerberos warning in the logs. |
| `Collector__ClientName` | collector | Default `uis-dat515group17-delayradar` = the **production** name. Never use the `-dev` name in the cluster (Entur rate-limits per client name; two collectors with one name caused HTTP 429). |
| `Collector__PollingIntervalSeconds` | collector | Default 30. Validated: 10–600. |
| `Collector__DatasetId` | collector | Default `KOL`. |
| `Collector__AuthorityId` | collector | Default `KOL:Authority:8` (for line names). |
| `Collector__BaseUrl`, `Collector__GraphQlUrl` | collector | Entur URLs; defaults are correct. |
| `Collector__ReplayFolder` | collector | Leave **unset** in production. If set, the collector reads saved XML files instead of calling Entur (offline/demo fallback). |

- Invalid or missing collector settings make the collector **refuse to start** with a clear error
  in the log (validation at startup).
- Both apps run in the `Production` environment inside the containers (no user secrets, no
  `/openapi` documentation, no stack traces in error responses).

## 3. Health checks and startup

**API**
- Liveness: `GET /healthz` → 200 as long as the process answers. It checks **no** dependencies,
  on purpose: if the database is down, restarting the API would not fix it.
- Readiness: `GET /readyz` → 200 only if the database answers **and** the `stop_events` table
  exists. On a fresh database the API is "not ready" until the collector has created the tables.
- Health endpoints are **not** rate-limited, so probes can call them as often as needed.

**Collector**
- No HTTP endpoint (yet). Kubernetes can only see whether the process is running.
- At startup it runs the database **migrations** (creates/upgrades the tables) **before** it starts
  polling. If the database is unreachable or the migration fails, the process **exits** (fail fast)
  and Kubernetes restarts it with back-off until the database is ready.
- After startup, errors from Entur or the database are logged and retried on the next poll; they
  never crash the collector.
- Graceful shutdown on SIGTERM: the current poll finishes, then the process exits with code 0.

## 4. Replicas and update strategy

**Collector: exactly 1 replica, strategy `Recreate`**
1. Entur delta updates depend on one `requestorId` per running collector; two collectors would
   each download full snapshots.
2. Entur rate-limits per client name; two collectors double the requests (we saw HTTP 429).
3. The collector owns the schema migrations; only one instance should change the database.

`Recreate` stops the old pod before starting the new one, so two never run at the same time.

**API: 2+ replicas, stateless, normal rolling update**
- Read-only: never writes to the database and never runs migrations.
- Output cache is **in memory per replica**: statistics are cached 10 minutes, live data 30
  seconds. With 2 replicas a statistic may be calculated twice per 10 minutes; that is fine.
- Rate limit: 60 requests per minute **per client IP** (429 above that).

## 5. Database

- PostgreSQL 17 with a persistent volume. The data cannot be re-collected (Entur only provides
  current data), so it must survive pod restarts, and it needs backups.
- Tables are created by the collector's migrations at startup (`stop_events`, `lines`,
  `__EFMigrationsHistory`). No manual SQL needed.
- Planned least-privilege database users:
  - **writer** for the collector: read, write **and create tables** (it runs the migrations)
  - **reader** for the API: `SELECT` only

  Until then both can use the same user; the connection strings are already separate.
- Existing data: `radar-collector-vm` (172.16.2.45) has collected since **24 Oct 2026**. Move it
  with `pg_dump -Fc` on the VM and `pg_restore` into the cluster database, then stop the VM
  collector (only after the cluster collector is saving data, so nothing is missed).

## 6. Network

- **Collector egress:** HTTPS to `api.entur.io:443` (SIRI-ET REST and the GraphQL API). Check
  this first from a pod in the cluster.
- **MTU:** on OpenStack the network MTU is 1450. On the VM, Docker's default 1500 made HTTPS from
  containers hang (builds and calls timed out while the host worked). Kubernetes networking
  (Flannel) normally detects the correct MTU, but if requests from pods hang, check this first.
- **API:** reachable **only through the Ingress**. The API trusts the `X-Forwarded-For` header
  to find the real client IP (needed for per-client rate limiting). If the API were reachable
  directly, a client could fake that header.
- **HTTPS ends at the Ingress** (TLS termination). The API itself speaks plain HTTP on 8080 and
  does not redirect to HTTPS.
- **Database:** ClusterIP Service only, never exposed outside the cluster.

## 7. Not done yet on the application side

- `/metrics` endpoints for Prometheus (collector and API).
- An HTTP health endpoint for the collector (so a stuck, not crashed, collector can be detected).
- CPU/memory requests and limits: not measured yet. Measure on the VM with `docker stats` and set
  requests/limits from that, with some margin for the full snapshot after a restart.
