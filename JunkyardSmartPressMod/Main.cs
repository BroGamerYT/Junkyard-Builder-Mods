using MelonLoader;
using MelonLoader.Utils;
using HarmonyLib;
using UnityEngine;
using System.IO;
using Il2CppScripts.Interactables;
using Il2Cpp;

[assembly: MelonInfo(typeof(JunkyardSmartPressMod.Main), "Junkyard Smart Press Mod", "1.0.0", "BroGamerYT")]
[assembly: MelonGame("FreemindGames", "Junkyard Builder")]

namespace JunkyardSmartPressMod
{
    public static class Config
    {
        public static bool EnableBuffer = true;
        public static bool EnableSmartFill = false; // Turned off by default so weight builds up naturally from multiple items
        public static bool EnableAutoInsert = true;
        public static bool EnableAlwaysInsert = true;
        public static bool EnableInstantCompress = true;

        private static string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "JunkyardSmartPressMod.txt");

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
                    string val = parts[1].Trim().ToLower();

                    bool parsedBool = val == "true" || val == "1" || val == "yes";

                    if (key == "enablebuffer") EnableBuffer = parsedBool;
                    else if (key == "enablesmartfill") EnableSmartFill = parsedBool;
                    else if (key == "enableautoinsert") EnableAutoInsert = parsedBool;
                    else if (key == "enablealwaysinsert") EnableAlwaysInsert = parsedBool;
                    else if (key == "enableinstantcompress") EnableInstantCompress = parsedBool;
                }
            }
            catch { }
        }

        public static void SaveDefaultConfig()
        {
            try
            {
                string contents =
                    "# ========================================================================\n" +
                    "# JUNKYARD SMART PRESS MOD - CONFIGURATION\n" +
                    "# ========================================================================\n\n" +
                    "# Enables the automatic excess scrap buffer (no resources wasted)\n" +
                    "EnableBuffer=true\n\n" +
                    "# Fills the press/baler weight to 100% on the first inserted item (set to true to enable instantly filling to 100%)\n" +
                    "EnableSmartFill=false\n\n" +
                    "# Automatically draws items into the press when they touch the green hopper\n" +
                    "EnableAutoInsert=true\n\n" +
                    "# Keeps the green hopper active even when the press is full\n" +
                    "EnableAlwaysInsert=true\n\n" +
                    "# Sets compression and Baler/Shredder duration to 0 seconds\n" +
                    "EnableInstantCompress=true\n";

                File.WriteAllText(configPath, contents);
            }
            catch { }
        }
    }

    public class Main : MelonMod
    {
        private static System.Collections.Generic.HashSet<int> processedItemIds = new System.Collections.Generic.HashSet<int>();
        private static float cleanupTimer = 0f;

        public override void OnInitializeMelon()
        {
            Config.Load();
            LoggerInstance.Msg("[Smart Press] Mod erfolgreich geladen und initialisiert!");
            LoggerInstance.Msg($"[Smart Press] Config: Buffer={Config.EnableBuffer}, SmartFill={Config.EnableSmartFill}, AutoInsert={Config.EnableAutoInsert}, AlwaysInsert={Config.EnableAlwaysInsert}, InstantCompress={Config.EnableInstantCompress}");
        }

        private static void ForceTriggerAreaActive(Il2CppScripts.Utility.TriggerDetector detector)
        {
            if (detector == null) return;
            try
            {
                // Force interaction state
                detector.canInputInteractable = true;
                
                // Force Renderers to be visible and green
                if (detector.inputAreaRenderers != null)
                {
                    foreach (var renderer in detector.inputAreaRenderers)
                    {
                        if (renderer != null)
                        {
                            renderer.enabled = true;
                            if (detector.defaultMaterial != null && renderer.sharedMaterial != detector.defaultMaterial)
                            {
                                renderer.sharedMaterial = detector.defaultMaterial;
                            }
                        }
                    }
                }
                
                // Force Collider to be active so physics engine always triggers OnTriggerEnter/Stay
                var col = detector.GetComponent<UnityEngine.Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
            catch { }
        }

        private static void AutoInsertFromHopper(
            UnityEngine.Vector3 hopperPosition,
            float radius,
            System.Func<Interactable, bool> isCorrectItem,
            System.Action<Interactable> insertItem)
        {
            try
            {
                var colliders = UnityEngine.Physics.OverlapSphere(hopperPosition, radius);
                if (colliders == null) return;

                foreach (var col in colliders)
                {
                    if (col == null) continue;
                    var interactable = col.GetComponent<Interactable>();
                    if (interactable != null)
                    {
                        // Check if the item is currently held by the player
                        if (interactable.transform.parent != null) continue;
                        
                        var rb = interactable.GetComponent<UnityEngine.Rigidbody>();
                        if (rb != null && rb.isKinematic) continue;

                        if (isCorrectItem(interactable) && !processedItemIds.Contains(interactable.GetInstanceID()))
                        {
                            processedItemIds.Add(interactable.GetInstanceID());
                            insertItem(interactable);
                            break; // Insert one item per frame for smooth syncing and animations
                        }
                    }
                }
            }
            catch { }
        }

        public override void OnUpdate()
        {
            // Clean up processed IDs periodically (every 5 seconds) to keep memory footprint clean
            cleanupTimer += Time.deltaTime;
            if (cleanupTimer > 5f)
            {
                processedItemIds.Clear();
                cleanupTimer = 0f;
            }

            // Force hoppers active & auto-suck items
            try
            {
                // 1. Metallpresse
                var scrapPresses = UnityEngine.Object.FindObjectsOfType<Scrappress>();
                foreach (var press in scrapPresses)
                {
                    if (press == null) continue;

                    // Force the green field active and visible in OnUpdate
                    bool shouldBeActive = Config.EnableAlwaysInsert || (press.WeightInside < press.totalWeightForBlock);
                    if (shouldBeActive)
                    {
                        ForceTriggerAreaActive(press.scrapInputArea);
                    }

                    if (Config.EnableAutoInsert && press.WeightInside < press.totalWeightForBlock && press.scrapInputArea != null)
                    {
                        if (press.cart != null)
                        {
                            press.InsertFromCart();
                        }
                        else
                        {
                            AutoInsertFromHopper(
                                press.scrapInputArea.transform.position,
                                2.5f,
                                (item) => press.IsCorrectItem(item),
                                (item) => {
                                    press.insertableScrap = item;
                                    press.InsertScrap();
                                }
                            );
                        }
                    }
                }

                // 2. Holzschredder
                var woodShredders = UnityEngine.Object.FindObjectsOfType<WoodShredder>();
                foreach (var shredder in woodShredders)
                {
                    if (shredder == null) continue;

                    bool shouldBeActive = Config.EnableAlwaysInsert || (shredder.WeightInside < shredder.totalWeightForBlock);
                    if (shouldBeActive)
                    {
                        ForceTriggerAreaActive(shredder.scrapInputArea);
                    }

                    if (Config.EnableAutoInsert && shredder.WeightInside < shredder.totalWeightForBlock && shredder.scrapInputArea != null)
                    {
                        if (shredder.cart != null)
                        {
                            shredder.InsertFromCart();
                        }
                        else
                        {
                            AutoInsertFromHopper(
                                shredder.scrapInputArea.transform.position,
                                2.5f,
                                (item) => shredder.IsCorrectItem(item),
                                (item) => {
                                    shredder.insertableScrap = item;
                                    shredder.InsertScrap();
                                }
                            );
                        }
                    }
                }

                // 3. Papierpresse
                var paperBalers = UnityEngine.Object.FindObjectsOfType<PaperBaler>();
                foreach (var baler in paperBalers)
                {
                    if (baler == null) continue;

                    bool shouldBeActive = Config.EnableAlwaysInsert || (baler.WeightInside < baler.totalWeightForBlock);
                    if (shouldBeActive)
                    {
                        ForceTriggerAreaActive(baler.scrapInputArea);
                    }

                    if (Config.EnableAutoInsert && baler.WeightInside < baler.totalWeightForBlock && baler.scrapInputArea != null)
                    {
                        if (baler.cart != null)
                        {
                            baler.InsertFromCart();
                        }
                        else
                        {
                            AutoInsertFromHopper(
                                baler.scrapInputArea.transform.position,
                                2.5f,
                                (item) => baler.IsCorrectItem(item),
                                (item) => {
                                    baler.insertableScrap = item;
                                    baler.InsertScrap();
                                }
                            );
                        }
                    }
                }

                // 4. Plastikpresse
                var plasticBalers = UnityEngine.Object.FindObjectsOfType<PlasticBaler>();
                foreach (var baler in plasticBalers)
                {
                    if (baler == null) continue;

                    bool shouldBeActive = Config.EnableAlwaysInsert || (baler.WeightInside < baler.totalWeightForBlock);
                    if (shouldBeActive)
                    {
                        ForceTriggerAreaActive(baler.scrapInputArea);
                    }

                    if (Config.EnableAutoInsert && baler.WeightInside < baler.totalWeightForBlock && baler.scrapInputArea != null)
                    {
                        if (baler.cart != null)
                        {
                            baler.InsertFromCart();
                        }
                        else
                        {
                            AutoInsertFromHopper(
                                baler.scrapInputArea.transform.position,
                                2.5f,
                                (item) => baler.IsCorrectItem(item),
                                (item) => {
                                    baler.insertableScrap = item;
                                    baler.InsertScrap();
                                }
                            );
                        }
                    }
                }
            }
            catch { }

            // Set compression duration to 0 if InstantCompress is enabled
            if (Config.EnableInstantCompress)
            {
                try
                {
                    var scrapPresses = UnityEngine.Object.FindObjectsOfType<Scrappress>();
                    foreach (var press in scrapPresses)
                    {
                        press.SetCompressionDuration(0);
                    }

                    var woodShredders = UnityEngine.Object.FindObjectsOfType<WoodShredder>();
                    foreach (var shredder in woodShredders)
                    {
                        shredder.SetCompressionDuration(0);
                    }

                    var paperBalers = UnityEngine.Object.FindObjectsOfType<PaperBaler>();
                    foreach (var baler in paperBalers)
                    {
                        baler.SetCompressionDuration(0);
                    }

                    var plasticBalers = UnityEngine.Object.FindObjectsOfType<PlasticBaler>();
                    foreach (var baler in plasticBalers)
                    {
                        baler.SetCompressionDuration(0);
                    }
                }
                catch { }
            }
        }
    }

    // ========================================================================
    // HARMONY PATCHES FOR AUTOMATIC BUFFER
    // ========================================================================
    [HarmonyPatch(typeof(Scrappress), nameof(Scrappress.OnCompressionEnded))]
    public static class Patch_Scrappress_OnCompressionEnded
    {
        private static int excessWeight = 0;

        public static void Prefix(Scrappress __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            excessWeight = Mathf.Max(0, __instance.WeightInside - __instance.totalWeightForBlock);
        }

        public static void Postfix(Scrappress __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            __instance.WeightInside = excessWeight;
        }
    }

    [HarmonyPatch(typeof(WoodShredder), nameof(WoodShredder.OnCompressionEnded))]
    public static class Patch_WoodShredder_OnCompressionEnded
    {
        private static int excessWeight = 0;

        public static void Prefix(WoodShredder __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            excessWeight = Mathf.Max(0, __instance.WeightInside - __instance.totalWeightForBlock);
        }

        public static void Postfix(WoodShredder __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            __instance.WeightInside = excessWeight;
        }
    }

    [HarmonyPatch(typeof(PaperBaler), nameof(PaperBaler.OnCompressionEnded))]
    public static class Patch_PaperBaler_OnCompressionEnded
    {
        private static int excessWeight = 0;

        public static void Prefix(PaperBaler __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            excessWeight = Mathf.Max(0, __instance.WeightInside - __instance.totalWeightForBlock);
        }

        public static void Postfix(PaperBaler __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            __instance.WeightInside = excessWeight;
        }
    }

    [HarmonyPatch(typeof(PlasticBaler), nameof(PlasticBaler.OnCompressionEnded))]
    public static class Patch_PlasticBaler_OnCompressionEnded
    {
        private static int excessWeight = 0;

        public static void Prefix(PlasticBaler __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            excessWeight = Mathf.Max(0, __instance.WeightInside - __instance.totalWeightForBlock);
        }

        public static void Postfix(PlasticBaler __instance)
        {
            if (__instance == null || !Config.EnableBuffer || Config.EnableSmartFill) return;
            __instance.WeightInside = excessWeight;
        }
    }

    // ========================================================================
    // HARMONY PATCHES FOR SMART FILL (ON ITEM INSERTION ONLY)
    // ========================================================================
    [HarmonyPatch(typeof(Scrappress), nameof(Scrappress.InsertScrap), new System.Type[] { })]
    public static class Patch_Scrappress_InsertScrap
    {
        public static void Postfix(Scrappress __instance)
        {
            if (__instance != null && Config.EnableSmartFill)
            {
                __instance.totalWeightInside = __instance.totalWeightForBlock;
                __instance.UpdateRequiredWeightText();
            }
        }
    }

    [HarmonyPatch(typeof(WoodShredder), nameof(WoodShredder.InsertScrap), new System.Type[] { })]
    public static class Patch_WoodShredder_InsertScrap
    {
        public static void Postfix(WoodShredder __instance)
        {
            if (__instance != null && Config.EnableSmartFill)
            {
                __instance.totalWeightInside = __instance.totalWeightForBlock;
                __instance.UpdateRequiredWeightText();
            }
        }
    }

    [HarmonyPatch(typeof(PaperBaler), nameof(PaperBaler.InsertScrap), new System.Type[] { })]
    public static class Patch_PaperBaler_InsertScrap
    {
        public static void Postfix(PaperBaler __instance)
        {
            if (__instance != null && Config.EnableSmartFill)
            {
                __instance.totalWeightInside = __instance.totalWeightForBlock;
                __instance.UpdateRequiredWeightText();
            }
        }
    }

    [HarmonyPatch(typeof(PlasticBaler), nameof(PlasticBaler.InsertScrap), new System.Type[] { })]
    public static class Patch_PlasticBaler_InsertScrap
    {
        public static void Postfix(PlasticBaler __instance)
        {
            if (__instance != null && Config.EnableSmartFill)
            {
                __instance.totalWeightInside = __instance.totalWeightForBlock;
                __instance.UpdateRequiredWeightText();
            }
        }
    }

    // ========================================================================
    // HARMONY PATCHES FOR ALWAYS-OPEN HOPPER / INSERTION TRIGGER
    // ========================================================================
    [HarmonyPatch(typeof(Scrappress), nameof(Scrappress.SetInsertionPossibility))]
    public static class Patch_Scrappress_SetInsertionPossibility
    {
        public static void Prefix(ref bool __0)
        {
            if (Config.EnableAlwaysInsert)
            {
                __0 = true;
            }
        }
    }

    [HarmonyPatch(typeof(WoodShredder), nameof(WoodShredder.SetInsertionPossibility))]
    public static class Patch_WoodShredder_SetInsertionPossibility
    {
        public static void Prefix(ref bool __0)
        {
            if (Config.EnableAlwaysInsert)
            {
                __0 = true;
            }
        }
    }

    [HarmonyPatch(typeof(PaperBaler), nameof(PaperBaler.SetInsertionPossibility))]
    public static class Patch_PaperBaler_SetInsertionPossibility
    {
        public static void Prefix(ref bool __0)
        {
            if (Config.EnableAlwaysInsert)
            {
                __0 = true;
            }
        }
    }

    [HarmonyPatch(typeof(PlasticBaler), nameof(PlasticBaler.SetInsertionPossibility))]
    public static class Patch_PlasticBaler_SetInsertionPossibility
    {
        public static void Prefix(ref bool __0)
        {
            if (Config.EnableAlwaysInsert)
            {
                __0 = true;
            }
        }
    }
}
