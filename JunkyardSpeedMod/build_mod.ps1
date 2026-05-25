Write-Host "==========================================" -ForegroundColor Green
Write-Host "     JUNKYARD SPEED MOD BUILDER           " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

$projectPath = "C:\Users\domin\.gemini\antigravity\scratch\JunkyardSpeedMod"
$assemblyPath = "E:\Games\Steam\steamapps\common\Junkyard Builder\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"

if (-not (Test-Path $assemblyPath)) {
    Write-Host ""
    Write-Host "FEHLER / WARNING:" -ForegroundColor Yellow
    Write-Host "Die Spieldateien wurden noch nicht von MelonLoader unhollowed." -ForegroundColor Red
    Write-Host "Bitte befolge diese zwei Schritte zuerst:" -ForegroundColor Cyan
    Write-Host "1. Installiere MelonLoader (Version 0.6.5+) auf 'Junkyard Builder.exe'."
    Write-Host "2. Starte das Spiel einmal über Steam, um die Referenzdateien zu generieren."
    Write-Host ""
    Write-Host "Sobald das Spiel einmal gestartet wurde, starte dieses Build-Skript erneut!" -ForegroundColor Green
    exit
}

Write-Host "Erstelle Mod..." -ForegroundColor Cyan
cd $projectPath
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "ERFOLG: Mod wurde erfolgreich kompiliert und in den Mods-Ordner installiert!" -ForegroundColor Green
    Write-Host "Pfad: E:\Games\Steam\steamapps\common\Junkyard Builder\Mods\JunkyardSpeedMod.dll" -ForegroundColor Gray
    Write-Host "Du kannst das Spiel jetzt starten und loslegen!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "FEHLER beim Kompilieren. Bitte überprüfe die Fehlermeldungen oben." -ForegroundColor Red
}
