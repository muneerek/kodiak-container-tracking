$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$count=0
foreach($script in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.ps1' | Where-Object FullName -NotMatch '\\artifacts\\|\\output\\') {
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ($script.Name+': '+(($errors | ForEach-Object Message) -join '; '))}
    $count++
}
. (Join-Path $root 'scripts\Setup-Core.ps1')
function Must-Reject([scriptblock]$Action,[string]$Reason) {
    $rejected=$false
    try {& $Action | Out-Null}catch{$rejected=$true}
    if(-not $rejected){throw "Validation did not reject: $Reason"}
}
$options=@{SiteName='KodiakTest';WebPort=8443;HostName='localhost';InstallPath=(Join-Path $env:TEMP ('kodiak-test-'+[Guid]::NewGuid().ToString('N')));AdminPassword='valid-test-password';AdminName='superadmin'}
$valid=Assert-InstallOptions $options
if($valid -ne $options.InstallPath){throw 'The valid installation path changed unexpectedly.'}
$bad=@{}+$options;$bad.SiteName='../OtherSite';Must-Reject {Assert-InstallOptions $bad} 'IIS name traversal'
$bad=@{}+$options;$bad.WebPort=65536;Must-Reject {Assert-InstallOptions $bad} 'invalid website port'
$bad=@{}+$options;$bad.HostName='localhost/other';Must-Reject {Assert-InstallOptions $bad} 'invalid hostname'
$bad=@{}+$options;$bad.InstallPath=[IO.Path]::GetPathRoot($env:windir);Must-Reject {Assert-InstallOptions $bad} 'drive root'
$bad=@{}+$options;$bad.InstallPath=$env:windir;Must-Reject {Assert-InstallOptions $bad} 'Windows directory'
$bad=@{}+$options;$bad.InstallPath='\\server\share\Kodiak';Must-Reject {Assert-InstallOptions $bad} 'UNC destination'
$bad=@{}+$options;$bad.AdminPassword='short';Must-Reject {Assert-InstallOptions $bad} 'short admin password'
$bad=@{}+$options;$bad.InstallPath=Split-Path $root;Must-Reject {Assert-InstallOptions $bad} 'unrelated nonempty directory'
Must-Reject {Assert-InstallerSignature (Join-Path $root 'README.md') 'INVALID'} 'tampered prerequisite'
$servers=@(Get-LocalPostgresServers)
if(-not $servers.Count){throw 'Local PostgreSQL discovery did not provide a fallback.'}
$existing=Get-PostgresInstallState @([pscustomobject]@{Detected=$true;Host='localhost';Port=5432})
if(-not $existing.Installed -or $existing.ShowInstallOptions){throw 'Existing PostgreSQL did not hide the install options.'}
$missing=Get-PostgresInstallState @([pscustomobject]@{Detected=$false;Host='localhost';Port=5432})
if($missing.Installed -or -not $missing.ShowInstallOptions){throw 'Missing PostgreSQL did not show the install options.'}
$mixed=Get-PostgresInstallState @([pscustomobject]@{Detected=$false},[pscustomobject]@{Detected=$true})
if($mixed.ShowInstallOptions){throw 'A fallback entry hid a detected installation.'}
if((Get-PostgresDataPort '') -ne 5432){throw 'Missing data directory did not use the default port.'}
Write-Host "Passed: $count PowerShell scripts parsed, 15 installer validation/discovery checks. IIS, databases and services were not changed."
