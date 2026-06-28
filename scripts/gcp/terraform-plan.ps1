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

Push-Location "$PSScriptRoot\..\..\infra\gcp"
try {
  terraform init
  terraform plan `
    -var "project_id=$ProjectId" `
    -var "github_owner=$GithubOwner" `
    -var "github_repo=$GithubRepo" `
    -var "operator_google_account=$RequiredAccount"
} finally {
  Pop-Location
}

