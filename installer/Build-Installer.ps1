param(
    [string]$HostingBundlePath='',
    [string]$PostgresInstallerPath='',
    [string]$OutputDirectory=(Join-Path $PSScriptRoot 'output'),
    [switch]$SkipTests
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path $PSScriptRoot
$uiSource=Join-Path $repo 'kodiak_container_tracking\ctraking_ui'
$backend=Join-Path $repo 'kodiak_container_tracking\ctrackin_webapi'
$build=Join-Path $PSScriptRoot ('artifacts\'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $build 'payload'
New-Item -ItemType Directory -Path $payload,$OutputDirectory -Force | Out-Null
$ui=Join-Path $build 'ui-build'
New-Item -ItemType Directory $ui | Out-Null
foreach($name in @('src','public','tests','index.html','package.json','package-lock.json','vite.config.js')) {
    $source=Join-Path $uiSource $name
    if(Test-Path $source){Copy-Item -LiteralPath $source -Destination $ui -Recurse -Force}
}
function Invoke-Build([string]$Directory,[scriptblock]$Command) {
    Push-Location $Directory
    try { & $Command;if($LASTEXITCODE -ne 0){throw "Build command failed in $Directory"} }
    finally {Pop-Location}
}
if(-not $SkipTests){
    foreach($test in @('Test-SetupScripts.ps1','Test-IisPrerequisites.ps1','Test-WebsiteAccess.ps1')){
        $testPath=Join-Path $PSScriptRoot ('tests\'+$test)
        Invoke-Build $PSScriptRoot {powershell.exe -NoProfile -ExecutionPolicy Bypass -File $testPath}
    }
    $wizardPath=Join-Path $PSScriptRoot 'scripts\Setup-Wizard.ps1'
    Invoke-Build $PSScriptRoot {powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $wizardPath -SmokeTest}
}
Write-Host 'Building and testing the Vue application...'
# Explicitly force same-origin /api even if this developer has a custom .env.local.
$previousApi=$env:VITE_API_BASE_URL;$env:VITE_API_BASE_URL='/api'
try {
    Invoke-Build $ui {npm.cmd ci}
    if(-not $SkipTests){Invoke-Build $ui {npm.cmd test}}
    Invoke-Build $ui {npm.cmd run build}
} finally {$env:VITE_API_BASE_URL=$previousApi}
if(-not $SkipTests) {Invoke-Build $backend {dotnet test --configuration Release}}
Write-Host 'Publishing a self-contained Windows x64 API...'
$apiOutput=Join-Path $payload 'Api'
Invoke-Build $backend {dotnet publish src/Kodiak.ContainerTracking.Api -c Release -r win-x64 --self-contained true -o $apiOutput -p:DebugType=None}
# Never ship development configuration, logs or uploaded customer data.
foreach($name in @('appsettings.Development.json','appsettings.Production.json')) {
    $path=Join-Path $apiOutput $name
    if(Test-Path $path){Remove-Item -LiteralPath $path}
}
foreach($name in @('App_Data','wwwroot')) {
    $path=Join-Path $apiOutput $name
    if(Test-Path $path) {
        $resolved=[IO.Path]::GetFullPath($path)
        if(-not $resolved.StartsWith([IO.Path]::GetFullPath($apiOutput)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Publish cleanup path escaped its staging directory.'}
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
New-Item -ItemType Directory (Join-Path $payload 'Web') -Force | Out-Null
Copy-Item (Join-Path $ui 'dist\*') (Join-Path $payload 'Web') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'scripts') $payload -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') $payload
$prerequisites=@{}
foreach($entry in @(@{Key='hostingBundle';Path=$HostingBundlePath},@{Key='postgresql';Path=$PostgresInstallerPath})) {
    if($entry.Path) {
        $file=Get-Item -LiteralPath $entry.Path
        $signature=Get-AuthenticodeSignature -LiteralPath $file.FullName
        if($signature.Status -ne 'Valid'){throw "Use an official, validly signed prerequisite installer: $($file.Name)"}
        $dir=Join-Path $payload 'Prerequisites';New-Item -ItemType Directory $dir -Force | Out-Null
        $name=$entry.Key+'.exe';Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $dir $name)
        $prerequisites[$entry.Key]=@{file=('Prerequisites/'+$name);sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;publisher=$signature.SignerCertificate.Subject}
    }
}
$prerequisites | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $payload 'prerequisites.json') -Encoding UTF8
$files=@(Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
    @{path=$_.FullName.Substring($payload.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
@{version='0.2.0';createdUtc=[DateTime]::UtcNow.ToString('o');architecture='win-x64';files=$files} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $payload 'manifest.json') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=Join-Path $build 'payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$launcher=Join-Path $PSScriptRoot 'Launcher'
Write-Host 'Building the single-file setup executable...'
Invoke-Build $launcher {dotnet publish Kodiak.Setup.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory "-p:PayloadPath=$zip"}
$setup=Join-Path $OutputDirectory 'Kodiak-Setup.exe'
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | Format-List
Write-Host "Installer created: $setup"
if($prerequisites.Count -lt 2){Write-Host 'Prerequisite installers were not both provided. This package requires those prerequisites to be installed separately.'}
