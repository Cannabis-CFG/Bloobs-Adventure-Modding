using HarmonyLib;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    [HarmonyPatch(typeof(OfflineProgression), "LateUpdate")]
    public class OfflineProgressionLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(MenuBar), "LateUpdate")]
    public class MenuBarLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(CameraMovement), "LateUpdate")]
    public class CameraMovementLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(MapToggle), "LateUpdate")]
    public class MapToggleLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PrestigeManager), "LateUpdate")]
    public class PrestigeManagerLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(BuildingManager), "LateUpdate")]
    public class BuildingManagerLateUpdatePatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterMovement), "HandleManualInput")]
    public class CharacterMovementHandleManualInputPatch
    {
        static bool Prefix()
        {
            if (ChatSystem.ShouldBlockKeyboardInput)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(TeleportScript), "OnTriggerEnter2D")]
    public class TeleportScriptTeleportPatch
    {
        static bool Prefix(TeleportScript __instance)
        {
            var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterMovement), "IsPointerOverUI")]
    public class CharacterMovementIsPointerOverUiPatch
    {
        static bool Prefix(ref bool __result)
        {
            if (ChatSystem.ShouldBlockGameInput)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterMovement), "Update")]
    public class CharacterMovementUpdatePatch
    {
        static bool Prefix(CharacterMovement __instance)
        {
            var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.CloseAllShops))]
    public class CharacterMovementCloseAllShopsPatch
    {
        static bool Prefix(CharacterMovement __instance)
        {
            var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return false;

            foreach (var shopUi in __instance.shops)
                shopUi.CloseShop();

            return false;
        }
    }

    //TODO Implement syncing of hat/wings
    [HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.SetPlayerWing))]
    public class BloobColourChangeWingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BloobColourChange __instance, int wingIndex)
        {
            var cloneComp = __instance.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return;

            if (MultiplayerPatchPlugin.instance == null)
                return;

            MultiplayerPatchPlugin.instance.OnLocalWingChanged(wingIndex);
        }
    }

    [HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.SetPlayerHat))]
    public class BloobColourChangeHatPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BloobColourChange __instance, int hatIndex)
        {
            var cloneComp = __instance.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return;

            if (MultiplayerPatchPlugin.instance == null)
                return;

            MultiplayerPatchPlugin.instance.OnLocalHatChanged(hatIndex);
        }
    }

    [HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.HideHat))]
    public class BloobColourChangeHideHatPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BloobColourChange __instance)
        {
            var cloneComp = __instance.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return;

            if (MultiplayerPatchPlugin.instance == null)
                return;

            MultiplayerPatchPlugin.instance.OnLocalHatChanged(-1);
        }
    }

    [HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.HideWings))]
    public class BloobColourChangeHideWingsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BloobColourChange __instance)
        {
            var cloneComp = __instance.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
                return;

            if (MultiplayerPatchPlugin.instance == null)
                return;

            MultiplayerPatchPlugin.instance.OnLocalWingChanged(-1);
        }
    }

    [HarmonyPatch(typeof(HomeSteadingSkill), nameof(HomeSteadingSkill.AddXP))]
    public class HomeSteadingSkillAddXpClanPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HomeSteadingSkill __instance, float baseXP, out double __state)
        {
            __state = 0d;

            if (__instance == null)
                return;

            if (baseXP <= 0f || float.IsNaN(baseXP) || float.IsInfinity(baseXP))
                return;

            __state = __instance.HomesteadingXP;
        }

        [HarmonyPostfix]
        private static void Postfix(HomeSteadingSkill __instance, float baseXP, double __state)
        {
            if (__instance == null || MultiplayerPatchPlugin.instance == null)
                return;

            if (baseXP <= 0f || float.IsNaN(baseXP) || float.IsInfinity(baseXP))
                return;

            double gainedXp = __instance.HomesteadingXP - __state;
            if (gainedXp <= 0d || double.IsNaN(gainedXp) || double.IsInfinity(gainedXp))
                return;

            MultiplayerPatchPlugin.instance.ReportClanSkillXpAction("Homesteading", gainedXp);
        }
    }


    [HarmonyPatch(typeof(Tracker), nameof(Tracker.OnBossDeath))]
    public class TrackerBossDeathPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string npcName)
        {
            if (MultiplayerPatchPlugin.instance == null)
                return;

            if (string.IsNullOrWhiteSpace(npcName))
                return;

            MultiplayerPatchPlugin.instance.ReportBossKill(npcName, 1);
        }
    }

    public class IsMultiplayerClone : MonoBehaviour
    {
        public string steamId;
        public string displayName;
    }

    /*public class Billboard : MonoBehaviour
    {
        void Update()
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }*/
}
