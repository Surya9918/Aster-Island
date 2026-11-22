# Run in an administrator PowerShell only after reviewing the public certificate.
[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='High')]
param([Parameter(Mandatory=$true)][string]$CertificatePath,[Parameter(Mandatory=$true)][string]$ExpectedThumbprint)
$ErrorActionPreference='Stop'
$cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path -LiteralPath $CertificatePath).Path)
if($cert.Thumbprint -ne $ExpectedThumbprint){throw 'Certificate fingerprint mismatch'}
if($cert.Subject -ne 'CN=AsterLocal'){throw 'Unexpected publisher'}
if($PSCmdlet.ShouldProcess('LocalMachine Trusted People','Trust this specific Aster local code-signing certificate for all Windows accounts')){
 Import-Certificate -FilePath $CertificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}
