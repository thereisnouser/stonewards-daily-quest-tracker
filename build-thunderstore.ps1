param(
    [string]$GameDir,
    [string]$ProfileDir = (Join-Path $env:APPDATA 'r2modmanPlus-local\Stonewards\profiles\stonewards-daily-quest-tracker'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$manifestPath = Join-Path $PSScriptRoot 'thunderstore\manifest.json'
$readmePath = Join-Path $PSScriptRoot 'thunderstore\README.md'
$changelogPath = Join-Path $PSScriptRoot 'thunderstore\CHANGELOG.md'
$iconPath = Join-Path $PSScriptRoot 'thunderstore\icon.png'
$pluginSource = Join-Path $PSScriptRoot 'src\StonewardsDailyQuestTracker\DailyQuestTrackerPlugin.cs'
$dll = Join-Path $PSScriptRoot 'src\StonewardsDailyQuestTracker\bin\Release\netstandard2.1\StonewardsDailyQuestTracker.dll'

foreach ($file in @($manifestPath, $readmePath, $changelogPath, $iconPath, $pluginSource)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required release file missing: $file. Add the release artwork if needed."
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.name -ne 'DailyQuestTracker') { throw 'Unexpected Thunderstore package name.' }
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Manifest version_number must be Major.Minor.Patch.'
}
$source = Get-Content -LiteralPath $pluginSource -Raw
$pluginVersionMatch = [regex]::Match($source, 'public const string Version\s*=\s*"([^"]+)"')
if (-not $pluginVersionMatch.Success -or
    $pluginVersionMatch.Groups[1].Value -ne $manifest.version_number) {
    throw "Plugin source version does not match manifest version $($manifest.version_number)."
}

# Thunderstore requires an exactly 256x256 PNG. The PNG IHDR stores the
# dimensions as big-endian 32-bit integers at offsets 16 and 20.
$iconBytes = [System.IO.File]::ReadAllBytes($iconPath)
$pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
if ($iconBytes.Length -lt 24) { throw 'icon.png is not a valid PNG.' }
for ($i = 0; $i -lt 8; $i++) {
    if ($iconBytes[$i] -ne $pngSignature[$i]) { throw 'icon.png has invalid PNG signature.' }
}
$width = ([int]$iconBytes[16] -shl 24) -bor ([int]$iconBytes[17] -shl 16) -bor
         ([int]$iconBytes[18] -shl 8) -bor [int]$iconBytes[19]
$height = ([int]$iconBytes[20] -shl 24) -bor ([int]$iconBytes[21] -shl 16) -bor
          ([int]$iconBytes[22] -shl 8) -bor [int]$iconBytes[23]
if ($width -ne 256 -or $height -ne 256) {
    throw "Thunderstore icon must be 256x256 PNG; got $width x $height."
}

if (-not $SkipBuild) {
    if ([string]::IsNullOrWhiteSpace($GameDir)) {
        throw '-GameDir is required unless -SkipBuild is supplied.'
    }
    if (-not (Test-Path -LiteralPath $ProfileDir -PathType Container)) {
        throw "r2modman profile was not found: $ProfileDir"
    }
    Write-Host 'Building mod for Thunderstore (without installing into the running game)...' -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build-and-install.ps1') `
        -GameDir $GameDir -ProfileDir $ProfileDir -Configuration Release -NoInstall
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
    throw "Compiled DLL not found: $dll. Build the mod before packaging."
}
if ((Get-Item -LiteralPath $dll).Length -lt 4096) {
    throw 'Compiled DLL is unexpectedly small; refusing to build release archive.'
}

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$name = "thereisnouser-DailyQuestTracker-$($manifest.version_number).zip"
$zip = Join-Path $dist $name
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("StonewardsDQT-" + [guid]::NewGuid().ToString('N'))

try {
    $pluginDir = Join-Path $stage 'plugins'
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stage 'manifest.json')
    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $stage 'README.md')
    Copy-Item -LiteralPath $changelogPath -Destination (Join-Path $stage 'CHANGELOG.md')
    Copy-Item -LiteralPath $iconPath -Destination (Join-Path $stage 'icon.png')
    Copy-Item -LiteralPath $dll -Destination (Join-Path $pluginDir 'StonewardsDailyQuestTracker.dll')
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

    # Verify expected entries. Avoid accidentally releasing a ZIP containing
    # source code, references to game DLLs, or local profile config.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
        foreach ($required in @('manifest.json', 'README.md', 'CHANGELOG.md',
                                 'icon.png', 'plugins/StonewardsDailyQuestTracker.dll')) {
            if ($names -notcontains $required) { throw "ZIP missing expected entry: $required" }
        }
    } finally {
        $archive.Dispose()
    }
    Write-Host "Thunderstore ZIP ready: $zip" -ForegroundColor Green
    Write-Host "Version: $($manifest.version_number) | Icon: 256x256 | Dependency: $($manifest.dependencies -join ', ')" -ForegroundColor Cyan
    Write-Host 'The package was NOT uploaded. Review it before publishing.' -ForegroundColor Yellow
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
