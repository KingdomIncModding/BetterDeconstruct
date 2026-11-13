using KingdomFoundation.Api;

namespace KingdomBetterDeconstruct.Patches
{
    internal static class GameUIToggleDeconstructModePatch
    {
        internal static void After(GameHookCall call)
        {
            if (!call.Argument<bool>(0))
            {
                DeconstructBoxController.Instance?.ResetState();
                if (DeconstructConfirmModal.IsOpen)
                    DeconstructConfirmModal.Instance?.Cancel();
            }
        }
    }
}
