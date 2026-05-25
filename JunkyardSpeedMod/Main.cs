using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using System.IO;
using System.Globalization;

[assembly: MelonInfo(typeof(JunkyardSpeedMod.Main), "Junkyard Speed & Background Mod", "1.0.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardSpeedMod
{
    public static class Config
    {
        public static float DefaultSpeed = 1.0f;
        public static bool RunInBackground = true;
        public static bool ShowWatermark = true;
        public static KeyCode SpeedUpKey = KeyCode.KeypadPlus;
        public static KeyCode SpeedDownKey = KeyCode.KeypadMinus;
        public static KeyCode NormalSpeedKey = KeyCode.KeypadMultiply;

        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardSpeedMod.txt");

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

                    if (key == "defaultspeed" && float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float speed))
                        DefaultSpeed = speed;
                    else if (key == "runinbackground")
                        RunInBackground = val.ToLower() == "true";
                    else if (key == "showwatermark")
                        ShowWatermark = val.ToLower() == "true";
                    else if (key == "speedupkey" && System.Enum.TryParse<KeyCode>(val, true, out KeyCode upK))
                        SpeedUpKey = upK;
                    else if (key == "speeddownkey" && System.Enum.TryParse<KeyCode>(val, true, out KeyCode downK))
                        SpeedDownKey = downK;
                    else if (key == "normalspeedkey" && System.Enum.TryParse<KeyCode>(val, true, out KeyCode normalK))
                        NormalSpeedKey = normalK;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Laden der SpeedMod Config: " + ex.Message);
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
                    writer.WriteLine("# JUNKYARD SPEED & BACKGROUND MOD - CONFIGURATION FILE");
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("# Du kannst die Werte hier anpassen. Starte das Spiel neu, um sie zu übernehmen.");
                    writer.WriteLine("#");
                    writer.WriteLine("# Standard-Spielgeschwindigkeit beim Start (z.B. 1.0, 2.0, 5.0)");
                    writer.WriteLine("DefaultSpeed=1.0");
                    writer.WriteLine("#");
                    writer.WriteLine("# Spiel im Hintergrund weiterlaufen lassen (true / false)");
                    writer.WriteLine("RunInBackground=true");
                    writer.WriteLine("#");
                    writer.WriteLine("# Wasserzeichen unten links anzeigen (true / false)");
                    writer.WriteLine("ShowWatermark=true");
                    writer.WriteLine("#");
                    writer.WriteLine("# Tastenbelegungen (z.B. PageUp, PageDown, Home, F3, F4, G, KeypadPlus, KeypadMinus)");
                    writer.WriteLine("SpeedUpKey=KeypadPlus");
                    writer.WriteLine("SpeedDownKey=KeypadMinus");
                    writer.WriteLine("NormalSpeedKey=KeypadMultiply");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Erstellen der SpeedMod Standard-Config: " + ex.Message);
            }
        }
    }

    public class Main : MelonMod
    {
        private float currentSpeed = 1.0f;
        private string statusMsg = "";
        private float statusTimer = 0f;
        private GUIStyle watermarkStyle;
        private GUIStyle alertStyle;

        public override void OnInitializeMelon()
        {
            Config.Load();
            currentSpeed = Config.DefaultSpeed;

            // Spielgeschwindigkeit setzen
            Time.timeScale = currentSpeed;

            // Hintergrund-Modus setzen
            Application.runInBackground = Config.RunInBackground;

            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("   JUNKYARD SPEED & BACKGROUND MOD ACTIVE!        ");
            LoggerInstance.Msg($"   Hotkeys: {Config.SpeedUpKey}=Schneller | {Config.SpeedDownKey}=Langsamer | {Config.NormalSpeedKey}=1x");
            LoggerInstance.Msg("==================================================");
        }

        public override void OnUpdate()
        {
            // Tastatur-Hotkeys abfragen aus Config geladen
            if (Input.GetKeyDown(Config.SpeedUpKey))
            {
                if (currentSpeed < 1.0f) currentSpeed += 0.1f;
                else currentSpeed = Mathf.Min(10.0f, currentSpeed + 1.0f);
                Time.timeScale = currentSpeed;
                ShowStatus($"⏩ Spielgeschwindigkeit: {currentSpeed.ToString("F1")}x", 2.5f);
            }
            else if (Input.GetKeyDown(Config.SpeedDownKey))
            {
                if (currentSpeed > 1.0f) currentSpeed -= 1.0f;
                else if (currentSpeed > 0.1f) currentSpeed = Mathf.Max(0.1f, currentSpeed - 0.1f);
                Time.timeScale = currentSpeed;
                ShowStatus($"⏪ Spielgeschwindigkeit: {currentSpeed.ToString("F1")}x", 2.5f);
            }
            else if (Input.GetKeyDown(Config.NormalSpeedKey))
            {
                currentSpeed = 1.0f;
                Time.timeScale = 1.0f;
                ShowStatus("⏹️ Spielgeschwindigkeit normalisiert (1.0x)", 2.5f);
            }

            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f) statusMsg = "";
            }
        }

        public override void OnGUI()
        {
            if (!Config.ShowWatermark) return;

            if (watermarkStyle == null)
            {
                watermarkStyle = new GUIStyle();
                watermarkStyle.fontSize = 12;
                watermarkStyle.normal.textColor = new Color(0.12f, 0.73f, 0.12f, 0.75f);
            }

            float wx = 15;
            float wy = Screen.height - 65;

            string bgStatusText = Config.RunInBackground ? "Aktiv" : "Inaktiv";
            GUI.Label(new Rect(wx, wy, 500, 18), $"🟢 Speed Mod Tempo: {currentSpeed.ToString("F1")}x | Hotkey: {Config.SpeedUpKey}/{Config.SpeedDownKey}", watermarkStyle);

            if (!string.IsNullOrEmpty(statusMsg))
            {
                if (alertStyle == null)
                {
                    alertStyle = new GUIStyle();
                    alertStyle.fontSize = 20;
                    alertStyle.fontStyle = FontStyle.Bold;
                    alertStyle.alignment = TextAnchor.MiddleCenter;
                    alertStyle.normal.textColor = new Color(0.2f, 0.65f, 1f, 1f);
                }

                GUI.Label(new Rect(0, 80, Screen.width, 40), statusMsg, alertStyle);
            }
        }

        private void ShowStatus(string msg, float duration)
        {
            statusMsg = msg;
            statusTimer = duration;
            LoggerInstance.Msg(msg);
        }
    }
}
