# DevControl

DevControl is a developer operations control plane for live apps. Stage 1 proves
the deployable skeleton: combined .NET API and React UI, PostgreSQL, Docker
Compose, Terraform-managed GCP infrastructure, Cloud Run, and GitHub Actions.

## GCP Account Requirement

All human GCP changes for this project must use:

```text
nickaccturk@gmail.com
```

The project scripts fail closed if `gcloud` is authenticated as any other
account. See [docs/gcp-account-policy.md](docs/gcp-account-policy.md).

## Local Development

Prerequisites:

- .NET SDK 10
- Node.js 22
- Docker

Run the full local stack:

```powershell
docker compose up --build
```

Then open:

```text
http://localhost:8080
http://localhost:8080/health/live
http://localhost:8080/health/ready
```

The app runs EF Core migrations on startup in Compose through:

```text
DEVCONTROL_RUN_MIGRATIONS_ON_STARTUP=true
```

## Tests

```powershell
npm ci --prefix src/DevControl.Web
npm run build --prefix src/DevControl.Web
dotnet restore DevControl.sln
dotnet build DevControl.sln --configuration Release
dotnet test DevControl.sln --configuration Release
```

The integration test for `/health/ready` runs only when
`DEVCONTROL_TEST_CONNECTION_STRING` is set.

## Stage 2 Tenant/Security

Stage 2 adds Google-backed user sign-in, local development sign-in,
organizations, projects, environments, members, invitations, RBAC, audit logs,
and the base control action model. See
[docs/stage-2-tenant-security.md](docs/stage-2-tenant-security.md).

## Stage 3 Live App Registry

Stage 3 adds scoped registration tokens, `devcontrol apps register`, generated
GitHub Actions snippets, and the live app registry. Existing app repos still own
their deployments; DevControl records post-deploy runtime facts. See
[docs/stage-3-live-app-registry.md](docs/stage-3-live-app-registry.md).

## Stage 4 API Keys and Usage

Stage 4 adds project/environment-scoped API keys, show-once runtime secrets,
revocation/rotation, fixed-window rate limiting, a protected runtime sample
endpoint, and usage counters visible in DevControl. See
[docs/stage-4-api-keys-usage.md](docs/stage-4-api-keys-usage.md).

## Stage 5 Feature Flags and SDK

Stage 5 adds boolean feature flags, boolean kill switches, audited production
governance, runtime flag snapshots with ETags, and the first cached local
evaluation C# SDK. See
[docs/stage-5-feature-flags-sdk.md](docs/stage-5-feature-flags-sdk.md).

## Stage 6 Safe Outbound HTTP and Webhooks

Stage 6 adds the SSRF-safe outbound HTTP layer, HMAC-signed webhooks, test
delivery, pause/resume, delivery attempts, and bounded retry batches. See
[docs/stage-6-safe-outbound-webhooks.md](docs/stage-6-safe-outbound-webhooks.md).

## Stage 7 Monitoring, Incidents, and Status Page

Stage 7 adds managed uptime monitors for registered live apps, scheduler-driven
health checks, automatic incident open/recovery, public status pages, and
manual release notes. See
[docs/stage-7-monitoring-incidents-status.md](docs/stage-7-monitoring-incidents-status.md).

## Stage 8 GitHub App and Live Control

Stage 8 adds GitHub App installation-token infrastructure, managed repo
onboarding PRs that install the registration hook with GitHub Actions OIDC, and
Admin-only `workflow_dispatch` control for deploy, redeploy, and rollback. See
[docs/stage-8-github-app-live-control.md](docs/stage-8-github-app-live-control.md).

## Stage 9 Observability and Production Hardening

Stage 9 adds local and live-on-demand Prometheus/Grafana dashboards, a
token-protected live `/metrics` endpoint, a DevControl-authenticated live
Grafana proxy, scheduler-driven retention cleanup, PostgreSQL backup/restore
scripts, a private 7-day backup bucket, and free-tier guard checks. See
[docs/stage-9-observability-production-hardening.md](docs/stage-9-observability-production-hardening.md).

Run the local observability demo:

```powershell
docker compose --profile observability up --build
```

Then open:

```text
http://localhost:8080
http://localhost:8080/metrics
http://localhost:9090
http://localhost:3000/d/devcontrol-stage-9/devcontrol-stage-9
```

Live GCP observability is on-demand, not always-on. Signed-in DevControl users
with organization access open Grafana from the app header or directly at:

```text
https://devcontrol-nictbzfhga-uc.a.run.app/observability/
```

The raw `devcontrol-observability` Cloud Run service is private infrastructure;
users should not log in to Grafana with a shared admin password.

```powershell
.\scripts\gcp\smoke-test-cloud-run.ps1 -ServiceUrl <cloud-run-url>
.\scripts\gcp\smoke-test-live-observability.ps1
.\scripts\gcp\assert-free-tier-guards.ps1
.\scripts\gcp\backup-postgres.ps1
.\scripts\gcp\verify-postgres-restore.ps1
```

Full Stage 9 demo path:

```powershell
# 1. Start local observability.
docker compose --profile observability up --build

# 2. Sign in locally, create/select an org/project/environment, then generate:
#    registration token + app registration, API-key sample traffic,
#    webhook test delivery, monitor check, incident recovery, and release.

# 3. Open local dashboards.
start http://localhost:8080
start http://localhost:3000/d/devcontrol-stage-9/devcontrol-stage-9

# 4. Run a scheduler tick and confirm cleanup appears in the JSON response.
Invoke-RestMethod -Method Post http://localhost:8080/internal/scheduler/tick `
  -Headers @{ "X-DevControl-Scheduler-Secret" = "devcontrol_local_scheduler_secret" }

# 5. Prove live GCP observability, lean resource limits, and backups.
$env:DEVCONTROL_GCP_PROJECT_ID = "devcontrol-r7m5o9ld"
.\scripts\gcp\smoke-test-live-observability.ps1
.\scripts\gcp\assert-free-tier-guards.ps1
.\scripts\gcp\backup-postgres.ps1
.\scripts\gcp\verify-postgres-restore.ps1

# 6. Push main, wait for GitHub Actions deploy, then smoke test live Cloud Run.
.\scripts\gcp\smoke-test-cloud-run.ps1 -ServiceUrl <cloud-run-url>
```

## GCP Stage 1

Use [docs/gcp-stage-1.md](docs/gcp-stage-1.md) for project bootstrap,
Terraform, GitHub WIF, and Cloud Run deployment.

Free-tier defaults:

- Region: `us-central1`
- Zone: `us-central1-a`
- PostgreSQL VM: `e2-micro`
- Boot disk: 10 GB `pd-standard`
- PostgreSQL data disk: 20 GB `pd-standard`, not auto-deleted
- Cloud Run: `min-instances=0`, `max-instances=1`
