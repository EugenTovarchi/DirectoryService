<#
.SYNOPSIS
Проверяет полный пользовательский OAuth 2.0 / OpenID Connect flow локального стенда.

.DESCRIPTION
Скрипт выполняет Authorization Code Flow с PKCE через AuthService, использует один
access token в AuthService, DirectoryService и FileService, затем проверяет ротацию
refresh token, стандартный `/connect/revoke` и запрет применения отозванного
или уже использованного refresh token.

BaseUri намеренно ограничен loopback HTTPS и использует обычную системную
проверку TLS certificate. Пароль принимается как SecureString, а полученные
tokens не выводятся и не сохраняются на диск.

.EXAMPLE
.\scripts\test-oauth-oidc-user-flow.ps1

Скрипт безопасно запросит пароль локального тестового пользователя.
#>
[CmdletBinding()]
param(
    [Uri]$BaseUri = "https://localhost:5001/",
    [string]$Email = "viewer-readonly@24eye.local",
    [SecureString]$Password,
    [string]$ClientId = "postman",
    [Uri]$RedirectUri = "https://oauth.pstmn.io/v1/callback"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-LoopbackUri {
    param([Uri]$Uri)

    $loopbackHosts = @(
        "localhost",
        "127.0.0.1",
        "::1"
    )

    if ($Uri.Scheme -ne "https" -or $Uri.Host -notin $loopbackHosts) {
        throw "BaseUri must be a local HTTPS address."
    }
}

# Кодирует случайные байты в формат Base64Url, используемый параметрами PKCE и state.
function ConvertTo-Base64Url {
    param([byte[]]$Bytes)

    $base64 = [Convert]::ToBase64String($Bytes)
    return $base64.TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

# Декодирует JWT payload только для проверки состава claims.
# Подпись, issuer, audience и lifetime проверяют API при реальных запросах ниже.
function Read-JwtPayload {
    param([string]$Token)

    $parts = $Token.Split(".")
    if ($parts.Count -ne 3) {
        throw "Expected a signed JWT with three parts."
    }

    $payload = $parts[1].Replace("-", "+").Replace("_", "/")
    $paddingLength = (4 - ($payload.Length % 4)) % 4
    $payload = $payload.PadRight($payload.Length + $paddingLength, "=")

    $payloadBytes = [Convert]::FromBase64String($payload)
    $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)

    return $payloadJson | ConvertFrom-Json
}

# Возвращает claim как массив независимо от того, записан он строкой или JSON array.
function Get-ClaimValues {
    param(
        [object]$Payload,
        [string]$Name
    )

    $property = $Payload.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return @()
    }

    return @($property.Value)
}

# Создаёт application/x-www-form-urlencoded body для OAuth и HTML form запросов.
function New-FormContent {
    param([hashtable]$Values)

    $pairs = [System.Collections.Generic.List[
        System.Collections.Generic.KeyValuePair[string, string]
    ]]::new()

    foreach ($key in $Values.Keys) {
        $pairs.Add(
            [System.Collections.Generic.KeyValuePair[string, string]]::new(
                [string]$key,
                [string]$Values[$key]))
    }

    return [System.Net.Http.FormUrlEncodedContent]::new($pairs)
}

# Отправляет form request и гарантированно освобождает его content.
function Send-Form {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Uri,
        [hashtable]$Values
    )

    $content = New-FormContent -Values $Values

    try {
        return $Client.PostAsync($Uri, $content).GetAwaiter().GetResult()
    }
    finally {
        $content.Dispose()
    }
}

# Завершает smoke-test понятной ошибкой, если шаг вернул неожиданный HTTP status.
function Assert-StatusCode {
    param(
        [System.Net.Http.HttpResponseMessage]$Response,
        [System.Net.HttpStatusCode]$Expected,
        [string]$Step
    )

    if ($Response.StatusCode -ne $Expected) {
        throw "$Step returned $([int]$Response.StatusCode), expected $([int]$Expected)."
    }
}

# Читает antiforgery token из HTML-формы login или consent.
function Read-AntiforgeryToken {
    param([System.Net.Http.HttpResponseMessage]$Response)

    $html = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $match = [regex]::Match(
        $html,
        'name="__RequestVerificationToken"[^>]*value="(?<token>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant,
        [TimeSpan]::FromSeconds(1))

    if (-not $match.Success) {
        throw "Antiforgery token was not rendered."
    }

    return $match.Groups["token"].Value
}

# Извлекает отдельное значение из query string OAuth callback URI.
function Get-QueryValue {
    param(
        [Uri]$Uri,
        [string]$Name
    )

    foreach ($pair in $Uri.Query.TrimStart("?").Split(
        "&",
        [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $parts = $pair.Split("=", 2)
        $key = [Uri]::UnescapeDataString($parts[0])

        if ($key -eq $Name -and $parts.Count -eq 2) {
            return [Uri]::UnescapeDataString($parts[1])
        }
    }

    return $null
}

# Разрешает server redirect только внутри того же локального HTTPS origin.
function Resolve-LocalRedirectUri {
    param(
        [Uri]$BaseUri,
        [Uri]$Location
    )

    $absoluteUri = [Uri]::new($BaseUri, $Location)

    if ($absoluteUri.Scheme -ne $BaseUri.Scheme -or
        $absoluteUri.Host -ne $BaseUri.Host -or
        $absoluteUri.Port -ne $BaseUri.Port) {
        throw "AuthService returned a redirect outside the local HTTPS origin."
    }

    return $absoluteUri
}

# Создаёт запрос к resource service со стандартным Authorization: Bearer header.
function New-AuthorizedRequest {
    param(
        [System.Net.Http.HttpMethod]$Method,
        [string]$Uri,
        [string]$AccessToken
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, $Uri)
    $request.Headers.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
            "Bearer",
            $AccessToken)

    return $request
}

# Проверяет ожидаемый результат вызова защищённого API и освобождает HTTP-объекты.
function Assert-ProtectedEndpoint {
    param(
        [System.Net.Http.HttpClient]$Client,
        [System.Net.Http.HttpMethod]$Method,
        [string]$Uri,
        [string]$AccessToken,
        [System.Net.HttpStatusCode]$Expected,
        [string]$Step
    )

    $request = New-AuthorizedRequest `
        -Method $Method `
        -Uri $Uri `
        -AccessToken $AccessToken

    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()

        try {
            Assert-StatusCode `
                -Response $response `
                -Expected $Expected `
                -Step $Step
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

Assert-LoopbackUri -Uri $BaseUri

if ($null -eq $Password) {
    $Password = Read-Host `
        -Prompt "Password for local OAuth test user $Email" `
        -AsSecureString
}

$passwordCredential = [PSCredential]::new("oauth-smoke-user", $Password)
$plainPassword = $passwordCredential.GetNetworkCredential().Password

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $true
$handler.CookieContainer = [System.Net.CookieContainer]::new()

# Локальный flow не должен уходить в системный HTTP proxy.
$handler.UseProxy = $false

$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = $BaseUri
$client.Timeout = [TimeSpan]::FromSeconds(30)

try {
    $codeVerifierBytes = [byte[]]::new(64)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill(
        $codeVerifierBytes)
    $codeVerifier = ConvertTo-Base64Url -Bytes $codeVerifierBytes
    $codeChallengeBytes = [System.Security.Cryptography.SHA256]::HashData(
        [System.Text.Encoding]::ASCII.GetBytes($codeVerifier))
    $codeChallenge = ConvertTo-Base64Url -Bytes $codeChallengeBytes
    $stateBytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($stateBytes)
    $state = ConvertTo-Base64Url -Bytes $stateBytes

    $scope = "openid profile email offline_access directory files auth"
    $authorizationQuery = @(
        "client_id=$([Uri]::EscapeDataString($ClientId))",
        "redirect_uri=$([Uri]::EscapeDataString($RedirectUri.AbsoluteUri))",
        "response_type=code",
        "scope=$([Uri]::EscapeDataString($scope))",
        "code_challenge=$([Uri]::EscapeDataString($codeChallenge))",
        "code_challenge_method=S256",
        "state=$([Uri]::EscapeDataString($state))"
    ) -join "&"
    $authorizationUri = "/connect/authorize?$authorizationQuery"

    $anonymousAuthorizationResponse = $client.GetAsync(
        $authorizationUri).GetAwaiter().GetResult()

    try {
        Assert-StatusCode `
            -Response $anonymousAuthorizationResponse `
            -Expected ([System.Net.HttpStatusCode]::Found) `
            -Step "Anonymous authorization"

        $loginUri = $anonymousAuthorizationResponse.Headers.Location
        if ($null -eq $loginUri) {
            throw "Login redirect is missing."
        }

        $loginUri = Resolve-LocalRedirectUri `
            -BaseUri $BaseUri `
            -Location $loginUri
    }
    finally {
        $anonymousAuthorizationResponse.Dispose()
    }

    $loginPageResponse = $client.GetAsync($loginUri).GetAwaiter().GetResult()

    try {
        Assert-StatusCode `
            -Response $loginPageResponse `
            -Expected ([System.Net.HttpStatusCode]::OK) `
            -Step "Login page"
        $loginAntiforgeryToken = Read-AntiforgeryToken `
            -Response $loginPageResponse
    }
    finally {
        $loginPageResponse.Dispose()
    }

    $loginResponse = Send-Form `
        -Client $client `
        -Uri "/connect/login" `
        -Values @{
            Email = $Email
            Password = $plainPassword
            ReturnUrl = $authorizationUri
            __RequestVerificationToken = $loginAntiforgeryToken
        }

    try {
        Assert-StatusCode `
            -Response $loginResponse `
            -Expected ([System.Net.HttpStatusCode]::Found) `
            -Step "Browser login"
    }
    finally {
        $loginResponse.Dispose()
    }

    $consentPageResponse = $client.GetAsync(
        $authorizationUri).GetAwaiter().GetResult()

    try {
        Assert-StatusCode `
            -Response $consentPageResponse `
            -Expected ([System.Net.HttpStatusCode]::OK) `
            -Step "Consent page"
        $consentAntiforgeryToken = Read-AntiforgeryToken `
            -Response $consentPageResponse
    }
    finally {
        $consentPageResponse.Dispose()
    }

    $authorizationResponse = Send-Form `
        -Client $client `
        -Uri "/connect/authorize" `
        -Values @{
            decision = "accept"
            client_id = $ClientId
            redirect_uri = $RedirectUri.AbsoluteUri
            response_type = "code"
            scope = $scope
            code_challenge = $codeChallenge
            code_challenge_method = "S256"
            state = $state
            __RequestVerificationToken = $consentAntiforgeryToken
        }

    try {
        Assert-StatusCode `
            -Response $authorizationResponse `
            -Expected ([System.Net.HttpStatusCode]::Found) `
            -Step "Authorization consent"

        $callbackUri = $authorizationResponse.Headers.Location
        if ($null -eq $callbackUri) {
            throw "Authorization callback is missing."
        }

        if ($callbackUri.GetLeftPart([UriPartial]::Path) -ne
            $RedirectUri.GetLeftPart([UriPartial]::Path)) {
            throw "Authorization callback URI does not match registered redirect URI."
        }

        $returnedState = Get-QueryValue -Uri $callbackUri -Name "state"
        if ($returnedState -ne $state) {
            throw "OAuth state validation failed."
        }

        $authorizationCode = Get-QueryValue -Uri $callbackUri -Name "code"
        if ([string]::IsNullOrWhiteSpace($authorizationCode)) {
            throw "Authorization code is missing."
        }
    }
    finally {
        $authorizationResponse.Dispose()
    }

    $tokenResponse = Send-Form `
        -Client $client `
        -Uri "/connect/token" `
        -Values @{
            grant_type = "authorization_code"
            client_id = $ClientId
            redirect_uri = $RedirectUri.AbsoluteUri
            code = $authorizationCode
            code_verifier = $codeVerifier
        }

    try {
        Assert-StatusCode `
            -Response $tokenResponse `
            -Expected ([System.Net.HttpStatusCode]::OK) `
            -Step "Authorization code exchange"

        $tokenJson = $tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $tokens = $tokenJson | ConvertFrom-Json

        $accessToken = [string]$tokens.access_token
        $idToken = [string]$tokens.id_token
        $refreshToken = [string]$tokens.refresh_token

        if ([string]::IsNullOrWhiteSpace($accessToken) -or
            [string]::IsNullOrWhiteSpace($idToken) -or
            [string]::IsNullOrWhiteSpace($refreshToken)) {
            throw "Token response does not contain required tokens."
        }
    }
    finally {
        $tokenResponse.Dispose()
    }

    $idTokenPayload = Read-JwtPayload -Token $idToken
    $idTokenPermissions = @(Get-ClaimValues `
        -Payload $idTokenPayload `
        -Name "permission")
    $idTokenServicePermissions = @(Get-ClaimValues `
        -Payload $idTokenPayload `
        -Name "service_permission")

    if ($idTokenPermissions.Count -ne 0 -or
        $idTokenServicePermissions.Count -ne 0) {
        throw "ID token contains business permissions."
    }

    $accessTokenPayload = Read-JwtPayload -Token $accessToken
    $accessTokenAudiences = @(Get-ClaimValues `
        -Payload $accessTokenPayload `
        -Name "aud")
    $accessTokenPermissions = @(Get-ClaimValues `
        -Payload $accessTokenPayload `
        -Name "permission")
    $accessTokenServicePermissions = @(Get-ClaimValues `
        -Payload $accessTokenPayload `
        -Name "service_permission")

    $expectedAudiences = @(
        "auth-service",
        "directory-service",
        "file-service"
    )

    foreach ($expectedAudience in $expectedAudiences) {
        if ($expectedAudience -notin $accessTokenAudiences) {
            throw "Access token does not contain expected audience $expectedAudience."
        }
    }

    if ($accessTokenPermissions.Count -eq 0) {
        throw "User access token does not contain business permissions."
    }

    if ($accessTokenServicePermissions.Count -ne 0) {
        throw "User access token contains service permissions."
    }

    Assert-ProtectedEndpoint `
        -Client $client `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Uri "/auth-service/api/auth/me" `
        -AccessToken $accessToken `
        -Expected ([System.Net.HttpStatusCode]::OK) `
        -Step "AuthService protected API"

    Assert-ProtectedEndpoint `
        -Client $client `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Uri "/directory-service/api/departments/roots" `
        -AccessToken $accessToken `
        -Expected ([System.Net.HttpStatusCode]::OK) `
        -Step "DirectoryService protected API"

    Assert-ProtectedEndpoint `
        -Client $client `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -Uri "/file-service/files/$([Guid]::NewGuid())" `
        -AccessToken $accessToken `
        -Expected ([System.Net.HttpStatusCode]::NotFound) `
        -Step "FileService protected API"

    $refreshResponse = Send-Form `
        -Client $client `
        -Uri "/connect/token" `
        -Values @{
            grant_type = "refresh_token"
            client_id = $ClientId
            refresh_token = $refreshToken
        }

    try {
        Assert-StatusCode `
            -Response $refreshResponse `
            -Expected ([System.Net.HttpStatusCode]::OK) `
            -Step "Refresh token rotation"

        $refreshedTokenJson = $refreshResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $refreshedTokens = $refreshedTokenJson | ConvertFrom-Json

        $refreshedAccessToken = [string]$refreshedTokens.access_token
        $rotatedRefreshToken = [string]$refreshedTokens.refresh_token

        if ([string]::IsNullOrWhiteSpace($refreshedAccessToken) -or
            [string]::IsNullOrWhiteSpace($rotatedRefreshToken) -or
            $rotatedRefreshToken -eq $refreshToken) {
            throw "Refresh token was not rotated."
        }
    }
    finally {
        $refreshResponse.Dispose()
    }

    $revocationResponse = Send-Form `
        -Client $client `
        -Uri "/connect/revoke" `
        -Values @{
            client_id = $ClientId
            token = $rotatedRefreshToken
            token_type_hint = "refresh_token"
        }

    try {
        Assert-StatusCode `
            -Response $revocationResponse `
            -Expected ([System.Net.HttpStatusCode]::OK) `
            -Step "Refresh token revocation"
    }
    finally {
        $revocationResponse.Dispose()
    }

    $revokedRefreshResponse = Send-Form `
        -Client $client `
        -Uri "/connect/token" `
        -Values @{
            grant_type = "refresh_token"
            client_id = $ClientId
            refresh_token = $rotatedRefreshToken
        }

    try {
        Assert-StatusCode `
            -Response $revokedRefreshResponse `
            -Expected ([System.Net.HttpStatusCode]::BadRequest) `
            -Step "Revoked refresh token"

        $revokedRefreshErrorJson = $revokedRefreshResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $revokedRefreshError = $revokedRefreshErrorJson | ConvertFrom-Json

        if ([string]$revokedRefreshError.error -ne "invalid_grant") {
            throw "Revoked refresh token did not return invalid_grant."
        }
    }
    finally {
        $revokedRefreshResponse.Dispose()
    }

    $replayResponse = Send-Form `
        -Client $client `
        -Uri "/connect/token" `
        -Values @{
            grant_type = "refresh_token"
            client_id = $ClientId
            refresh_token = $refreshToken
        }

    try {
        Assert-StatusCode `
            -Response $replayResponse `
            -Expected ([System.Net.HttpStatusCode]::BadRequest) `
            -Step "Old refresh token replay"

        $replayErrorJson = $replayResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $replayError = $replayErrorJson | ConvertFrom-Json

        if ([string]$replayError.error -ne "invalid_grant") {
            throw "Old refresh token replay did not return invalid_grant."
        }
    }
    finally {
        $replayResponse.Dispose()
    }

    Assert-ProtectedEndpoint `
        -Client $client `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Uri "/auth-service/api/auth/me" `
        -AccessToken $refreshedAccessToken `
        -Expected ([System.Net.HttpStatusCode]::OK) `
        -Step "Refreshed access token"

    Write-Host "OAuth/OIDC user flow completed successfully."
}
finally {
    $plainPassword = $null
    $passwordCredential = $null
    $Password = $null
    $client.Dispose()
    $handler.Dispose()
}
