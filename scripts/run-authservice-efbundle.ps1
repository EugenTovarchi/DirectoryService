param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$bundlePath = Join-Path $repoRoot "artifacts/migrations/AuthService/efbundle.exe"

if (-not (Test-Path $bundlePath)) {
    throw "AuthService EF bundle not found. Run scripts/build-authservice-efbundle.ps1 first."
}

& $bundlePath --connection $ConnectionString

if ($LASTEXITCODE -ne 0) {
    throw "AuthService EF bundle failed with exit code $LASTEXITCODE."
}
