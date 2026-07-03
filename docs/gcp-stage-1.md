# Stage 1 GCP Setup

Stage 1 is locked to `us-central1` for strict Always Free eligibility of the
PostgreSQL `e2-micro` VM. The nearest EU regions to Vienna are lower latency,
but they do not satisfy the free-tier PostgreSQL VM requirement.

## Prerequisites

- .NET SDK 10
- Node.js 22
- Docker
- Terraform
- Google Cloud SDK
- GitHub CLI, optional but useful

## Bootstrap The GCP Project

Pick a globally unique project ID, then run:

```powershell
$env:DEVCONTROL_GCP_PROJECT_ID = "your-unique-devcontrol-project-id"
$env:DEVCONTROL_GCP_REQUIRED_ACCOUNT = "<operator-google-account>"
.\scripts\gcp\bootstrap-project.ps1
```

The script logs in as the configured operator account if needed, asserts that
it is the active account, creates the project, asks for a billing account, links
billing, and enables required APIs.

## Provision Infrastructure

Set the GitHub owner/repo that will run Actions:

```powershell
$env:DEVCONTROL_GCP_PROJECT_ID = "your-unique-devcontrol-project-id"
$env:DEVCONTROL_GCP_REQUIRED_ACCOUNT = "<operator-google-account>"
$env:DEVCONTROL_GITHUB_OWNER = "your-github-owner"
$env:DEVCONTROL_GITHUB_REPO = "DevControl"
.\scripts\gcp\terraform-plan.ps1
.\scripts\gcp\terraform-apply.ps1
```

Terraform outputs:

- `github_workload_identity_provider`
- `github_deployer_service_account`
- `artifact_registry_repository`
- `cloud_run_service_url`

## Configure GitHub Variables

Create repository variables:

```text
GCP_PROJECT_ID
GCP_WORKLOAD_IDENTITY_PROVIDER
GCP_DEPLOYER_SERVICE_ACCOUNT
```

Do not create or upload a service-account JSON key.

## Verify Cloud Run

After the deploy workflow runs:

```powershell
.\scripts\gcp\smoke-test-cloud-run.ps1 -ServiceUrl "<cloud_run_service_url>"
```

Expected results:

- `/health/live` returns 200.
- `/health/ready` returns 200 only when Cloud Run reaches PostgreSQL through the
  private VPC path.
