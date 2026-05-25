using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using Il2CppScripts.Upgrades;
using Il2CppScripts.Managers;
using Il2CppScripts.Interactables;
using Il2Cpp;

[assembly: MelonInfo(typeof(JunkyardWeightMod.Main), "Junkyard Weight Mod", "1.0.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardWeightMod
{
    public static class Config
    {
        public static float WeightMultiplier = 2.0f;
        public static bool ShowWatermark = true;

        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardWeightMod.txt");

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

                    if (key == "weightmultiplier" && float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float mult))
                        WeightMultiplier = mult;
                    else if (key == "showwatermark")
                        ShowWatermark = val.ToLower() == "true";
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Laden der WeightMod Config: " + ex.Message);
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
                    writer.WriteLine("# JUNKYARD WEIGHT MOD - CONFIGURATION FILE");
                    writer.WriteLine("# ========================================================================");
                    writer.WriteLine("# Du kannst die Werte hier anpassen. Starte das Spiel neu, um sie zu übernehmen.");
                    writer.WriteLine("#");
                    writer.WriteLine("# Gewichtslimit Multiplikator (z.B. 2.0 für doppelte Traglast, 3.0 für dreifache)");
                    writer.WriteLine("WeightMultiplier=2.0");
                    writer.WriteLine("#");
                    writer.WriteLine("# Wasserzeichen unten links anzeigen (true / false)");
                    writer.WriteLine("ShowWatermark=true");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("Fehler beim Erstellen der WeightMod Standard-Config: " + ex.Message);
            }
        }
    }

    public class Main : MelonMod
    {
        private GUIStyle watermarkStyle;
        private string watermarkText = "";
        private float checkTimer = 0f;
        private static HashSet<string> modifiedUpgrades = new HashSet<string>();
        private static HashSet<int> modifiedCarts = new HashSet<int>();

        public override void OnInitializeMelon()
        {
            Config.Load();

            // Register Harmony patches
            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("   JUNKYARD WEIGHT MOD ACTIVE!                    ");
            LoggerInstance.Msg($"   Multiplier: {Config.WeightMultiplier.ToString("F1")}x (Carry Limit & Cart Capacity)");
            LoggerInstance.Msg("==================================================");
            
            // Set up watermark based on system language
            bool isGerman = Application.systemLanguage == SystemLanguage.German;
            if (isGerman)
            {
                watermarkText = $"🟢 Gewicht Mod: {Config.WeightMultiplier.ToString("F1")}x Traglast & Schubkarre";
            }
            else
            {
                watermarkText = $"🟢 Weight Mod: {Config.WeightMultiplier.ToString("F1")}x Carry Limit & Cart Capacity";
            }

            ApplyWeightMultipliers();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            ApplyWeightMultipliers();
        }

        public override void OnUpdate()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= 2.0f) // Check every 2 seconds for dynamically loaded assets
            {
                checkTimer = 0f;
                ApplyWeightMultipliers();
            }
        }

        private static void ApplyWeightMultipliers()
        {
            try
            {
                // 1. Double Player Carrying Limit
                var manager = UnityEngine.Object.FindObjectOfType<UpgradesManager>();
                if (manager != null)
                {
                    var packs = manager.upgradeSetsPacks;
                    if (packs != null)
                    {
                        for (int i = 0; i < packs.Count; i++)
                        {
                            var pack = packs[i];
                            if (pack == null) continue;

                            var sets = pack.UpgradeSetsSO;
                            if (sets == null) continue;

                            for (int j = 0; j < sets.Count; j++)
                            {
                                var set = sets[j];
                                if (set == null) continue;

                                // Only apply the multiplier to the Player's Carrying Limit upgrade!
                                // UpgradeType.CantHoldAllTheseItems (29) represents the carry weight upgrades.
                                if (set.UpgradeType == UpgradeType.CantHoldAllTheseItems)
                                {
                                    var levels = set.UpgradeLevelScriptableObjects;
                                    if (levels == null) continue;

                                    for (int k = 0; k < levels.Count; k++)
                                    {
                                        var level = levels[k];
                                        if (level == null) continue;

                                        string key = level.name + "_" + level.GetInstanceID();
                                        if (modifiedUpgrades.Contains(key)) continue;

                                        float origVal = level.UpgradeValue;
                                        float origSecVal = level.UpgradeSecondValue;

                                        // Double player carry limit
                                        level.UpgradeValue *= Config.WeightMultiplier;
                                        level.UpgradeSecondValue *= Config.WeightMultiplier;

                                        modifiedUpgrades.Add(key);
                                        MelonLogger.Msg($"[Weight Mod] Modifiziertes Traglast-Upgrade '{level.name}': level value {origVal} -> {level.UpgradeValue} (Second: {origSecVal} -> {level.UpgradeSecondValue})");
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Scan for ALL Deposit components in the scene (active and inactive)
                var deposits = Resources.FindObjectsOfTypeAll<Deposit>();
                if (deposits != null && deposits.Length > 0)
                {
                    foreach (var dep in deposits)
                    {
                        if (dep == null) continue;
                        int id = dep.GetInstanceID();
                        if (modifiedCarts.Contains(id)) continue;

                        // Check if this deposit is part of a cart/wheelbarrow/vehicle/handcart
                        string name = dep.name.ToLower();
                        bool isCart = name.Contains("cart") || name.Contains("wheelbarrow") || name.Contains("schubkarre") || name.Contains("vehicle") || name.Contains("handcart");
                        
                        // Check parent hierarchy for matching names
                        var t = dep.transform;
                        while (t != null && !isCart)
                        {
                            string parentName = t.name.ToLower();
                            if (parentName.Contains("cart") || parentName.Contains("wheelbarrow") || parentName.Contains("schubkarre") || parentName.Contains("vehicle") || parentName.Contains("handcart"))
                            {
                                isCart = true;
                            }
                            t = t.parent;
                        }

                        if (isCart)
                        {
                            int originalMax = dep.maxWeightInside;
                            dep.maxWeightInside = Mathf.RoundToInt(originalMax * Config.WeightMultiplier);
                            modifiedCarts.Add(id);
                            MelonLogger.Msg($"[Weight Mod] Modifizierte Deposit '{dep.name}' (Objekt: {dep.gameObject.name}) Kapazität: {originalMax} -> {dep.maxWeightInside}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("Fehler beim Scannen der Upgrade-Manager Strukturen: " + ex.Message);
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
            float wy = Screen.height - 45; // Offset from SpeedMod watermark (which is at -65)

            GUI.Label(new Rect(wx, wy, 500, 18), watermarkText, watermarkStyle);
        }
    }

    // 3. Double the cart/wheelbarrow max capacity limit via Harmony patch as backup
    [HarmonyPatch(typeof(CartManager), nameof(CartManager.GetCartMaxCapacity))]
    public static class Patch_GetCartMaxCapacity
    {
        public static void Postfix(ref int __result)
        {
            __result = Mathf.RoundToInt(__result * Config.WeightMultiplier);
        }
    }

    // 4. Intercept SetCapacity calls on Deposits of carts
    [HarmonyPatch(typeof(Deposit), nameof(Deposit.SetCapacity))]
    public static class Patch_Deposit_SetCapacity
    {
        public static void Prefix(Deposit __instance, ref int value)
        {
            if (__instance == null) return;
            string name = __instance.name.ToLower();
            bool isCart = name.Contains("cart") || name.Contains("wheelbarrow") || name.Contains("schubkarre") || name.Contains("vehicle") || name.Contains("handcart");
            
            var t = __instance.transform;
            while (t != null && !isCart)
            {
                string parentName = t.name.ToLower();
                if (parentName.Contains("cart") || parentName.Contains("wheelbarrow") || parentName.Contains("schubkarre") || parentName.Contains("vehicle") || parentName.Contains("handcart"))
                {
                    isCart = true;
                }
                t = t.parent;
            }

            if (isCart)
            {
                int original = value;
                value = Mathf.RoundToInt(value * Config.WeightMultiplier);
                MelonLogger.Msg($"[Weight Mod] Intercepted Deposit.SetCapacity für '{__instance.name}': {original} -> {value}");
            }
        }
    }
}
