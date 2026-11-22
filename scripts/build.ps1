param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & $Dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Install the ARM64 .NET SDK version specified in global.json.' }
    & $Dotnet restore Aster.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & $Dotnet build Aster.sln -c Release --no-restore -m:1 -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    New-Item -ItemType Directory -Force artifacts\reports | Out-Null
    & $Dotnet run --project tests\Aster.Tests -c Release --no-build -- --report artifacts\reports\tests.json
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }
    & $Dotnet publish src\Aster.App\Aster.App.csproj -c Release -r win-arm64 --self-contained true --no-restore -p:PublishTrimmed=false -p:PublishReadyToRun=false -o artifacts\Aster-Glass-win-arm64 -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'ARM64 publish failed.' }
    Write-Output 'Built artifacts\Aster-Glass-win-arm64\Aster.App.exe'
} finally { Pop-Location }
