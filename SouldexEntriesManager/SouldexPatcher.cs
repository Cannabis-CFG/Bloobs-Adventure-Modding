using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.UI;
using UnityEngine;

namespace SouldexEntriesManager
{
    [BepInPlugin("com.cannabis.souldexentriesmanager", "Souldex Entries Manager", "1.2.2")]
    public class SouldexPatcher : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;
        public static ConfigEntry<int> multiplier;
        public static ConfigEntry<float> windowYSize;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin Souldex Entries Manager is loaded!");
            Harmony.CreateAndPatchAll(typeof(EntriesPatch));
            Harmony.CreateAndPatchAll(typeof(EntriesWindowPatch));
            multiplier = Config.Bind("General", "Multiplier", 10, "Multiplier for souldex max entries");
            windowYSize = Config.Bind("General", "Window Y Size", 15000f,
                "Determines the maximum amount you're allowed to scroll to view auto entries");
        }
    }

    [HarmonyPatch(typeof(SoulCompendiumManager), nameof(SoulCompendiumManager.GetMaxAutoManagedEntries))]
    public static class EntriesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref int __result)
        {
            __result *= SouldexPatcher.multiplier.Value;
        }

    }

    [HarmonyPatch(typeof(SoulCompendiumManager), "OpenAuto")]
    public static class EntriesWindowPatch
    {
        [HarmonyPostfix]
        public static void PatchScroll(SoulCompendiumManager __instance)
        {
            GameObject autoPanel = __instance.autoPanel;

            if (autoPanel == null)
            {
                Debug.LogWarning("[SoulScrollPatch] AutoPanel is null.");
                return;
            }

            ScrollRect scroll = autoPanel.GetComponentInChildren<ScrollRect>(true);
            if (scroll == null || scroll.content == null)
            {
                Debug.LogWarning("[SoulScrollPatch] Target ScrollRect or content is null.");
                return;
            }

            float desiredHeight = SouldexPatcher.windowYSize.Value;

            scroll.content.sizeDelta = new Vector2(
                scroll.content.sizeDelta.x,
                desiredHeight
            );

            //Debug.Log($"[SoulScrollPatch] Successfully updated scroll height to {desiredHeight}");
        }
    }
}


