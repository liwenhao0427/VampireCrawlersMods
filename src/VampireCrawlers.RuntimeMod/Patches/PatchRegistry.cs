using BepInEx.Logging;
using HarmonyLib;

namespace VampireCrawlers.RuntimeMod.Patches;

internal static class PatchRegistry
{
    public static void Apply(Harmony harmony, ManualLogSource logger, RuntimeModConfig config)
    {
        if (config.EnableLifecycleProbe.Value)
        {
            LifecycleProbePatch.Apply(harmony, logger, config);
        }

        logger.LogInfo("??????????");
    }
}
