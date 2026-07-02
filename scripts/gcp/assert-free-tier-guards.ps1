param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = "nickaccturk@gmail.com",
  [string]$Region = "us-central1",
  [string]$Zone = "us-central1-a",
  [string]$ServiceName = "devcontrol",
  [string]$ObservabilityServiceName = "devcontrol-observability",
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

function Get-RunTemplateDetails {
  param($Service)

  if ($null -ne $Service.template) {
    $scaling = $Service.template.scaling
    return [pscustomobject]@{
      MinInstances = if ($null -eq $scaling.minInstanceCount) { 0 } else { [int]$scaling.minInstanceCount }
      MaxInstances = [int]$scaling.maxInstanceCount
      Containers = @($Service.template.containers)
    }
  }

  $annotations = $Service.spec.template.metadata.annotations
  return [pscustomobject]@{
    MinInstances = if ($null -eq $annotations."autoscaling.knative.dev/minScale") { 0 } else { [int]$annotations."autoscaling.knative.dev/minScale" }
    MaxInstances = [int]$annotations."autoscaling.knative.dev/maxScale"
    Containers = @($Service.spec.template.spec.containers)
  }
}

function Get-ContainerCpuIdle {
  param($Container)

  if ($null -ne $Container.resources.cpuIdle) {
    return [bool]$Container.resources.cpuIdle
  }

  return $true
}

function Get-RunServiceAccount {
  param($Service)

  if ($null -ne $Service.template.serviceAccount) {
    return $Service.template.serviceAccount
  }

  return $Service.spec.template.spec.serviceAccountName
}

$service = To-JsonObject `
  -Command @("run", "services", "describe", $ServiceName, "--project", $ProjectId, "--region", $Region, "--format=json") `
  -Operation "describe Cloud Run service"
$serviceTemplate = Get-RunTemplateDetails -Service $service
$container = $serviceTemplate.Containers[0]

Assert-True ($service.metadata.name -eq $ServiceName) "Cloud Run service name is not $ServiceName."
Assert-True ($service.metadata.labels.app -eq "devcontrol") "Cloud Run service is missing app=devcontrol label."
Assert-True ($serviceTemplate.MinInstances -eq 0) "Cloud Run min instances must be 0."
Assert-True ($serviceTemplate.MaxInstances -eq 1) "Cloud Run max instances must be 1."
Assert-True ($container.resources.limits.memory -eq "512Mi") "Cloud Run memory limit must be 512Mi."
Assert-True ($container.resources.limits.cpu -eq "1") "Cloud Run CPU limit must be 1."
Assert-True (Get-ContainerCpuIdle -Container $container) "Cloud Run must use request-based billing/cpu_idle=true."

$observabilityService = To-JsonObject `
  -Command @("run", "services", "describe", $ObservabilityServiceName, "--project", $ProjectId, "--region", $Region, "--format=json") `
  -Operation "describe live observability Cloud Run service"
$observabilityTemplate = Get-RunTemplateDetails -Service $observabilityService
$observabilityContainers = @($observabilityTemplate.Containers)
$grafanaContainer = $observabilityContainers | Where-Object { $_.name -eq "grafana" } | Select-Object -First 1
$prometheusContainer = $observabilityContainers | Where-Object { $_.name -eq "prometheus" } | Select-Object -First 1

Assert-True ($observabilityService.metadata.name -eq $ObservabilityServiceName) "Observability Cloud Run service name is not $ObservabilityServiceName."
Assert-True ($observabilityService.metadata.labels.app -eq "devcontrol") "Observability Cloud Run service is missing app=devcontrol label."
Assert-True ($observabilityTemplate.MinInstances -eq 0) "Observability Cloud Run min instances must be 0."
Assert-True ($observabilityTemplate.MaxInstances -eq 1) "Observability Cloud Run max instances must be 1."
Assert-True ($observabilityContainers.Count -eq 2) "Observability Cloud Run must have exactly Grafana and Prometheus containers."
Assert-True ($null -ne $grafanaContainer) "Grafana container is missing from observability Cloud Run."
Assert-True ($null -ne $prometheusContainer) "Prometheus container is missing from observability Cloud Run."
Assert-True ($grafanaContainer.resources.limits.memory -eq "512Mi") "Grafana memory limit must be 512Mi."
Assert-True ($grafanaContainer.resources.limits.cpu -eq "1") "Grafana CPU limit must be 1."
Assert-True (Get-ContainerCpuIdle -Container $grafanaContainer) "Grafana must use request-based billing/cpu_idle=true."
Assert-True ($prometheusContainer.resources.limits.memory -eq "512Mi") "Prometheus memory limit must be 512Mi."
Assert-True ($prometheusContainer.resources.limits.cpu -eq "1") "Prometheus CPU limit must be 1."
Assert-True (Get-ContainerCpuIdle -Container $prometheusContainer) "Prometheus must use request-based billing/cpu_idle=true."

$runtimeServiceAccount = Get-RunServiceAccount -Service $service
$observabilityIamPolicy = To-JsonObject `
  -Command @("run", "services", "get-iam-policy", $ObservabilityServiceName, "--project", $ProjectId, "--region", $Region, "--format=json") `
  -Operation "get observability Cloud Run IAM policy"
$observabilityInvokerMembers = @(
  $observabilityIamPolicy.bindings |
    Where-Object { $_.role -eq "roles/run.invoker" } |
    ForEach-Object { $_.members } |
    ForEach-Object { $_ }
)
Assert-True ($observabilityInvokerMembers -notcontains "allUsers") "Observability Cloud Run must not allow public allUsers invoker."
Assert-True ($observabilityInvokerMembers -notcontains "allAuthenticatedUsers") "Observability Cloud Run must not allow allAuthenticatedUsers invoker."
Assert-True ($observabilityInvokerMembers -contains "serviceAccount:$runtimeServiceAccount") "Observability Cloud Run must allow the DevControl runtime service account to invoke it."

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
$deployedObservabilityServices = @($runServiceNames | Where-Object {
  ($_ -match "prometheus|grafana|observability") -and $_ -ne $ObservabilityServiceName
})
Assert-True ($deployedObservabilityServices.Count -eq 0) "Unexpected observability Cloud Run services are deployed: $($deployedObservabilityServices -join ', ')"
Assert-True (@($runServiceNames | Where-Object { $_ -eq $ObservabilityServiceName }).Count -eq 1) "Approved observability Cloud Run service $ObservabilityServiceName was not found."

$vmNames = & $gcloud compute instances list --project $ProjectId --format="value(name)"
Assert-LastExitCode "list Compute Engine VMs"
$deployedObservabilityVms = @($vmNames | Where-Object { $_ -match "prometheus|grafana|observability" })
Assert-True ($deployedObservabilityVms.Count -eq 0) "Prometheus/Grafana/observability must not be deployed as VMs: $($deployedObservabilityVms -join ', ')"

Write-Host "Free-tier guard check passed for project $ProjectId."
