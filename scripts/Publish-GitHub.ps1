param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$Dotnet = 'dotnet',
    [string]$Notes,
    [string]$SigningKey = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'LunarSyncPublisher\lunarsync-private.dpapi')
)
$ErrorActionPreference = 'Stop'
$repo = 'lnrzartou/lunarsync'
$project = Split-Path -Parent $PSScriptRoot
function Gh {
    param([string[]]$Arguments)
    $result = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub a refusé une opération. Aucune publication finale effectuée par cette étape.' }
    return $result
}
function Run-Publisher {
    param([string[]]$Arguments)
    & (Join-Path $project 'tools\publisher\LunarSync.Release.exe') @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Vérification ou signature refusée.' }
}
Push-Location $project
try {
    $login = Gh -Arguments @('api','user','--jq','.login')
    if ($login.Trim() -ne 'lnrzartou') { throw 'Connecte GitHub CLI au compte lnrzartou avant de publier.' }
    if (-not (Test-Path -LiteralPath $SigningKey)) { throw 'Clé privée Windows introuvable. Ne crée pas une nouvelle clé pour remplacer celle déjà distribuée.' }
    $dirty = & git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Enregistre les changements avec git commit, puis git push, avant de publier.' }
    $commit = (& git rev-parse HEAD).Trim()
    $remoteCommit = Gh -Arguments @('api',"repos/$repo/commits/main",'--jq','.sha')
    if ($remoteCommit.Trim() -ne $commit) { throw 'Le commit local doit être le commit main publié sur GitHub.' }
    [xml]$props = Get-Content -LiteralPath 'Directory.Build.props' -Raw
    if ($props.Project.PropertyGroup.Version -ne $Version) { throw 'La version demandée doit correspondre à Directory.Build.props.' }
    $sequence = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $channelFile = Join-Path $project 'src\LunarSync\update-channel.json'
    $channel = Get-Content -LiteralPath $channelFile -Raw | ConvertFrom-Json
    if (-not $channel.configured -or $channel.manifestUrl -ne "https://github.com/$repo/releases/latest/download/release.json") { throw 'Canal GitHub inattendu ou non configuré.' }
    & (Join-Path $project 'build.ps1') -Version $Version -Dotnet $Dotnet
    if ($LASTEXITCODE -ne 0) { throw 'La construction a échoué.' }
    $latestFile = Join-Path $project 'distribution\previous-release.json'
    try {
        Invoke-WebRequest -Uri $channel.manifestUrl -OutFile $latestFile -TimeoutSec 45
        # Verify before trusting a previous manifest's sequence or version.
        Run-Publisher -Arguments @('verify-history',$channelFile,$latestFile,'0.0.0')
        $envelope = Get-Content -LiteralPath $latestFile -Raw | ConvertFrom-Json
        $previous = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload)) | ConvertFrom-Json
        if ([version]$Version -le [version]$previous.version) { throw 'Une nouvelle publication doit avoir une version supérieure.' }
        $sequence = [Math]::Max($sequence,[long]$previous.sequence + 1)
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw }
    }
    $zip = Join-Path $project "distribution\LunarSync-$Version-win-x64.zip"
    $setup = Join-Path $project "distribution\LunarSync-Setup-$Version-win-x64.exe"
    $manifest = Join-Path $project 'distribution\release.json'
    $checksums = Join-Path $project 'distribution\SHA256SUMS.txt'
    if (-not $Notes) { $Notes = Join-Path $project "docs\releases\$Version.md" }
    if (-not (Test-Path -LiteralPath $Notes)) { throw 'Ajoute les notes de version avant la publication.' }
    $packageUrl = "https://github.com/$repo/releases/download/v$Version/LunarSync-$Version-win-x64.zip"
    Run-Publisher -Arguments @('sign-windows',$SigningKey,$zip,$Version,"$sequence",$packageUrl,$manifest,$Notes)
    Run-Publisher -Arguments @('verify',$channelFile,$manifest,$Version,$zip)
    $tracked = & git ls-files
    if ($tracked | Where-Object { $_ -match '(?i)(\.dpapi$|-private\.pem$|\.pfx$|\.p12$|\.key$|(^|/)\.env($|\.))' }) { throw 'Un fichier privé apparaît parmi les fichiers suivis : publication interrompue.' }
    Get-FileHash -LiteralPath $zip,$setup,$manifest -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath $checksums
    Gh -Arguments @('release','create',"v$Version",'--repo',$repo,'--target',$commit,'--title',"LunarSync $Version",'--notes-file',$Notes,'--draft')
    Gh -Arguments @('release','upload',"v$Version",$zip,$setup,$manifest,$checksums,'--repo',$repo)
    Gh -Arguments @('release','edit',"v$Version",'--repo',$repo,'--draft=false','--latest=true')
    Write-Output "Publication disponible : https://github.com/$repo/releases/tag/v$Version"
} finally { Pop-Location }
