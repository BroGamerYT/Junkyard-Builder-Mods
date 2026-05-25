using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using System.IO;
using System.Globalization;
using Il2CppScripts.Managers;
using Il2CppScripts.Interactables;
using Il2CppScripts.Player;
using Il2Cpp;

[assembly: MelonInfo(typeof(JunkyardCheatMod.Main), "Junkyard Cheat Box Mod", "1.0.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardCheatMod
{
    public static class Config
    {
        public static KeyCode ToggleKey = KeyCode.F2;
        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardCheatMod.txt");

        public static void Load()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    SaveDefaultConfig();
                    return;
                }

                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    string[] parts = trimmed.Split(new char[] { '=' }, 2);
                    if (parts.Length < 2)
                        continue;

                    string key = parts[0].Trim().ToLower();
                    string val = parts[1].Trim();

                    if (key == "togglekey")
                    {
                        if (System.Enum.TryParse<KeyCode>(val, true, out KeyCode parsedKey))
                        {
                            ToggleKey = parsedKey;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Laden der CheatMod Config: " + ex.Message);
            }
        }

        private static void SaveDefaultConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (StreamWriter writer = new StreamWriter(configPath))
                {
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("# JUNKYARD CHEAT BOX MOD - CONFIGURATION FILE");
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("#");
                    writer.WriteLine("# Tastaturtaste zum Oeffnen/Schliessen des Cheat-Menues (z.B. F2, F3, Insert, Tab)");
                    writer.WriteLine("ToggleKey=F2");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Erstellen der CheatMod Standard-Config: " + ex.Message);
            }
        }
    }

    public class Main : MelonMod
    {
        private bool showMenu = false;
        private Rect windowRect = new Rect(100, 100, 360, 520);

        // Cheat Toggles & Sliders
        private bool instantCrush = false;
        private bool instantDelivery = false;
        
        // Laufgeschwindigkeit Variables
        private float runSpeedMultiplier = 1.0f;
        private float originalMovementSpeed = -1.0f;

        // LKW Timer Variables (Slider-basiert für 100% IL2CPP Unstripping-Kompatibilität)
        private float truckTimerMinutes = 30.0f;

        // GUI Styles
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toggleStyle;
        private GUIStyle textStyle;
        private GUIStyle watermarkStyle;

        public override void OnInitializeMelon()
        {
            Config.Load();

            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("   JUNKYARD CHEAT BOX MOD INITIALIZED!           ");
            LoggerInstance.Msg($"   💡 Hotkey: Drücke [{Config.ToggleKey}] zum Öffnen/Schließen");
            LoggerInstance.Msg("==================================================");
        }

        public override void OnUpdate()
        {
            // Hotkey zum Umschalten des Menüs aus Config geladen
            if (Input.GetKeyDown(Config.ToggleKey))
            {
                showMenu = !showMenu;
                LoggerInstance.Msg(showMenu ? "Cheat-Menü geöffnet" : "Cheat-Menü geschlossen");

                if (showMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Wenn das Menü offen ist, schaltet die rechte Maustaste (Mouse 1) den Cursor um
            if (showMenu && Input.GetMouseButtonDown(1))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // 1. Sofort-Zerkleinern (Baler & Shredders & Car Crusher)
            if (instantCrush)
            {
                try
                {
                    // Paper Balers
                    var paperBalers = UnityEngine.Object.FindObjectsOfType<PaperBaler>();
                    foreach (var baler in paperBalers)
                    {
                        baler.SetCompressionDuration(0);
                    }

                    // Plastic Balers
                    var plasticBalers = UnityEngine.Object.FindObjectsOfType<PlasticBaler>();
                    foreach (var baler in plasticBalers)
                    {
                        baler.SetCompressionDuration(0);
                    }

                    // Wood Shredders
                    var woodShredders = UnityEngine.Object.FindObjectsOfType<WoodShredder>();
                    foreach (var shredder in woodShredders)
                    {
                        shredder.SetCompressionDuration(0);
                    }

                    // Car Crushers
                    var carCrushers = UnityEngine.Object.FindObjectsOfType<CarCrusher>();
                    foreach (var crusher in carCrushers)
                    {
                        crusher.throwOutDuration = 0.1f;
                        if (crusher.carToCrush != null)
                        {
                            crusher.OnDoorsClosed();
                            crusher.OnCrushingComplete();
                        }
                    }
                }
                catch { }
            }

            // 2. Schnelle Lieferung (LKW) - Setzt den Timer einmalig auf 5 Sekunden zurück, falls er aktiv ist und zu hoch läuft
            if (instantDelivery)
            {
                try
                {
                    var truckMgr = UnityEngine.Object.FindObjectOfType<TruckManager>();
                    if (truckMgr != null && truckMgr.secondsPerSpawn > 5)
                    {
                        truckMgr.secondsPerSpawn = 5;
                        truckMgr.SetTimePerDelivery(5);
                    }
                }
                catch { }
            }

            // 3. Laufgeschwindigkeit anwenden
            try
            {
                var playerMove = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                if (playerMove != null)
                {
                    if (originalMovementSpeed < 0)
                    {
                        originalMovementSpeed = playerMove.movementSpeed;
                    }
                    playerMove.movementSpeed = originalMovementSpeed * runSpeedMultiplier;
                }
            }
            catch { }
        }

        public override void OnGUI()
        {
            // Absolut crash-sicher: Abbrechen wenn GUI Skin noch nicht bereit ist
            if (GUI.skin == null) return;

            // Grünes Wasserzeichen unten links
            if (watermarkStyle == null)
            {
                watermarkStyle = new GUIStyle(GUI.skin.label);
                watermarkStyle.fontSize = 12;
                watermarkStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f, 0.75f); // Orange-Gold
            }

            float wx = 15;
            float wy = Screen.height - 85;
            GUI.Label(new Rect(wx, wy, 500, 18), $"🟢 Cheat Box Mod: {Config.ToggleKey} drücken", watermarkStyle);

            if (!showMenu) return;

            InitStyles();

            // Erzwinge die perfekte Breite & Höhe vor dem Zeichnen, um Auto-Shrink unter IL2CPP zu verhindern
            windowRect.width = 360;
            windowRect.height = 520;

            // Wir zeichnen das GUI.Window OHNE einen fehlerhaften fünften Parameter!
            windowRect = GUI.Window(999, windowRect, (GUI.WindowFunction)DrawWindowContents, "");
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;
            if (GUI.skin == null) return;

            // Header-Stil (Vererbt von label für fehlerfreie Font-Zuordnung)
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 16;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f, 1f);

            // Sektionen-Stil
            sectionStyle = new GUIStyle(GUI.skin.label);
            sectionStyle.fontSize = 13;
            sectionStyle.fontStyle = FontStyle.Bold;
            sectionStyle.normal.textColor = new Color(0.3f, 0.75f, 1f, 1f);
            sectionStyle.margin = new RectOffset(0, 0, 8, 3);

            // Button-Stil (Vererbt von button für klickbare Rahmen und Fonts)
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(0.3f, 0.75f, 1f, 1f);
            buttonStyle.fontSize = 12;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            // Toggle-Stil
            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.fontSize = 12;
            toggleStyle.normal.textColor = Color.white;
            toggleStyle.margin = new RectOffset(0, 0, 4, 4);

            // Text-Stil
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 11;
            textStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            textStyle.margin = new RectOffset(0, 0, 1, 1);
        }

        private void DrawWindowContents(int windowID)
        {
            // Absolut crash-sicherer Hintergrund
            for (int i = 0; i < 6; i++)
            {
                GUI.Box(new Rect(0, 0, 360, 520), "");
            }

            // Wir stellen sicher, dass alle Styles initialisiert sind
            InitStyles();

            // Layout-Sicherheit: GUILayout.EndVertical wird IMMER im finally-Block aufgerufen!
            // Wir erzwingen explizit die Breite (330px) und Höhe (490px) für das Haupt-Layout, um Schrumpfen zu verhindern!
            GUILayout.BeginVertical(GUILayout.Width(330), GUILayout.Height(490));
            try
            {
                if (headerStyle == null)
                {
                    GUILayout.Label("Warte auf GUI Initialisierung...", GUI.skin.label);
                    return;
                }

                GUILayout.Space(10);
                GUILayout.Label("⭐ JUNKYARD CHEAT BOX ⭐", headerStyle);
                GUILayout.Space(5);

                // --- SEKTION 1: GELD CHEATS ---
                GUILayout.Label("💰 GELD CHEATS", sectionStyle);
                
                string cashText = "Nicht geladen";
                try
                {
                    var cashMgr = UnityEngine.Object.FindObjectOfType<CashManager>();
                    if (cashMgr != null)
                    {
                        cashText = $"{cashMgr.CurrentCash.ToString("N0", CultureInfo.InvariantCulture)} $";
                    }
                }
                catch { }
                GUILayout.Label($"Guthaben: {cashText}", textStyle);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+10k $", buttonStyle, GUILayout.Height(22))) AddMoney(10000);
                if (GUILayout.Button("+100k $", buttonStyle, GUILayout.Height(22))) AddMoney(100000);
                if (GUILayout.Button("+1M $", buttonStyle, GUILayout.Height(22))) AddMoney(1000000);
                GUILayout.EndHorizontal();

                // --- SEKTION 2: XP CHEATS ---
                GUILayout.Label("⭐ LEVEL & ERFAHRUNG", sectionStyle);
                
                string levelText = "Nicht geladen";
                try
                {
                    var levelMgr = UnityEngine.Object.FindObjectOfType<PlayerLevelManager>();
                    if (levelMgr != null)
                    {
                        levelText = $"Level {levelMgr.GetCurrentLevel()} (XP: {levelMgr.GetExperience((EExpSource)0).ToString("N0")})";
                    }
                }
                catch { }
                GUILayout.Label($"Rang: {levelText}", textStyle);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+1.000 XP", buttonStyle, GUILayout.Height(22))) AddXP(1000);
                if (GUILayout.Button("+10.000 XP", buttonStyle, GUILayout.Height(22))) AddXP(10000);
                if (GUILayout.Button("+50.000 XP", buttonStyle, GUILayout.Height(22))) AddXP(50000);
                GUILayout.EndHorizontal();

                // --- SEKTION 3: MASCHINEN CHEATS ---
                GUILayout.Label("⚙️ MASCHINEN (SCHNELL-CRUSH)", sectionStyle);
                instantCrush = GUILayout.Toggle(instantCrush, "  Sofort-Zerkleinern (Presse / Schredder)", toggleStyle);

                // --- SEKTION 4: LAUFGESCHWINDIGKEIT ---
                GUILayout.Label($"🏃‍♂️ LAUFGESCHWINDIGKEIT ({runSpeedMultiplier.ToString("F1")}x)", sectionStyle);
                runSpeedMultiplier = GUILayout.HorizontalSlider(runSpeedMultiplier, 1.0f, 10.0f);
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Normal (1x)", buttonStyle, GUILayout.Height(22))) runSpeedMultiplier = 1.0f;
                if (GUILayout.Button("Schnell (3x)", buttonStyle, GUILayout.Height(22))) runSpeedMultiplier = 3.0f;
                if (GUILayout.Button("Turbo (6x)", buttonStyle, GUILayout.Height(22))) runSpeedMultiplier = 6.0f;
                if (GUILayout.Button("Flash (10x)", buttonStyle, GUILayout.Height(22))) runSpeedMultiplier = 10.0f;
                GUILayout.EndHorizontal();

                // --- SEKTION 5: LKW EINSTELLUNGEN ---
                GUILayout.Label("🚚 LIEFERUNGS-TIMER", sectionStyle);
                instantDelivery = GUILayout.Toggle(instantDelivery, "  Schnelle LKW-Lieferung (5 Sek.)", toggleStyle);

                // Slider-basierter Lieferzeit-Wähler (Minimum 0.25 Minuten = 15s um Deadlocks zu verhindern!)
                GUILayout.Label($"Einstellzeit: {truckTimerMinutes.ToString("F1")} Min.", textStyle);
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-0.5m", buttonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                {
                    truckTimerMinutes = Mathf.Max(0.25f, truckTimerMinutes - 0.5f);
                    ApplyCustomTruckTimer(truckTimerMinutes);
                }
                
                truckTimerMinutes = GUILayout.HorizontalSlider(truckTimerMinutes, 0.25f, 60.0f);
                
                if (GUILayout.Button("+0.5m", buttonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                {
                    truckTimerMinutes = Mathf.Min(60.0f, truckTimerMinutes + 0.5f);
                    ApplyCustomTruckTimer(truckTimerMinutes);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Sofort rufen", buttonStyle, GUILayout.Height(22))) { CallTruckNow(); }
                if (GUILayout.Button("5 Min", buttonStyle, GUILayout.Height(22))) { truckTimerMinutes = 5.0f; ApplyCustomTruckTimer(5.0f); }
                if (GUILayout.Button("10 Min", buttonStyle, GUILayout.Height(22))) { truckTimerMinutes = 10.0f; ApplyCustomTruckTimer(10.0f); }
                if (GUILayout.Button("30 Min", buttonStyle, GUILayout.Height(22))) { truckTimerMinutes = 30.0f; ApplyCustomTruckTimer(30.0f); }
                GUILayout.EndHorizontal();

                GUILayout.Space(3);
                if (GUILayout.Button("🚚 Nächsten LKW SOFORT rufen", buttonStyle, GUILayout.Height(28)))
                {
                    CallTruckNow();
                }

                GUILayout.Space(12);

                // Schließen Button
                if (GUILayout.Button("Schließen", buttonStyle, GUILayout.Height(26)))
                {
                    showMenu = false;
                }
            }
            catch (System.Exception ex)
            {
                GUILayout.Label("GUI Render-Fehler:", GUI.skin.label);
                GUILayout.Label(ex.ToString(), GUI.skin.label);
            }
            finally
            {
                GUILayout.EndVertical();
            }

            // Dragging
            GUI.DragWindow();
        }

        private void AddMoney(int amount)
        {
            try
            {
                var cashMgr = UnityEngine.Object.FindObjectOfType<CashManager>();
                if (cashMgr != null)
                {
                    cashMgr.CurrentCash += amount;
                    LoggerInstance.Msg($"[Cheat Box] {amount} $ hinzugefügt!");
                }
            }
            catch { }
        }

        private void AddXP(int amount)
        {
            try
            {
                var levelMgr = UnityEngine.Object.FindObjectOfType<PlayerLevelManager>();
                if (levelMgr != null)
                {
                    levelMgr.AddExperience(amount);
                    LoggerInstance.Msg($"[Cheat Box] {amount} XP hinzugefügt!");
                }
            }
            catch { }
        }

        private void ApplyCustomTruckTimer(float minutes)
        {
            try
            {
                int seconds = Mathf.Max(15, (int)(minutes * 60f));
                var truckMgr = UnityEngine.Object.FindObjectOfType<TruckManager>();
                if (truckMgr != null)
                {
                    truckMgr.secondsPerSpawn = seconds;
                    truckMgr.SetTimePerDelivery(seconds);
                    LoggerInstance.Msg($"[Cheat Box] LKW-Lieferzeit auf {minutes} Minuten ({seconds} Sek.) eingestellt!");
                }
            }
            catch { }
        }

        private void CallTruckNow()
        {
            try
            {
                var truckMgr = UnityEngine.Object.FindObjectOfType<TruckManager>();
                if (truckMgr != null)
                {
                    // Wir rufen die Bestellung zuerst auf, damit alle Standardwerte im Spawner initiiert sind
                    truckMgr.OrderTrash();
                    
                    // Erst DANACH setzen wir die verbleibende Lieferzeit auf 5 Sekunden!
                    // Das überschreibt den Standardwert perfekt und verhindert Deadlocks!
                    truckMgr.SetTimePerDelivery(5);
                    
                    LoggerInstance.Msg("[Cheat Box] LKW-Lieferung erfolgreich mit 5s Puffer gestartet!");
                }
            }
            catch { }
        }
    }
}
