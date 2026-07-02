param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = "nickaccturk@gmail.com",
  [string]$Region = "us-central1",
  [string]$Zone = "us-central1-a",
  [string]$ServiceName = "devcontrol",
  [string]$PostgresInstanceName = "devcontrol-postgres",
  [string]$ArtifactRepository = "devcontrol-images",
  [string]$BackupBucketUrl
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID or pass -ProjectId."
}

if ([string]::IsNullOrWhiteSpace($BackupBucketUrl)) {
  $BackupBucketUrl = "gs://$ProjectId-devcontrol-postgres-backups"
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount
$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

function Assert-True {
  param(
    [bool]$Condition,
    [string]$Message
  )

  if (-not $Condition) {
    throw $Message
  }
}

function To-JsonObject {
  param(
    [string[]]$Command,
    [string]$Operation
  )

  $json = & $gcloud @Command
  Assert-LastExitCode $Operation
  return ($json | ConvertFrom-Json)
}

$service = To-JsonObject `
  -Command @("run", "services", "describe", $ServiceName, "--project", $ProjectId, "--region", $Region, "--format=json") `
  -Operation "describe Cloud Run service"
$container = $null
$minInstances = 0
$maxInstances = 0
if ($null -ne $service.template) {
  $scaling = $service.template.scaling
  $minInstances = if ($null -eq $scaling.minInstanceCount) { 0 } else { [int]$scaling.minInstanceCount }
  $maxInstances = [int]$scaling.maxInstanceCount
  $container = $service.template.containers[0]
} else {
  $annotations = $service.spec.template.metadata.annotations
  $minInstances = if ($null -eq $annotations."autoscaling.knative.dev/minScale") { 0 } else { [int]$annotations."autoscaling.knative.dev/minScale" }
  $maxInstances = [int]$annotations."autoscaling.knative.dev/maxScale"
  $container = $service.spec.template.spec.containers[0]
}

Assert-True ($service.metadata.name -eq $ServiceName) "Cloud Run service name is not $ServiceName."
Assert-True ($service.metadata.labels.app -eq "devcontrol") "Cloud Run service is missing app=devcontrol label."
Assert-True ($minInstances -eq 0) "Cloud Run min instances must be 0."
Assert-True ($maxInstances -eq 1) "Cloud Run max instances must be 1."
Assert-True ($container.resources.limits.memory -eq "512Mi") "Cloud Run memory limit must be 512Mi."
Assert-True ($container.resources.limits.cpu -eq "1") "Cloud Run CPU limit must be 1."

$instance = To-JsonObject `
  -Command @("compute", "instances", "describe", $PostgresInstanceName, "--project", $ProjectId, "--zone", $Zone, "--format=json") `
  -Operation "describe PostgreSQL VM"
Assert-True ($instance.machineType.EndsWith("/e2-micro")) "PostgreSQL VM must be e2-micro."
$bootDisk = $instance.disks | Where-Object { $_.boot -eq $true } | Select-Object -First 1
$dataDisk = $instance.disks | Where-Object { $_.deviceName -eq "postgres-data" } | Select-Object -First 1
Assert-True ($null -ne $bootDisk) "PostgreSQL boot disk was not found."
Assert-True ([int]$bootDisk.diskSizeGb -eq 10) "PostgreSQL boot disk must be 10 GB."
Assert-True ($null -ne $dataDisk) "PostgreSQL data disk was not found."
Assert-True ([int]$dataDisk.diskSizeGb -eq 20) "PostgreSQL data disk must be 20 GB."

$artifactRepo = To-JsonObject `
  -Command @("artifacts", "repositories", "describe", $ArtifactRepository, "--project", $ProjectId, "--location", $Region, "--format=json") `
  -Operation "describe Artifact Registry repository"
$cleanupPolicyIds = @()
if ($artifactRepo.cleanupPolicies -is [array]) {
  $cleanupPolicyIds = @($artifactRepo.cleanupPolicies | ForEach-Object { $_.id })
} elseif ($null -ne $artifactRepo.cleanupPolicies) {
  $cleanupPolicyIds = @($artifactRepo.cleanupPolicies.PSObject.Properties.Name)
}
Assert-True ($cleanupPolicyIds -contains "keep-recent") "Artifact Registry keep-recent cleanup policy is missing."
Assert-True ($cleanupPolicyIds -contains "delete-older-untagged") "Artifact Registry delete-older-untagged cleanup policy is missing."

$bucket = To-JsonObject `
  -Command @("storage", "buckets", "describe", $BackupBucketUrl, "--format=json") `
  -Operation "describe backup bucket"
$bucketStorageClass = if ($null -ne $bucket.storageClass) { $bucket.storageClass } elseif ($null -ne $bucket.defaultStorageClass) { $bucket.defaultStorageClass } else { $bucket.default_storage_class }
$bucketUniformAccess = if ($null -ne $bucket.iamConfiguration) { $bucket.iamConfiguration.uniformBucketLevelAccess.enabled } else { $bucket.uniform_bucket_level_access }
$bucketPublicAccessPrevention = if ($null -ne $bucket.iamConfiguration) { $bucket.iamConfiguration.publicAccessPrevention } else { $bucket.public_access_prevention }
$bucketVersioningEnabled = if ($null -ne $bucket.versioning) { $bucket.versioning.enabled } else { $bucket.versioning_enabled }
$bucketLifecycleRules = if ($null -ne $bucket.lifecycle) { $bucket.lifecycle.rule } else { $bucket.lifecycle_config.rule }

Assert-True ($bucket.location.ToLowerInvariant() -eq $Region) "Backup bucket must be in $Region."
Assert-True ($bucketStorageClass -eq "STANDARD") "Backup bucket must use STANDARD storage."
Assert-True ($bucketUniformAccess -eq $true) "Backup bucket must use uniform bucket-level access."
Assert-True ($bucketPublicAccessPrevention -eq "enforced") "Backup bucket must enforce public access prevention."
Assert-True ($bucketVersioningEnabled -eq $false) "Backup bucket versioning must be disabled."
$deleteRules = @($bucketLifecycleRules | Where-Object { $_.action.type -eq "Delete" -and [int]$_.condition.age -eq 7 })
Assert-True ($deleteRules.Count -ge 1) "Backup bucket must delete objects after 7 days."

$runServiceNames = & $gcloud run services list --project $ProjectId --region $Region --format="value(metadata.name)"
Assert-LastExitCode "list Cloud Run services"
$deployedObservabilityServices = @($runServiceNames | Where-Object { $_ -match "prometheus|grafana" })
Assert-True ($deployedObservabilityServices.Count -eq 0) "Prometheus/Grafana must not be deployed to Cloud Run: $($deployedObservabilityServices -join ', ')"

$vmNames = & $gcloud compute instances list --project $ProjectId --format="value(name)"
Assert-LastExitCode "list Compute Engine VMs"
$deployedObservabilityVms = @($vmNames | Where-Object { $_ -match "prometheus|grafana" })
Assert-True ($deployedObservabilityVms.Count -eq 0) "Prometheus/Grafana must not be deployed as VMs: $($deployedObservabilityVms -join ', ')"

Write-Host "Free-tier guard check passed for project $ProjectId."
