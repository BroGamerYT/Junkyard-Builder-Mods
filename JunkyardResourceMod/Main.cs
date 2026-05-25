using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using UnityEngine;
using Il2CppScripts.Managers;
using Il2CppScripts.Upgrades;
using Il2Cpp;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(JunkyardResourceMod.Main), "Junkyard Resource & Upgrade Booster", "1.3.1", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardResourceMod
{
    public static class Config
    {
        public static float Multiplier = 1.0f; // Standard vanilla value by default
        public static float ContractMultiplier = 1.0f; // Standard vanilla value by default
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

        public static void SaveDefaultConfig()
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
                    writer.WriteLine("# Multiplikator für Schrottmengen, Platzkapazitäten und Fahrzeuge (Vanilla Default: 1.0)");
                    writer.WriteLine("Multiplier=1.0");
                    writer.WriteLine("#");
                    writer.WriteLine("# Multiplikator für Geldauszahlungen bei Kundenaufträgen (Vanilla Default: 1.0)");
                    writer.WriteLine("ContractMultiplier=1.0");
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
        private float checkTimer = 0f;
        private static HashSet<string> modifiedUpgrades = new HashSet<string>();
        private static HashSet<string> modifiedContractIds = new HashSet<string>(); // Tracks boosted contracts by ContractID to prevent overflows
        private static HashSet<int> modifiedTrucks = new HashSet<int>();

        public override void OnInitializeMelon()
        {
            Config.Load();

            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("   JUNKYARD RESOURCE & UPGRADE BOOSTER ACTIVE!   ");
            LoggerInstance.Msg($"   Multiplier: {Config.Multiplier}x | Contracts: {Config.ContractMultiplier}x | Unlock: {Config.UnlockAllUpgrades}");
            LoggerInstance.Msg("==================================================");

            ApplyResourceBoosts();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            ApplyResourceBoosts();
        }

        public override void OnUpdate()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= 2.0f) // Check every 2 seconds for dynamically loaded assets
            {
                checkTimer = 0f;
                ApplyResourceBoosts();
            }
        }

        private static void ApplyResourceBoosts()
        {
            try
            {
                // 1. Boost Upgrade values (carrying capacity, yard capacity, etc.)
                var upgrades = Resources.FindObjectsOfTypeAll<UpgradeLevelScriptableObject>();
                if (upgrades != null && upgrades.Length > 0)
                {
                    foreach (var upgrade in upgrades)
                    {
                        if (upgrade == null) continue;
                        
                        // Skip weight upgrades so they are managed exclusively by JunkyardWeightMod!
                        if (upgrade.ValuesAreWeight) continue;

                        string key = upgrade.name + "_" + upgrade.GetInstanceID();
                        if (modifiedUpgrades.Contains(key)) continue;

                        upgrade.UpgradeValue *= Config.Multiplier;
                        upgrade.UpgradeSecondValue *= Config.Multiplier;
                        modifiedUpgrades.Add(key);
                        MelonLogger.Msg($"[Resource Mod] Boosted Upgrade '{upgrade.name}': {upgrade.UpgradeValue} (Second: {upgrade.UpgradeSecondValue})");
                    }
                }

                // 2. Boost Contract payouts in active ContractsManager (with self-cleaning ID tracking to prevent overflow)
                var manager = UnityEngine.Object.FindObjectOfType<ContractsManager>();
                if (manager != null)
                {
                    var contractItems = manager.GetContractsItems();
                    if (contractItems != null)
                    {
                        var activeIds = new HashSet<string>();
                        for (int i = 0; i < contractItems.Count; i++)
                        {
                            var contract = contractItems[i];
                            if (contract == null) continue;
                            
                            string contractIdStr = contract.ContractID.ToString();
                            activeIds.Add(contractIdStr);

                            // Detect and repair negative / overflowing values from previous save files or glitches to Vanilla standard values
                            if (contract.Value <= 0)
                            {
                                // Vanilla standard base values: $1500, $2500, $3500...
                                int baseValue = 1500 + (i * 1000);
                                contract.Value = (int)(baseValue * Config.ContractMultiplier);
                                MelonLogger.Warning($"[Resource Mod] Repaired corrupted negative contract '{contract.ContractID}' to standard vanilla payout {contract.Value}");
                                // Force add to modifiedContractIds so it doesn't get boosted again
                                modifiedContractIds.Add(contractIdStr);
                                continue;
                            }
                            
                            if (modifiedContractIds.Contains(contractIdStr)) continue;

                            contract.Value = (int)(contract.Value * Config.ContractMultiplier);
                            modifiedContractIds.Add(contractIdStr);
                            MelonLogger.Msg($"[Resource Mod] Boosted Active Contract '{contract.ContractID}': payout {contract.Value}");
                        }
                        
                        // Clean up modifiedContractIds so that only currently active contracts are kept
                        modifiedContractIds.IntersectWith(activeIds);
                    }
                }

                // 3. Boost Truck delivery object counts
                var trucks = Resources.FindObjectsOfTypeAll<TruckManager>();
                if (trucks != null && trucks.Length > 0)
                {
                    foreach (var truck in trucks)
                    {
                        if (truck == null) continue;
                        int key = truck.GetInstanceID();
                        if (modifiedTrucks.Contains(key)) continue;

                        truck.objectsPerSpawn = (int)(truck.objectsPerSpawn * Config.Multiplier);
                        modifiedTrucks.Add(key);
                        MelonLogger.Msg($"[Resource Mod] Boosted Truck objectsPerSpawn: {truck.objectsPerSpawn}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("Fehler beim Scannen der Ressourcen-Booster: " + ex.Message);
            }
        }
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
}
