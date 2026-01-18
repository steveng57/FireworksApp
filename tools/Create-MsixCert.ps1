<#
.SYNOPSIS
Creates or reuses a persistent self-signed code-signing certificate for MSIX signing and prints values for GitHub Actions secrets.

.DESCRIPTION
- Looks for an existing cert by subject in CurrentUser\My.
- If none is found, creates a new self-signed code-signing cert.
- Exports a PFX with a freshly generated random password to the temp folder.
- Prints subject, PFX path, and base64 to set secrets:
  * MSIX_PUBLISHER_SUBJECT
  * MSIX_CERT_PASSWORD
  * MSIX_CERT_PFX_BASE64

.PARAMETER Subject
Certificate subject (CN=...). Defaults to 'CN=FireworksApp Dev'. Use the exact string for the Publisher in Appx manifest and MSIX signing.

.PARAMETER FriendlyName
Friendly name for the certificate store entry.

.EXAMPLE
./Create-MsixCert.ps1 -Subject 'CN=FireworksApp Dev' -FriendlyName 'FireworksApp Dev MSIX'

.NOTES
Run in a user session (no admin required). Do not commit the generated PFX or password; set them as GitHub secrets.
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=Fireworks Simulator',
    [string]$FriendlyName = 'Fireworks Simulator MSIX'
)

$ErrorActionPreference = 'Stop'

function New-RandomPassword {
    param([int]$Length = 32)
    $bytes = New-Object byte[] $Length
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes)
}

Write-Host "Subject: $Subject" -ForegroundColor Cyan

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store 'My','CurrentUser'
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
try {
    $existing = $store.Certificates | Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } | Select-Object -First 1
    if ($existing) {
        Write-Host "Found existing certificate, reusing." -ForegroundColor Yellow
        $cert = $existing
    }
    else {
        Write-Host "No existing certificate found; creating new self-signed code-signing cert." -ForegroundColor Green
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -FriendlyName $FriendlyName -CertStoreLocation Cert:\CurrentUser\My
    }
}
finally {
    $store.Close()
}

if (-not $cert) {
    throw "Failed to acquire or create certificate."
}

$passwordPlain = New-RandomPassword
$password = ConvertTo-SecureString $passwordPlain -AsPlainText -Force
$pfxPath = Join-Path $env:TEMP "msix-dev-cert.pfx"

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null

$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))

Write-Host "`n--- GitHub Secrets (set in repo -> Settings -> Secrets and variables -> Actions) ---" -ForegroundColor Cyan
Write-Host "MSIX_PUBLISHER_SUBJECT = $($cert.Subject)" -ForegroundColor White
Write-Host "MSIX_CERT_PASSWORD    = $passwordPlain" -ForegroundColor White
Write-Host "MSIX_CERT_PFX_BASE64  = $base64" -ForegroundColor White

Write-Host "`nPFX saved to: $pfxPath" -ForegroundColor DarkGray
Write-Host "Keep the PFX and password secure. Do NOT commit them." -ForegroundColor Yellow
