param(
    [string]$Dotnet = 'dotnet',
    [string]$Version = '1.2.0',
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$projectRoot = $PSScriptRoot
$appOutput = Join-Path $projectRoot "distribution\app-$Runtime"
$setupOutput = Join-Path $projectRoot "distribution\setup-$Runtime"
$toolsOutput = Join-Path $projectRoot 'tools\publisher'
function Invoke-Dotnet {
    param([string[]]$Arguments)
    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet a échoué avec le code $LASTEXITCODE." }
}
Invoke-Dotnet -Arguments @('run','--project',"$projectRoot\tests\LunarSync.Tests\LunarSync.Tests.csproj",'-c','Release')
Invoke-Dotnet -Arguments @('publish',"$projectRoot\src\LunarSync\LunarSync.csproj",'-c','Release','-r',$Runtime,'--self-contained','true',"-p:Version=$Version",'-p:DebugType=None','-o',$appOutput,'--nologo')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = Join-Path $projectRoot 'distribution\app-package.zip'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package }
[System.IO.Compression.ZipFile]::CreateFromDirectory($appOutput,$package,[System.IO.Compression.CompressionLevel]::Optimal,$false)
Copy-Item -LiteralPath $package -Destination (Join-Path $projectRoot "distribution\LunarSync-$Version-$Runtime.zip") -Force
Invoke-Dotnet -Arguments @('publish',"$projectRoot\src\LunarSync.Installer\LunarSync.Installer.csproj",'-c','Release','-r',$Runtime,'--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',"-p:Version=$Version",'-p:DebugType=None','-o',$setupOutput,'--nologo')
Copy-Item -LiteralPath (Join-Path $setupOutput 'LunarSync-Setup.exe') -Destination (Join-Path $projectRoot "distribution\LunarSync-Setup-$Version-$Runtime.exe") -Force
Invoke-Dotnet -Arguments @('publish',"$projectRoot\src\LunarSync.Release\LunarSync.Release.csproj",'-c','Release','-r',$Runtime,'--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',"-p:Version=$Version",'-p:DebugType=None','-o',$toolsOutput,'--nologo')
Get-FileHash -LiteralPath (Join-Path $projectRoot "distribution\LunarSync-Setup-$Version-$Runtime.exe"),(Join-Path $projectRoot "distribution\LunarSync-$Version-$Runtime.zip") -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $([System.IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath (Join-Path $projectRoot 'distribution\SHA256SUMS.txt')
Write-Output "LunarSync $Version compilé pour $Runtime."
