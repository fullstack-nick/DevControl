param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = $env:DEVCONTROL_GCP_REQUIRED_ACCOUNT,
  [string]$Zone = "us-central1-a",
  [string]$InstanceName = "devcontrol-postgres",
  [string]$Network = "devcontrol-vpc",
  [string]$TargetTag = "devcontrol-postgres",
  [string]$SshSourceRange,
  [switch]$SkipTemporarySshFirewall,
  [string]$BucketUrl,
  [string]$BackupObject,
  [string]$LocalDumpPath,
  [string]$RestoreDatabase = "devcontrol_restore_verify",
  [switch]$KeepDatabase,
  [switch]$AllowProductionOverwrite
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID or pass -ProjectId."
}

if ([string]::IsNullOrWhiteSpace($BucketUrl)) {
  $BucketUrl = "gs://$ProjectId-devcontrol-postgres-backups"
}

if ($RestoreDatabase -eq "devcontrol" -and -not $AllowProductionOverwrite) {
  throw "Refusing to restore over production database 'devcontrol'. Use the default verification database or pass -AllowProductionOverwrite intentionally."
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
    --description "Temporary DevControl operator SSH for PostgreSQL restore verification." `
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

if ([string]::IsNullOrWhiteSpace($LocalDumpPath)) {
  if ([string]::IsNullOrWhiteSpace($BackupObject)) {
    $objects = & $gcloud storage ls $BucketUrl
    Assert-LastExitCode "list backups in $BucketUrl"
    $BackupObject = $objects |
      Where-Object { $_ -match "\.dump$" } |
      Sort-Object |
      Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($BackupObject)) {
      throw "No .dump backups found in $BucketUrl."
    }
  }

  $downloadDirectory = Join-Path (Get-Location) ".artifacts/restore"
  New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null
  $LocalDumpPath = Join-Path $downloadDirectory (Split-Path -Leaf $BackupObject)
  & $gcloud storage cp $BackupObject $LocalDumpPath
  Assert-LastExitCode "download $BackupObject"
}

if (-not (Test-Path -LiteralPath $LocalDumpPath)) {
  throw "Backup dump '$LocalDumpPath' does not exist."
}

$remotePath = "/tmp/$(Split-Path -Leaf $LocalDumpPath)"
$dropAfter = if ($KeepDatabase) { "false" } else { "true" }
$remoteScriptPath = "/tmp/devcontrol-restore-verify-$((New-Guid).ToString('N').Substring(0, 12)).sh"
$restoreScript = @"
set -euo pipefail
RESTORE_DATABASE='$RestoreDatabase'
REMOTE_PATH='$remotePath'
DROP_AFTER='$dropAfter'

cleanup() {
  rm -f "`$REMOTE_PATH"
  if [ "`$DROP_AFTER" = "true" ]; then
    sudo -u postgres dropdb --if-exists "`$RESTORE_DATABASE" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

sudo -u postgres dropdb --if-exists "`$RESTORE_DATABASE"
sudo -u postgres createdb "`$RESTORE_DATABASE"
sudo -u postgres pg_restore --no-owner -d "`$RESTORE_DATABASE" "`$REMOTE_PATH"
TABLE_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from information_schema.tables where table_schema = 'public';")
SCHEMA_VERSION_TABLE_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'schema_versions';")
SCHEMA_VERSION_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from schema_versions;")
USER_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from users;")
ORGANIZATION_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from organizations;")
LIVE_APP_COUNT=`$(sudo -u postgres psql -d "`$RESTORE_DATABASE" -tAc "select count(*) from live_apps;")
if [ "`$TABLE_COUNT" -lt 10 ]; then
  echo "Restore verification failed: expected at least 10 public tables, found `$TABLE_COUNT." >&2
  exit 1
fi
if [ "`$SCHEMA_VERSION_TABLE_COUNT" -ne 1 ]; then
  echo "Restore verification failed: schema_versions table was not restored." >&2
  exit 1
fi
echo "restore_database=`$RESTORE_DATABASE"
echo "table_count=`$TABLE_COUNT"
echo "schema_version_count=`$SCHEMA_VERSION_COUNT"
echo "user_count=`$USER_COUNT"
echo "organization_count=`$ORGANIZATION_COUNT"
echo "live_app_count=`$LIVE_APP_COUNT"
echo "kept_database=$KeepDatabase"
"@
$restoreScriptBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($restoreScript))
$restoreCommand = "printf '%s' '$restoreScriptBase64' | base64 -d > '$remoteScriptPath'; bash '$remoteScriptPath'; status=`$?; rm -f '$remoteScriptPath'; exit `$status"

$firewallRule = New-TemporarySshFirewallRule
try {
  & $gcloud compute scp $LocalDumpPath "$InstanceName`:$remotePath" `
    --project $ProjectId `
    --zone $Zone
  Assert-LastExitCode "copy dump to $InstanceName"

  & $gcloud compute ssh $InstanceName `
    --project $ProjectId `
    --zone $Zone `
    --command $restoreCommand
  Assert-LastExitCode "restore verification on $InstanceName"
} catch {
  try {
    & $gcloud compute ssh $InstanceName `
      --project $ProjectId `
      --zone $Zone `
      --command "rm -f '$remotePath'"
  } catch {
    Write-Warning "Failed to remove remote temporary dump '$remotePath' after restore error."
  }
  throw
} finally {
  Remove-TemporarySshFirewallRule -RuleName $firewallRule
}
