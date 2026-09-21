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


# ---------------------------------------------------------------- zip writing
#
# NOT Compress-Archive. Windows PowerShell 5.1's Compress-Archive writes entry paths with
# BACKSLASHES ("BepInEx\plugins\AMEAccess.dll") and no folder entries at all. The zip format
# requires forward slashes, so a viewer that follows it shows that entry as one oddly named file
# at the top level, or hides it - which is how the 1.0.0 and 1.0.1 downloads appeared to contain
# only the speech DLLs. Writing the zip through .NET directly lets us set the names ourselves.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Zip([string]$sourceDir, [string]$zipPath) {
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    $base = (Resolve-Path $sourceDir).Path.TrimEnd('\') + '\'
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        # Folder entries first, so every viewer shows the folder structure.
        Get-ChildItem -Path $sourceDir -Recurse -Directory | ForEach-Object {
            $rel = $_.FullName.Substring($base.Length) -replace '\\', '/'
            [void]$zip.CreateEntry($rel + '/')
        }
        Get-ChildItem -Path $sourceDir -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($base.Length) -replace '\\', '/'
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $zip.Dispose() }
}

# Open a finished zip and prove it is right. Stops the script rather than let a broken download
# be published: every path must use forward slashes, and every file named must be in it.
function Test-Zip([string]$zipPath, [string[]]$mustContain) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName })
    }
    finally { $zip.Dispose() }

    $bad = @($names | Where-Object { $_ -like '*\*' })
    if ($bad.Count -gt 0) { throw "Zip check failed, backslash paths in ${zipPath}: $($bad -join ', ')" }

    foreach ($need in $mustContain) {
        if ($names -notcontains $need) { throw "Zip check failed: $need is missing from $zipPath" }
    }

    Write-Host "  Checked: $($names.Count) entries, all forward-slash paths, required files present."
    foreach ($need in $mustContain) { Write-Host "    contains $need" }
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
New-Zip $stageMod $modZip
Write-Host "Wrote $modZip"
Test-Zip $modZip @("BepInEx/plugins/AMEAccess.dll", "UniversalSpeech.dll")

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
New-Zip $stageAll $allZip
Write-Host "Wrote $allZip"
Test-Zip $allZip @("Install.cmd", "install.ps1", "files/winhttp.dll",
                   "files/BepInEx/core/BepInEx.dll", "files/BepInEx/plugins/AMEAccess.dll",
                   "files/UniversalSpeech.dll")
