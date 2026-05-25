Write-Output "=========================================="
Write-Output "   JUNKYARD MOD MANAGER BUILD & INSTALL   "
Write-Output "=========================================="

$projectDir = "C:\Users\domin\.gemini\antigravity\scratch\JunkyardModManager"

# Compile project in Debug mode (uses PostBuild event to copy to game Mods folder)
Write-Output "Erstelle Mod Manager..."
dotnet build "$projectDir\JunkyardModManager.csproj" -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Output "ERFOLG: Mod Manager wurde erfolgreich kompiliert und installiert!"
} else {
    Write-Output "FEHLER beim Kompilieren. Bitte überprüfe die Fehlermeldungen oben."
    exit $LASTEXITCODE
}
