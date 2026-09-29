param(
    [string]$CertificateDirectory = "D:\docker-data\backend\nginx\certificates"
)

$ErrorActionPreference = "Stop"

# Nginx читает certificate и private key из внешнего Docker data directory.
# Эти файлы предназначены только для локальной разработки и никогда не должны попадать в Git.
$certificatePath = Join-Path $CertificateDirectory "localhost.pem"
$privateKeyPath = Join-Path $CertificateDirectory "localhost.key"

New-Item -ItemType Directory -Path $CertificateDirectory -Force | Out-Null

# Private key не зашифрован, потому что Nginx должен прочитать его без пароля при старте.
# Поэтому убираем унаследованный доступ и оставляем права только владельцу, SYSTEM и администраторам.
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

& icacls.exe $CertificateDirectory `
    /inheritance:r `
    /grant:r `
    "${currentUser}:(OI)(CI)F" `
    "*S-1-5-18:(OI)(CI)F" `
    "*S-1-5-32-544:(OI)(CI)F" | Out-Null

if ($LASTEXITCODE -ne 0)
{
    throw "Не удалось ограничить доступ к каталогу HTTPS certificate."
}

$certificateExists = Test-Path -LiteralPath $certificatePath
$privateKeyExists = Test-Path -LiteralPath $privateKeyPath

if ($certificateExists -ne $privateKeyExists)
{
    throw "В каталоге HTTPS certificate найден только один из двух ожидаемых файлов."
}

if (-not $certificateExists)
{
    # dotnet dev-certs создаёт certificate для localhost, добавляет его в trusted store
    # текущего пользователя и экспортирует пару localhost.pem + localhost.key для Nginx.
    dotnet dev-certs https `
        --trust `
        --export-path $certificatePath `
        --format PEM `
        --no-password

    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet dev-certs не смог экспортировать локальный HTTPS certificate."
    }
}

if (-not (Test-Path -LiteralPath $certificatePath) -or
    -not (Test-Path -LiteralPath $privateKeyPath))
{
    throw "Не удалось подготовить certificate и private key для локального Nginx."
}

# Явно назначаем ACL каждому файлу: уже существующие файлы не обязаны заново
# унаследовать ограничения каталога после изменения его ACL.
$certificateFiles = @(
    $certificatePath,
    $privateKeyPath
)

foreach ($certificateFile in $certificateFiles)
{
    & icacls.exe $certificateFile `
        /inheritance:r `
        /grant:r `
        "${currentUser}:F" `
        "*S-1-5-18:F" `
        "*S-1-5-32-544:F" | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        throw "Не удалось ограничить доступ к файлу: $certificateFile"
    }
}

Write-Host "HTTPS certificate подготовлен: $certificatePath"
Write-Host "Теперь можно запустить Nginx: docker compose -f docker-compose-dev.yml up -d nginx"
