using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Nosebleed.Pancake.Achievements;
using Nosebleed.Pancake.GameConfig.Accessors;
using Nosebleed.Pancake.MetaProgression;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UI;
using VampireCrawlers.RuntimeMod;
using Object = UnityEngine.Object;

namespace VampireCrawlers.UnlockController;

[BepInPlugin("com.local.vampirecrawlers.unlockcontroller", "??????", "0.1.0")]
[BepInDependency(ModCore.RuntimePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private const string ControlsName = "VC_UnlockControllerControls";
    private static ManualLogSource Logger;
    private static ConfigEntry<bool> EnableMod;
    private static ConfigEntry<bool> AllowResetAll;
    private static Harmony _harmony;
    private static Font _font;

    public override void Load()
    {
        Logger = Log;
        EnableMod = Config.Bind("General", "EnableUnlockController", true, "?????????????/?????????");
        AllowResetAll = Config.Bind("General", "AllowResetAll", true, "?????????????????????????");
        if (!EnableMod.Value)
        {
            Logger.LogInfo("??????????");
            return;
        }

        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _harmony = new Harmony("com.local.vampirecrawlers.unlockcontroller");
        _harmony.Patch(
            AccessTools.Method(typeof(AchievementsSummaryScreen), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(AchievementsSummaryOnEnablePostfix)));
        _harmony.Patch(
            AccessTools.Method(typeof(AchievementsSummaryScreen), "SelectUnlock"),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(AchievementsSummarySelectUnlockPostfix)));

        Logger.LogInfo("?????????????????????????/?????");
    }

    private static void AchievementsSummaryOnEnablePostfix(AchievementsSummaryScreen __instance)
    {
        EnsureControls(__instance);
        RefreshSelectedText(__instance);
    }

    private static void AchievementsSummarySelectUnlockPostfix(AchievementsSummaryScreen __instance)
    {
        RefreshSelectedText(__instance);
    }

    private static void EnsureControls(AchievementsSummaryScreen screen)
    {
        if (screen == null || screen.transform.Find(ControlsName) != null)
        {
            return;
        }

        var root = new GameObject(ControlsName);
        root.transform.SetParent(screen.transform, false);
        var rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-34f, 108f);
        rect.sizeDelta = new Vector2(230f, 190f);

        var background = root.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.14f, 0.62f);
        AddBorder(root.transform, new Color(1f, 0.72f, 0.22f, 1f), 2f);

        var selectedText = CreateText("SelectedText", root.transform, 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 1f, 1f));
        var selectedRect = selectedText.GetComponent<RectTransform>();
        selectedRect.anchorMin = new Vector2(0f, 1f);
        selectedRect.anchorMax = new Vector2(1f, 1f);
        selectedRect.pivot = new Vector2(0.5f, 1f);
        selectedRect.anchoredPosition = new Vector2(0f, -8f);
        selectedRect.sizeDelta = new Vector2(-14f, 34f);
        selectedText.text = "????";

        CreateButton(root.transform, "????", new Vector2(-59f, 110f), new Vector2(108f, 38f), () => UnlockSelected(screen));
        CreateButton(root.transform, "????", new Vector2(-59f, 68f), new Vector2(108f, 38f), () => ResetSelected(screen));
        CreateButton(root.transform, "????", new Vector2(-59f, 26f), new Vector2(108f, 38f), () => UnlockAll(screen));
        if (AllowResetAll.Value)
        {
            CreateButton(root.transform, "????", new Vector2(59f, 26f), new Vector2(108f, 38f), () => ResetAll(screen));
        }

        Logger.LogInfo("????????????????");
    }

    private static void UnlockSelected(AchievementsSummaryScreen screen)
    {
        var achievement = GetSelectedAchievement(screen);
        if (achievement == null)
        {
            Logger.LogWarning("??????????????");
            return;
        }

        ExecuteForAchievement("????", achievement, manager => manager.DEBUG_ForceUnlockAchievement(achievement), progression => progression.ForceUnlockAchievement(achievement));
        RefreshOriginalScreen(screen);
    }

    private static void ResetSelected(AchievementsSummaryScreen screen)
    {
        var achievement = GetSelectedAchievement(screen);
        if (achievement == null)
        {
            Logger.LogWarning("??????????????");
            return;
        }

        ExecuteForAchievement("????", achievement, manager => manager.DEBUG_ResetAchievement(achievement), progression => progression.DEBUG_ResetAchievement(achievement));
        RefreshOriginalScreen(screen);
    }

    private static void UnlockAll(AchievementsSummaryScreen screen)
    {
        try
        {
            var manager = FindLoadedComponent<AchievementManager>();
            if (manager != null)
            {
                manager.DEBUG_ForceUnlockAllAchievements();
            }
            else
            {
                foreach (var achievement in GetLoadedAchievements())
                {
                    Globals.Progression?.ForceUnlockAchievement(achievement);
                }
            }

            Logger.LogInfo("?????????");
            RefreshOriginalScreen(screen);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"???????{ex.Message}");
        }
    }

    private static void ResetAll(AchievementsSummaryScreen screen)
    {
        try
        {
            var manager = FindLoadedComponent<AchievementManager>();
            foreach (var achievement in GetLoadedAchievements())
            {
                if (manager != null)
                {
                    manager.DEBUG_ResetAchievement(achievement);
                }
                else
                {
                    Globals.Progression?.DEBUG_ResetAchievement(achievement);
                }
            }

            Logger.LogInfo("?????????");
            RefreshOriginalScreen(screen);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"???????{ex.Message}");
        }
    }

    private static void ExecuteForAchievement(string actionName, AchievementConfig achievement, Action<AchievementManager> managerAction, Action<ProgressionData> progressionAction)
    {
        try
        {
            var manager = FindLoadedComponent<AchievementManager>();
            if (manager != null)
            {
                managerAction(manager);
            }
            else if (Globals.Progression != null)
            {
                progressionAction(Globals.Progression);
            }
            else
            {
                Logger.LogWarning($"{actionName}?????? AchievementManager ? ProgressionData?");
                return;
            }

            Logger.LogInfo($"{actionName}???{GetAchievementTitle(achievement)}?");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"{actionName}???{ex.Message}");
        }
    }

    private static AchievementConfig GetSelectedAchievement(AchievementsSummaryScreen screen)
    {
        var field = AccessTools.Field(typeof(AchievementsSummaryScreen), "_selectedItem");
        return field?.GetValue(screen) is AchievementItemView item ? item.AchievementConfig : null;
    }

    private static void RefreshOriginalScreen(AchievementsSummaryScreen screen)
    {
        // ??????????????????????????????? UI ???
        AccessTools.Method(typeof(AchievementsSummaryScreen), "RefreshView")?.Invoke(screen, Array.Empty<object>());
        AccessTools.Method(typeof(AchievementsSummaryScreen), "UpdateDescriptionBox")?.Invoke(screen, Array.Empty<object>());
        RefreshSelectedText(screen);
    }

    private static void RefreshSelectedText(AchievementsSummaryScreen screen)
    {
        var root = screen != null ? screen.transform.Find(ControlsName) : null;
        var label = root != null ? root.Find("SelectedText")?.GetComponent<Text>() : null;
        if (label == null)
        {
            return;
        }

        var achievement = GetSelectedAchievement(screen);
        label.text = achievement == null ? "????" : $"???{TrimTitle(GetAchievementTitle(achievement), 12)}";
    }

    private static List<AchievementConfig> GetLoadedAchievements()
    {
        // ??????????????????????? IL2CPP IReadOnlyList ?? foreach ????
        return Resources.FindObjectsOfTypeAll<AchievementConfig>()
            .Where(achievement => achievement != null && !achievement.IsDisabled && !achievement.IsSilent)
            .Distinct()
            .ToList();
    }

    private static string GetAchievementTitle(AchievementConfig achievement)
    {
        return FirstNonEmpty(GetLocalizedString(achievement.AchievementName), achievement.ID, achievement.AssetId, achievement.name);
    }

    private static string TrimTitle(string title, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length <= maxChars)
        {
            return title;
        }

        return $"{title.Substring(0, maxChars)}...";
    }

    private static Text CreateText(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Action action)
    {
        var buttonObject = new GameObject($"Button_{label}");
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.10f, 0.24f, 0.82f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        AddBorder(buttonObject.transform, new Color(1f, 0.72f, 0.22f, 1f), 2f);

        var text = CreateText("Label", buttonObject.transform, 18, TextAnchor.MiddleCenter, Color.white);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;
    }

    private static void AddBorder(Transform parent, Color color, float thickness)
    {
        var border = parent.gameObject.AddComponent<Outline>();
        border.effectColor = color;
        border.effectDistance = new Vector2(thickness, -thickness);
    }

    private static T FindLoadedComponent<T>() where T : Component
    {
        foreach (var component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null && component.gameObject != null && component.gameObject.scene.IsValid())
            {
                return component;
            }
        }

        return null;
    }

    private static string GetLocalizedString(LocalizedString value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return value.GetLocalizedString();
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"??????????{ex.Message}");
            return null;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "??";
    }
}
