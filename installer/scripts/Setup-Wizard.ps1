param([string]$PackageRoot=(Split-Path $PSScriptRoot),[switch]$ValidateOnly,[switch]$SmokeTest)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'Setup-Core.ps1')
if ($ValidateOnly) { 'Kodiak setup scripts loaded successfully.';return }
if(-not $SmokeTest){Assert-Administrator}
[Windows.Forms.Application]::EnableVisualStyles()
$form=New-Object Windows.Forms.Form
$form.Text='Kodiak - Installation and configuration';$form.Size=New-Object Drawing.Size(850,790)
$form.MinimumSize=$form.Size;$form.StartPosition='CenterScreen';$form.Font=New-Object Drawing.Font('Segoe UI',10)
$form.BackColor=[Drawing.Color]::FromArgb(245,248,251)
$heading=New-Object Windows.Forms.Label
$heading.Text='Install Kodiak Container Tracking';$heading.Font=New-Object Drawing.Font('Segoe UI',19,[Drawing.FontStyle]::Bold)
$heading.Location=New-Object Drawing.Point(25,20);$heading.Size=New-Object Drawing.Size(780,38);$form.Controls.Add($heading)
$intro=New-Object Windows.Forms.Label
$intro.Text='Choose a destination, connect PostgreSQL, and create your operations workspace.'
$intro.Location=New-Object Drawing.Point(27,65);$intro.Size=New-Object Drawing.Size(780,28);$form.Controls.Add($intro)
$tabs=New-Object Windows.Forms.TabControl
$tabs.Location=New-Object Drawing.Point(25,105);$tabs.Size=New-Object Drawing.Size(785,495);$form.Controls.Add($tabs)
function New-Page([string]$Title) { $page=New-Object Windows.Forms.TabPage;$page.Text=$Title;$page.BackColor=[Drawing.Color]::White;$tabs.TabPages.Add($page);return $page }
function Add-Label($Parent,[string]$Text,[int]$X,[int]$Y,[int]$Width=690) { $label=New-Object Windows.Forms.Label;$label.Text=$Text;$label.Location=New-Object Drawing.Point($X,$Y);$label.Size=New-Object Drawing.Size($Width,25);$Parent.Controls.Add($label);return $label }
function Add-Text($Parent,[string]$Title,[int]$Y,[string]$Value='',[switch]$Password) {
    [void](Add-Label $Parent $Title 22 $Y)
    $box=New-Object Windows.Forms.TextBox;$box.Location=New-Object Drawing.Point(25,($Y+27));$box.Size=New-Object Drawing.Size(690,28);$box.Text=$Value
    if ($Password) { $box.UseSystemPasswordChar=$true };$Parent.Controls.Add($box);return $box
}
function Add-Button($Parent,[string]$Text,[int]$X,[int]$Y,[int]$Width=170) {
    $button=New-Object Windows.Forms.Button;$button.Text=$Text;$button.Location=New-Object Drawing.Point($X,$Y);$button.Size=New-Object Drawing.Size($Width,34);$button.FlatStyle='Flat';$Parent.Controls.Add($button);return $button
}
$install=New-Page '1. Installation'
$folder=Add-Text $install 'Installation folder (a dedicated empty local folder)' 20 (Join-Path $env:ProgramFiles 'Kodiak')
$browse=Add-Button $install 'Choose folder...' 25 87
$browse.Add_Click({ $dialog=New-Object Windows.Forms.FolderBrowserDialog;$dialog.Description='Choose the Kodiak installation folder';if ($dialog.ShowDialog() -eq 'OK') {$folder.Text=$dialog.SelectedPath};$dialog.Dispose() })
$iisStatus=Add-Label $install 'Checking the existing IIS installation...' 25 145
$iisHelp=Add-Label $install 'Only missing prerequisites are installed. Existing IIS websites and bindings are preserved.' 25 181
$hosting=Add-Button $install '.NET Hosting Bundle' 25 230 220
$hosting.Add_Click({Start-Process 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0'})
$pg=Add-Button $install 'Install PostgreSQL...' 260 230 220
$pg.Add_Click({
    try { $form.Enabled=$false;Install-Prerequisite $PackageRoot 'postgresql' -Interactive;Refresh-Servers }
    catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message,'PostgreSQL setup') | Out-Null }
    finally {$form.Enabled=$true}
})
$pgLink=Add-Button $install 'PostgreSQL downloads' 495 230 220
$pgLink.Add_Click({Start-Process 'https://www.postgresql.org/download/windows/'})
$pgStatus=Add-Label $install 'Checking for an existing PostgreSQL installation...' 25 289
$autoPg=New-Object Windows.Forms.CheckBox;$autoPg.Text='Automatically install local PostgreSQL if it is missing';$autoPg.Checked=$true;$autoPg.Location=New-Object Drawing.Point(25,325);$autoPg.Size=New-Object Drawing.Size(690,28);$install.Controls.Add($autoPg)
$pgHelp=Add-Label $install 'Automatic installation uses the database username, password and port from the next tab.' 25 370
[void](Add-Label $install 'No Node.js or .NET SDK is required on this machine. Administrator rights are required.' 25 410)
$dbPage=New-Page '2. Database'
[void](Add-Label $dbPage 'Detected local installations / previously tested servers' 25 14)
$servers=New-Object Windows.Forms.ComboBox;$servers.Location=New-Object Drawing.Point(25,42);$servers.Size=New-Object Drawing.Size(520,28);$servers.DropDownStyle='DropDownList';$dbPage.Controls.Add($servers)
$refresh=Add-Button $dbPage 'Refresh local servers' 555 39 185
$hostBox=Add-Text $dbPage 'PostgreSQL host (local or remote)' 80 'localhost'
$portBox=Add-Text $dbPage 'PostgreSQL port' 142 '5432'
$userBox=Add-Text $dbPage 'Database username' 204 'postgres'
$passBox=Add-Text $dbPage 'Database password' 266 '' -Password
[void](Add-Label $dbPage 'TLS mode' 25 333 150)
$tls=New-Object Windows.Forms.ComboBox;$tls.Location=New-Object Drawing.Point(190,330);$tls.Size=New-Object Drawing.Size(180,28);$tls.DropDownStyle='DropDownList';$tls.Items.AddRange(@('Prefer','VerifyFull','Require','Disable'));$tls.SelectedIndex=0;$dbPage.Controls.Add($tls)
$test=Add-Button $dbPage 'Test and list databases' 395 327 320
$create=New-Object Windows.Forms.CheckBox;$create.Text='Create a new empty application database';$create.Checked=$true;$create.Location=New-Object Drawing.Point(25,374);$create.Size=New-Object Drawing.Size(440,25);$dbPage.Controls.Add($create)
$dbName=New-Object Windows.Forms.ComboBox;$dbName.Location=New-Object Drawing.Point(25,412);$dbName.Size=New-Object Drawing.Size(690,28);$dbName.Text='kodiak_container_tracking';$dbPage.Controls.Add($dbName)
$script:serverEntries=@()
$historyPath=Join-Path $env:ProgramData 'KodiakSetup\servers.json'
function Refresh-Servers {
    $iisState=Get-IisPrerequisiteState
    $hosting.Visible=-not $iisState.HostingBinaryPresent
    if($iisState.WebServerPresent -and $iisState.ManagementPresent -and $iisState.HostingBinaryPresent){
        $iisStatus.Text='IIS and hosting components detected. Existing configuration will be reused.'
        $iisStatus.ForeColor=[Drawing.Color]::FromArgb(20,125,85)
    }else{
        $iisStatus.Text='Setup will install only the missing IIS or ASP.NET Core hosting prerequisites.'
        $iisStatus.ForeColor=[Drawing.Color]::FromArgb(65,85,105)
    }
    $servers.Items.Clear();$script:serverEntries=@(Get-LocalPostgresServers)
    $state=Get-PostgresInstallState $script:serverEntries
    $pg.Visible=$state.ShowInstallOptions;$pgLink.Visible=$state.ShowInstallOptions
    $autoPg.Visible=$state.ShowInstallOptions;$autoPg.Checked=$state.ShowInstallOptions
    if($state.Installed){
        $pgStatus.Text='PostgreSQL is already installed. No additional installation is needed.'
        $pgStatus.ForeColor=[Drawing.Color]::FromArgb(20,125,85)
        $pgHelp.Text='Continue to Database, select your server, and enter its connection credentials.'
    }else{
        $pgStatus.Text='No local PostgreSQL installation was detected. Install it here or connect to a remote server.'
        $pgStatus.ForeColor=[Drawing.Color]::FromArgb(65,85,105)
        $pgHelp.Text='Automatic installation uses the database username, password and port from the next tab.'
    }
    if (Test-Path $historyPath -ErrorAction SilentlyContinue) { try { $script:serverEntries+=@(Get-Content $historyPath -Raw | ConvertFrom-Json) } catch { } }
    foreach($item in $script:serverEntries){[void]$servers.Items.Add($item.Label)}
    if ($servers.Items.Count -gt 0) {$servers.SelectedIndex=0}
}
$servers.Add_SelectedIndexChanged({if($servers.SelectedIndex -ge 0){$entry=$script:serverEntries[$servers.SelectedIndex];$hostBox.Text=$entry.Host;$portBox.Text=[string]$entry.Port}})
$refresh.Add_Click({Refresh-Servers})
function Get-DatabaseRequest {
    $port=0;if(-not [int]::TryParse($portBox.Text,[ref]$port) -or $port -lt 1 -or $port -gt 65535){throw 'Enter a valid PostgreSQL port.'}
    return @{Host=$hostBox.Text.Trim();Port=$port;UserName=$userBox.Text.Trim();Password=$passBox.Text;Database=$dbName.Text.Trim();SslMode=[string]$tls.SelectedItem}
}
$test.Add_Click({
    try {
        $form.Enabled=$false;$request=Get-DatabaseRequest;$request.Action='list';$request.Database='postgres'
        $result=Invoke-SetupTask (Join-Path $PackageRoot 'Api\Kodiak.ContainerTracking.Api.exe') $request
        $current=$dbName.Text;$dbName.Items.Clear();foreach($name in $result.databases){[void]$dbName.Items.Add($name)};$dbName.Text=$current
        Set-PrivateDirectory (Split-Path $historyPath)
        $entries=@($script:serverEntries | Where-Object {$_.Host -ne $request.Host -or $_.Port -ne $request.Port})
        $entries+=[pscustomobject]@{Host=$request.Host;Port=$request.Port;Label=('{0}:{1} (previously tested)' -f $request.Host,$request.Port)}
        ConvertTo-Json -InputObject $entries | Set-Content $historyPath
        [Windows.Forms.MessageBox]::Show('Connected successfully. Available databases are now listed below.','PostgreSQL') | Out-Null
    } catch {[Windows.Forms.MessageBox]::Show($_.Exception.Message,'Connection test') | Out-Null}
    finally {$form.Enabled=$true}
})
$sitePage=New-Page '3. Website'
$siteName=Add-Text $sitePage 'IIS website / application pool name' 5 'Kodiak'
[void](Add-Label $sitePage 'Access mode' 25 68)
$accessMode=New-Object Windows.Forms.ComboBox;$accessMode.Location=New-Object Drawing.Point(25,94);$accessMode.Size=New-Object Drawing.Size(690,28);$accessMode.DropDownStyle='DropDownList';$accessMode.Items.AddRange(@('This PC only','Office network / IP address','Public domain'));$sitePage.Controls.Add($accessMode)
$hostLabel=Add-Label $sitePage 'Website hostname' 25 133 440
$webHost=New-Object Windows.Forms.TextBox;$webHost.Location=New-Object Drawing.Point(25,160);$webHost.Size=New-Object Drawing.Size(430,28);$sitePage.Controls.Add($webHost)
$publicLabel=Add-Label $sitePage 'Public IP (optional DNS target)' 475 133 270
$publicIp=New-Object Windows.Forms.TextBox;$publicIp.Location=New-Object Drawing.Point(475,160);$publicIp.Size=New-Object Drawing.Size(240,28);$sitePage.Controls.Add($publicIp)
[void](Add-Label $sitePage 'Server binding IP (* = all local addresses)' 25 200 430)
$bindingIp=New-Object Windows.Forms.ComboBox;$bindingIp.Location=New-Object Drawing.Point(25,227);$bindingIp.Size=New-Object Drawing.Size(430,28);$bindingIp.DropDownStyle='DropDownList';$bindingIp.Items.AddRange(@('*','127.0.0.1'));$localWebIps=@(Get-LocalWebAddresses);foreach($address in $localWebIps){[void]$bindingIp.Items.Add($address)};$sitePage.Controls.Add($bindingIp)
[void](Add-Label $sitePage 'Website port' 475 200 200)
$webPort=New-Object Windows.Forms.TextBox;$webPort.Location=New-Object Drawing.Point(475,227);$webPort.Size=New-Object Drawing.Size(240,28);$sitePage.Controls.Add($webPort)
$https=New-Object Windows.Forms.CheckBox;$https.Text='Use HTTPS';$https.Checked=$true;$https.Location=New-Object Drawing.Point(25,267);$https.Size=New-Object Drawing.Size(300,28);$sitePage.Controls.Add($https)
$thumbprint=Add-Text $sitePage 'Certificate thumbprint (Local Computer / Personal; required for a public domain)' 302 ''
$thumbprint.Width=540
$chooseCertificate=Add-Button $sitePage 'Choose certificate' 580 327 135
$chooseCertificate.Add_Click({
    $dialog=$null
    try{
        $certificates=@(Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) -and $_.NotBefore -le (Get-Date)} | Sort-Object NotAfter -Descending)
        if(-not $certificates.Count){throw 'No valid server certificates with private keys were found in Local Computer / Personal. Import your domain certificate there, then choose it here.'}
        $dialog=New-Object Windows.Forms.Form;$dialog.Text='Select an installed HTTPS certificate';$dialog.Size=New-Object Drawing.Size(780,190);$dialog.StartPosition='CenterParent'
        $list=New-Object Windows.Forms.ComboBox;$list.Location=New-Object Drawing.Point(20,20);$list.Size=New-Object Drawing.Size(720,30);$list.DropDownStyle='DropDownList'
        foreach($certificate in $certificates){[void]$list.Items.Add(($certificate.Subject+' | expires '+$certificate.NotAfter.ToString('dd MMM yyyy')))};$list.SelectedIndex=0;$dialog.Controls.Add($list)
        $accept=Add-Button $dialog 'Use certificate' 550 80 190;$accept.DialogResult='OK';$dialog.AcceptButton=$accept
        if($dialog.ShowDialog($form) -eq 'OK'){$thumbprint.Text=$certificates[$list.SelectedIndex].Thumbprint}
    }catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'HTTPS certificate')|Out-Null}
    finally{if($dialog){$dialog.Dispose()}}
})
$openFirewall=New-Object Windows.Forms.CheckBox;$openFirewall.Text='Create a Windows Firewall rule for the selected website port';$openFirewall.Location=New-Object Drawing.Point(25,372);$openFirewall.Size=New-Object Drawing.Size(690,28);$sitePage.Controls.Add($openFirewall)
$accessHelp=Add-Label $sitePage '' 25 411 465;$accessHelp.Height=44
$updateAccess=Add-Button $sitePage 'Update existing website' 500 413 215
function Get-WebsiteOptions {
    $port=0;if(-not [int]::TryParse($webPort.Text,[ref]$port)){throw 'Enter a valid website port.'}
    $mode=@('Local','Network','Public')[$accessMode.SelectedIndex]
    return @{InstallPath=$folder.Text;SiteName=$siteName.Text.Trim();HostName=$webHost.Text.Trim();WebPort=$port;Https=$https.Checked;CertificateThumbprint=$thumbprint.Text.Replace(' ','');AccessMode=$mode;BindingIP=[string]$bindingIp.SelectedItem;PublicIP=$publicIp.Text.Trim();OpenFirewall=$openFirewall.Checked}
}
$accessMode.Add_SelectedIndexChanged({
    $mode=$accessMode.SelectedIndex;$isPublic=$mode -eq 2
    $publicIp.Visible=$isPublic;$publicLabel.Visible=$isPublic
    $openFirewall.Enabled=$mode -ne 0;$openFirewall.Checked=$mode -ne 0
    $bindingIp.Enabled=$mode -ne 0
    if($mode -eq 0){$hostLabel.Text='Website hostname';$webHost.Text='localhost';$bindingIp.SelectedItem='127.0.0.1';$https.Checked=$true;$webPort.Text='8443';$accessHelp.Text='This PC only. A blank certificate creates a local self-signed certificate.'}
    elseif($mode -eq 1){$hostLabel.Text='This server''s office IPv4 address';$webHost.Text='';if($localWebIps.Count){$webHost.Text=$localWebIps[0]};$bindingIp.SelectedItem='*';$https.Checked=$false;$webPort.Text='8080';$accessHelp.Text='Office firewall access is limited to Domain/Private profiles and the local subnet.'}
    else{$hostLabel.Text='Public domain (for example tracking.company.com)';$webHost.Text='';$bindingIp.SelectedItem='*';$https.Checked=$true;$webPort.Text='443';$accessHelp.Text='Set DNS and router port forwarding separately. Public IP is a DNS target, not a server binding.'}
})
$bindingIp.Add_SelectedIndexChanged({if($accessMode.SelectedIndex -eq 1 -and $bindingIp.SelectedItem -notin @('*','127.0.0.1')){$webHost.Text=[string]$bindingIp.SelectedItem}})
$accessMode.SelectedIndex=0
$adminPage=New-Page '4. Administrator'
$adminName=Add-Text $adminPage 'Initial superadmin username' 20 'superadmin'
$adminPass=Add-Text $adminPage 'Initial password (12-128 characters)' 100 '' -Password
$confirmPass=Add-Text $adminPage 'Confirm password' 180 '' -Password
$seed=New-Object Windows.Forms.CheckBox;$seed.Text='Add country and state/province master data';$seed.Checked=$true;$seed.Location=New-Object Drawing.Point(25,270);$seed.Size=New-Object Drawing.Size(650,28);$adminPage.Controls.Add($seed)
[void](Add-Label $adminPage 'No sample shipments, shippers or consignees will be added.' 25 320)
[void](Add-Label $adminPage 'Connecting an existing database applies migrations. Back it up before continuing.' 25 356)
[void](Add-Label $adminPage 'An existing compatible superadmin is retained; this setup never resets its password.' 25 392)
$progressPage=New-Page '5. Progress'
$progress=New-Object Windows.Forms.TextBox;$progress.Multiline=$true;$progress.ReadOnly=$true;$progress.ScrollBars='Vertical';$progress.Location=New-Object Drawing.Point(20,20);$progress.Size=New-Object Drawing.Size(730,410);$progressPage.Controls.Add($progress)
$script:OnProgress={param($message) $progress.AppendText($message+[Environment]::NewLine);[Windows.Forms.Application]::DoEvents()}
$back=Add-Button $form 'Back' 25 620 100
$next=Add-Button $form 'Next' 135 620 100
$installButton=Add-Button $form 'Install Kodiak' 565 620 245
$installButton.BackColor=[Drawing.Color]::FromArgb(6,130,180);$installButton.ForeColor=[Drawing.Color]::White
$openButton=Add-Button $form 'Open Kodiak' 565 665 245;$openButton.Enabled=$false
$status=Add-Label $form 'Ready. Review all four configuration tabs, then choose Install Kodiak.' 25 675 520
$script:installing=$false;$script:installedUrl=''
$back.Add_Click({if($tabs.SelectedIndex -gt 0){$tabs.SelectedIndex--}})
$next.Add_Click({if($tabs.SelectedIndex -lt 3){$tabs.SelectedIndex++}})
$openButton.Add_Click({if($script:installedUrl){Start-Process $script:installedUrl}})
$updateAccess.Add_Click({
    try{
        $options=Get-WebsiteOptions
        $script:installing=$true;$form.Enabled=$false
        $script:installedUrl=Update-KodiakAccess $folder.Text $options
        $status.Text='Website access updated. Database and application data were retained.';$openButton.Enabled=$true
        [Windows.Forms.MessageBox]::Show(('Website access updated: '+$script:installedUrl),'Kodiak') | Out-Null
    }catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'Update website access')|Out-Null}
    finally{$script:installing=$false;$form.Enabled=$true}
})
$form.Add_FormClosing({param($sender,$e) if($script:installing){$e.Cancel=$true}})
$installButton.Add_Click({
    try {
        if($adminPass.Text -ne $confirmPass.Text){throw 'The administrator passwords do not match.'}
        $options=Get-WebsiteOptions
        $options.CreateDatabase=$create.Checked;$options.AdminName=$adminName.Text.Trim();$options.AdminPassword=$adminPass.Text;$options.SeedGeography=$seed.Checked;$options.AutoInstallPostgres=$autoPg.Checked
        $request=Get-DatabaseRequest
        if(-not $create.Checked){$answer=[Windows.Forms.MessageBox]::Show('Existing database: setup will apply migrations and add missing required records. Confirm you have a backup before proceeding.','Existing database','YesNo','Warning');if($answer -ne 'Yes'){return}}
        $script:installing=$true;$installButton.Enabled=$false;$back.Enabled=$false;$next.Enabled=$false
        foreach($page in @($install,$dbPage,$sitePage,$adminPage)){$page.Enabled=$false}
        $tabs.SelectedTab=$progressPage;$status.Text='Installation in progress...'
        $script:installedUrl=Install-Kodiak $PackageRoot $options $request
        $status.Text='Installed successfully. Use Open Kodiak to sign in.';$openButton.Enabled=$true
        $adminPass.Clear();$confirmPass.Clear();$passBox.Clear()
    } catch {
        $status.Text='Installation did not complete. Review the message and retry.'
        $progress.AppendText('ERROR: '+$_.Exception.Message+[Environment]::NewLine)
        [Windows.Forms.MessageBox]::Show($_.Exception.Message,'Kodiak setup') | Out-Null
    } finally {
        $script:installing=$false
        if(-not $script:installedUrl){$installButton.Enabled=$true;$back.Enabled=$true;$next.Enabled=$true;foreach($page in @($install,$dbPage,$sitePage,$adminPage)){$page.Enabled=$true}}
    }
})
Refresh-Servers
if($SmokeTest){
    if($tabs.TabPages.Count -ne 5){throw 'The setup wizard did not create all five pages.'}
    $state=Get-PostgresInstallState @(Get-LocalPostgresServers)
    if($state.Installed -and $autoPg.Checked){throw 'Automatic PostgreSQL installation remained enabled for an existing installation.'}
    $accessMode.SelectedIndex=1
    if($https.Checked -or $webPort.Text -ne '8080' -or -not $openFirewall.Checked){throw 'Office access defaults were not applied.'}
    $accessMode.SelectedIndex=2
    if(-not $https.Checked -or $webPort.Text -ne '443'){throw 'Public HTTPS defaults were not applied.'}
    $webHost.Text='tracking.example.com';$thumbprint.Text='TEST-CERT'
    $options=Get-WebsiteOptions
    if((Get-KodiakAccessPlan $options).Url -ne 'https://tracking.example.com/'){throw 'The public access URL is incorrect.'}
    $accessMode.SelectedIndex=0
    if($openFirewall.Checked -or $bindingIp.SelectedItem -ne '127.0.0.1'){throw 'This-PC-only mode did not restore local-only defaults.'}
    $next.PerformClick();$back.PerformClick()
    $form.Dispose();'Wizard controls initialized successfully; no installation was performed.';return
}
[void]$form.ShowDialog();$form.Dispose()
