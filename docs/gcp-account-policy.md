# GCP Account Policy

Human Google Cloud changes for a DevControl deployment must be performed by
the configured operator account. Set it locally before running any GCP script:

```powershell
$env:DEVCONTROL_GCP_REQUIRED_ACCOUNT = "<operator-google-account>"
```

This applies to project creation, billing linkage, Terraform, manual `gcloud`
commands, Cloud Run smoke tests, and any future GCP maintenance scripts.

The scripts under `scripts/gcp` call `assert-gcp-account.ps1` before making GCP
changes. The guard fails closed when the active `gcloud` account is not
the configured operator account.

GitHub Actions deploys through Workload Identity Federation instead of a user
login or JSON key.
