param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = $env:DEVCONTROL_GCP_REQUIRED_ACCOUNT,
  [string]$Zone = "us-central1-a",
  [string]$InstanceName = "devcontrol-postgres",
  [string]$Database = "devcontrol",
  [string]$Network = "devcontrol-vpc",
  [string]$TargetTag = "devcontrol-postgres",
  [string]$SshSourceRange,
  [switch]$SkipTemporarySshFirewall,
  [string]$BucketUrl,
  [string]$LocalOutputDirectory = ".artifacts/backups"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID or pass -ProjectId."
}

if ([string]::IsNullOrWhiteSpace($BucketUrl)) {
  $BucketUrl = "gs://$ProjectId-devcontrol-postgres-backups"
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount
$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

function Get-OperatorSshSourceRange {
  if (-not [string]::IsNullOrWhiteSpace($SshSourceRange)) {
    return $SshSourceRange
  }

  $publicIp = (Invoke-RestMethod -UseBasicParsing -Uri "https://api.ipify.org" -TimeoutSec 10).Trim()
  if ($publicIp -notmatch "^\d{1,3}(\.\d{1,3}){3}$") {
    throw "Could not determine an IPv4 source range for temporary SSH access."
  }

  return "$publicIp/32"
}

function New-TemporarySshFirewallRule {
  if ($SkipTemporarySshFirewall) {
    return $null
  }

  $sourceRange = Get-OperatorSshSourceRange
  $ruleName = "devcontrol-ssh-$((New-Guid).ToString('N').Substring(0, 12))"
  & $gcloud compute firewall-rules create $ruleName `
    --project $ProjectId `
    --network $Network `
    --direction INGRESS `
    --priority 1000 `
    --allow tcp:22 `
    --source-ranges $sourceRange `
    --target-tags $TargetTag `
    --description "Temporary DevControl operator SSH for PostgreSQL backup." `
    --quiet | Out-Null
  Assert-LastExitCode "create temporary SSH firewall rule $ruleName"
  Write-Host "Temporary SSH firewall rule $ruleName created for $sourceRange."
  return $ruleName
}

function Remove-TemporarySshFirewallRule {
  param([string]$RuleName)

  if ([string]::IsNullOrWhiteSpace($RuleName)) {
    return
  }

  & $gcloud compute firewall-rules delete $RuleName --project $ProjectId --quiet | Out-Null
  if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to remove temporary SSH firewall rule $RuleName. Remove it manually."
  } else {
    Write-Host "Temporary SSH firewall rule $RuleName removed."
  }
}

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$fileName = "devcontrol-$timestamp.dump"
$remotePath = "/tmp/$fileName"
$localDirectory = Join-Path (Get-Location) $LocalOutputDirectory
$localPath = Join-Path $localDirectory $fileName

New-Item -ItemType Directory -Force -Path $localDirectory | Out-Null

$firewallRule = New-TemporarySshFirewallRule

try {
  $dumpCommand = "sudo -u postgres pg_dump -Fc -d '$Database' -f '$remotePath'"
  & $gcloud compute ssh $InstanceName `
    --project $ProjectId `
    --zone $Zone `
    --command $dumpCommand
  Assert-LastExitCode "pg_dump on $InstanceName"

  try {
    & $gcloud compute scp "$InstanceName`:$remotePath" $localPath `
      --project $ProjectId `
      --zone $Zone
    Assert-LastExitCode "copy backup from $InstanceName"
  } finally {
    & $gcloud compute ssh $InstanceName `
      --project $ProjectId `
      --zone $Zone `
      --command "sudo rm -f '$remotePath'"
  }
} finally {
  Remove-TemporarySshFirewallRule -RuleName $firewallRule
}

$hash = Get-FileHash -Algorithm SHA256 -Path $localPath
$sizeBytes = (Get-Item -LiteralPath $localPath).Length

& $gcloud storage cp $localPath "$BucketUrl/$fileName"
Assert-LastExitCode "upload backup to $BucketUrl"

[pscustomobject]@{
  LocalPath = $localPath
  CloudObject = "$BucketUrl/$fileName"
  SizeBytes = $sizeBytes
  Sha256 = $hash.Hash
} | ConvertTo-Json
