# Close privacy screen
$tempScreen = Get-Process -Name "WWAHost" -ErrorAction SilentlyContinue
if ( $tempScreen )
{
    $tempScreen | Stop-Process -Force;
}

if ( Test-Path Registry::HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\OOBE )
{
    Set-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\OOBE -Name DisablePrivacyExperience -Value 1
}

if ( Test-Path Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization )
{
    Set-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization -Name AllowInputPersonalization -Value 0
}

if ( Test-Path Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection )
{
    Set-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection -Name AllowTelemetry -Value 0 -Type DWord
}

if ( Test-Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location )
{
    Set-ItemProperty -Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location -Name Value -Value Deny
}

if ( Test-Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo )
{
    Set-ItemProperty -Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo -Name Enabled -Value 0
}

if ( Test-Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy )
{
    Set-ItemProperty -Path Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy -Name TailoredExperiencesWithDiagnosticDataEnabled -Value 0
}

# Setup Firewall
Set-NetConnectionProfile -InterfaceAlias Ethernet -NetworkCategory "Private"

# Install NuGet
Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force

# Install 7Zip
$tempInstaller = $Env:Temp + "\7z1900-x64.msi"
Invoke-WebRequest "https://www.7-zip.org/a/7z1900-x64.msi" -OutFile $tempInstaller
Start-Process msiexec.exe -ArgumentList "/i $tempInstaller INSTALLDIR=`"C:\Program Files\7-Zip`" /qn /quiet" -Wait

# Install NodeJS
$tempInstaller = $Env:Temp + "\node-installer.msi"
Invoke-WebRequest "https://nodejs.org/dist/v17.3.1/node-v17.3.1-x64.msi" -OutFile $tempInstaller
msiexec /i $tempInstaller /qn /quiet
Start-Process msiexec.exe -ArgumentList "/i $tempInstaller /qn /quiet" -Wait

# Set NodeJS Firewall
New-NetFirewallRule -DisplayName "NodeJS" -Program "C:\Program Files\nodejs\node.exe" -Action Allow

# Disable Disk Defrag
Get-ScheduledTask "ScheduledDefrag" | Disable-ScheduledTask

# Install Azure Module
Install-Module -Name Az -AllowClobber -Force
Set-ExecutionPolicy remoteSigned -Force
Import-Module Az -Force

# Enable NTLMv2
Set-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Lsa -Name LmCompatibilityLevel -Value 3

# Generate Reboot Script
# A single-quoted here-string keeps the second-stage environment variables literal until reboot.
$tempScript = @'
# Set NVIDIA GPUs to WDDM mode
$tempGPUs = & "${Env:Programfiles}\NVIDIA Corporation\NVSMI\nvidia-smi" --query-gpu=pci.bus_id --format=csv,noheader

foreach ( $tempGPU in $tempGPUs )
{
    & "${Env:Programfiles}\NVIDIA Corporation\NVSMI\nvidia-smi" -g $tempGPU -dm 0
}

# Disable Hyper-V GPU
Get-PnpDevice | Where-Object { $_.FriendlyName -like "Hyper-V" } | Disable-PnpDevice

# Configure Azure Files credential from deployment-provided environment variables.
# Required variables:
#   BYOH_FILE_SHARE_HOST
#   BYOH_FILE_SHARE_USER
#   BYOH_FILE_SHARE_PASSWORD
if ( [string]::IsNullOrWhiteSpace( $Env:BYOH_FILE_SHARE_HOST ) -or
     [string]::IsNullOrWhiteSpace( $Env:BYOH_FILE_SHARE_USER ) -or
     [string]::IsNullOrWhiteSpace( $Env:BYOH_FILE_SHARE_PASSWORD ) )
{
    throw "BYOH file-share environment variables are not configured."
}

cmd.exe /C "cmdkey /add:`"$Env:BYOH_FILE_SHARE_HOST`" /user:`"$Env:BYOH_FILE_SHARE_USER`" /pass:`"$Env:BYOH_FILE_SHARE_PASSWORD`""

# Install Host Service
$tempInstaller = Join-Path $Env:Temp "Host_Service.msi"
$tempInstallerSource = "\\$Env:BYOH_FILE_SHARE_HOST\files\Host_Service\Installer.msi"
Copy-Item -Path $tempInstallerSource -Destination $tempInstaller
Start-Process msiexec.exe -ArgumentList "/i `"$tempInstaller`" ALLUSERS=1 /qn /quiet" -Wait

# Delete task and reboot
Start-Process schtasks.exe -ArgumentList "/delete /f /tn VM_Initial_Setup_2" -Wait
Restart-Computer
'@

$tempScript | Out-File "${Env:Temp}\VM_Initial_Setup_2.ps1"
Start-Process schtasks.exe -ArgumentList "/create /f /tn VM_Initial_Setup_2 /ru SYSTEM /sc ONSTART /tr `"powershell.exe -file ${Env:Temp}\VM_Initial_Setup_2.ps1`""
