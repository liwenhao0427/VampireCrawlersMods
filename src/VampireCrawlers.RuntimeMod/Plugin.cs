using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCrawlers.RuntimeMod.Patches;

namespace VampireCrawlers.RuntimeMod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = ModCore.RuntimePluginGuid;
    public const string PluginName = ModCore.RuntimePluginName;
    public const string PluginVersion = ModCore.RuntimePluginVersion;

    internal static ManualLogSource Logger { get; private set; }
    internal static RuntimeModConfig Settings { get; private set; }

    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        Settings = RuntimeModConfig.Bind(Config);

        var compatibility = GameCompatibility.Probe();
        Logger.LogInfo($"{PluginName} {PluginVersion} ????");
        Logger.LogInfo($"??????{compatibility}");

        if (!Settings.EnableMod.Value)
        {
            Logger.LogInfo("????????? false??????????????????");
            return;
        }

        if (!compatibility.IsExpectedBuild && !Settings.AllowUnknownBuild.Value)
        {
            Logger.LogWarning("???????????????????????");
            return;
        }

        _harmony = new Harmony(PluginGuid);
        PatchRegistry.Apply(_harmony, Logger, Settings);
    }
}
