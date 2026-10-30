$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot) 'scripts\Setup-Core.ps1')
function Assert-Access([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Reject-Access([hashtable]$Options){$rejected=$false;try{Get-KodiakAccessPlan $Options @('192.168.10.20')|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Invalid access configuration was accepted.'}}
$local=@{AccessMode='Local';HostName='localhost';WebPort=8443;Https=$true;BindingIP='*';CertificateThumbprint='';OpenFirewall=$true}
$plan=Get-KodiakAccessPlan $local
Assert-Access ($plan.BindingIP -eq '127.0.0.1' -and -not $plan.OpenFirewall -and $plan.Url -eq 'https://localhost:8443/') 'Local mode was exposed to the network.'
$office=@{AccessMode='Network';HostName='192.168.10.20';WebPort=8080;Https=$false;BindingIP='192.168.10.20';CertificateThumbprint='';OpenFirewall=$true;SiteName='KodiakTest'}
$plan=Get-KodiakAccessPlan $office @('192.168.10.20')
Assert-Access ($plan.HostHeader -eq '' -and $plan.ConnectAddress -eq '192.168.10.20' -and $plan.Url -eq 'http://192.168.10.20:8080/') 'Office IP binding/health check was not configured correctly.'
$public=@{AccessMode='Public';HostName='tracking.example.com';WebPort=443;Https=$true;BindingIP='*';CertificateThumbprint='TEST';OpenFirewall=$true;PublicIP='203.0.113.25';SiteName='KodiakTest'}
$plan=Get-KodiakAccessPlan $public @('192.168.10.20')
Assert-Access ($plan.BindingIP -eq '*' -and $plan.HostHeader -eq 'tracking.example.com' -and $plan.Url -eq 'https://tracking.example.com/' -and $plan.PublicIP -eq '203.0.113.25') 'The public router IP was confused with a local binding IP.'
$bad=@{}+$public;$bad.Https=$false;Reject-Access $bad
$bad=@{}+$public;$bad.CertificateThumbprint='';Reject-Access $bad
$bad=@{}+$public;$bad.HostName='https://tracking.example.com/path';Reject-Access $bad
$bad=@{}+$public;$bad.HostName='203.0.113.25';Reject-Access $bad
$bad=@{}+$public;$bad.BindingIP='203.0.113.25';Reject-Access $bad
$bad=@{}+$office;$bad.HostName='192.168.10.99';Reject-Access $bad
$bad=@{}+$local;$bad.HostName='0.0.0.0';Reject-Access $bad
$script:Firewall=@{}
function New-NetFirewallRule {
    param($Name,$DisplayName,$Description,$Direction,$Action,$Protocol,$LocalPort,$Profile,$RemoteAddress,$LocalAddress)
    $script:Firewall=@{Port=$LocalPort;Profiles=$Profile;Remote=$RemoteAddress;Address=$LocalAddress}
}
[void](Add-KodiakFirewall $office (Get-KodiakAccessPlan $office @('192.168.10.20')) 'C:\KodiakTest')
Assert-Access ($script:Firewall.Port -eq 8080 -and $script:Firewall.Remote -eq 'LocalSubnet' -and 'Public' -notin $script:Firewall.Profiles) 'The office firewall rule exposed the app beyond its intended scope.'
[void](Add-KodiakFirewall $public (Get-KodiakAccessPlan $public @('192.168.10.20')) 'C:\KodiakTest')
Assert-Access ($script:Firewall.Port -eq 443 -and $script:Firewall.Remote -eq 'Any') 'The public firewall rule did not select the HTTPS website port.'
function Get-Website { [pscustomobject]@{Name='OtherBusinessSite'} }
function Get-WebBinding {param($Name,$Protocol,$Port) [pscustomobject]@{bindingInformation='*:443:tracking.example.com';sslFlags=1}}
$rejected=$false;try{Assert-KodiakBindingAvailable $public (Get-KodiakAccessPlan $public)}catch{$rejected=$true}
Assert-Access $rejected 'An existing other-site binding conflict was ignored.'
& {
    function Assert-Administrator {}
    function Import-Module {param($Name)}
    function Get-LocalWebAddresses {'192.168.10.20'}
    $script:TestAccessRoot=Join-Path $env:TEMP ('kodiak-access-test-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($script:TestAccessRoot)|Out-Null
    $script:AddedAccessBindings=0;$script:RemovedAccessBindings=0
    function Get-Website {param($Name) [pscustomobject]@{Name='KodiakTest';physicalPath=(Join-Path $script:TestAccessRoot 'Api')}}
    function Get-WebBinding {param($Name,$Protocol,$Port,$IPAddress,$HostHeader)}
    function New-WebBinding {param($Name,$Protocol,$Port,$IPAddress,$HostHeader) $script:AddedAccessBindings++}
    function Remove-WebBinding {param($Name,$Protocol,$Port,$IPAddress,$HostHeader) $script:RemovedAccessBindings++}
    $script:HealthShouldFail=$true
    function Test-KodiakSite {param($Options,$Scheme) if($script:HealthShouldFail){throw 'Expected test health-check failure.'}}
    function Invoke-SetupTask {throw 'Access updates must not call database setup.'}
    function Set-KodiakDesktopShortcut {param($SiteName,$Url) $script:UpdatedShortcut=$Url}
    try{
        @{installPath=$script:TestAccessRoot;siteName='KodiakTest';url='https://localhost:8443/'}|ConvertTo-Json|Set-Content (Join-Path $script:TestAccessRoot 'installed.json')
        $message='';try{Update-KodiakAccess $script:TestAccessRoot $office}catch{$message=$_.Exception.Message}
        Assert-Access ($message -eq 'Expected test health-check failure.' -and $script:AddedAccessBindings -eq 1 -and $script:RemovedAccessBindings -eq 1) 'A failed access update did not roll back only its new binding.'
        $record=Get-Content (Join-Path $script:TestAccessRoot 'installed.json') -Raw|ConvertFrom-Json
        Assert-Access ($record.url -eq 'https://localhost:8443/') 'A failed update changed the installed URL.'
        $script:HealthShouldFail=$false;$script:AddedAccessBindings=0;$script:RemovedAccessBindings=0
        $url=Update-KodiakAccess $script:TestAccessRoot $office
        $record=Get-Content (Join-Path $script:TestAccessRoot 'installed.json') -Raw|ConvertFrom-Json
        Assert-Access ($url -is [string] -and $url -eq 'http://192.168.10.20:8080/' -and $record.url -eq $url -and $script:UpdatedShortcut -eq $url) 'A successful access update did not persist and return the correct URL.'
        Assert-Access ($script:AddedAccessBindings -eq 1 -and $script:RemovedAccessBindings -eq 0 -and $record.firewallRules.Count -eq 1) 'A successful update removed existing access or failed to track its firewall rule.'
    }finally{
        $path=[IO.Path]::GetFullPath($script:TestAccessRoot)
        if(-not $path.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Test cleanup escaped its temporary directory.'}
        Remove-Item -LiteralPath $path -Recurse -Force
        $script:LogFile=$null
    }
}
Write-Host 'Passed: local, office-IP and public-domain plans; validation; firewall scope; binding conflict protection. No IIS/firewall settings were changed.'
