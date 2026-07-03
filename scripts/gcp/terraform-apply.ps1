param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = $env:DEVCONTROL_GCP_REQUIRED_ACCOUNT,
  [string]$GithubOwner = $env:DEVCONTROL_GITHUB_OWNER,
  [string]$GithubRepo = $env:DEVCONTROL_GITHUB_REPO,
  [string]$Region = "us-central1",
  [string]$ServiceName = "devcontrol",
  [switch]$AutoApprove
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID before running Terraform."
}

if ([string]::IsNullOrWhiteSpace($GithubOwner) -or [string]::IsNullOrWhiteSpace($GithubRepo)) {
  throw "Set DEVCONTROL_GITHUB_OWNER and DEVCONTROL_GITHUB_REPO before running Terraform."
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount
$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

function Set-TerraformEnvIfEmpty {
  param(
    [string]$Name,
    [string]$Value
  )

  if (-not [string]::IsNullOrWhiteSpace($Value) -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Name))) {
    [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
  }
}

function Try-LoadSecret {
  param([string]$SecretName)

  try {
    & $gcloud secrets describe $SecretName --project $ProjectId --format="value(name)" 2>$null | Out-Null
  } catch {
    return $null
  }

  if ($LASTEXITCODE -ne 0) {
    return $null
  }

  return "__preserve_existing_secret__"
}

function Preserve-LiveOptionalTerraformVariables {
  $serviceJson = & $gcloud run services describe $ServiceName --project $ProjectId --region $Region --format=json 2>$null
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($serviceJson)) {
    return
  }

  $service = $serviceJson | ConvertFrom-Json
  $envItems = if ($null -ne $service.template) { $service.template.containers[0].env } else { $service.spec.template.spec.containers[0].env }
  Set-TerraformEnvIfEmpty "TF_VAR_auth_google_client_id" (($envItems | Where-Object name -eq "DEVCONTROL_AUTH_GOOGLE_CLIENT_ID" | Select-Object -First 1).value)
  Set-TerraformEnvIfEmpty "TF_VAR_github_app_id" (($envItems | Where-Object name -eq "DEVCONTROL_GITHUB_APP_ID" | Select-Object -First 1).value)
  Set-TerraformEnvIfEmpty "TF_VAR_auth_google_client_secret" (Try-LoadSecret "devcontrol-google-oauth-client-secret")
  Set-TerraformEnvIfEmpty "TF_VAR_operator_bootstrap_secret" (Try-LoadSecret "devcontrol-operator-bootstrap-secret")
  Set-TerraformEnvIfEmpty "TF_VAR_github_app_private_key" (Try-LoadSecret "devcontrol-github-app-private-key")
  Set-TerraformEnvIfEmpty "TF_VAR_smtp_password" (Try-LoadSecret "devcontrol-smtp-password")
}

Preserve-LiveOptionalTerraformVariables

Push-Location "$PSScriptRoot\..\..\infra\gcp"
try {
  terraform init
  Assert-LastExitCode "terraform init"
  $applyArgs = @(
    "-var", "project_id=$ProjectId",
    "-var", "github_owner=$GithubOwner",
    "-var", "github_repo=$GithubRepo",
    "-var", "operator_google_account=$RequiredAccount"
  )
  if ($AutoApprove) {
    $applyArgs = @("-auto-approve") + $applyArgs
  }

  terraform apply @applyArgs
  Assert-LastExitCode "terraform apply"
} finally {
  Pop-Location
}
