using KingdomFoundation.Api;

namespace KingdomBetterDeconstruct.Patches
{
    internal static class GameUIProcessCancelPatch
    {
        internal static void Before(GameHookCall call)
        {
            if (DeconstructConfirmModal.IsOpen && DeconstructConfirmModal.Instance != null)
            {
                DeconstructConfirmModal.Instance.Cancel();
                call.Cancel();
                return;
            }

            if (DeconstructBoxController.IsDragging && DeconstructBoxController.Instance != null)
            {
                DeconstructBoxController.Instance.CancelDrag();
                call.Cancel();
            }
        }
    }
}
