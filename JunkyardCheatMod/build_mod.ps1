Write-Host "==========================================" -ForegroundColor Green
Write-Host "     JUNKYARD CHEAT MOD BUILDER           " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

$projectPath = $PSScriptRoot
$assemblyPath = "E:\Games\Steam\steamapps\common\Junkyard Builder\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"

if (-not (Test-Path $assemblyPath)) {
    Write-Host ""
    Write-Host "FEHLER / WARNING:" -ForegroundColor Yellow
    Write-Host "Die Referenzdateien wurden noch nicht generiert." -ForegroundColor Red
    exit
}

Write-Host "Erstelle Mod..." -ForegroundColor Cyan
cd $projectPath
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "ERFOLG: Mod wurde erfolgreich kompiliert und in den Mods-Ordner installiert!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "FEHLER beim Kompilieren." -ForegroundColor Red
}
