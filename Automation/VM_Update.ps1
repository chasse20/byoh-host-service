Start-Transcript -Path "C:\VMUpdateLog.txt" -Force -Append

# Uninstall Host Service
Stop-Service -Name "BYOH Host Service" -ErrorAction SilentlyContinue
$service = Get-WmiObject -Class Win32_Service -Filter "Name='BYOH Host Service'"
if ( $service )
{
	$service.delete()
}

$Directory = Split-Path $MyInvocation.MyCommand.Path -Parent

Write-Host "Disabled service"

$dotnetInstall = Join-Path $Directory "dotnet-install.ps1"
if ( -not ( Test-Path $dotnetInstall ) )
{
	throw "dotnet-install.ps1 is intentionally not vendored in the public repository. Obtain the official script from Microsoft for deployment."
}
& $dotnetInstall -Channel 5.0

Write-Host "Installed NET5"

# Install Host Service
msiexec.exe /i "$Directory\Installer.msi" ALLUSERS=1 /qn

Write-Host "Installed new BYOH version 2.0.1"

Stop-Transcript