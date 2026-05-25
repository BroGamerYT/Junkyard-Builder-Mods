using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using UnityEngine;
using Il2CppScripts.Managers;
using Il2CppScripts.Upgrades;
using Il2Cpp;
using System.IO;
using System.Globalization;

[assembly: MelonInfo(typeof(JunkyardResourceMod.Main), "Junkyard Resource & Upgrade Booster", "1.3.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardResourceMod
{
    public static class Config
    {
        public static float Multiplier = 5.0f;
        public static float ContractMultiplier = 5.0f;
        public static bool UnlockAllUpgrades = false;

        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardResourceMod.txt");

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

                    if (key == "multiplier" && float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float mult))
                        Multiplier = mult;
                    else if (key == "contractmultiplier" && float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float cMult))
                        ContractMultiplier = cMult;
                    else if (key == "unlockallupgrades")
                        UnlockAllUpgrades = val.ToLower() == "true";
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Laden der eigenen Config-Datei: " + ex.Message);
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
                    writer.WriteLine("# JUNKYARD RESOURCE & UPGRADE BOOSTER - MOD CONFIGURATION FILE");
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("# Du kannst die Werte hier anpassen. Starte das Spiel neu, um sie zu übernehmen.");
                    writer.WriteLine("# You can customize the settings below. Restart the game to apply changes.");
                    writer.WriteLine("#");
                    writer.WriteLine("# Multiplikator für Schrottmengen, Platzkapazitäten und Fahrzeuge (Default: 5.0)");
                    writer.WriteLine("Multiplier=5.0");
                    writer.WriteLine("#");
                    writer.WriteLine("# Multiplikator für Geldauszahlungen bei Kundenaufträgen (Default: 5.0)");
                    writer.WriteLine("ContractMultiplier=5.0");
                    writer.WriteLine("#");
                    writer.WriteLine("# Auf 'true' stellen, um alle Upgrades sofort kostenlos freizuschalten.");
                    writer.WriteLine("# Auf 'false' stellen, um Upgrades normal zu kaufen (Default: false)");
                    writer.WriteLine("UnlockAllUpgrades=false");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Erstellen der Standard-Config: " + ex.Message);
            }
        }
    }

    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Config.Load();

            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("   JUNKYARD RESOURCE & UPGRADE BOOSTER ACTIVE!   ");
            LoggerInstance.Msg($"   Multiplier: {Config.Multiplier}x | Contracts: {Config.ContractMultiplier}x | Unlock: {Config.UnlockAllUpgrades}");
            LoggerInstance.Msg("==================================================");
        }

        // 1. Alle Upgrades freischalten (wenn auf true gesetzt)
        [HarmonyPatch(typeof(UpgradesManager), "IsUpgradeBought")]
        public static class Patch_IsUpgradeBought
        {
            public static void Postfix(ref bool __result)
            {
                if (Config.UnlockAllUpgrades)
                {
                    __result = true;
                }
            }
        }

        // 2. Upgrade Wert multiplizieren
        [HarmonyPatch(typeof(UpgradeLevelScriptableObject), "get_UpgradeValue")]
        public static class Patch_UpgradeValue
        {
            public static void Postfix(UpgradeLevelScriptableObject __instance, ref float __result)
            {
                string upgradeName = __instance.name.ToLower();
                float original = __result;

                __result *= Config.Multiplier;

                MelonLogger.Msg($"[Upgrade Booster] {upgradeName}: {original} -> {__result} ({Config.Multiplier}x Boost!)");
            }
        }

        // 3. Zweiter Upgrade Wert
        [HarmonyPatch(typeof(UpgradeLevelScriptableObject), "get_UpgradeSecondValue")]
        public static class Patch_UpgradeSecondValue
        {
            public static void Postfix(ref float __result)
            {
                __result *= Config.Multiplier;
            }
        }

        // 4. Geldbelohnung bei Kundenaufträgen multiplizieren
        [HarmonyPatch(typeof(ContractDataItem), "get_Value")]
        public static class Patch_ContractValue
        {
            public static void Postfix(ref int __result)
            {
                int original = __result;
                __result = (int)(__result * Config.ContractMultiplier);
                MelonLogger.Msg($"[Contract Payout Booster] Bezahlung erhöht: {original} -> {__result} ({Config.ContractMultiplier}x Belohnung!)");
            }
        }

        // 5. LKW-Entladungsmenge (Container-Lieferung / Müllautos) direkt multiplizieren!
        // Funktioniert vollständig ohne Upgrades und gilt für alle Schrott-Lieferungen!
        [HarmonyPatch(typeof(TruckManager), "get_objectsPerSpawn")]
        public static class Patch_TruckObjectsPerSpawn
        {
            public static void Postfix(ref int __result)
            {
                int original = __result;
                __result = (int)(__result * Config.Multiplier);
                MelonLogger.Msg($"[Truck Delivery Booster] LKW-Liefermenge erhöht: {original} -> {__result} ({Config.Multiplier}x Objekte!)");
            }
        }
    }
}
