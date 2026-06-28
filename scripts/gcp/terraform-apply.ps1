param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = "nickaccturk@gmail.com",
  [string]$GithubOwner = $env:DEVCONTROL_GITHUB_OWNER,
  [string]$GithubRepo = $env:DEVCONTROL_GITHUB_REPO
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID before running Terraform."
}

if ([string]::IsNullOrWhiteSpace($GithubOwner) -or [string]::IsNullOrWhiteSpace($GithubRepo)) {
  throw "Set DEVCONTROL_GITHUB_OWNER and DEVCONTROL_GITHUB_REPO before running Terraform."
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

Push-Location "$PSScriptRoot\..\..\infra\gcp"
try {
  terraform init
  Assert-LastExitCode "terraform init"
  terraform apply `
    -var "project_id=$ProjectId" `
    -var "github_owner=$GithubOwner" `
    -var "github_repo=$GithubRepo" `
    -var "operator_google_account=$RequiredAccount"
  Assert-LastExitCode "terraform apply"
} finally {
  Pop-Location
}
