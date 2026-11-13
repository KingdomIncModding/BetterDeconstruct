using KingdomFoundation.Api;

namespace KingdomBetterDeconstruct.Patches
{
    internal static class DeconstructOverlayUpdatePatch
    {
        internal static void Before(GameHookCall call)
        {
            if (FoundationApi.UiAccess.DeconstructionMode == GameDeconstructionMode.Hold &&
                !FoundationApi.UiAccess.DeconstructionHeld && !DeconstructConfirmModal.IsOpen)
            {
                DeconstructBoxController.Instance?.ResetState(call.View);
                return;
            }
            BetterDeconstructPlugin.EnsureUiInitialized();
            if (DeconstructBoxController.Instance != null &&
                DeconstructBoxController.Instance.HandleOverlayUpdate(call.View)) call.Cancel();
        }
    }
}
