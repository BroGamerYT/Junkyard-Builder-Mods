# ========================================================================
# JUNKYARD BUILDER MODS SUITE - UNIFIED BUILD & PACKAGE SCRIPT
# ========================================================================
$ErrorActionPreference = "Stop"

Write-Host "=== 1. COMPILING ALL MODS IN RELEASE MODE ===" -ForegroundColor Cyan
dotnet build JunkyardModManager/JunkyardModManager.csproj --configuration Release
dotnet build JunkyardCheatMod/JunkyardCheatMod.csproj --configuration Release
dotnet build JunkyardResourceMod/JunkyardResourceMod.csproj --configuration Release
dotnet build JunkyardSpeedMod/JunkyardSpeedMod.csproj --configuration Release

Write-Host "=== 2. PACKAGING ALL MODS INTO ZIP ARCHIVES ===" -ForegroundColor Cyan
function Package-Mod($name, $version, $dllPath, $desc) {
    $dir = "C:\Users\domin\.gemini\antigravity\scratch\${name}_ReleaseTemp"
    # If running on GitHub Actions, use local workspace path instead
    if ($env:GITHUB_WORKSPACE) {
        $dir = "$env:GITHUB_WORKSPACE\${name}_ReleaseTemp"
    }
    
    if (Test-Path $dir) {
        Remove-Item -Path $dir -Recurse -Force | Out-Null
    }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Copy-Item $dllPath "$dir\${name}.dll" -Force
    
    $readme = "=======================================================`r`n" +
              "JUNKYARD BUILDER $name - v$version`r`n" +
              "=======================================================`r`n" +
              "Author: BroGamerYT`r`n`r`n" +
              "$desc`r`n`r`n" +
              "INSTALLATION (DEUTSCH):`r`n" +
              "1. Kopiere die $name.dll in deinen 'Junkyard Builder/Mods' Ordner.`r`n" +
              "2. Starte das Spiel.`r`n`r`n" +
              "INSTALLATION (ENGLISH):`r`n" +
              "1. Copy $name.dll into your 'Junkyard Builder/Mods' folder.`r`n" +
              "2. Launch the game.`r`n"
              
    New-Item -ItemType File -Path "$dir\README.txt" -Value $readme -Force | Out-Null
    
    $zipPath = "C:\Users\domin\.gemini\antigravity\scratch\${name}_v${version}.zip"
    if ($env:GITHUB_WORKSPACE) {
        $zipPath = "$env:GITHUB_WORKSPACE\${name}_v${version}.zip"
    }
    
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force | Out-Null
    }
    Compress-Archive -Path "$dir\*" -DestinationPath $zipPath -Force
    Remove-Item -Path $dir -Recurse -Force | Out-Null
    Write-Host "Successfully packaged: ${name}_v${version}.zip" -ForegroundColor Green
}

Package-Mod -name 'JunkyardModManager' -version '1.0.0' -dllPath 'JunkyardModManager/bin/Release/JunkyardModManager.dll' -desc 'The ultimate in-game Mod Manager and automatic updater for Junkyard Builder! Manage and update all your mods in-game via F1.'
Package-Mod -name 'JunkyardCheatMod' -version '1.0.0' -dllPath 'JunkyardCheatMod/bin/Release/JunkyardCheatMod.dll' -desc 'The ultimate in-game Cheat Box for Junkyard Builder! Geld, XP, Laufgeschwindigkeit, Lieferzeit-Slider und Sofort-Zerkleinern via F2.'
Package-Mod -name 'JunkyardResourceMod' -version '1.3.0' -dllPath 'JunkyardResourceMod/bin/Release/JunkyardResourceMod.dll' -desc 'Configure scrap delivery amounts, yard capacities, contract payouts and unlock all upgrades instantly in Junkyard Builder.'
Package-Mod -name 'JunkyardSpeedMod' -version '1.0.0' -dllPath 'JunkyardSpeedMod/bin/Release/JunkyardSpeedMod.dll' -desc 'Control the entire game speed in Junkyard Builder using customizable hotkeys (Num+, Num-, Num*).'

Write-Host "=== BUILD & PACKAGING COMPLETE! ===" -ForegroundColor Green
