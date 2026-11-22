param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & "$PSScriptRoot\build.ps1" -Dotnet $Dotnet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $exe=Join-Path $PWD 'artifacts\Aster-Glass-win-arm64\Aster.App.exe'
    $folder=Join-Path $PWD 'artifacts\glass-visuals-final'
    New-Item -ItemType Directory -Force $folder | Out-Null
    $process=Start-Process -FilePath $exe -ArgumentList @('--visual-test',('"'+$folder+'"')) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { throw 'Visual tests are still running; inspect the process before retrying.' }
    if ($process.ExitCode -ne 0) { throw 'Visual harness failed; inspect the output folder.' }
    & python "$PSScriptRoot\visual-regression.py"
    if ($LASTEXITCODE -ne 0) { throw 'Visual regression failed; review the rendered differences.' }
} finally { Pop-Location }
