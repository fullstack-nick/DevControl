param(
  [string]$ServiceUrl,
  [string]$RequiredAccount = "nickaccturk@gmail.com"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ServiceUrl)) {
  throw "Pass -ServiceUrl with the Cloud Run service URL."
}

& "$PSScriptRoot\assert-gcp-account.ps1" -RequiredAccount $RequiredAccount

$baseUrl = $ServiceUrl.TrimEnd("/")
$live = Invoke-WebRequest -Uri "$baseUrl/health/live" -UseBasicParsing
$ready = Invoke-WebRequest -Uri "$baseUrl/health/ready" -UseBasicParsing

if ($live.StatusCode -ne 200) {
  throw "/health/live returned $($live.StatusCode)."
}

if ($ready.StatusCode -ne 200) {
  throw "/health/ready returned $($ready.StatusCode)."
}

Write-Host "Cloud Run smoke test passed for $baseUrl."

