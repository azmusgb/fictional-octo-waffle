$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path (Join-Path $Root 'client\node_modules'))) {
    & (Join-Path $PSScriptRoot 'bootstrap.ps1')
}

$Api = Start-Process dotnet -ArgumentList @('run', '--project', (Join-Path $Root 'server\WorkbenchStudio.Api\WorkbenchStudio.Api.csproj')) -PassThru -NoNewWindow
try {
    Push-Location (Join-Path $Root 'client')
    try {
        npm run dev
    }
    finally {
        Pop-Location
    }
}
finally {
    if (-not $Api.HasExited) {
        Stop-Process -Id $Api.Id -Force
    }
}
