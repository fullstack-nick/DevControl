param(
  [string]$RequiredAccount = $env:DEVCONTROL_GCP_REQUIRED_ACCOUNT
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RequiredAccount)) {
  throw "Set DEVCONTROL_GCP_REQUIRED_ACCOUNT or pass -RequiredAccount before running GCP scripts."
}

$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
  $activeAccount = (& $gcloud config get-value account --quiet 2>&1 | Select-Object -First 1)
} finally {
  $ErrorActionPreference = $previousErrorActionPreference
}

if ($activeAccount -is [System.Management.Automation.ErrorRecord]) {
  $activeAccount = $activeAccount.Exception.Message
}

$activeAccount = if ($null -eq $activeAccount) { "" } else { ([string]$activeAccount).Trim() }

if ([string]::IsNullOrWhiteSpace($activeAccount) -or $activeAccount -eq "(unset)") {
  throw "No active gcloud account. Run: gcloud auth login $RequiredAccount"
}

if ($activeAccount -ne $RequiredAccount) {
  throw "Refusing to touch GCP. Active gcloud account is '$activeAccount', but DevControl requires '$RequiredAccount'."
}

Write-Host "GCP account guard passed: $activeAccount"
