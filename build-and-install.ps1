param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$ProfileDir,
    [string]$BepInExDir,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$NoInstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Missing .NET SDK. Install .NET SDK 8 or newer.'
}
$game = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
$managed = Join-Path $game 'Stonewards_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "Assembly-CSharp.dll was not found in $managed"
}

$profile = $null
if ($ProfileDir) {
    $profile = (Resolve-Path -LiteralPath $ProfileDir -ErrorAction Stop).Path
}
if ($BepInExDir) {
    $bep = (Resolve-Path -LiteralPath $BepInExDir -ErrorAction Stop).Path
} elseif ($profile) {
    $bep = Join-Path $profile 'BepInEx'
} else {
    $bep = Join-Path $game 'BepInEx'
}

$required = @(
    (Join-Path $managed 'Mirror.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managed 'UnityEngine.UIElementsModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'Unity.InputSystem.dll'),
    (Join-Path (Join-Path $bep 'core') 'BepInEx.dll')
)
foreach ($dependency in $required) {
    if (-not (Test-Path -LiteralPath $dependency -PathType Leaf)) {
        throw "Missing dependency: $dependency"
    }
}

$project = Join-Path $PSScriptRoot 'src\StonewardsDailyQuestTracker\StonewardsDailyQuestTracker.csproj'
Write-Host "Building Daily Quest Tracker ($Configuration)..." -ForegroundColor Cyan
& dotnet build $project --configuration $Configuration "-p:GameDir=$game" "-p:BepInExDir=$bep"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)." }

$dll = Join-Path $PSScriptRoot "src\StonewardsDailyQuestTracker\bin\$Configuration\netstandard2.1\StonewardsDailyQuestTracker.dll"
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "Missing output DLL: $dll" }
Write-Host "Built: $dll" -ForegroundColor Green

if ($NoInstall -or -not $profile) {
    Write-Host 'Build-only mode: no files installed.' -ForegroundColor Yellow
    exit 0
}
$pluginPath = Join-Path $bep 'plugins\StonewardsDailyQuestTracker'
New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null
$destination = Join-Path $pluginPath 'StonewardsDailyQuestTracker.dll'
Copy-Item -LiteralPath $dll -Destination $destination -Force
Write-Host "Installed into test profile: $destination" -ForegroundColor Green
Write-Host 'Run the game via that r2modman profile. F6 = toggle HUD; F9 = quest API report.' -ForegroundColor Cyan
