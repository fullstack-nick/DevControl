# GCP Account Policy

All human Google Cloud changes for DevControl must be performed as:

```text
nickaccturk@gmail.com
```

This applies to project creation, billing linkage, Terraform, manual `gcloud`
commands, Cloud Run smoke tests, and any future GCP maintenance scripts.

The scripts under `scripts/gcp` call `assert-gcp-account.ps1` before making GCP
changes. The guard fails closed when the active `gcloud` account is not
`nickaccturk@gmail.com`.

GitHub Actions deploys through Workload Identity Federation instead of a user
login or JSON key. The WIF service account is created inside the GCP project
bootstrapped by `nickaccturk@gmail.com`.

