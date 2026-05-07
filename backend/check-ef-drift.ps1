$ErrorActionPreference = "Stop"

$backendRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

$env:APPDATA = Join-Path $backendRoot ".appdata"
$env:DOTNET_CLI_HOME = Join-Path $backendRoot ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $backendRoot ".nuget-packages"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null
New-Item -ItemType Directory -Force -Path $env:NUGET_PACKAGES | Out-Null

Write-Host "Restoring packages..."
dotnet restore "$backendRoot\src\SIMS.API\SIMS.API.csproj" `
  --configfile "$backendRoot\NuGet.Config"

Write-Host "Building backend..."
dotnet build "$backendRoot\src\SIMS.API\SIMS.API.csproj" `
  --no-restore

Write-Host "Checking EF model drift..."
dotnet ef migrations has-pending-model-changes `
  --project "$backendRoot\src\SIMS.Infrastructure" `
  --startup-project "$backendRoot\src\SIMS.API" `
  --no-build

Write-Host "EF drift check completed."
