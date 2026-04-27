using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Nosebleed.Pancake.GameLogic.GameStates;
using Nosebleed.Pancake.View;
using UnityEngine;
using UnityEngine.UI;
using VampireCrawlers.RuntimeMod;

namespace VampireCrawlers.DpsOverlay;

[BepInPlugin("com.local.vampirecrawlers.dpsoverlay", "DPS ??", "0.1.0")]
[BepInDependency(ModCore.RuntimePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static readonly HashSet<int> ActiveEnemies = new();
    private static readonly Dictionary<int, double> LastEnemyHealth = new();
    private static readonly Color TextColor = new(1f, 0.94f, 0.76f, 1f);
    private static ManualLogSource Logger;
    private static ConfigEntry<bool> EnableOverlay;
    private static ConfigEntry<float> BattleLeftRatio;
    private static ConfigEntry<float> BattleRightRatio;
    private static ConfigEntry<float> BattleTopRatio;
    private static ConfigEntry<float> BattleBottomRatio;
    private static ConfigEntry<float> IntentLineRatio;
    private static ConfigEntry<float> PositionXOffset;
    private static ConfigEntry<float> PositionYOffset;
    private static Text _dpsText;
    private static Harmony _harmony;
    private static double _currentTurnDamage;
    private static double _completedTurnDamage;
    private static int _completedTurnCount;
    private static bool _hasStartedTurn;
    private static bool _wasInCombat;

    public override void Load()
    {
        Logger = Log;
        EnableOverlay = Config.Bind("General", "EnableOverlay", true, "?? DPS ???");
        BattleLeftRatio = Config.Bind("Layout", "BattleLeftRatio", 0.205f, "???????? 16:9 ??????????");
        BattleRightRatio = Config.Bind("Layout", "BattleRightRatio", 0.795f, "???????? 16:9 ??????????");
        BattleTopRatio = Config.Bind("Layout", "BattleTopRatio", 0.035f, "???????? 16:9 ??????????");
        BattleBottomRatio = Config.Bind("Layout", "BattleBottomRatio", 0.855f, "???????? 16:9 ??????????");
        IntentLineRatio = Config.Bind("Layout", "IntentLineRatio", 0.17f, "?????????????????");
        PositionXOffset = Config.Bind("Layout", "PositionXOffset", 0f, "DPS ???????????? X ???");
        PositionYOffset = Config.Bind("Layout", "PositionYOffset", 0f, "DPS ?????????? Y ???");
        if (!EnableOverlay.Value)
        {
            Logger.LogInfo("DPS ??????");
            return;
        }

        EnsureCanvas();
        _harmony = new Harmony("com.local.vampirecrawlers.dpsoverlay");
        _harmony.Patch(AccessTools.Method(typeof(EnemyView), "LateUpdate"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterEnemyLateUpdate)));
        _harmony.Patch(AccessTools.Method(typeof(EnemyView), "OnDestroy"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterEnemyDestroyed)));
        _harmony.Patch(AccessTools.Method(typeof(PlayerTurnState), "OnEnterState"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterPlayerTurnStarted)));
        Logger.LogInfo("DPS ????????");
    }

    private static void AfterEnemyLateUpdate(EnemyView __instance)
    {
        if (__instance == null)
        {
            RefreshText();
            return;
        }

        var id = __instance.GetInstanceID();
        var currentHealth = Math.Max(0, __instance.CurrentHealth);
        if (LastEnemyHealth.TryGetValue(id, out var lastHealth))
        {
            AddDamageAmount(Math.Max(0, lastHealth - currentHealth));
        }

        if (__instance.gameObject.activeInHierarchy && !__instance.IsDead)
        {
            ActiveEnemies.Add(id);
            LastEnemyHealth[id] = currentHealth;
            _wasInCombat = true;
        }
        else
        {
            ActiveEnemies.Remove(id);
            LastEnemyHealth.Remove(id);
        }

        RefreshText();
    }

    private static void AfterPlayerTurnStarted()
    {
        if (_hasStartedTurn)
        {
            _completedTurnDamage += _currentTurnDamage;
            _completedTurnCount++;
        }

        _currentTurnDamage = 0;
        _hasStartedTurn = true;
        RefreshText();
    }

    private static void AfterEnemyDestroyed(EnemyView __instance)
    {
        if (__instance != null)
        {
            var id = __instance.GetInstanceID();
            if (LastEnemyHealth.TryGetValue(id, out var lastHealth))
            {
                AddDamageAmount(Math.Max(0, lastHealth - Math.Max(0, __instance.CurrentHealth)));
            }

            ActiveEnemies.Remove(id);
            LastEnemyHealth.Remove(id);
        }

        RefreshText();
    }

    private static void AddDamageAmount(double damage)
    {
        if (damage <= 0)
        {
            return;
        }

        if (!_hasStartedTurn)
        {
            _hasStartedTurn = true;
        }

        _currentTurnDamage += damage;
        RefreshText();
    }

    private static void EnsureCanvas()
    {
        if (_dpsText != null)
        {
            return;
        }

        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var canvas = ModCore.CreateOverlayCanvas("VC_DpsOverlayCanvas", 5000);
        _dpsText = ModCore.CreateOverlayText("DpsText", canvas.transform, font, 24, TextAnchor.UpperRight);
        _dpsText.color = TextColor;

        var rect = _dpsText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(480f, 34f);
        _dpsText.gameObject.SetActive(false);
    }

    private static void RefreshText()
    {
        EnsureCanvas();

        if (ActiveEnemies.Count == 0)
        {
            if (_wasInCombat)
            {
                ResetCombatStats();
            }

            _dpsText.gameObject.SetActive(false);
            return;
        }

        var displayedTurnCount = _completedTurnCount + (_hasStartedTurn ? 1 : 0);
        var displayedTotalDamage = _completedTurnDamage + _currentTurnDamage;
        var average = displayedTurnCount > 0 ? displayedTotalDamage / displayedTurnCount : 0;
        // ?????????? DPS ?????????????????????????
        _dpsText.text = $"????{Format(_currentTurnDamage)}  ???{Format(displayedTotalDamage)}  ?/???{Format(average)}";
        _dpsText.GetComponent<RectTransform>().anchoredPosition = GetIntentLineRightPosition();
        _dpsText.gameObject.SetActive(true);
    }

    private static Vector2 GetIntentLineRightPosition()
    {
        var battleRect = ModCore.GetBattleWindowRect(
            BattleLeftRatio.Value,
            BattleRightRatio.Value,
            BattleTopRatio.Value,
            BattleBottomRatio.Value);
        var y = battleRect.yMax - battleRect.height * IntentLineRatio.Value;
        return new Vector2(battleRect.xMax + PositionXOffset.Value, y + PositionYOffset.Value);
    }

    private static void ResetCombatStats()
    {
        _currentTurnDamage = 0;
        _completedTurnDamage = 0;
        _completedTurnCount = 0;
        ActiveEnemies.Clear();
        LastEnemyHealth.Clear();
        _hasStartedTurn = false;
        _wasInCombat = false;
    }

    private static string Format(double value)
    {
        return value >= 100 ? Math.Ceiling(value).ToString("0") : value.ToString("0.#");
    }
}
