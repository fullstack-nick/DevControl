param(
  [string]$ProjectId = $env:DEVCONTROL_GCP_PROJECT_ID,
  [string]$RequiredAccount = $env:DEVCONTROL_GCP_REQUIRED_ACCOUNT,
  [string]$Region = "us-central1",
  [string]$ServiceName = "devcontrol",
  [string]$ObservabilityServiceName = "devcontrol-observability",
  [string]$MetricsSecretId = "devcontrol-metrics-scrape-token",
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

function Invoke-CurlText {
  param([string[]]$Arguments)

  $output = & curl.exe @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "curl failed with exit code $LASTEXITCODE."
  }

  return ($output -join "`n")
}

$serviceUrl = & $gcloud run services describe $ServiceName --project $ProjectId --region $Region --format="value(status.url)"
Assert-LastExitCode "describe Cloud Run service $ServiceName"
$serviceUrl = ($serviceUrl -join "").Trim()
if ([string]::IsNullOrWhiteSpace($serviceUrl)) {
  throw "Could not resolve Cloud Run URL for $ServiceName."
}

$observabilityUpstreamUrl = & $gcloud run services describe $ObservabilityServiceName --project $ProjectId --region $Region --format="value(status.url)"
Assert-LastExitCode "describe Cloud Run service $ObservabilityServiceName"
$observabilityUpstreamUrl = ($observabilityUpstreamUrl -join "").Trim()
if ([string]::IsNullOrWhiteSpace($observabilityUpstreamUrl)) {
  throw "Could not resolve Cloud Run URL for $ObservabilityServiceName."
}

$metricsToken = Get-SecretValue -SecretId $MetricsSecretId

$missingTokenStatus = Invoke-CurlText -Arguments @(
  "--silent",
  "--output", "NUL",
  "--write-out", "%{http_code}",
  "$serviceUrl/metrics"
)
if ($missingTokenStatus -eq 200) {
  throw "Live /metrics returned 200 without a bearer token."
}

$metricsBody = Invoke-WithRetries -Description "tokened /metrics" -Operation {
  Invoke-CurlText -Arguments @(
    "--fail",
    "--silent",
    "--header", "Authorization: Bearer $metricsToken",
    "$serviceUrl/metrics"
  )
}
$metricsStatus = Invoke-CurlText -Arguments @(
  "--silent",
  "--output", "NUL",
  "--write-out", "%{http_code}",
  "--header", "Authorization: Bearer $metricsToken",
  "$serviceUrl/metrics"
)
if ($metricsStatus -ne "200" -or $metricsBody -notmatch "devcontrol_http_requests_total") {
  throw "Live /metrics did not return expected Prometheus text."
}

$publicConfig = Invoke-WithRetries -Description "DevControl public config" -Operation {
  Invoke-RestMethod -Uri "$serviceUrl/api/public/config"
}
if ($publicConfig.observabilityUrl -ne "/observability/") {
  throw "DevControl public config did not return the proxied observability path."
}

$directGrafanaStatus = Invoke-CurlText -Arguments @(
  "--silent",
  "--output", "NUL",
  "--write-out", "%{http_code}",
  "$observabilityUpstreamUrl/api/health"
)
if ($directGrafanaStatus -eq "200") {
  throw "Direct observability Cloud Run returned 200 without Cloud Run IAM."
}

[pscustomobject]@{
  serviceUrl = $serviceUrl
  observabilityUrl = "$serviceUrl/observability/"
  observabilityUpstreamUrl = $observabilityUpstreamUrl
  metricsWithoutTokenStatus = $missingTokenStatus
  metricsWithTokenStatus = [int]$metricsStatus
  directGrafanaWithoutIamStatus = $directGrafanaStatus
} | ConvertTo-Json -Depth 5
