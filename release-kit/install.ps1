# A Monster's Expedition Access installer: finds the game folder and copies everything from the
# "files" folder into it. Plain console text only, so screen readers read every message.

$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = Join-Path $here "files"

function Wait-Close { Read-Host "Press Enter to close" | Out-Null }

# The folder name contains an apostrophe on Steam and does not on GOG, so both spellings are tried.
$names = @("A Monster's Expedition", "A Monsters Expedition")

function Test-GameFolder([string]$path) {
    if (-not (Test-Path $path)) { return $false }
    return (Get-ChildItem -Path $path -Filter "*_Data" -Directory -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
}

function Find-Game {
    $libraries = New-Object System.Collections.Generic.List[string]

    $steam = $null
    try { $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction Stop).SteamPath } catch { }
    if ($steam) {
        $steam = $steam -replace '/', '\'
        $libraries.Add($steam)
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $text = Get-Content -Path $vdf -Raw
            foreach ($m in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                $libraries.Add(($m.Groups[1].Value -replace '\\\\', '\'))
            }
        }
    }
    $libraries.Add("C:\Program Files (x86)\Steam")
    $libraries.Add("C:\Program Files\Steam")

    foreach ($lib in $libraries) {
        foreach ($n in $names) {
            $game = Join-Path $lib "steamapps\common\$n"
            if (Test-GameFolder $game) { return $game }
        }
    }

    foreach ($root in @("C:\GOG Games", "D:\GOG Games", "C:\Games", "D:\Games")) {
        foreach ($n in $names) {
            $game = Join-Path $root $n
            if (Test-GameFolder $game) { return $game }
        }
    }
    return $null
}

Write-Host "A Monster's Expedition Access installer."

if (-not (Test-Path $files)) {
    Write-Host "The folder named files is missing next to this installer. Extract the whole zip first, then run Install.cmd again."
    Wait-Close; exit 1
}

$running = Get-Process -ErrorAction SilentlyContinue |
           Where-Object { $_.ProcessName -like "*Monster*Expedition*" -or $_.ProcessName -like "AMonsters*" }
if ($running) {
    Write-Host "The game is running. Close it, then run Install.cmd again."
    Wait-Close; exit 1
}

$game = Find-Game
if (-not $game) {
    Write-Host "Could not find the game automatically."
    Write-Host "Open the game folder (the one containing the game's .exe), copy its address, and paste it here."
    $game = (Read-Host "Game folder").Trim('"').Trim()
}

if (-not (Test-GameFolder $game)) {
    Write-Host "That does not look like the game folder: no *_Data folder inside it. Nothing was changed."
    Wait-Close; exit 1
}

Write-Host "Installing into: $game"
try {
    Copy-Item -Path (Join-Path $files "*") -Destination $game -Recurse -Force -ErrorAction Stop
} catch {
    Write-Host "Copying failed: $($_.Exception.Message)"
    Write-Host "If the game is in Program Files, right-click Install.cmd and choose Run as administrator."
    Wait-Close; exit 1
}

Write-Host "Done."
Write-Host "Start the game. You should hear: Accessibility mod ready. Press F1 for the key list."
Write-Host "If you hear nothing, press F12 in the game and send BepInEx\LogOutput.log."
Wait-Close
