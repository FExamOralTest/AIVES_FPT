# Configuration is supplied by the caller; do not store credentials in this script.
$ErrorActionPreference = 'Stop'
$aivesRoot = Split-Path $PSScriptRoot -Parent
$aivesProject = Join-Path $aivesRoot 'backend/Aives.Api/Aives.Api.csproj'

if (-not $env:ConnectionStrings__DefaultConnection) {
    throw 'Set ConnectionStrings__DefaultConnection to the intended database before starting the backend.'
}
if (-not $env:ASPNETCORE_ENVIRONMENT) {
    throw 'Set ASPNETCORE_ENVIRONMENT explicitly before starting the backend.'
}

# dotnet run builds the current source, including newly added API controllers.
& dotnet run --project $aivesProject --no-launch-profile
exit $LASTEXITCODE
