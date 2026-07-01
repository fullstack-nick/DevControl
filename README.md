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
