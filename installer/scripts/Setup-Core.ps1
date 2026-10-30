Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Assert-Administrator {
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run Kodiak Setup as administrator.' }
}
function Set-PrivateDirectory([string]$Path) {
    [IO.Directory]::CreateDirectory($Path) | Out-Null
    $acl=New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @('S-1-5-18','S-1-5-32-544')) {
        $rule=New-Object Security.AccessControl.FileSystemAccessRule ([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Write-SetupLog([string]$Message) {
    if ($script:LogFile) { Add-Content -LiteralPath $script:LogFile -Value ('{0:s} {1}' -f (Get-Date),$Message) }
    if ($script:OnProgress) { & $script:OnProgress $Message }
}
function Invoke-SetupTask([string]$Executable,[hashtable]$Request) {
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$Executable; $info.Arguments='--setup-task'; $info.WorkingDirectory=Split-Path $Executable
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardInput=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $info.EnvironmentVariables['ASPNETCORE_ENVIRONMENT']='Production'
    $process=New-Object Diagnostics.Process; $process.StartInfo=$info
    try {
        [void]$process.Start()
        $outTask=$process.StandardOutput.ReadToEndAsync(); $errTask=$process.StandardError.ReadToEndAsync()
        $bytes=[Text.Encoding]::UTF8.GetBytes(($Request | ConvertTo-Json -Depth 8 -Compress))
        $process.StandardInput.BaseStream.Write($bytes,0,$bytes.Length)
        $process.StandardInput.BaseStream.Flush(); $process.StandardInput.Close()
        $deadline=(Get-Date).AddMinutes(15)
        while(-not $process.WaitForExit(150)) {
            if ([type]::GetType('System.Windows.Forms.Application, System.Windows.Forms')) { [Windows.Forms.Application]::DoEvents() }
            if ((Get-Date) -gt $deadline) { $process.Kill(); throw 'The database setup operation timed out. Check the server and retry.' }
        }
        $output=$outTask.GetAwaiter().GetResult(); [void]$errTask.GetAwaiter().GetResult()
        try { $result=$output.Trim() | ConvertFrom-Json } catch { throw 'The database helper did not return a valid result. Check that the package is complete.' }
        if ($process.ExitCode -ne 0 -or -not $result.ok) { throw $result.error }
        return $result
    } finally { $process.Dispose() }
}
function Get-LocalPostgresServers {
    $results=@()
    foreach($key in @('HKLM:\SOFTWARE\PostgreSQL\Installations','HKLM:\SOFTWARE\WOW6432Node\PostgreSQL\Installations')) {
        if (Test-Path $key) { foreach($entry in Get-ChildItem $key) {
            $values=Get-ItemProperty $entry.PSPath
            $port=5432
            if ($values.PSObject.Properties.Name -contains 'Port') { $port=[int]$values.Port }
            elseif ($values.PSObject.Properties.Name -contains 'Data Directory') { $port=Get-PostgresDataPort $values.'Data Directory' }
            $results+= [pscustomobject]@{Host='localhost';Port=$port;Detected=$true;Label=('Local PostgreSQL ({0}, port {1})' -f $entry.PSChildName,$port)}
        } }
    }
    # Some PostgreSQL distributions and service-only installations have no EDB registry keys.
    $services=@(Get-CimInstance Win32_Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'postgres' -or $_.PathName -match '(pg_ctl|postgres)\.exe' })
    foreach($service in $services) {
        $port=5432
        if($service.PathName -match '(?:^|\s)-D\s*(?:"([^"]+)"|(\S+))') {
            $directory=$Matches[1];if(-not $directory){$directory=$Matches[2]}
            $port=Get-PostgresDataPort $directory
        }
        $results+=[pscustomobject]@{Host='localhost';Port=$port;Detected=$true;Label=('Local PostgreSQL service ({0}, port {1})' -f $service.Name,$port)}
    }
    # Detect a running server without a registry entry/service (for example Docker).
    if($results.Count -eq 0 -and (Test-LocalPostgresPort 5432)) {
        $results+=[pscustomobject]@{Host='localhost';Port=5432;Detected=$true;Label='Running local PostgreSQL (port 5432)'}
    }
    $results=@($results | Sort-Object Port -Unique)
    if ($results.Count -eq 0) { $results+=[pscustomobject]@{Host='localhost';Port=5432;Detected=$false;Label='localhost:5432 (enter credentials to connect)'} }
    return $results
}
function Get-PostgresDataPort([string]$Directory) {
    if([string]::IsNullOrWhiteSpace($Directory)){return 5432}
    $file=Join-Path $Directory 'postgresql.conf'
    if(Test-Path -LiteralPath $file -ErrorAction SilentlyContinue){
        $port=5432
        foreach($line in Get-Content -LiteralPath $file -ErrorAction SilentlyContinue){
            if($line -match '^\s*port\s*=\s*''?(\d+)''?'){$port=[int]$Matches[1]}
        }
        if($port -ge 1 -and $port -le 65535){return $port}
    }
    return 5432
}
function Test-LocalPostgresPort([int]$Port) {
    $client=New-Object Net.Sockets.TcpClient
    try {
        if(-not $client.ConnectAsync('127.0.0.1',$Port).Wait(350)){return $false}
        $stream=$client.GetStream();$stream.ReadTimeout=350;$stream.WriteTimeout=350
        # PostgreSQL SSL negotiation: distinguish PostgreSQL from unrelated listeners.
        [byte[]]$request=0,0,0,8,4,210,22,47
        $stream.Write($request,0,$request.Length)
        $reply=$stream.ReadByte();return $reply -in @(83,78)
    } catch {return $false} finally {$client.Dispose()}
}
function Get-PostgresInstallState([object[]]$Servers) {
    $detected=@($Servers | Where-Object { $_.PSObject.Properties.Name -contains 'Detected' -and $_.Detected })
    return [pscustomobject]@{Installed=($detected.Count -gt 0);ShowInstallOptions=($detected.Count -eq 0)}
}
function Assert-InstallerSignature([string]$Path,[string]$ExpectedHash) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'The prerequisite installer is missing from this package.' }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'The prerequisite installer checksum did not match. Rebuild the package.' }
    if ((Get-AuthenticodeSignature -LiteralPath $Path).Status -ne 'Valid') { throw 'The prerequisite installer does not have a valid trusted publisher signature.' }
}
function Install-Prerequisite([string]$PackageRoot,[string]$Name,[switch]$Interactive,[string]$OptionFile='',[switch]$Repair) {
    $manifest=Get-Content (Join-Path $PackageRoot 'prerequisites.json') -Raw | ConvertFrom-Json
    $item=$manifest.PSObject.Properties[$Name]
    if (-not $item) { throw "This package does not include $Name. Use the prerequisite links in the wizard or rebuild with its official installer." }
    $path=Join-Path $PackageRoot $item.Value.file
    Assert-InstallerSignature $path $item.Value.sha256
    if ($OptionFile) { $process=Start-Process -FilePath $path -ArgumentList ('--optionfile "'+$OptionFile+'"') -PassThru -WindowStyle Hidden }
    elseif ($Interactive) { $process=Start-Process -FilePath $path -PassThru }
    else { $action='/install';if($Repair){$action='/repair'};$process=Start-Process -FilePath $path -ArgumentList ($action+' /quiet /norestart') -PassThru -WindowStyle Hidden }
    while(-not $process.WaitForExit(200)) { [Windows.Forms.Application]::DoEvents() }
    if ($process.ExitCode -eq 3010) { throw 'A prerequisite requires a Windows restart. Restart and run Kodiak Setup again.' }
    if ($process.ExitCode -ne 0) { throw "The $Name installer did not complete successfully (exit $($process.ExitCode))." }
}
function Install-LocalPostgres([string]$PackageRoot,[string]$Root,[hashtable]$Database) {
    if ($Database.Password -match '[\r\n\x00]' -or [string]::IsNullOrWhiteSpace($Database.Password)) { throw 'For a new PostgreSQL installation, enter a nonempty database password without line breaks.' }
    if ($Database.UserName -notmatch '^[a-z][a-z0-9_]{0,47}$') { throw 'For a new local PostgreSQL server, use postgres or another lowercase database administrator name.' }
    if (Get-Service -Name 'KodiakPostgreSQL' -ErrorAction SilentlyContinue) { throw 'The KodiakPostgreSQL service already exists. Connect to that server or use the vendor installer to repair it.' }
    Write-SetupLog 'Installing a local PostgreSQL server using the supplied database administrator credentials...'
    $optionFile=Join-Path $Root 'postgres-install.options'
    try {
        @('mode=unattended','unattendedmodeui=minimal',('superaccount='+$Database.UserName),('superpassword='+$Database.Password),('serverport='+$Database.Port),'servicename=KodiakPostgreSQL') | Set-Content -LiteralPath $optionFile -Encoding UTF8
        Install-Prerequisite $PackageRoot 'postgresql' -OptionFile $optionFile
    } finally { if(Test-Path -LiteralPath $optionFile){Remove-Item -LiteralPath $optionFile} }
}
function Get-IisPrerequisiteState {
    $service=Get-Service -Name W3SVC -ErrorAction SilentlyContinue
    return [pscustomobject]@{
        WebServerPresent=($null -ne $service)
        ManagementPresent=(@(Get-Module -ListAvailable WebAdministration).Count -gt 0)
        HostingBinaryPresent=(Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")
        Running=($null -ne $service -and $service.Status -eq 'Running')
    }
}
function Get-IisFeaturePlan([object[]]$Features) {
    $missing=@();$pending=@()
    foreach($feature in $Features){
        $state=[string]$feature.State
        if($state -in @('Enabled','Installed')){continue}
        if($state -match 'Pending'){$pending+=$feature.Name}else{$missing+=$feature.Name}
    }
    return [pscustomobject]@{Missing=$missing;Pending=$pending}
}
function Set-KodiakHttpsBinding([string]$SiteName,[int]$Port,[string]$HostName,[string]$Thumbprint,[string]$IPAddress='*') {
    $bindings=@(Get-WebBinding -Name $SiteName -Protocol https -Port ([string]$Port) -HostHeader $HostName -IPAddress $IPAddress)
    if($bindings.Count -ne 1){throw 'The new Kodiak HTTPS binding could not be uniquely identified.'}
    # Set-WebBinding does not accept -Protocol. Select the exact binding returned
    # by Get-WebBinding instead, preserving all other sites and bindings.
    $flags=0;if($HostName){$flags=1}
    Set-WebBinding -Name $SiteName -BindingInformation $bindings[0].bindingInformation -PropertyName sslFlags -Value $flags
    $binding=Get-WebBinding -Name $SiteName -Protocol https -Port ([string]$Port) -HostHeader $HostName -IPAddress $IPAddress
    $binding.AddSslCertificate($Thumbprint,'My')
}
function Get-LocalWebAddresses {
    $addresses=@()
    foreach($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()){
        if($adapter.OperationalStatus -ne 'Up'){continue}
        foreach($item in $adapter.GetIPProperties().UnicastAddresses){
            if($item.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork -and -not [Net.IPAddress]::IsLoopback($item.Address) -and $item.Address.ToString() -notlike '169.254.*'){$addresses+=$item.Address.ToString()}
        }
    }
    return @($addresses | Sort-Object -Unique)
}
function Get-KodiakAccessPlan([hashtable]$Options,[string[]]$LocalAddresses=@(Get-LocalWebAddresses)) {
    $mode='Local';if($Options.ContainsKey('AccessMode')){$mode=$Options.AccessMode}
    if($mode -notin @('Local','Network','Public')){throw 'Select this PC, office network, or public domain access.'}
    if($Options.WebPort -lt 1 -or $Options.WebPort -gt 65535){throw 'Choose a website port between 1 and 65535.'}
    $hostName=$Options.HostName.Trim().ToLowerInvariant();$ip=$null
    $isIp=[Net.IPAddress]::TryParse($hostName,[ref]$ip)
    $bindingIp='*';if($Options.ContainsKey('BindingIP')){$bindingIp=$Options.BindingIP}
    if($bindingIp -ne '*' -and $bindingIp -ne '127.0.0.1' -and $bindingIp -notin $LocalAddresses){throw 'The binding IP must be an address assigned to this server. A router/public IP belongs in the DNS target field.'}
    $https=$Options.ContainsKey('Https') -and $Options.Https
    $header=$hostName
    if($mode -eq 'Local'){
        if($hostName -notin @('localhost','127.0.0.1')){throw 'For this PC only, use localhost or 127.0.0.1.'}
        $bindingIp='127.0.0.1'
    }elseif($mode -eq 'Network'){
        if(-not $isIp -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $hostName -notin $LocalAddresses){throw 'Choose this server''s local IPv4 address for office-network access.'}
        if($bindingIp -eq '127.0.0.1' -or ($bindingIp -ne '*' -and $bindingIp -ne $hostName)){throw 'The office binding must use the selected server IP or all server addresses.'}
    }else{
        if($isIp -or $hostName.Length -gt 253 -or $hostName -notmatch '^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z](?:[a-z0-9-]{0,61}[a-z0-9])?$'){throw 'Enter a domain name such as tracking.company.com, without https://, a port, or a path.'}
        if(-not $https){throw 'Public domain access requires HTTPS.'}
        if($bindingIp -eq '127.0.0.1'){throw 'Public access cannot bind only to the loopback address.'}
        if(-not $Options.ContainsKey('CertificateThumbprint') -or [string]::IsNullOrWhiteSpace($Options.CertificateThumbprint)){throw 'Select an installed HTTPS certificate for the public domain. A self-signed certificate is only created for local/office access.'}
    }
    if($isIp){$header=''} # IP requests must not use SNI or a hostname-only binding.
    $publicIp='';if($Options.ContainsKey('PublicIP')){$publicIp=$Options.PublicIP.Trim()}
    if($publicIp){$parsed=$null;if(-not [Net.IPAddress]::TryParse($publicIp,[ref]$parsed) -or $parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or [Net.IPAddress]::IsLoopback($parsed) -or $parsed.Equals([Net.IPAddress]::Any)){throw 'Enter a valid public IPv4 address for the DNS target, or leave it blank.'}}
    $scheme='http';if($https){$scheme='https'}
    $suffix=':'+ $Options.WebPort;if(($https -and $Options.WebPort -eq 443) -or (-not $https -and $Options.WebPort -eq 80)){$suffix=''}
    $connect='127.0.0.1';if($bindingIp -ne '*'){$connect=$bindingIp}
    $firewall=$mode -ne 'Local' -and $Options.ContainsKey('OpenFirewall') -and $Options.OpenFirewall
    return [pscustomobject]@{Mode=$mode;HostName=$hostName;BindingIP=$bindingIp;HostHeader=$header;Scheme=$scheme;Url=($scheme+'://'+$hostName+$suffix+'/');ConnectAddress=$connect;PublicIP=$publicIp;OpenFirewall=$firewall}
}
function Get-KodiakCertificate([hashtable]$Options,$Plan) {
    if($Options.CertificateThumbprint){$certificate=Get-Item ('Cert:\LocalMachine\My\'+$Options.CertificateThumbprint)}
    elseif($Plan.HostHeader){$certificate=New-SelfSignedCertificate -DnsName $Plan.HostName -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName ('Kodiak '+$Options.SiteName)}
    else{$certificate=New-SelfSignedCertificate -Subject ('CN='+$Plan.HostName) -TextExtension @('2.5.29.17={text}IPAddress='+$Plan.HostName) -Type SSLServerAuthentication -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName ('Kodiak '+$Options.SiteName)}
    if(-not $certificate.HasPrivateKey -or $certificate.NotAfter -lt (Get-Date) -or $certificate.NotBefore -gt (Get-Date)){throw 'Select a currently valid certificate with its private key installed on this server.'}
    if($Plan.Mode -eq 'Public'){
        $matchesName=$false
        foreach($name in $certificate.DnsNameList){
            $dns=[string]$name.Unicode
            if($dns -ieq $Plan.HostName){$matchesName=$true}
            if($dns.StartsWith('*.') -and $Plan.HostName -match ('^[^.]+\.'+[regex]::Escape($dns.Substring(2))+'$')){$matchesName=$true}
        }
        if(-not $matchesName){throw 'The selected certificate does not include the public domain. Install a matching server certificate first.'}
    }
    return $certificate
}
function Add-KodiakFirewall([hashtable]$Options,$Plan,[string]$Root) {
    if(-not $Plan.OpenFirewall){return ''}
    $name='Kodiak-'+[Guid]::NewGuid().ToString('N')
    $parameters=@{Name=$name;DisplayName=('Kodiak '+$Options.SiteName+' TCP '+$Options.WebPort);Description=('Kodiak installation: '+$Root);Direction='Inbound';Action='Allow';Protocol='TCP';LocalPort=$Options.WebPort}
    if($Plan.Mode -eq 'Network'){$parameters.Profile=@('Domain','Private');$parameters.RemoteAddress='LocalSubnet'}else{$parameters.Profile='Any';$parameters.RemoteAddress='Any'}
    if($Plan.BindingIP -ne '*'){$parameters.LocalAddress=$Plan.BindingIP}
    New-NetFirewallRule @parameters | Out-Null
    Write-SetupLog ('Created a dedicated '+$Plan.Mode+' firewall rule for TCP port '+$Options.WebPort+'.')
    return $name
}
function Set-KodiakDesktopShortcut([string]$SiteName,[string]$Url) {
    $shell=New-Object -ComObject WScript.Shell
    $shortcut=$shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) ($SiteName+'.url')))
    $shortcut.TargetPath=$Url;$shortcut.Save()
}
function Assert-KodiakBindingAvailable([hashtable]$Options,$Plan,[switch]$AllowExisting) {
    foreach($site in Get-Website){
        foreach($binding in @(Get-WebBinding -Name $site.Name -Protocol $Plan.Scheme -Port ([string]$Options.WebPort))){
            if($binding.bindingInformation -notmatch '^(.*):(\d+):(.*)$'){continue}
            $oldIp=$Matches[1];$oldHost=$Matches[3]
            $overlap=$oldIp -eq '*' -or $Plan.BindingIP -eq '*' -or $oldIp -eq $Plan.BindingIP
            if(-not $overlap){continue}
            if($AllowExisting -and $site.Name -eq $Options.SiteName -and $oldIp -eq $Plan.BindingIP -and $oldHost -ieq $Plan.HostHeader){continue}
            $sameHost=$oldHost -ieq $Plan.HostHeader
            $sharedIpCertificate=$Plan.Scheme -eq 'https' -and -not $Plan.HostHeader -and (([int]$binding.sslFlags -band 1) -eq 0)
            if($sameHost -or $sharedIpCertificate){throw ('The selected IP/port/host conflicts with an existing binding on IIS site '+$site.Name+'. Choose a different port, server IP, or domain. Existing bindings were not changed.')}
        }
    }
}
function Update-KodiakAccess([string]$InstallPath,[hashtable]$Options) {
    Assert-Administrator
    $root=[IO.Path]::GetFullPath($InstallPath).TrimEnd('\');$path=Join-Path $root 'installed.json'
    if(-not (Test-Path -LiteralPath $path)){throw 'Select an existing Kodiak installation folder on the Installation tab first.'}
    $record=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if(-not $root.Equals([IO.Path]::GetFullPath($record.installPath).TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase) -or $record.siteName -ne $Options.SiteName){throw 'The selected installation folder and IIS site name must match the installed Kodiak application.'}
    $plan=Get-KodiakAccessPlan $Options
    Import-Module WebAdministration
    $site=Get-Website -Name $Options.SiteName
    if(-not $site -or -not ([IO.Path]::GetFullPath($site.physicalPath)).Equals((Join-Path $root 'Api'),[StringComparison]::OrdinalIgnoreCase)){throw 'The IIS site does not point to this Kodiak installation. No bindings were changed.'}
    $script:LogFile=Join-Path $root 'access-setup.log'
    Assert-KodiakBindingAvailable $Options $plan -AllowExisting
    $existing=@(Get-WebBinding -Name $Options.SiteName -Protocol $plan.Scheme -Port ([string]$Options.WebPort) -IPAddress $plan.BindingIP -HostHeader $plan.HostHeader)
    $added=$false;$rule=''
    try{
        if($existing.Count){
            if($existing.Count -ne 1){throw 'The requested binding is not unique.'}
            if($Options.Https){
                $hash=$existing[0].certificateHash
                if($hash -is [byte[]]){$hash=[BitConverter]::ToString($hash).Replace('-','')}
                if($Options.CertificateThumbprint -and $Options.CertificateThumbprint -ine [string]$hash){throw 'This binding already uses another certificate. Select that certificate or choose a new port/domain.'}
                $Options.CertificateThumbprint=[string]$hash
                [void](Get-KodiakCertificate $Options $plan)
            }
            Write-SetupLog 'The requested binding is already present. Reusing it.'
        }else{
            $certificate=$null;if($Options.Https){$certificate=Get-KodiakCertificate $Options $plan;$Options.CertificateThumbprint=$certificate.Thumbprint}
            New-WebBinding -Name $Options.SiteName -Protocol $plan.Scheme -Port $Options.WebPort -IPAddress $plan.BindingIP -HostHeader $plan.HostHeader | Out-Null
            $added=$true
            if($Options.Https){Set-KodiakHttpsBinding $Options.SiteName $Options.WebPort $plan.HostHeader $certificate.Thumbprint $plan.BindingIP}
        }
        Test-KodiakSite $Options $plan.Scheme
        $rule=Add-KodiakFirewall $Options $plan $root
        $rules=@();if($record.PSObject.Properties.Name -contains 'firewallRules'){$rules=@($record.firewallRules)};if($rule){$rules+=$rule}
        foreach($entry in @{url=$plan.Url;accessMode=$plan.Mode;publicIP=$plan.PublicIP;firewallRules=$rules}.GetEnumerator()){$record|Add-Member -NotePropertyName $entry.Key -NotePropertyValue $entry.Value -Force}
        Copy-Item -LiteralPath $path -Destination ($path+'.bak') -Force
        $record|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $path -Encoding UTF8
        Set-KodiakDesktopShortcut $Options.SiteName $plan.Url
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-Kodiak.ps1') -Destination (Join-Path $root 'Uninstall-Kodiak.ps1') -Force
        Write-SetupLog ('Added/validated website access: '+$plan.Url+'. Existing Kodiak bindings and database were retained.')
        return $plan.Url
    }catch{
        if($rule){Remove-NetFirewallRule -Name $rule}
        if($added){Remove-WebBinding -Name $Options.SiteName -Protocol $plan.Scheme -IPAddress $plan.BindingIP -Port ([string]$Options.WebPort) -HostHeader $plan.HostHeader}
        throw
    }
}
function Enable-KodiakIis([string]$PackageRoot) {
    Write-SetupLog 'Validating the existing IIS installation and hosting module...'
    $current=Get-IisPrerequisiteState
    if($current.WebServerPresent -and $current.ManagementPresent){
        Write-SetupLog 'IIS and its management module are already available. Windows feature configuration will not be changed.'
    }else{
        $serverEdition=$null -ne (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue)
        $features=@()
        # ASP.NET Core serves its own static files. The IIS console, default-document
        # and static-content optional features are not prerequisites for this package.
        if($serverEdition){
            $names=@();if(-not $current.WebServerPresent){$names+='Web-Server'};if(-not $current.ManagementPresent){$names+='Web-Scripting-Tools'}
            foreach($feature in Get-WindowsFeature -Name $names){$features+=[pscustomobject]@{Name=$feature.Name;State=[string]$feature.InstallState}}
        }else{
            $names=@();if(-not $current.WebServerPresent){$names+='IIS-WebServerRole'};if(-not $current.ManagementPresent){$names+='IIS-ManagementScriptingTools'}
            foreach($name in $names){$feature=Get-WindowsOptionalFeature -Online -FeatureName $name;$features+=[pscustomobject]@{Name=$name;State=[string]$feature.State}}
        }
        $plan=Get-IisFeaturePlan $features
        if($plan.Pending.Count){throw ('A required IIS component is pending a restart from an earlier Windows change: '+($plan.Pending -join ', ')+'. This run did not change these components. Restart Windows to finish that pending change.')}
        if($plan.Missing.Count){
            Write-SetupLog ('Enabling only missing prerequisites: '+($plan.Missing -join ', '))
            $restart=$false
            if($serverEdition){$result=Install-WindowsFeature -Name $plan.Missing;if(-not $result.Success){throw 'Windows could not install the required IIS components.'};$restart=([string]$result.RestartNeeded -eq 'Yes')}
            else{foreach($name in $plan.Missing){$result=Enable-WindowsOptionalFeature -Online -FeatureName $name -All -NoRestart;$restart=$restart -or $result.RestartNeeded}}
            if($restart){throw ('Windows requires a restart to finish installing the missing prerequisites: '+($plan.Missing -join ', ')+'. Restart and rerun setup. Existing IIS sites and bindings were not changed.')}
        }
        $current=Get-IisPrerequisiteState
        if(-not $current.WebServerPresent -or -not $current.ManagementPresent){throw 'The required IIS service or management module is still unavailable. Review the Windows feature installation results.'}
    }
    Import-Module WebAdministration
    $registered=@(Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2').Count -gt 0
    if($current.HostingBinaryPresent -and $registered){
        Write-SetupLog 'The ASP.NET Core IIS hosting module is already installed and registered. Reusing it.'
    }else{
        Write-SetupLog 'The ASP.NET Core IIS hosting module is missing or unregistered. Installing/repairing only that prerequisite...'
        Install-Prerequisite $PackageRoot 'hostingBundle' -Repair:$current.HostingBinaryPresent
        if(-not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')){throw 'The ASP.NET Core IIS module is still not registered. Review the Hosting Bundle installation results.'}
    }
    if((Get-Service W3SVC).Status -ne 'Running'){Write-SetupLog 'Starting the existing IIS service...';Start-Service W3SVC}
    else{Write-SetupLog 'IIS is already running. No service restart is needed.'}
}
function Assert-InstallOptions([hashtable]$Options) {
    if ($Options.SiteName -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,49}$') { throw 'Use a website name beginning with a letter, followed by letters, digits, underscores or hyphens (maximum 50).' }
    if ($Options.WebPort -lt 1 -or $Options.WebPort -gt 65535) { throw 'Choose a website port between 1 and 65535.' }
    [void](Get-KodiakAccessPlan $Options)
    $root=[IO.Path]::GetFullPath($Options.InstallPath).TrimEnd('\')
    if ($root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\') -or $root.StartsWith('\\')) { throw 'Choose a dedicated folder on a local drive.' }
    foreach($protected in @($env:windir, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData, $env:USERPROFILE)) {
        if ($protected -and $root.Equals($protected.TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Select an application subfolder rather than a system or user root folder.' }
    }
    $ancestor=$root
    while($ancestor) {
        if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Choose a folder without directory junctions or symbolic links in its path.' }
        $ancestor=Split-Path $ancestor -Parent
    }
    if (Test-Path (Join-Path $root 'installed.json')) { throw 'Kodiak is already installed in this folder. This first installer supports new installations and recovery of incomplete installations; use a different folder.' }
    if ((Test-Path $root) -and @(Get-ChildItem -LiteralPath $root -Force).Count -gt 0 -and -not (Test-Path (Join-Path $root 'setup-state.json'))) { throw 'Choose an empty folder. Setup will not overwrite unrelated files.' }
    if ($Options.AdminPassword.Length -lt 12 -or $Options.AdminPassword.Length -gt 128) { throw 'The superadmin password must contain 12-128 characters.' }
    if ([string]::IsNullOrWhiteSpace($Options.AdminName)) { throw 'Enter the superadmin username.' }
    return $root
}
function Install-Kodiak([string]$PackageRoot,[hashtable]$Options,[hashtable]$Database) {
    Assert-Administrator
    $root=Assert-InstallOptions $Options
    $access=Get-KodiakAccessPlan $Options
    $script:LogFile=$null
    Set-PrivateDirectory $root
    $script:LogFile=Join-Path $root 'setup.log'
    $statePath=Join-Path $root 'setup-state.json'
    if (-not (Test-Path $statePath)) { @{siteName=$Options.SiteName;created=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content $statePath }
    $state=Get-Content $statePath -Raw | ConvertFrom-Json
    if ($state.siteName -ne $Options.SiteName) { throw 'Use the same website name when retrying an incomplete installation.' }
    Write-SetupLog 'Starting Kodiak installation. Passwords and connection strings are excluded from this log.'
    Enable-KodiakIis $PackageRoot
    if ($Options.AutoInstallPostgres -and $Database.Host -in @('localhost','127.0.0.1','::1') -and @((Get-LocalPostgresServers) | Where-Object Detected).Count -eq 0) {
        # Probe first: a running server can exist without an EDB registry entry.
        $reachable=$false
        try { $probe=@{}+$Database;$probe.Action='test';$probe.Database='postgres';[void](Invoke-SetupTask (Join-Path $PackageRoot 'Api\Kodiak.ContainerTracking.Api.exe') $probe);$reachable=$true } catch { }
        if(-not $reachable) {
            $socket=New-Object Net.Sockets.TcpClient
            try { $listening=$socket.ConnectAsync($Database.Host,[int]$Database.Port).Wait(3000) -and $socket.Connected } catch { $listening=$false } finally { $socket.Dispose() }
            if($listening){throw 'A server already answers on the selected PostgreSQL port. Check its credentials and TLS setting; setup will not install another server over it.'}
            Install-LocalPostgres $PackageRoot $root $Database
        }
    }
    if (Test-Path "IIS:\Sites\$($Options.SiteName)") { throw 'An IIS website with this name already exists. Choose another name; existing websites were not changed.' }
    if (Test-Path "IIS:\AppPools\$($Options.SiteName)") { throw 'An IIS application pool with this name already exists. Choose another name.' }
    Assert-KodiakBindingAvailable $Options $access
    $api=Join-Path $root 'Api';$web=Join-Path $root 'Web';$data=Join-Path $root 'Data'
    foreach($path in @($api,$web,$data)) { [IO.Directory]::CreateDirectory($path) | Out-Null }
    Write-SetupLog 'Copying the published API and compiled Vue website...'
    Copy-Item -Path (Join-Path $PackageRoot 'Api\*') -Destination $api -Recurse -Force
    Copy-Item -Path (Join-Path $PackageRoot 'Web\*') -Destination $web -Recurse -Force
    $exe=Join-Path $api 'Kodiak.ContainerTracking.Api.exe'
    $configPath=Join-Path $api 'appsettings.Production.json'
    if (Test-Path $configPath) {
        Write-SetupLog 'Recovering the saved configuration from the previous incomplete installation...'
        $config=Get-Content $configPath -Raw | ConvertFrom-Json
        # Recover the dedicated application credentials, never create another database on retry.
        $connection=$config.ConnectionStrings.TrackingDatabase
    } else {
        if ($Options.CreateDatabase) {
            Write-SetupLog 'Creating a new database and a dedicated application login...'
            $request=@{}+$Database;$request.Action='create'
            $created=Invoke-SetupTask $exe $request
            $connection=$created.connectionString
        } else {
            Write-SetupLog 'Testing the existing application database...'
            $request=@{}+$Database;$request.Action='test';[void](Invoke-SetupTask $exe $request)
            # Build the connection string through the helper to preserve special characters.
            $request.Action='connection';$connectionResult=Invoke-SetupTask $exe $request
            $connection=$connectionResult.connectionString
        }
        $key=New-Object byte[] 48;[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)
        $config=@{ConnectionStrings=@{TrackingDatabase=$connection};Jwt=@{SigningKey=[Convert]::ToBase64String($key)};Bootstrap=@{Enabled=$false};Frontend=@{RootPath=$web};PhotoStorage=@{RootPath=(Join-Path $data 'Photos')};ContainerStorage=@{RootPath=(Join-Path $data 'ContainerAttachments')}}
        $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8
    }
    Write-SetupLog 'Applying database migrations and creating the superadmin...'
    # The protected config owns connection settings. Pass the connection only through stdin.
    $request=@{Action='initialize';ConnectionString=$connection;AdminName=$Options.AdminName;AdminPassword=$Options.AdminPassword;SeedGeography=$Options.SeedGeography}
    [void](Invoke-SetupTask $exe $request)
    $createdSite=$false;$createdPool=$false;$firewallRule=''
    try {
        Write-SetupLog 'Creating the dedicated IIS website and application pool...'
        New-WebAppPool -Name $Options.SiteName | Out-Null;$createdPool=$true
        Set-ItemProperty "IIS:\AppPools\$($Options.SiteName)" -Name managedRuntimeVersion -Value ''
        Set-ItemProperty "IIS:\AppPools\$($Options.SiteName)" -Name processModel.identityType -Value 4
        $identity="IIS AppPool\$($Options.SiteName)"
        $acl=Get-Acl $root
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule ($identity,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
        Set-Acl -LiteralPath $root -AclObject $acl
        $acl=Get-Acl $data
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule ($identity,'Modify','ContainerInherit,ObjectInherit','None','Allow')))
        Set-Acl -LiteralPath $data -AclObject $acl
        if ($Options.Https) {
            $certificate=Get-KodiakCertificate $Options $access
            New-Website -Name $Options.SiteName -PhysicalPath $api -ApplicationPool $Options.SiteName -Port $Options.WebPort -IPAddress $access.BindingIP -HostHeader $access.HostHeader -Ssl | Out-Null;$createdSite=$true
            Set-KodiakHttpsBinding $Options.SiteName $Options.WebPort $access.HostHeader $certificate.Thumbprint $access.BindingIP
            $scheme='https'
        } else {
            New-Website -Name $Options.SiteName -PhysicalPath $api -ApplicationPool $Options.SiteName -Port $Options.WebPort -IPAddress $access.BindingIP -HostHeader $access.HostHeader | Out-Null;$createdSite=$true
            $scheme='http'
        }
        if ($Options.Https) { $Options.CertificateThumbprint=$certificate.Thumbprint }
        Start-WebAppPool $Options.SiteName -ErrorAction SilentlyContinue
        Start-Website $Options.SiteName
        Write-SetupLog 'Checking the website and API through IIS...'
        Test-KodiakSite $Options $scheme
        $firewallRule=Add-KodiakFirewall $Options $access $root
        $url=$access.Url
        @{installPath=$root;siteName=$Options.SiteName;url=$url;accessMode=$access.Mode;publicIP=$access.PublicIP;firewallRules=@($firewallRule|Where-Object {$_});installed=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $root 'installed.json')
        Copy-Item (Join-Path $PackageRoot 'scripts\Uninstall-Kodiak.ps1') (Join-Path $root 'Uninstall-Kodiak.ps1') -Force
        Set-KodiakDesktopShortcut $Options.SiteName $url
        Write-SetupLog 'Installation completed. The database and uploaded files are preserved by the uninstall utility.'
        return $url
    } catch {
        if($firewallRule){Remove-NetFirewallRule -Name $firewallRule}
        if ($createdSite) { Remove-Website $Options.SiteName }
        if ($createdPool) { Remove-WebAppPool $Options.SiteName }
        Write-SetupLog 'Installation was incomplete. The database, saved settings and files were retained; rerun setup with the same folder and site name.'
        throw
    }
}
function Test-KodiakSite([hashtable]$Options,[string]$Scheme) {
    $access=Get-KodiakAccessPlan $Options
    # Connect locally while sending the configured SNI/Host, without requiring DNS.
    # HTTPS is pinned to the certificate selected/generated by this installation.
    if (-not ('KodiakSetupProbe' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
public static class KodiakSetupProbe {
    public static int Status(int port,string host,string path,bool https,string thumbprint,string connectAddress) {
        using(var client=new TcpClient()) {
            if(!client.ConnectAsync(connectAddress,port).Wait(5000)) throw new IOException("Connection timed out.");
            client.ReceiveTimeout=5000;client.SendTimeout=5000;
            Stream stream=client.GetStream();
            if(https) {
                var tls=new SslStream(stream,false,(sender,cert,chain,errors)=>cert!=null && String.Equals(cert.GetCertHashString(),thumbprint,StringComparison.OrdinalIgnoreCase));
                tls.ReadTimeout=5000;tls.WriteTimeout=5000;tls.AuthenticateAsClient(host);stream=tls;
            }
            using(stream) {
                var bytes=Encoding.ASCII.GetBytes("GET "+path+" HTTP/1.1\r\nHost: "+host+":"+port+"\r\nConnection: close\r\n\r\n");
                stream.Write(bytes,0,bytes.Length);
                using(var reader=new StreamReader(stream)) {
                    var first=reader.ReadLine();if(first==null) return 0;
                    var parts=first.Split(' ');return parts.Length>1?Int32.Parse(parts[1]):0;
                }
            }
        }
    }
}
'@ }
    for($attempt=0;$attempt -lt 15;$attempt++) {
        try {
            $page=[KodiakSetupProbe]::Status($Options.WebPort,$access.HostName,'/',($Scheme -eq 'https'),$Options.CertificateThumbprint,$access.ConnectAddress)
            $api=[KodiakSetupProbe]::Status($Options.WebPort,$access.HostName,'/api/profile/avatar',($Scheme -eq 'https'),$Options.CertificateThumbprint,$access.ConnectAddress)
            if ($page -eq 200 -and $api -eq 401) { return }
        } catch { }
        [Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 400
    }
    throw 'The IIS health check failed. Check Event Viewer, the Hosting Bundle, database configuration and port bindings. Setup has retained the database and configuration for retry.'
}
$script:LogFile=$null
$script:OnProgress=$null
