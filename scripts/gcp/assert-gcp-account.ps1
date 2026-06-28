param(
  [string]$RequiredAccount = "nickaccturk@gmail.com"
)

$ErrorActionPreference = "Stop"

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
