using HarmonyLib;
using osu.Framework.Platform;

namespace Maniac;

static class HarmonyFrameHook
{
    static Harmony? harmony;
    static Action<GameHost>? onFrame;

    public static bool TryAttach(Action<GameHost> callback)
    {
        onFrame = callback;
        try
        {
            harmony = new Harmony("maniac-stub");
            var updateFrame = AccessTools.Method(typeof(GameHost), "UpdateFrame")
                ?? throw new MissingMethodException("GameHost.UpdateFrame");
            harmony.Patch(updateFrame, postfix: new HarmonyMethod(typeof(HarmonyFrameHook), nameof(Postfix)));
            return true;
        }
        catch (Exception ex)
        {
            Session.Log(ex);
            return false;
        }
    }

    static void Postfix(GameHost __instance) => onFrame?.Invoke(__instance);
}
