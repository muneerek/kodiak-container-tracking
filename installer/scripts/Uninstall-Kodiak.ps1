param([string]$InstallPath=$PSScriptRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run the uninstall utility as administrator.' }
$root=[IO.Path]::GetFullPath($InstallPath)
$manifest=Join-Path $root 'installed.json'
if (-not (Test-Path $manifest)) { throw 'No completed Kodiak installation was found in this folder.' }
$state=Get-Content $manifest -Raw | ConvertFrom-Json
if (-not $root.Equals([IO.Path]::GetFullPath($state.installPath),[StringComparison]::OrdinalIgnoreCase)) { throw 'The installation path does not match its manifest.' }
$answer=[Windows.Forms.MessageBox]::Show('Remove the Kodiak IIS website and application pool? The database, settings, application files and uploaded documents will be preserved.','Uninstall Kodiak','YesNo','Question')
if ($answer -ne 'Yes') { return }
Import-Module WebAdministration
$site=Get-Website -Name $state.siteName -ErrorAction SilentlyContinue
if ($site -and -not ([IO.Path]::GetFullPath($site.physicalPath)).Equals((Join-Path $root 'Api'),[StringComparison]::OrdinalIgnoreCase)) { throw 'The IIS website now belongs to another folder. Nothing was removed.' }
if ($site) { Remove-Website -Name $state.siteName }
if($state.PSObject.Properties.Name -contains 'firewallRules'){
    foreach($name in $state.firewallRules){
        $rule=Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue
        if($rule -and $rule.Description -eq ('Kodiak installation: '+$root)){Remove-NetFirewallRule -Name $name}
    }
}
if (Test-Path "IIS:\AppPools\$($state.siteName)") {
    $used=@(Get-Website | Where-Object applicationPool -eq $state.siteName)+@(Get-WebApplication | Where-Object applicationPool -eq $state.siteName)
    if ($used.Count -eq 0) { Remove-WebAppPool $state.siteName }
}
$shortcut=Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) ($state.siteName+'.url')
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
[Windows.Forms.MessageBox]::Show('The IIS website was removed. Your database, configuration, application files and uploads remain in place.','Kodiak') | Out-Null
