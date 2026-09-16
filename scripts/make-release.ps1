# Builds the player downloads from the Release build output.
#
#   powershell -ExecutionPolicy Bypass -File scripts\make-release.ps1 -UseInstalledBepInEx
#
# -UseInstalledBepInEx packages the BepInEx already installed in your game folder, so the
# release contains the exact version you have been testing with. That is usually what you want:
# it needs no download and cannot ship a version you have never run.
#
# Or, to package a downloaded BepInEx zip instead:
#   powershell -ExecutionPolicy Bypass -File scripts\make-release.ps1 -BepInExZip "C:\path\BepInEx_x86_5.4.23.3.zip"
#
# Optional: -NvdaClient "C:\path\nvdaControllerClient32.dll"  (or put it in the native folder)
#           -UniversalSpeech "C:\path\UniversalSpeech.dll"    (or put it in the native folder)
#
# Creates in the release folder:
#   AMEAccess-<version>-complete.zip  : everything (BepInEx, mod, speech files, installer). For most players.
#   AMEAccess-<version>-mod-only.zip  : only the mod files, for players who already have BepInEx.
#
# Build the solution in Visual Studio (Release) first.
#
# NOTE: BepInEx must be the win_x86 build. The game is a 32-bit Mono process; the x64 build
# will not load, and BepInEx 6 is for IL2CPP games.

param(
    [string]$BepInExZip = "",
    [string]$NvdaClient = "",
    [string]$UniversalSpeech = "",
    [string]$FromGameFolder = "",
    [switch]$UseInstalledBepInEx
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bin  = Join-Path $root "AMEAccess\bin\Release"
$kit  = Join-Path $root "release-kit"
$dll  = Join-Path $bin "AMEAccess.dll"
if (-not (Test-Path $dll)) { throw "Build the solution in Release first: $dll not found." }

$version = (Get-Item $dll).VersionInfo.ProductVersion
if (-not $version) { $version = "unknown" }
$version = ($version -split '\+')[0]

if ($NvdaClient -eq "") {
    foreach ($n in @("nvdaControllerClient32.dll", "nvdaControllerClient.dll")) {
        $c = Join-Path $root "native\$n"
        if (Test-Path $c) { $NvdaClient = $c; break }
    }
}
if ($UniversalSpeech -eq "") {
    $c = Join-Path $root "native\UniversalSpeech.dll"
    if (Test-Path $c) { $UniversalSpeech = $c }
}

function New-Dir([string]$path) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

# Mod files laid out as they belong in the game folder.
function Add-ModFiles([string]$gameRoot) {
    New-Item -ItemType Directory -Force -Path (Join-Path $gameRoot "BepInEx\plugins") | Out-Null
    Copy-Item $dll (Join-Path $gameRoot "BepInEx\plugins")

    # Speech files sit next to the game's .exe, which is where UniversalSpeech looks for its
    # companion DLLs.
    if ($UniversalSpeech -ne "") { Copy-Item $UniversalSpeech $gameRoot }
    if ($NvdaClient -ne "") {
        # Universal Speech loads this by the exact name with the 32 in it.
        Copy-Item $NvdaClient (Join-Path $gameRoot "nvdaControllerClient32.dll")
    }
}

$release = Join-Path $root "release"
New-Item -ItemType Directory -Force -Path $release | Out-Null

# ---------------------------------------------------------------- mod-only zip
$stageMod = Join-Path $release "stage-mod"
New-Dir $stageMod
Add-ModFiles $stageMod
Copy-Item (Join-Path $root "docs\KEYBOARD.md")       $stageMod
Copy-Item (Join-Path $root "docs\PLAYER-INSTALL.md") $stageMod
$lic = Join-Path $stageMod "AMEAccess-licenses"
New-Item -ItemType Directory -Force -Path $lic | Out-Null
Copy-Item (Join-Path $root "LICENSE")                (Join-Path $lic "AMEAccess-LICENSE.txt")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") $lic
Get-ChildItem (Join-Path $kit "licenses") -Filter *.txt -ErrorAction SilentlyContinue |
    ForEach-Object { Copy-Item $_.FullName $lic }

$modZip = Join-Path $release "AMEAccess-$version-mod-only.zip"
if (Test-Path $modZip) { Remove-Item $modZip -Force }
Compress-Archive -Path (Join-Path $stageMod "*") -DestinationPath $modZip
Write-Host "Wrote $modZip"

# ---------------------------------------------------------------- complete zip
if ($BepInExZip -eq "" -and -not $UseInstalledBepInEx) {
    Write-Host "Neither -UseInstalledBepInEx nor -BepInExZip was given, so only the mod-only zip was built."
    exit 0
}

$stageAll = Join-Path $release "stage-complete"
New-Dir $stageAll
$files = Join-Path $stageAll "files"
New-Item -ItemType Directory -Force -Path $files | Out-Null

if ($UseInstalledBepInEx) {
    # Find the game folder the same way the installer does, unless one was given.
    $game = $FromGameFolder
    if ($game -eq "") {
        $names = @("A Monster's Expedition", "A Monsters Expedition")
        $roots = New-Object System.Collections.Generic.List[string]
        $steam = $null
        try { $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction Stop).SteamPath } catch { }
        if ($steam) {
            $steam = $steam -replace '/', '\'
            $roots.Add($steam)
            $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
            if (Test-Path $vdf) {
                $text = Get-Content -Path $vdf -Raw
                foreach ($m in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                    $roots.Add(($m.Groups[1].Value -replace '\\\\', '\'))
                }
            }
        }
        $roots.Add("C:\Program Files (x86)\Steam")
        $roots.Add("C:\Program Files\Steam")
        foreach ($r in $roots) {
            foreach ($n in $names) {
                $candidate = Join-Path $r "steamapps\common\$n"
                if (Test-Path (Join-Path $candidate "BepInEx\core\BepInEx.dll")) { $game = $candidate; break }
            }
            if ($game -ne "") { break }
        }
    }

    if ($game -eq "" -or -not (Test-Path (Join-Path $game "BepInEx\core\BepInEx.dll"))) {
        throw "Could not find an installed BepInEx. Pass -FromGameFolder ""<your game folder>""."
    }

    Write-Host "Packaging the BepInEx installed in: $game"

    # Only the loader itself. Your config, your logs and any other plugins stay out.
    Copy-Item (Join-Path $game "BepInEx\core") (Join-Path $files "BepInEx\core") -Recurse -Force
    foreach ($f in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "version.dll")) {
        $src = Join-Path $game $f
        if (Test-Path $src) { Copy-Item $src $files }
    }

    $ver = (Get-Item (Join-Path $files "BepInEx\core\BepInEx.dll")).VersionInfo.ProductVersion
    Write-Host "  BepInEx version: $ver"
}
else {
    if (-not (Test-Path $BepInExZip)) { throw "BepInEx zip not found: $BepInExZip" }
    Expand-Archive -Path $BepInExZip -DestinationPath $files -Force
}
if (-not (Test-Path (Join-Path $files "BepInEx\core\BepInEx.dll"))) {
    throw "That does not look like a BepInEx 5 zip: BepInEx\core\BepInEx.dll is missing after extracting."
}
if (-not (Test-Path (Join-Path $files "winhttp.dll"))) {
    Write-Host "WARNING: winhttp.dll is missing from the BepInEx zip. That file is what loads BepInEx; make sure you have the win_x86 build."
}

Add-ModFiles $files
Copy-Item (Join-Path $kit "Install.cmd")      $stageAll
Copy-Item (Join-Path $kit "install.ps1")      $stageAll
Copy-Item (Join-Path $kit "README-FIRST.txt") $stageAll
Copy-Item (Join-Path $root "docs\KEYBOARD.md") $stageAll
Copy-Item (Join-Path $root "CHANGELOG.md")     (Join-Path $files "changelog.txt")

$lic2 = Join-Path $stageAll "licenses"
New-Item -ItemType Directory -Force -Path $lic2 | Out-Null
Copy-Item (Join-Path $root "LICENSE")                (Join-Path $lic2 "AMEAccess-LICENSE.txt")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") $lic2
Get-ChildItem (Join-Path $kit "licenses") -Filter *.txt -ErrorAction SilentlyContinue |
    ForEach-Object { Copy-Item $_.FullName $lic2 }

$allZip = Join-Path $release "AMEAccess-$version-complete.zip"
if (Test-Path $allZip) { Remove-Item $allZip -Force }
Compress-Archive -Path (Join-Path $stageAll "*") -DestinationPath $allZip
Write-Host "Wrote $allZip"
