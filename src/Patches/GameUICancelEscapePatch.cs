using Assets.Behaviour.UI;
using HarmonyLib;

namespace KingdomBetterDeconstruct.Patches
{
    [HarmonyPatch(typeof(GameUI), nameof(GameUI.ProcessCancel))]
    internal static class GameUIProcessCancelPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool Prefix()
        {
            if (DeconstructConfirmModal.IsOpen && DeconstructConfirmModal.Instance != null)
            {
                DeconstructConfirmModal.Instance.Cancel();
                return false;
            }

            if (DeconstructBoxController.IsDragging && DeconstructBoxController.Instance != null)
            {
                DeconstructBoxController.Instance.CancelDrag();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GameUI), nameof(GameUI.ProcessEscape))]
    internal static class GameUIProcessEscapePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool Prefix()
        {
            if (DeconstructConfirmModal.IsOpen && DeconstructConfirmModal.Instance != null)
            {
                DeconstructConfirmModal.Instance.Cancel();
                return false;
            }

            if (DeconstructBoxController.IsDragging && DeconstructBoxController.Instance != null)
            {
                DeconstructBoxController.Instance.CancelDrag();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GameUI), nameof(GameUI.ToggleDeconstructMode), new[] { typeof(bool) })]
    internal static class GameUIToggleDeconstructModePatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool toggleTo)
        {
            if (!toggleTo)
            {
                DeconstructBoxController.Instance?.ResetState();
                if (DeconstructConfirmModal.IsOpen)
                {
                    DeconstructConfirmModal.Instance?.Cancel();
                }
            }
        }
    }
}

