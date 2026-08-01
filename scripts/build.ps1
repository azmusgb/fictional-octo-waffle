$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

Push-Location $Root
try {
    dotnet restore .\WorkbenchStudio.sln
    dotnet test .\WorkbenchStudio.sln --configuration Release --no-restore

    Push-Location .\client
    try {
        npm install
        npm test
        npm run build
    }
    finally {
        Pop-Location
    }

    dotnet publish .\server\WorkbenchStudio.Api\WorkbenchStudio.Api.csproj `
        --configuration Release `
        --output .\artifacts\publish `
        --no-restore

    Write-Host 'Published to artifacts\publish' -ForegroundColor Green
}
finally {
    Pop-Location
}
