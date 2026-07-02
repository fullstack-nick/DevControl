# Stage 9 Observability and Production Hardening

Stage 9 finishes the deployed-light/local-rich observability split.

Local development gets Prometheus and Grafana through Docker Compose. The GCP
deployment stays lean: Cloud Run logs, health checks, a scheduler tick, bounded
cleanup work, Artifact Registry cleanup policies, and a short-retention private
PostgreSQL backup bucket.

## Local observability

Start the full local stack:

```powershell
docker compose --profile observability up --build
```

Open:

```text
http://localhost:8080
http://localhost:8080/metrics
http://localhost:9090
http://localhost:3000/d/devcontrol-stage-9/devcontrol-stage-9
```

Grafana is provisioned with:

```text
admin / admin
```

Anonymous viewer access is enabled for local demos.

The dashboard shows:

- HTTP request rate and p95 latency
- scheduler work by component
- monitor up/down/slow checks
- webhook delivery outcomes
- API-key runtime traffic and rate-limit hits
- retention cleanup changes
- .NET runtime memory, GC, and thread metrics

## Deployed metrics boundary

`GET /metrics` is controlled by:

```text
DEVCONTROL_METRICS_ENABLED
```

The default is disabled. Docker Compose enables it for local Prometheus. The
Cloud Run Terraform does not set it, so the live service returns 404 for
`/metrics`.

This is intentional. Do not deploy Prometheus or Grafana to Cloud Run, Compute
Engine, or another always-on service for the MVP.

## Structured log cleanup

The API keeps JSON console logs for Cloud Run. Routine access logs for these
paths are suppressed:

```text
/health/live
/health/ready
/metrics
/assets/*
```

Request logs remain secret-safe. They do not record headers, query strings,
bodies, API keys, registration tokens, or GitHub OIDC tokens.

## Retention cleanup

The scheduler tick now runs retention cleanup after monitor checks, webhook
retries, and GitHub sync:

```text
POST /internal/scheduler/tick
```

Defaults:

```text
DEVCONTROL_RETENTION_RATE_LIMIT_WINDOWS_DAYS=14
DEVCONTROL_RETENTION_MONITOR_CHECKS_DAYS=30
DEVCONTROL_RETENTION_WEBHOOK_PREVIEW_DAYS=30
DEVCONTROL_RETENTION_WEBHOOK_DELIVERIES_DAYS=90
DEVCONTROL_CLEANUP_BATCH_SIZE=500
```

Cleanup behavior:

- deletes old API-key per-minute rate-limit windows
- deletes old monitor check rows
- compacts old webhook response previews/errors
- deletes old terminal webhook delivery rows and orphaned webhook events
- preserves audit logs, control actions, feature flag history, incidents,
  releases, live app registrations, and deployment history

The scheduler response includes a `cleanup` object with changed row counts.

## Backups

Terraform creates:

```text
gs://<project-id>-devcontrol-postgres-backups
```

The bucket is regional `us-central1`, Standard storage, private, uniform-access,
public-access-prevention enforced, versioning disabled, and configured to delete
objects after 7 days.

Create a backup:

```powershell
$env:DEVCONTROL_GCP_PROJECT_ID = "devcontrol-r7m5o9ld"
.\scripts\gcp\backup-postgres.ps1
```

The script:

1. verifies the active Google account is `nickaccturk@gmail.com`,
2. opens a temporary SSH firewall rule for the operator's current `/32`,
3. runs `pg_dump -Fc` on the PostgreSQL VM,
4. downloads the dump into `.artifacts/backups`,
5. uploads the dump to the backup bucket,
6. removes the temporary SSH firewall rule,
7. prints JSON containing the local path, GCS object, size, and SHA-256.

Verify restore:

```powershell
.\scripts\gcp\verify-postgres-restore.ps1
```

The restore verifier downloads the latest dump unless `-BackupObject` or
`-LocalDumpPath` is supplied, restores it into `devcontrol_restore_verify`,
checks the restored schema and basic row counts, and drops the verification
database unless `-KeepDatabase` is set. It also uses a temporary operator `/32`
SSH firewall rule and removes it before exiting. It refuses to restore over the
production `devcontrol` database unless `-AllowProductionOverwrite` is
explicitly passed.

## Cost guards

Run:

```powershell
.\scripts\gcp\assert-free-tier-guards.ps1
```

The guard verifies:

- Cloud Run `min-instances=0`, `max-instances=1`, `cpu=1`, `memory=512Mi`
- PostgreSQL VM is `e2-micro`
- boot disk is 10 GB and data disk is 20 GB
- Artifact Registry cleanup policies exist
- backup bucket is private and has a 7-day lifecycle
- Prometheus and Grafana are not deployed as Cloud Run services or VMs

## Stage 9 proof checklist

Local:

```powershell
npm ci --prefix src/DevControl.Web
npm run build --prefix src/DevControl.Web
dotnet restore DevControl.sln
dotnet build DevControl.sln --configuration Release
dotnet test DevControl.sln --configuration Release
docker compose --profile observability up --build
```

GCP:

```powershell
terraform -chdir=infra/gcp fmt -check -recursive
terraform -chdir=infra/gcp validate
.\scripts\gcp\terraform-plan.ps1
git push origin main
.\scripts\gcp\smoke-test-cloud-run.ps1 -ServiceUrl <cloud-run-url>
.\scripts\gcp\assert-free-tier-guards.ps1
.\scripts\gcp\backup-postgres.ps1
.\scripts\gcp\verify-postgres-restore.ps1
```

Stage 9 is complete only after the code is on `origin/main`, GitHub Actions has
deployed it to Cloud Run, live `/health/live` and `/health/ready` pass, live
`/metrics` returns 404, backup/restore proof passes, and the demo screenshots in
`docs/assets/stage-9/` are committed.
