$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

Write-Host 'Checking prerequisites...'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 10 SDK is required. Install the current .NET 10 SDK and rerun this script.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js 22 or newer is required.'
}

Push-Location $Root
try {
    dotnet restore .\WorkbenchStudio.sln
    Push-Location .\client
    try {
        npm install
    }
    finally {
        Pop-Location
    }
    Write-Host 'Dependencies restored successfully.' -ForegroundColor Green
}
finally {
    Pop-Location
}
