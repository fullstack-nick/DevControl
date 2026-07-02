param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = "nickaccturk@gmail.com",
  [string]$Region = "us-central1",
  [string]$ServiceName = "devcontrol",
  [string]$ObservabilityServiceName = "devcontrol-observability",
  [string]$MetricsSecretId = "devcontrol-metrics-scrape-token",
  [string]$GrafanaAdminSecretId = "devcontrol-grafana-admin-password",
  [int]$Retries = 12,
  [int]$DelaySeconds = 10
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectId)) {
  throw "Set DEVCONTROL_GCP_PROJECT_ID or pass -ProjectId."
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount
$gcloud = & "$PSScriptRoot\resolve-gcloud.ps1"

function Assert-LastExitCode {
  param([string]$Operation)

  if ($LASTEXITCODE -ne 0) {
    throw "$Operation failed with exit code $LASTEXITCODE."
  }
}

function Get-SecretValue {
  param([string]$SecretId)

  $value = & $gcloud secrets versions access latest --project $ProjectId --secret $SecretId
  Assert-LastExitCode "access secret $SecretId"
  return (($value -join "`n").Trim())
}

function Invoke-WithRetries {
  param(
    [scriptblock]$Operation,
    [string]$Description
  )

  $lastError = $null
  for ($attempt = 1; $attempt -le $Retries; $attempt++) {
    try {
      return & $Operation
    } catch {
      $lastError = $_
      if ($attempt -eq $Retries) {
        break
      }

      Write-Host "$Description failed on attempt $attempt/$Retries. Retrying in $DelaySeconds seconds."
      Start-Sleep -Seconds $DelaySeconds
    }
  }

  throw $lastError
}

$serviceUrl = & $gcloud run services describe $ServiceName --project $ProjectId --region $Region --format="value(status.url)"
Assert-LastExitCode "describe Cloud Run service $ServiceName"
$serviceUrl = ($serviceUrl -join "").Trim()
if ([string]::IsNullOrWhiteSpace($serviceUrl)) {
  throw "Could not resolve Cloud Run URL for $ServiceName."
}

$grafanaUrl = & $gcloud run services describe $ObservabilityServiceName --project $ProjectId --region $Region --format="value(status.url)"
Assert-LastExitCode "describe Cloud Run service $ObservabilityServiceName"
$grafanaUrl = ($grafanaUrl -join "").Trim()
if ([string]::IsNullOrWhiteSpace($grafanaUrl)) {
  throw "Could not resolve Cloud Run URL for $ObservabilityServiceName."
}

$metricsToken = Get-SecretValue -SecretId $MetricsSecretId
$grafanaPassword = Get-SecretValue -SecretId $GrafanaAdminSecretId

$missingTokenStatus = $null
try {
  $missingTokenResponse = Invoke-WebRequest -Uri "$serviceUrl/metrics" -Method Get -MaximumRedirection 0
  $missingTokenStatus = [int]$missingTokenResponse.StatusCode
} catch {
  $missingTokenStatus = [int]$_.Exception.Response.StatusCode
}
if ($missingTokenStatus -eq 200) {
  throw "Live /metrics returned 200 without a bearer token."
}

$metricsResponse = Invoke-WithRetries -Description "tokened /metrics" -Operation {
  Invoke-WebRequest `
    -Uri "$serviceUrl/metrics" `
    -Method Get `
    -Headers @{ Authorization = "Bearer $metricsToken" }
}
if ($metricsResponse.StatusCode -ne 200 -or $metricsResponse.Content -notmatch "devcontrol_http_requests_total") {
  throw "Live /metrics did not return expected Prometheus text."
}

$basicAuthBytes = [System.Text.Encoding]::ASCII.GetBytes("admin:$grafanaPassword")
$basicAuth = [Convert]::ToBase64String($basicAuthBytes)
$grafanaHeaders = @{ Authorization = "Basic $basicAuth" }

$grafanaHealth = Invoke-WithRetries -Description "Grafana health" -Operation {
  Invoke-RestMethod -Uri "$grafanaUrl/api/health" -Headers $grafanaHeaders
}
if ($grafanaHealth.database -ne "ok") {
  throw "Grafana health did not report an ok database."
}

$targets = Invoke-WithRetries -Description "Prometheus target query through Grafana" -Operation {
  Invoke-RestMethod -Uri "$grafanaUrl/api/datasources/proxy/uid/Prometheus/api/v1/targets" -Headers $grafanaHeaders
}
$activeTargets = @($targets.data.activeTargets)
$upTargets = @($activeTargets | Where-Object { $_.health -eq "up" -and $_.labels.job -eq "devcontrol-live" })
if ($upTargets.Count -lt 1) {
  throw "Prometheus did not report an up devcontrol-live target."
}

[pscustomobject]@{
  serviceUrl = $serviceUrl
  grafanaUrl = $grafanaUrl
  metricsWithoutTokenStatus = $missingTokenStatus
  metricsWithTokenStatus = [int]$metricsResponse.StatusCode
  prometheusTargetHealth = $upTargets[0].health
  prometheusLastScrape = $upTargets[0].lastScrape
} | ConvertTo-Json -Depth 5
