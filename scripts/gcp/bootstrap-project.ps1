param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = "nickaccturk@gmail.com",
  [string]$BillingAccountId = $env:DEVCONTROL_GCP_BILLING_ACCOUNT_ID
)

$ErrorActionPreference = "Stop"

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID to a globally unique project ID before running bootstrap."
}

$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

$credentialedAccounts = @(& $gcloud auth list --format="value(account)" 2>$null)
if ($credentialedAccounts -notcontains $RequiredAccount) {
  Write-Host "Opening gcloud login for $RequiredAccount..."
  & $gcloud auth login $RequiredAccount
  Assert-LastExitCode "gcloud auth login"
}

& $gcloud config set account $RequiredAccount | Out-Null
Assert-LastExitCode "gcloud config set account"
& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
  $projectDescribeOutput = & $gcloud projects describe $ProjectId --format="value(projectId)" 2>$null
  $projectExists = $LASTEXITCODE -eq 0
} finally {
  $ErrorActionPreference = $previousErrorActionPreference
}

if (-not $projectExists) {
  Write-Host "Creating GCP project $ProjectId..."
  & $gcloud projects create $ProjectId --name="DevControl"
  Assert-LastExitCode "gcloud projects create"
} else {
  Write-Host "GCP project $ProjectId already exists."
}

& $gcloud config set project $ProjectId | Out-Null
Assert-LastExitCode "gcloud config set project"

if ([string]::IsNullOrWhiteSpace($BillingAccountId)) {
  Write-Host "Available billing accounts:"
  & $gcloud billing accounts list
  $BillingAccountId = Read-Host "Enter the billing account ID to link to $ProjectId"
}

if ([string]::IsNullOrWhiteSpace($BillingAccountId)) {
  throw "A billing account is required for Cloud Run, Compute Engine, Artifact Registry, and Secret Manager."
}

Write-Host "Linking billing account $BillingAccountId to $ProjectId..."
& $gcloud billing projects link $ProjectId --billing-account=$BillingAccountId
Assert-LastExitCode "gcloud billing projects link"

$services = @(
  "artifactregistry.googleapis.com",
  "cloudbuild.googleapis.com",
  "compute.googleapis.com",
  "iam.googleapis.com",
  "iamcredentials.googleapis.com",
  "run.googleapis.com",
  "secretmanager.googleapis.com",
  "serviceusage.googleapis.com",
  "sts.googleapis.com"
)

Write-Host "Enabling required APIs..."
& $gcloud services enable $services --project=$ProjectId
Assert-LastExitCode "gcloud services enable"

Write-Host "Bootstrap complete for $ProjectId using $RequiredAccount."
