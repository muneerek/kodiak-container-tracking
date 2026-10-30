$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot) 'scripts\Setup-Core.ps1')
# All operating-system commands are mocked in this test process. Nothing is
# installed, enabled, repaired or restarted on the developer's machine.
$script:Changes=@();$script:Registered=$true;$script:Restart=$false
$script:State=[pscustomobject]@{WebServerPresent=$true;ManagementPresent=$true;HostingBinaryPresent=$true;Running=$true}
$script:Features=@()
function Get-IisPrerequisiteState {return $script:State}
function Import-Module {param($Name)}
function Get-WebGlobalModule {if($script:Registered){[pscustomobject]@{Name='AspNetCoreModuleV2'}}}
function Get-Service {param($Name) [pscustomobject]@{Status='Running'}}
function Start-Service {throw 'An existing IIS service must not be restarted.'}
function Get-WindowsFeature {param($Name) $script:Features | Where-Object Name -In $Name}
function Install-WindowsFeature {
    param($Name)
    $script:Changes+=@($Name);$script:State.ManagementPresent=$true
    [pscustomobject]@{Success=$true;RestartNeeded=if($script:Restart){'Yes'}else{'No'}}
}
function Enable-WindowsOptionalFeature {throw 'The ready/Windows Server scenarios must not enable Windows client features.'}
function Install-Prerequisite {param($PackageRoot,$Name,[switch]$Repair) $script:Changes+=('Prerequisite:'+ $Name+':Repair='+$Repair);$script:Registered=$true}
function Assert-Condition([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}

Enable-KodiakIis 'mock-package'
Assert-Condition ($script:Changes.Count -eq 0) 'An existing ready IIS installation was changed.'
$plan=Get-IisFeaturePlan @([pscustomobject]@{Name='A';State='Enabled'},[pscustomobject]@{Name='B';State='Installed'},[pscustomobject]@{Name='C';State='EnablePending'},[pscustomobject]@{Name='D';State='Disabled'})
Assert-Condition ($plan.Missing.Count -eq 1 -and $plan.Missing[0] -eq 'D') 'Enabled features were included in the enable plan.'
Assert-Condition ($plan.Pending.Count -eq 1 -and $plan.Pending[0] -eq 'C') 'A pending feature was treated as missing.'

$script:State.ManagementPresent=$false
$script:Features=@([pscustomobject]@{Name='Web-Scripting-Tools';InstallState='InstallPending'})
$message='';try{Enable-KodiakIis 'mock-package'}catch{$message=$_.Exception.Message}
Assert-Condition ($message -like '*earlier Windows change*') 'A pre-existing restart was not explained.'
Assert-Condition ($script:Changes.Count -eq 0) 'A pending feature was enabled again.'

$script:Features=@([pscustomobject]@{Name='Web-Scripting-Tools';InstallState='Available'})
Enable-KodiakIis 'mock-package'
Assert-Condition ($script:Changes.Count -eq 1 -and $script:Changes[0] -eq 'Web-Scripting-Tools') 'Setup changed features beyond the missing management component.'

$script:Changes=@();$script:State.ManagementPresent=$false;$script:Restart=$true
$message='';try{Enable-KodiakIis 'mock-package'}catch{$message=$_.Exception.Message}
Assert-Condition ($message -like '*missing prerequisites: Web-Scripting-Tools*') 'A required restart did not identify the feature that caused it.'

$script:Changes=@();$script:Restart=$false;$script:Registered=$false
Enable-KodiakIis 'mock-package'
Assert-Condition ($script:Changes.Count -eq 1 -and $script:Changes[0] -eq 'Prerequisite:hostingBundle:Repair=True') 'An unregistered hosting module was not repaired independently.'
$script:BindingChanges=@();$script:CertificateAdded=$false
$script:ExpectedBindingHost='localhost';$script:ExpectedBindingIP='*'
function Get-WebBinding {
    param($Name,$Protocol,$Port,$HostHeader,$IPAddress)
    if($Name -ne 'KodiakTest' -or $Protocol -ne 'https' -or $Port -ne '8443' -or $HostHeader -ne $script:ExpectedBindingHost -or $IPAddress -ne $script:ExpectedBindingIP){throw 'HTTPS selector was not restricted to the new site binding.'}
    $binding=[pscustomobject]@{bindingInformation=('{0}:8443:{1}' -f $script:ExpectedBindingIP,$script:ExpectedBindingHost)}
    $binding | Add-Member -MemberType ScriptMethod -Name AddSslCertificate -Value {param($thumbprint,$store) if($thumbprint -ne 'TEST-CERT' -or $store -ne 'My'){throw 'Unexpected certificate.'};$script:CertificateAdded=$true}
    return $binding
}
function Set-WebBinding {
    [CmdletBinding()]param($Name,$BindingInformation,$PropertyName,$Value)
    $script:BindingChanges+=@{Name=$Name;Binding=$BindingInformation;Property=$PropertyName;Value=$Value}
}
Set-KodiakHttpsBinding 'KodiakTest' 8443 'localhost' 'TEST-CERT'
Assert-Condition ($script:BindingChanges.Count -eq 1 -and $script:BindingChanges[0].Binding -eq '*:8443:localhost' -and $script:BindingChanges[0].Property -eq 'sslFlags' -and $script:BindingChanges[0].Value -eq 1 -and $script:CertificateAdded) 'HTTPS SNI/certificate configuration failed.'
$script:ExpectedBindingHost='';$script:ExpectedBindingIP='192.168.10.20';$script:BindingChanges=@();$script:CertificateAdded=$false
Set-KodiakHttpsBinding 'KodiakTest' 8443 '' 'TEST-CERT' '192.168.10.20'
Assert-Condition ($script:BindingChanges[0].Value -eq 0 -and $script:BindingChanges[0].Binding -eq '192.168.10.20:8443:' -and $script:CertificateAdded) 'IP-based HTTPS incorrectly required SNI or a hostname.'
Write-Host 'Passed: existing IIS reuse, missing/pending prerequisites, hosting repair, and HTTPS binding configuration. No actual Windows configuration was changed.'
