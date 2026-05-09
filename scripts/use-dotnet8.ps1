$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = $env:USERPROFILE
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Write-Host "Repo: $repoRoot"
Write-Host "DOTNET_CLI_HOME: $env:DOTNET_CLI_HOME"
dotnet --version
