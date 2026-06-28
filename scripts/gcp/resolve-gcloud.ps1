$ErrorActionPreference = "Stop"

$command = Get-Command gcloud -ErrorAction SilentlyContinue
if ($command) {
  return $command.Source
}

$candidates = @(
  "$env:LOCALAPPDATA\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd",
  "C:\Program Files\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd",
  "C:\Program Files (x86)\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd"
)

foreach ($candidate in $candidates) {
  if (Test-Path -LiteralPath $candidate) {
    return $candidate
  }
}

throw "Google Cloud SDK is not installed or gcloud is not on PATH."

