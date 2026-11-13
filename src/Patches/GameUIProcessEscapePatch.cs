using KingdomFoundation.Api;

namespace KingdomBetterDeconstruct.Patches
{
    internal static class GameUIProcessEscapePatch
    {
        internal static void Before(GameHookCall call) => GameUIProcessCancelPatch.Before(call);
    }
}
