using Assets.Behaviour.UI.HUD;
using HarmonyLib;

namespace KingdomBetterDeconstruct.Patches
{
    [HarmonyPatch(typeof(DeconstructOverlay), "Update")]
    internal static class DeconstructOverlayUpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(DeconstructOverlay __instance)
        {
            BetterDeconstructPlugin.EnsureUiInitialized();

            if (DeconstructBoxController.Instance != null &&
                DeconstructBoxController.Instance.HandleOverlayUpdate(__instance))
            {
                return false;
            }

            return true;
        }
    }
}

