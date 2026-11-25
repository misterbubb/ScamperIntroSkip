using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ScamperIntroSkip;

[BepInPlugin("mrbub.scamper.introskip", "Scamper Intro Skip", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    private static ManualLogSource? logger;
    private Harmony? harmony;

    public override void Load()
    {
        logger = Log;
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "BezbroGames.Scamper");
        var introType = gameAssembly?.GetType("Intro", false);
        if (introType is null)
        {
            Log.LogWarning("Could not find the Intro type. This game version may not be supported.");
            return;
        }

        harmony = new Harmony("mrbub.scamper.introskip");
        PatchElapsedClock(introType, "GetIntroElapsedSeconds");
        var videoStartedMethod = introType.GetMethod(
            "OnVideoStarted",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (videoStartedMethod is null)
        {
            Log.LogWarning("Could not find Intro.OnVideoStarted. This game version may not be supported.");
        }
        else
        {
            harmony.Patch(videoStartedMethod, postfix: new HarmonyMethod(typeof(Plugin), nameof(CompleteIntroVideo)));
            Log.LogInfo("Startup video completion hook installed.");
        }

        Log.LogInfo("Intro timeline skip enabled. Video preparation, startup readiness, and transition preloads remain active.");
    }

    public override bool Unload()
    {
        harmony?.UnpatchSelf();
        return true;
    }

    private void PatchElapsedClock(Type introType, string methodName)
    {
        var method = introType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
        {
            Log.LogWarning("Could not find Intro." + methodName + ".");
            return;
        }

        harmony!.Patch(method, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceIntroClock)));
        Log.LogInfo("Intro clock skip installed: " + methodName + ".");
    }

    private static void AdvanceIntroClock(ref float __result)
    {
        __result = 3600f;
    }

    private static void CompleteIntroVideo(object __instance)
    {
        try
        {
            var complete = AccessTools.Method(__instance.GetType(), "CompleteVideo360Playback");
            if (complete is null)
            {
                logger?.LogWarning("Could not find Intro.CompleteVideo360Playback.");
                return;
            }

            complete.Invoke(__instance, null);
            logger?.LogInfo("Startup video marked complete after playback started.");
        }
        catch (Exception exception)
        {
            logger?.LogWarning("Startup video completion failed: " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}
