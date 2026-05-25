using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

[assembly: MelonInfo(typeof(JunkyardModManager.Main), "Junkyard Mod Manager", "1.0.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardModManager
{
    public static class Config
    {
        public static KeyCode ToggleKey = KeyCode.F1;
        public static string UpdateURL = "https://raw.githubusercontent.com/BroGamerYT/Junkyard-Builder-Mods/main/versions.txt";
        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardModManager.txt");

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
                    else if (key == "updateurl")
                    {
                        UpdateURL = val;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Laden der ModManager Config: " + ex.Message);
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
                    writer.WriteLine("# JUNKYARD MOD MANAGER - CONFIGURATION FILE");
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("#");
                    writer.WriteLine("# Tastaturtaste zum Oeffnen/Schliessen des Mod-Managers (z.B. F1, F3, Tab, Insert)");
                    writer.WriteLine("ToggleKey=F1");
                    writer.WriteLine("#");
                    writer.WriteLine("# URL fuer den Mod-Updater (Pfad zu einer versions.txt)");
                    writer.WriteLine("UpdateURL=https://raw.githubusercontent.com/BroGamerYT/Junkyard-Builder-Mods/main/versions.txt");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Erstellen der ModManager Standard-Config: " + ex.Message);
            }
        }
    }

    public class Main : MelonMod
    {
        private bool showMenu = false;
        private Rect windowRect;
        private Vector2 modsScrollPos = Vector2.zero;
        private Vector2 configScrollPos = Vector2.zero;

        // Mod-Listen Verwaltung
        private List<ModFile> modFiles = new List<ModFile>();
        private ModFile selectedMod = null;

        // Config-Listen Verwaltung
        private List<ConfigLine> selectedConfigLines = new List<ConfigLine>();
        private string configFilePath = "";

        // Status-Meldungen
        private string statusMessage = "";
        private float statusTimer = 0f;
        private bool restartNeeded = false;

        // Updater-Verwaltung
        private string updateStatus = "";
        private class RemoteModUpdate
        {
            public string Version;
            public string DownloadUrl;
            public bool UpdateAvailable;
            public bool IsUpdating;
            public string Status = "";
        }
        private System.Collections.Generic.Dictionary<string, RemoteModUpdate> remoteUpdates = new System.Collections.Generic.Dictionary<string, RemoteModUpdate>();

        // GUI Styles
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle modActiveStyle;
        private GUIStyle modDisabledStyle;
        private GUIStyle commentStyle;
        private GUIStyle watermarkStyle;

        public class ModFile
        {
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public bool IsActive { get; set; }
            public string DisplayName => Path.GetFileNameWithoutExtension(FileName).Replace(".disabled", "");
        }

        public class ConfigLine
        {
            public bool IsComment { get; set; }
            public string Content { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
        }

        public override void OnInitializeMelon()
        {
            Config.Load();
            windowRect = new Rect(0, 0, 0, 0);
            RefreshModList(false);
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(Config.ToggleKey))
            {
                showMenu = !showMenu;
                if (showMenu)
                {
                    RefreshModList(false);
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
            }

            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f) statusMessage = "";
            }
        }

        public override void OnGUI()
        {
            // 1. Elegantes Wasserzeichen
            if (watermarkStyle == null)
            {
                watermarkStyle = new GUIStyle();
                watermarkStyle.fontSize = 12;
                watermarkStyle.normal.textColor = new Color(0.12f, 0.73f, 0.12f, 0.75f);
            }

            float wx = 15;
            float wy = Screen.height - 45;
            int activeModsCount = MelonMod.RegisteredMelons.Count;
            bool isGerman = Application.systemLanguage == SystemLanguage.German;

            string watermarkText = isGerman ? 
                $"🟢 Junkyard Mod Manager v1.0.0 Aktiv (Drücke {Config.ToggleKey})" : 
                $"🟢 Junkyard Mod Manager v1.0.0 Active (Press {Config.ToggleKey})";
            string activeSessionText = isGerman ? 
                $"🟢 Aktive Session-Mods: {activeModsCount}" : 
                $"🟢 Active Session Mods: {activeModsCount}";

            GUI.Label(new Rect(wx, wy, 400, 18), watermarkText, watermarkStyle);
            GUI.Label(new Rect(wx, wy + 16, 400, 18), activeSessionText, watermarkStyle);

            // 2. Mod Manager GUI
            if (!showMenu) return;

            if (windowRect.width == 0 || windowRect.height == 0)
            {
                windowRect = new Rect((Screen.width - 850) / 2f, (Screen.height - 500) / 2f, 850, 500);
            }

            InitStyles();

            windowRect = GUI.Window(999, windowRect, (GUI.WindowFunction)DrawWindow, "");
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            titleStyle = new GUIStyle();
            titleStyle.fontSize = 14;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1.0f, 0.75f, 0.1f); // Edles Gold/Gelb für den Haupttitel

            headerStyle = new GUIStyle();
            headerStyle.fontSize = 15;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.2f, 0.65f, 1f); // Schickes Cyan-Blau

            modActiveStyle = new GUIStyle();
            modActiveStyle.fontSize = 13;
            modActiveStyle.fontStyle = FontStyle.Bold;
            modActiveStyle.normal.textColor = new Color(0.12f, 0.73f, 0.12f); // Grün

            modDisabledStyle = new GUIStyle();
            modDisabledStyle.fontSize = 13;
            modDisabledStyle.fontStyle = FontStyle.Bold;
            modDisabledStyle.normal.textColor = new Color(0.95f, 0.25f, 0.25f); // Klares, gut lesbares Hellrot

            commentStyle = new GUIStyle();
            commentStyle.fontSize = 11;
            commentStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f); // Sehr helles Grau für maximale Lesbarkeit
        }

        private void DrawWindow(int windowId)
        {
            bool isGerman = Application.systemLanguage == SystemLanguage.German;

            // ABSOLUT CRASH-SICHERER HINTERGRUND
            for (int i = 0; i < 6; i++)
            {
                GUI.Box(new Rect(0, 0, 850, 500), "");
            }

            GUILayout.BeginVertical();
            GUILayout.Space(10);

            // Kopfzeile mit Custom Gold-Titel & Update Button
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            GUILayout.Label(isGerman ? $"🛠️ JUNKYARD MOD MANAGER ({Config.ToggleKey}) 🛠️" : $"🛠️ JUNKYARD MOD MANAGER ({Config.ToggleKey}) 🛠️", titleStyle, GUILayout.Width(300));
            
            string updateBtnText = isGerman ? "🔄 Updates suchen" : "🔄 Check Updates";
            if (updateStatus == "Suchen...") updateBtnText = isGerman ? "⏳ Suche läuft..." : "⏳ Checking...";
            else if (updateStatus == "Gefunden") updateBtnText = isGerman ? "🔄 Erneut suchen" : "🔄 Search Again";
            else if (updateStatus == "Fehler") updateBtnText = isGerman ? "❌ Fehler! Erneut suchen" : "❌ Error! Search Again";

            if (GUILayout.Button(updateBtnText, GUILayout.Width(180), GUILayout.Height(24)))
            {
                CheckForUpdates();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(isGerman ? "Schließen" : "Close", GUILayout.Width(100), GUILayout.Height(24)))
            {
                showMenu = false;
            }
            GUILayout.Space(15);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Spalten-Layout
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);

            // LINKE SPALTE: Mod-Liste (Breite: 380)
            GUILayout.BeginVertical(GUILayout.Width(380));
            GUILayout.Label(isGerman ? "Installierte Mods (in /Mods)" : "Installed Mods (in /Mods)", headerStyle);
            GUILayout.Space(5);

            modsScrollPos = GUILayout.BeginScrollView(modsScrollPos, "box", GUILayout.Height(330));
            
            if (modFiles.Count == 0)
            {
                GUILayout.Label(isGerman ? "Keine Mods im /Mods-Ordner gefunden." : "No mods found in /Mods directory.");
            }

            foreach (var mod in modFiles)
            {
                GUILayout.BeginHorizontal("box");
                
                string statusText = mod.IsActive ? "🟢" : "🔴";
                GUIStyle nameStyle = mod.IsActive ? modActiveStyle : modDisabledStyle;
                
                GUILayout.Label($"{statusText} {mod.DisplayName}", nameStyle, GUILayout.Width(180));

                if (GUILayout.Button(isGerman ? "Config" : "Config", GUILayout.Width(60)))
                {
                    LoadModConfig(mod);
                }

                string actionText = mod.IsActive ? 
                    (isGerman ? "Deaktivieren" : "Disable") : 
                    (isGerman ? "Aktivieren" : "Enable");
                if (GUILayout.Button(actionText, GUILayout.Width(90)))
                {
                    ToggleModState(mod);
                }

                GUILayout.EndHorizontal();

                // Zeige Update-Hinweis direkt unter dem Mod
                string cleanLowerName = mod.DisplayName.ToLower();
                if (remoteUpdates.TryGetValue(cleanLowerName, out RemoteModUpdate upInfo))
                {
                    if (upInfo.UpdateAvailable)
                    {
                        GUILayout.BeginHorizontal("box");
                        GUILayout.Label(isGerman ? $"🆕 Update v{upInfo.Version} verfügbar!" : $"🆕 Update v{upInfo.Version} available!", commentStyle);
                        if (upInfo.IsUpdating)
                        {
                            string stat = upInfo.Status;
                            if (stat == "Lade...") stat = isGerman ? "Lade..." : "Loading...";
                            GUILayout.Label(stat, commentStyle, GUILayout.Width(90));
                        }
                        else
                        {
                            if (GUILayout.Button(isGerman ? "📥 Update" : "📥 Update", GUILayout.Width(90)))
                            {
                                InstallUpdate(mod.DisplayName, upInfo.DownloadUrl);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button(isGerman ? "Liste aktualisieren" : "Refresh List", GUILayout.Height(30)))
            {
                RefreshModList(true);
            }

            GUILayout.EndVertical();

            GUILayout.Space(20);

            // RECHTE SPALTE: Config-Editor (Breite: 400)
            GUILayout.BeginVertical(GUILayout.Width(400));
            GUILayout.Label(isGerman ? "Mod-Einstellungen" : "Mod Settings", headerStyle);
            GUILayout.Space(5);

            if (selectedMod == null)
            {
                GUILayout.BeginVertical("box", GUILayout.Height(330));
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(isGerman ? "Wähle links eine Mod aus,\num die Konfiguration zu bearbeiten." : "Select a mod on the left\nto edit its configuration.");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Label((isGerman ? "Konfiguration für: " : "Configuration for: ") + selectedMod.DisplayName, GUILayout.Width(380));
                GUILayout.Space(5);

                configScrollPos = GUILayout.BeginScrollView(configScrollPos, "box", GUILayout.Height(280));

                if (selectedConfigLines.Count == 0)
                {
                    GUILayout.Label(isGerman ? "Diese Mod besitzt keine konfigurierbaren Einstellungen." : "This mod has no configurable settings.");
                }

                foreach (var line in selectedConfigLines)
                {
                    if (line.IsComment)
                    {
                        GUILayout.Label(line.Content, commentStyle);
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(line.Key + ":", GUILayout.Width(150));
                        
                        if (line.Value.ToLower() == "true" || line.Value.ToLower() == "false")
                        {
                            bool val = line.Value.ToLower() == "true";
                            string toggleLabel = val ? 
                                (isGerman ? "🟢 AKTIVIERT" : "🟢 ENABLED") : 
                                (isGerman ? "🔴 DEAKTIVIERT" : "🔴 DISABLED");
                            if (GUILayout.Button(toggleLabel, GUILayout.Width(130)))
                            {
                                line.Value = val ? "false" : "true";
                            }
                        }
                        else if (float.TryParse(line.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float num))
                        {
                            GUILayout.Label($"{num.ToString("F1", CultureInfo.InvariantCulture)}x", GUILayout.Width(50));
                            if (GUILayout.Button("-", GUILayout.Width(35)))
                            {
                                num = Mathf.Max(0.5f, num - 0.5f);
                                line.Value = num.ToString("F1", CultureInfo.InvariantCulture);
                            }
                            if (GUILayout.Button("+", GUILayout.Width(35)))
                            {
                                num = num + 0.5f;
                                line.Value = num.ToString("F1", CultureInfo.InvariantCulture);
                            }
                        }
                        else
                        {
                            GUILayout.Label(line.Value);
                        }

                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndScrollView();

                // Button-Leiste
                GUILayout.Space(5);
                GUILayout.Label(isGerman ? "⚠️ Hinweis: Änderungen erfordern einen Spielneustart!" : "⚠️ Note: Changes require a game restart!", commentStyle);
                
                GUILayout.BeginHorizontal();
                
                if (GUILayout.Button(isGerman ? "✔️ OK (Speichern)" : "✔️ OK (Save)", GUILayout.Height(30)))
                {
                    SaveModConfig();
                    restartNeeded = true;
                    selectedMod = null;
                    selectedConfigLines.Clear();
                }

                if (GUILayout.Button(isGerman ? "❌ Abbrechen (Zurück)" : "❌ Cancel (Back)", GUILayout.Height(30)))
                {
                    selectedMod = null;
                    selectedConfigLines.Clear();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.Space(15);
            GUILayout.EndHorizontal();

            // Statuszeile
            GUILayout.Space(10);
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(statusMessage, modActiveStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (restartNeeded)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(isGerman ? 
                    "⚠️ Änderungen vorgenommen! Bitte starte das Spiel neu, um sie zu aktivieren." : 
                    "⚠️ Changes made! Please restart the game to activate them.", modDisabledStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void RefreshModList(bool showStatus)
        {
            modFiles.Clear();
            string modsDir = MelonEnvironment.ModsDirectory;
            bool isGerman = Application.systemLanguage == SystemLanguage.German;

            if (!Directory.Exists(modsDir)) return;

            string[] dlls = Directory.GetFiles(modsDir, "*.dll");
            foreach (var path in dlls)
            {
                if (Path.GetFileName(path).ToLower() == "junkyardmodmanager.dll")
                    continue;

                modFiles.Add(new ModFile
                {
                    FileName = Path.GetFileName(path),
                    FullPath = path,
                    IsActive = true
                });
            }

            string[] disabled = Directory.GetFiles(modsDir, "*.dll.disabled");
            foreach (var path in disabled)
            {
                modFiles.Add(new ModFile
                {
                    FileName = Path.GetFileName(path),
                    FullPath = path,
                    IsActive = false
                });
            }

            if (showStatus)
            {
                ShowStatus(isGerman ? "Mod-Liste aktualisiert!" : "Mod list refreshed!", 2f);
            }
        }

        private void ToggleModState(ModFile mod)
        {
            bool isGerman = Application.systemLanguage == SystemLanguage.German;
            try
            {
                string modsDir = MelonEnvironment.ModsDirectory;
                string oldPath = mod.FullPath;
                string newPath = "";

                if (mod.IsActive)
                {
                    newPath = Path.Combine(modsDir, mod.FileName + ".disabled");
                }
                else
                {
                    string cleanName = mod.FileName.Replace(".disabled", "");
                    newPath = Path.Combine(modsDir, cleanName);
                }

                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                    restartNeeded = true;
                    RefreshModList(false);
                    selectedMod = null;
                    selectedConfigLines.Clear();
                }
            }
            catch (System.Exception ex)
            {
                ShowStatus((isGerman ? "Fehler beim Umschalten: " : "Error toggling state: ") + ex.Message, 4f);
            }
        }

        private void LoadModConfig(ModFile mod)
        {
            selectedMod = mod;
            selectedConfigLines.Clear();
            bool isGerman = Application.systemLanguage == SystemLanguage.German;

            string configName = mod.DisplayName + ".txt";
            configFilePath = Path.Combine(MelonEnvironment.UserDataDirectory, configName);

            if (!File.Exists(configFilePath))
            {
                configFilePath = Path.Combine(MelonEnvironment.UserDataDirectory, mod.DisplayName + ".cfg");
            }

            if (!File.Exists(configFilePath))
            {
                ShowStatus(isGerman ? $"Keine Config gefunden (UserData/{configName})" : $"No config found (UserData/{configName})", 3f);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(configFilePath);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                        continue;

                    if (trimmed.StartsWith("#"))
                    {
                        selectedConfigLines.Add(new ConfigLine
                        {
                            IsComment = true,
                            Content = line
                        });
                    }
                    else if (trimmed.Contains("="))
                    {
                        string[] parts = trimmed.Split(new char[] { '=' }, 2);
                        selectedConfigLines.Add(new ConfigLine
                        {
                            IsComment = false,
                            Key = parts[0].Trim(),
                            Value = parts[1].Trim()
                        });
                    }
                }
                ShowStatus(isGerman ? "Einstellungen geladen!" : "Settings loaded!", 1.5f);
            }
            catch (System.Exception ex)
            {
                ShowStatus((isGerman ? "Fehler beim Lesen: " : "Error reading: ") + ex.Message, 4f);
            }
        }

        private void SaveModConfig()
        {
            if (string.IsNullOrEmpty(configFilePath) || selectedMod == null) return;
            bool isGerman = Application.systemLanguage == SystemLanguage.German;

            try
            {
                using (StreamWriter writer = new StreamWriter(configFilePath))
                {
                    foreach (var line in selectedConfigLines)
                    {
                        if (line.IsComment)
                        {
                            writer.WriteLine(line.Content);
                        }
                        else
                        {
                            writer.WriteLine($"{line.Key}={line.Value}");
                        }
                    }
                }
                ShowStatus(isGerman ? "⚙️ Einstellungen erfolgreich gespeichert!" : "⚙️ Settings saved successfully!", 3f);
            }
            catch (System.Exception ex)
            {
                ShowStatus((isGerman ? "Fehler beim Speichern: " : "Error saving: ") + ex.Message, 4f);
            }
        }

        private void CheckForUpdates()
        {
            if (updateStatus == "Suchen...") return;
            updateStatus = "Suchen...";
            bool isGerman = Application.systemLanguage == SystemLanguage.German;
            ShowStatus(isGerman ? "Suche nach Mod-Updates..." : "Checking for mod updates...", 3f);

            new System.Threading.Thread(new System.Threading.ThreadStart(delegate
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        string content = client.DownloadString(Config.UpdateURL);
                        
                        var newUpdates = new System.Collections.Generic.Dictionary<string, RemoteModUpdate>();
                        
                        string[] lines = content.Split(new string[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                            string[] parts = trimmed.Split(new char[] { '=' }, 2);
                            if (parts.Length < 2) continue;

                            string modName = parts[0].Trim();
                            string[] valParts = parts[1].Split(new char[] { '|' }, 2);
                            if (valParts.Length < 2) continue;

                            string remoteVer = valParts[0].Trim();
                            string downloadUrl = valParts[1].Trim();

                            // Finde lokale Version
                            string localVer = "0.0.0";
                            foreach (var melon in MelonMod.RegisteredMelons)
                            {
                                if (melon.Info.Name.ToLower().Contains(modName.ToLower()) || 
                                    melon.MelonAssembly.Assembly.GetName().Name.ToLower().Contains(modName.ToLower()))
                                {
                                    localVer = melon.Info.Version;
                                    break;
                                }
                            }

                            bool hasUpdate = false;
                            if (System.Version.TryParse(remoteVer, out System.Version rVer) && 
                                System.Version.TryParse(localVer, out System.Version lVer))
                            {
                                hasUpdate = rVer > lVer;
                            }
                            else
                            {
                                hasUpdate = remoteVer != localVer && localVer != "0.0.0";
                            }

                            newUpdates[modName.ToLower()] = new RemoteModUpdate
                            {
                                Version = remoteVer,
                                DownloadUrl = downloadUrl,
                                UpdateAvailable = hasUpdate
                            };
                        }

                        remoteUpdates = newUpdates;
                        updateStatus = "Gefunden";
                        ShowStatus(isGerman ? "Update-Check abgeschlossen!" : "Update check complete!", 3f);
                    }
                }
                catch (System.Exception ex)
                {
                    updateStatus = "Fehler";
                    ShowStatus((isGerman ? "Fehler beim Update-Check: " : "Error during update check: ") + ex.Message, 4f);
                }
            })).Start();
        }

        private void InstallUpdate(string modName, string downloadUrl)
        {
            bool isGerman = Application.systemLanguage == SystemLanguage.German;
            if (remoteUpdates.ContainsKey(modName.ToLower()))
            {
                remoteUpdates[modName.ToLower()].IsUpdating = true;
                remoteUpdates[modName.ToLower()].Status = "Lade...";
            }
            ShowStatus(isGerman ? $"Update für {modName} wird geladen..." : $"Downloading update for {modName}...", 3f);

            new System.Threading.Thread(new System.Threading.ThreadStart(delegate
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        
                        string modsDir = MelonEnvironment.ModsDirectory;
                        string targetPath = Path.Combine(modsDir, modName + ".dll");
                        
                        if (!File.Exists(targetPath) && File.Exists(Path.Combine(modsDir, modName + ".dll.disabled")))
                        {
                            targetPath = Path.Combine(modsDir, modName + ".dll.disabled");
                        }

                        client.DownloadFile(downloadUrl, targetPath);

                        if (remoteUpdates.ContainsKey(modName.ToLower()))
                        {
                            remoteUpdates[modName.ToLower()].IsUpdating = false;
                            remoteUpdates[modName.ToLower()].UpdateAvailable = false;
                            remoteUpdates[modName.ToLower()].Status = "Erfolgreich!";
                        }

                        restartNeeded = true;
                        ShowStatus(isGerman ? $"🎉 {modName} erfolgreich aktualisiert!" : $"🎉 {modName} successfully updated!", 4f);
                    }
                }
                catch (System.Exception ex)
                {
                    if (remoteUpdates.ContainsKey(modName.ToLower()))
                    {
                        remoteUpdates[modName.ToLower()].IsUpdating = false;
                        remoteUpdates[modName.ToLower()].Status = "Fehlgeschlagen";
                    }
                    ShowStatus((isGerman ? $"Fehler beim Update von {modName}: " : $"Error updating {modName}: ") + ex.Message, 5f);
                }
            })).Start();
        }

        private void ShowStatus(string msg, float duration)
        {
            statusMessage = msg;
            statusTimer = duration;
        }
    }
}
