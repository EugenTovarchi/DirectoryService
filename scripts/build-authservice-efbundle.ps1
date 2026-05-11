$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputDir = Join-Path $repoRoot "artifacts/migrations/AuthService"
$outputPath = Join-Path $outputDir "efbundle.exe"
$authServiceEnvPath = Join-Path $repoRoot "AuthService.Development.env"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if (-not $env:ConnectionStrings__DefaultConnection -and (Test-Path $authServiceEnvPath)) {
    Get-Content -LiteralPath $authServiceEnvPath | ForEach-Object {
        if ($_ -match '^ConnectionStrings__DefaultConnection=(.*)$') {
            $env:ConnectionStrings__DefaultConnection = $matches[1]
        }
    }
}

Push-Location $repoRoot
try {
    dotnet ef migrations bundle `
        --project "backend/AuthService/AuthService.Infrastructure.Postgres/AuthService.Infrastructure.Postgres.csproj" `
        --startup-project "backend/AuthService/AuthService.Web/AuthService.Web.csproj" `
        --context "AuthServiceDbContext" `
        --configuration Release `
        --output $outputPath `
        --force

    if ($LASTEXITCODE -ne 0) {
        throw "AuthService EF bundle build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
