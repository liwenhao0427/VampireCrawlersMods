using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Nosebleed.Pancake.View;
using UnityEngine;
using UnityEngine.UI;
using VampireCrawlers.RuntimeMod;
using Object = UnityEngine.Object;

namespace VampireCrawlers.EnemyHealthOverlay;

[BepInPlugin("com.local.vampirecrawlers.enemyhealth", "??????", "0.2.0")]
[BepInDependency(ModCore.RuntimePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static readonly Dictionary<int, EnemyLabel> Labels = new();
    private static readonly Color PanelColor = new(0.05f, 0.045f, 0.055f, 0.72f);
    private static readonly Color TextColor = new(1f, 0.94f, 0.76f, 1f);
    private static readonly Color MutedTextColor = new(0.76f, 0.71f, 0.64f, 1f);
    private static readonly Color HealthColor = new(0.78f, 0.14f, 0.18f, 1f);
    private static readonly Color HealthBackColor = new(0.15f, 0.08f, 0.08f, 0.9f);
    private static ManualLogSource Logger;
    private static ConfigEntry<bool> EnableOverlay;
    private static ConfigEntry<float> BattleLeftRatio;
    private static ConfigEntry<float> BattleRightRatio;
    private static ConfigEntry<float> BattleTopRatio;
    private static ConfigEntry<float> BattleBottomRatio;
    private static ConfigEntry<float> IntentLineRatio;
    private static ConfigEntry<float> SummaryXOffset;
    private static ConfigEntry<float> SummaryYOffset;
    private static ConfigEntry<float> SingleYOffset;
    private static GameObject _summaryRoot;
    private static Text _summaryTitleText;
    private static Text _summaryFrontText;
    private static Text _summaryTotalText;
    private static RectTransform _summaryFrontFill;
    private static Font _font;
    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        EnableOverlay = Config.Bind("General", "EnableOverlay", true, "?????????");
        BattleLeftRatio = Config.Bind("Layout", "BattleLeftRatio", 0.205f, "???????? 16:9 ??????????");
        BattleRightRatio = Config.Bind("Layout", "BattleRightRatio", 0.795f, "???????? 16:9 ??????????");
        BattleTopRatio = Config.Bind("Layout", "BattleTopRatio", 0.035f, "???????? 16:9 ??????????");
        BattleBottomRatio = Config.Bind("Layout", "BattleBottomRatio", 0.855f, "???????? 16:9 ??????????");
        IntentLineRatio = Config.Bind("Layout", "IntentLineRatio", 0.17f, "?????????????????");
        SummaryXOffset = Config.Bind("Layout", "SummaryXOffset", 0f, "??????????????? X ???");
        SummaryYOffset = Config.Bind("Layout", "SummaryYOffset", 0f, "????????????? Y ???");
        SingleYOffset = Config.Bind("Layout", "SingleEnemyYOffset", -28f, "????????????? Y ???");

        if (!EnableOverlay.Value)
        {
            Logger.LogInfo("??????????");
            return;
        }

        EnsureCanvas();
        _harmony = new Harmony("com.local.vampirecrawlers.enemyhealth");
        _harmony.Patch(AccessTools.Method(typeof(EnemyView), "LateUpdate"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterEnemyLateUpdate)));
        _harmony.Patch(AccessTools.Method(typeof(EnemyView), "OnDestroy"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterEnemyDestroyed)));
        Logger.LogInfo("????????????");
    }

    private static void AfterEnemyLateUpdate(EnemyView __instance)
    {
        RefreshEnemy(__instance);
    }

    private static void AfterEnemyDestroyed(EnemyView __instance)
    {
        RemoveEnemy(__instance);
    }

    private static void RefreshEnemy(EnemyView enemy)
    {
        if (enemy == null)
        {
            return;
        }

        var id = enemy.GetInstanceID();
        if (!Labels.TryGetValue(id, out var label) || label.Root == null)
        {
            label = CreateEnemyLabel(enemy);
            Labels[id] = label;
        }

        var visible = enemy.gameObject.activeInHierarchy && !enemy.IsDead;
        label.View = enemy;
        label.Root.SetActive(visible);
        if (visible)
        {
            var current = Math.Max(0, enemy.CurrentHealth);
            var max = Math.Max(0, enemy.EnemyModel != null ? enemy.EnemyModel.MaxHealth : 0);
            label.Text.text = $"{Format(current)} / {Format(max)}";
            SetFill(label.Fill, current, max, 168f);
            PositionSingleLabel(label);
        }

        RefreshSummary();
    }

    private static void RemoveEnemy(EnemyView enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (Labels.Remove(enemy.GetInstanceID(), out var label) && label.Root != null)
        {
            Object.Destroy(label.Root);
        }

        RefreshSummary();
    }

    private static void EnsureCanvas()
    {
        if (_summaryRoot != null)
        {
            return;
        }

        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var canvas = ModCore.CreateOverlayCanvas("VC_EnemyHealthOverlayCanvas", 5000);
        _summaryRoot = new GameObject("HealthSummaryPanel");
        _summaryRoot.transform.SetParent(canvas.transform, false);
        var image = _summaryRoot.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;

        var rect = _summaryRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 104f);

        _summaryTitleText = CreateText("HealthSummaryTitle", _summaryRoot.transform, 16, TextAnchor.UpperLeft, TextColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(390f, 24f), new Vector2(14f, -10f));
        _summaryFrontText = CreateText("FrontRowHealthText", _summaryRoot.transform, 22, TextAnchor.UpperLeft, TextColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(390f, 30f), new Vector2(14f, -34f));
        _summaryTotalText = CreateText("TotalHealthText", _summaryRoot.transform, 18, TextAnchor.UpperLeft, MutedTextColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(390f, 26f), new Vector2(14f, -64f));
        CreateImage("SummaryHealthBack", _summaryRoot.transform, HealthBackColor, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(392f, 8f), new Vector2(14f, 15f));
        _summaryFrontFill = CreateImage("SummaryHealthFill", _summaryRoot.transform, HealthColor, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 8f), new Vector2(14f, 15f));
        _summaryRoot.SetActive(false);
    }

    private static EnemyLabel CreateEnemyLabel(EnemyView enemy)
    {
        EnsureCanvas();
        var root = new GameObject("EnemyHealthBadge");
        root.transform.SetParent(_summaryRoot.transform.parent, false);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.035f, 0.04f, 0.76f);
        bg.raycastTarget = false;

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(190f, 36f);

        var text = CreateText("EnemyHealthText", root.transform, 16, TextAnchor.MiddleCenter, TextColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(178f, 20f), new Vector2(0f, -4f));
        CreateImage("EnemyHealthBack", root.transform, HealthBackColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(168f, 5f), new Vector2(-84f, 8f));
        var fill = CreateImage("EnemyHealthFill", root.transform, HealthColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 5f), new Vector2(-84f, 8f));
        return new EnemyLabel(root, text, fill, enemy);
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        return ModCore.CreateOverlayText(name, parent, _font, fontSize, alignment);
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
    {
        var text = CreateText(name, parent, fontSize, alignment);
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    private static void PositionSingleLabel(EnemyLabel label)
    {
        var camera = Camera.main;
        if (camera == null || label.View == null)
        {
            label.Root.SetActive(false);
            return;
        }

        // ?????? Text??????????????????????/????????????
        var anchor = label.View.MoveTransform != null ? label.View.MoveTransform.position : label.View.transform.position;
        var screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= 0)
        {
            label.Root.SetActive(false);
            return;
        }

        label.Root.GetComponent<RectTransform>().anchoredPosition = new Vector2(screen.x, screen.y + SingleYOffset.Value);
    }

    private static void RefreshSummary()
    {
        var enemies = Labels.Values
            .Where(label => label.View != null && label.View.gameObject.activeInHierarchy && !label.View.IsDead)
            .Select(label => label.View)
            .ToList();

        if (enemies.Count == 0)
        {
            if (_summaryRoot != null)
            {
                _summaryRoot.SetActive(false);
            }
            return;
        }

        // ??? CurrentRowIndex ??????????? Min ????????
        var frontRow = enemies.Max(enemy => enemy.CurrentRowIndex);
        var frontEnemies = enemies.Where(enemy => enemy.CurrentRowIndex == frontRow).ToList();
        var frontCurrent = frontEnemies.Sum(enemy => Math.Max(0, enemy.CurrentHealth));
        var frontMax = frontEnemies.Sum(enemy => Math.Max(0, enemy.EnemyModel != null ? enemy.EnemyModel.MaxHealth : 0));
        var totalCurrent = enemies.Sum(enemy => Math.Max(0, enemy.CurrentHealth));
        var totalMax = enemies.Sum(enemy => Math.Max(0, enemy.EnemyModel != null ? enemy.EnemyModel.MaxHealth : 0));

        _summaryTitleText.text = "????";
        _summaryFrontText.text = $"?? {Format(frontCurrent)} / {Format(frontMax)}";
        _summaryTotalText.text = $"????? {Format(totalCurrent)} / {Format(totalMax)}   ?? {enemies.Count}";
        SetFill(_summaryFrontFill, frontCurrent, frontMax, 392f);
        _summaryRoot.GetComponent<RectTransform>().anchoredPosition = GetIntentLineLeftPosition();
        _summaryRoot.SetActive(true);
    }

    private static Vector2 GetIntentLineLeftPosition()
    {
        var battleRect = ModCore.GetBattleWindowRect(
            BattleLeftRatio.Value,
            BattleRightRatio.Value,
            BattleTopRatio.Value,
            BattleBottomRatio.Value);
        var y = battleRect.yMax - battleRect.height * IntentLineRatio.Value;
        return new Vector2(battleRect.xMin + SummaryXOffset.Value, y + SummaryYOffset.Value);
    }

    private static string Format(double value)
    {
        return value >= 100 ? Math.Ceiling(value).ToString("0") : value.ToString("0.#");
    }

    private static void SetFill(RectTransform fill, double current, double max, float width)
    {
        var ratio = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0f;
        fill.sizeDelta = new Vector2(width * ratio, fill.sizeDelta.y);
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private sealed class EnemyLabel
    {
        public EnemyLabel(GameObject root, Text text, RectTransform fill, EnemyView view)
        {
            Root = root;
            Text = text;
            Fill = fill;
            View = view;
        }

        public GameObject Root { get; }
        public Text Text { get; }
        public RectTransform Fill { get; }
        public EnemyView View { get; set; }
    }
}
