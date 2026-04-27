using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace VampireCrawlers.RuntimeMod.Patches;

internal static class LifecycleProbePatch
{
    private static ManualLogSource _logger;
    private static RuntimeModConfig _config;

    public static void Apply(Harmony harmony, ManualLogSource logger, RuntimeModConfig config)
    {
        _logger = logger;
        _config = config;

        var target = AccessTools.Method(typeof(object), nameof(ToString));
        if (target is null)
        {
            logger.LogWarning("????????????");
            return;
        }

        // ? reverse-map.md ??????????? patch ?????????????????
        logger.LogInfo($"?????????????????????{FormatMethod(target)}");
    }

    private static string FormatMethod(MethodBase method)
    {
        var typeName = method.DeclaringType != null ? method.DeclaringType.FullName : "<unknown>";
        return $"{typeName}.{method.Name}";
    }

    public static void LogProbe(string message)
    {
        if (_config != null && _config.EnableDebugLogs.Value)
        {
            if (_logger != null)
            {
                _logger.LogInfo($"[??????] {message}");
            }
        }
    }
}
