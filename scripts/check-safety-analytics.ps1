param(
    [string]$ConnectionString = $env:SAFETY_ANALYTICS_CONNECTION
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Host "SAFETY_ANALYTICS_CONNECTION is not set." -ForegroundColor Yellow
    Write-Host "Set it first, then rerun this script:"
    Write-Host '$env:SAFETY_ANALYTICS_CONNECTION="Host=...;Database=sims_safety_analytics;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"'
    exit 1
}

$workspace = Split-Path -Parent $PSScriptRoot

Write-Host "Checking safety analytics database..." -ForegroundColor Cyan

$tempDir = Join-Path $workspace "temp"
if (-not (Test-Path -LiteralPath $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

$sourcePath = Join-Path $tempDir "CheckSafetyAnalytics.cs"
$projectPath = Join-Path $tempDir "CheckSafetyAnalytics.csproj"

@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="8.0.6" />
  </ItemGroup>
</Project>
'@ | Set-Content -Path $projectPath -Encoding UTF8

@'
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("SAFETY_ANALYTICS_CONNECTION is not set.");
    return 1;
}

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

var tables = new[]
{
    "fmcsa_analytics_import_batches",
    "fmcsa_carrier_peer_snapshots",
    "fmcsa_basic_peer_measures"
};

foreach (var table in tables)
{
    await using var cmd = new NpgsqlCommand($"select count(*) from {table}", conn);
    var count = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"{table}: {count}");
}

Console.WriteLine();
Console.WriteLine("Latest batches:");

await using var latestCmd = new NpgsqlCommand("""
select snapshot_month, source_name, status, started_at, completed_at, rows_imported, coalesce(error_message, '')
from fmcsa_analytics_import_batches
order by started_at desc
limit 5
""", conn);

await using var reader = await latestCmd.ExecuteReaderAsync();
var found = false;
while (await reader.ReadAsync())
{
    found = true;
    Console.WriteLine($"{reader.GetString(0)} | {reader.GetString(1)} | {reader.GetString(2)} | rows {reader.GetInt32(5)} | started {reader.GetDateTime(3):u}");
    if (!reader.IsDBNull(6) && !string.IsNullOrWhiteSpace(reader.GetString(6)))
        Console.WriteLine($"  error: {reader.GetString(6)}");
}

if (!found)
    Console.WriteLine("No import batches found.");

return 0;
'@ | Set-Content -Path $sourcePath -Encoding UTF8

dotnet run --project $projectPath
exit $LASTEXITCODE
