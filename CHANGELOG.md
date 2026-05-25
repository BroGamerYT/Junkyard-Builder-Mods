# 📝 Changelog - Junkyard Builder Mods Suite

Alle Änderungen, Updates und Releases der Mods-Suite für **Junkyard Builder**.

---

## 🚀 [1.0.0] - 2026-05-26 - JunkyardSmartPressMod (NEU)
Die ultimative, eigenständige Optimierung für Presse, Schredder und Ballenpressen!

### Hinzugefügt
- **Eigenständige Mod:** Komplett neue Mod zur Stabilisierung und Automatisierung aller Einzugs- und Pressvorgänge im Spiel.
- **3D-Physik-Scan (OverlapSphere):** Ersetzt die fehlerhafte, triggerbasierte Standard-Logik des Spiels durch einen permanenten, hochpräzisen physikalischen Scan. Items werden im Einzugsbereich immer erkannt!
- **Auto-Suck-Logik:** Erkennt herumliegenden Schrott über dem Trichter vollautomatisch und zieht ihn ein.
- **Player-Handschutz:** Verhindert das automatische Einziehen von Items, die der Spieler gerade in der Hand hält (vollständige Rigidbody- und Parent-Verifikation).
- **Trichter-Daueraktivierung:** Hält das grüne Licht-Feld (Einzugsbereich) der Maschinen unter allen Umständen aktiv.
- **Smart-Weight-Buffering:** Verhindert das Verschwinden von überschüssigem Schrott, wenn das Maschinengewicht voll ist. Überschüsse werden im Speicher gepuffert und beim nächsten Block automatisch verrechnet.
- **Sofortige Kompression:** Option für sekundenschnelles Pressen (`InstantCompress`).
- **Eigene Konfiguration:** Einstellungen sind über die `UserData\JunkyardSmartPressMod.txt` anpassbar.

---

## 🔧 [1.3.1] - 2026-05-26 - JunkyardResourceMod
Kritischer Hotfix für die Auszahlungen von Kundenaufträgen und standardmäßige Anpassungen.

### Behoben
- **Auftrags-Overflow gelöst:** Ein Fehler im Speicher-Tracking führte dazu, dass Auftrags-Auszahlungen bei jedem Öffnen des Menüs multipliziert wurden und so auf `-2147483648` (Zahlenüberlauf) sanken. Das wird jetzt durch eindeutiges Tracking über die `ContractID` verhindert.
- **Savegame-Selbstheilung:** Lädt das Spiel einen bereits korrupten/negativen Betrag aus einem Savegame, wird dieser nun **vollautomatisch erkannt und zu den originalen Standard-Beträgen geheilt**.
- **Vanilla-Standardwerte:** Die Standard-Multiplikatoren (`Multiplier` und `ContractMultiplier`) wurden standardmäßig auf **`1.0`** (Originalspiel) gesetzt, um ein komplett natürliches Spielerlebnis ohne ungewollte Multiplikatoren zu bieten.

---

## 🧹 [1.1.1] - 2026-05-26 - JunkyardCheatMod
Code-Bereinigung und Optimierung.

### Geändert
- **Code-Bereinigung:** Redundante und instabile Harmony-Patches für die Schrott-Presse wurden aus der Cheat-Mod entfernt, da diese nun vollständig und stabiler in der eigenständigen `JunkyardSmartPressMod` integriert sind.
- **Stabilität:** Bessere Performance und Kompatibilität mit anderen Mods durch die Entlastung des Harmony-Systems.

---

## 🚀 Ältere Releases

### [1.0.0] - JunkyardModManager, JunkyardSpeedMod, JunkyardWeightMod
- Initiale Veröffentlichung der Suite.
- Mod Manager mit In-Game-UI (F1) und automatischen Updates von GitHub.
- Speed Mod (Num+/Num-/Num*) zur Steuerung der Spielgeschwindigkeit.
- Weight Mod zur Verdopplung von Tragekapazitäten und Schubkarrenlimits.
