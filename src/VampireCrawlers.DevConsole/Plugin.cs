using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Nosebleed.Pancake.Achievements;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.GameConfig.Accessors;
using Nosebleed.Pancake.GameCommands;
using Nosebleed.Pancake.GameLogic;
using Nosebleed.Pancake.InputSystem;
using Nosebleed.Pancake.LevelSelect;
using Nosebleed.Pancake.Modal;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.Shops.Arcana;
using Nosebleed.Pancake.Shops.Blacksmith;
using Nosebleed.Pancake.Shops.Crawlers;
using Nosebleed.Pancake.View;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Localization;
using UnityEngine.UI;
using VampireCrawlers.RuntimeMod;
using Object = UnityEngine.Object;
using GameInputManager = Nosebleed.Pancake.InputSystem.InputManager;

namespace VampireCrawlers.DevConsole;

[BepInPlugin("com.local.vampirecrawlers.devconsole", "??????", "0.1.0")]
[BepInDependency(ModCore.RuntimePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private const int CommandsPerPage = 7;
    private const int StatRowsPerPage = 7;
    private const int SelectorRowsPerPage = 8;

    private static ManualLogSource Logger;
    private static ConfigEntry<bool> EnableConsole;
    private static ConfigEntry<int> SmallMoneyAmount;
    private static ConfigEntry<int> LargeMoneyAmount;
    private static GameObject _root;
    private static Text _statusText;
    private static Text _pageText;
    private static Transform _commandParent;
    private static Font _commandFont;
    private static readonly List<GameObject> CommandRows = new();
    private static readonly List<ConsoleCommand> Commands = new();
    private static readonly List<ConsoleCommand> VisibleCommands = new();
    private static readonly List<GameObject> StatRows = new();
    private static readonly List<GameObject> SelectorRows = new();
    private static readonly List<StatEditorEntry> StatEntries = new();
    private static readonly List<SelectorEntry> SelectorEntries = new();
    private static readonly List<SelectorEntry> FilteredSelectorEntries = new();
    private static int _pageIndex;
    private static int _statPageIndex;
    private static int _selectorPageIndex;
    private static bool _visible;
    private static bool _hotkeyFailureLogged;
    private static Harmony _harmony;
    private static GameObject _statEditorRoot;
    private static GameObject _selectorRoot;
    private static GameObject _statLevelButton;
    private static GameObject _statXpAddButton;
    private static GameObject _statXpSubtractButton;
    private static Text _statPageText;
    private static Text _selectorTitleText;
    private static Text _selectorPageText;
    private static Text _selectorCostFilterText;
    private static int? _selectorCostFilter;

    public override void Load()
    {
        Logger = Log;
        EnableConsole = Config.Bind("General", "EnableConsole", true, "??? ~ ??????????");
        SmallMoneyAmount = Config.Bind("Commands", "SmallMoneyAmount", 1000, "?????????????");
        LargeMoneyAmount = Config.Bind("Commands", "LargeMoneyAmount", 10000, "?????????????");
        if (!EnableConsole.Value)
        {
            Logger.LogInfo("??????????");
            return;
        }

        EnsureConsole();
        SetVisible(false);

        _harmony = new Harmony("com.local.vampirecrawlers.devconsole");
        _harmony.Patch(AccessTools.Method(typeof(GameInputManager), "Update"), postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterInputManagerUpdate)));
        Logger.LogInfo("??????????? ` / ~ / ? ????????????");
    }

    private static void AfterInputManagerUpdate()
    {
        if (IsConsoleHotkeyPressed())
        {
            ToggleConsole();
        }
    }

    private static bool IsConsoleHotkeyPressed()
    {
        try
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            // ???? Unity ? Input System????? UnityEngine.Input?????????????? OEM3?
            // ??????????????? `?~ ? ? ????
            return WasPressed(keyboard.backquoteKey)
                || WasPressed(keyboard[Key.Backquote])
                || WasPressed(keyboard[Key.OEM3]);
        }
        catch (Exception ex)
        {
            if (!_hotkeyFailureLogged)
            {
                _hotkeyFailureLogged = true;
                Logger.LogWarning($"??????????????????{ex.Message}");
            }

            return false;
        }
    }

    private static bool WasPressed(ButtonControl control)
    {
        return control != null && control.wasPressedThisFrame;
    }

    private static void ToggleConsole()
    {
        EnsureConsole();
        SetVisible(!_visible);
    }

    private static void EnsureConsole()
    {
        if (_root != null)
        {
            return;
        }

        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var canvas = ModCore.CreateOverlayCanvas("VC_DevConsoleCanvas", 8000);
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        _root = new GameObject("VC_DevConsoleRoot");
        _root.transform.SetParent(canvas.transform, false);
        _root.SetActive(false);
        var rootRect = _root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var blocker = _root.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.35f);

        var panel = CreatePanel(_root.transform);
        CreateHeader(panel.transform, font);
        _statusText = CreateStatusText(panel.transform, font);
        CreateCommandRows(panel.transform, font);
        CreatePageControls(panel.transform, font);
        CreateCloseButton(panel.transform, font);
        CreateStatEditor(_root.transform, font);
        CreateSelector(_root.transform, font);
    }

    private static GameObject CreatePanel(Transform parent)
    {
        var panel = new GameObject("ConsolePanel");
        panel.transform.SetParent(parent, false);
        var image = panel.AddComponent<Image>();
        image.color = new Color(0.28f, 0.31f, 0.48f, 0.97f);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(980f, 760f);

        AddBorder(panel.transform, new Color(1f, 0.72f, 0.22f, 1f), 4f);
        return panel;
    }

    private static void CreateHeader(Transform parent, Font font)
    {
        var title = CreateText("Header", parent, font, 30, TextAnchor.MiddleCenter, Color.white);
        var rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(-60f, 56f);
        title.text = "??????";
    }

    private static Text CreateStatusText(Transform parent, Font font)
    {
        var text = CreateText("Status", parent, font, 20, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 1f, 1f));
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -86f);
        rect.sizeDelta = new Vector2(-96f, 44f);
        return text;
    }

    private static void CreateCommandRows(Transform parent, Font font)
    {
        _commandParent = parent;
        _commandFont = font;
        Commands.Clear();
        Commands.AddRange(new[]
        {
            new ConsoleCommand(CommandScope.InRun, "??", "?????", "??????????????????????????", RestorePlayerHealth),
            new ConsoleCommand(CommandScope.InRun, "??", "????????", "????????????????????????", OpenStatEditor),
            new ConsoleCommand(CommandScope.InRun, "??", "????/?????", "????????????????????????????????", OpenCardSelector),
            new ConsoleCommand(CommandScope.OutOfRun, "??", $"?? {SmallMoneyAmount.Value} ??", "??????????????", () => AddMoney(SmallMoneyAmount.Value)),
            new ConsoleCommand(CommandScope.OutOfRun, "??", $"?? {LargeMoneyAmount.Value} ??", "???????????", () => AddMoney(LargeMoneyAmount.Value)),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "??????????????????????", UnlockAllAchievements),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "????????????????????", UnlockRelicProgression),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "???????", "???????????????????????", ResetCardGemSlots),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "???????", "????????????????????", ResetGemFrequencies),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "?????????", "?????????????????????", UnlockAndDiscoverCards),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "??????????????", UnlockAllGems),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "?????????????????", AddCharacterSlot),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "?????????????????", UnlockCrawlerAchievements),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "???????????", "????????????????????", UnlockArcanaBuilding),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "?????????????????", UnlockArcanaAchievements),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "???????", "?????????????????", UnlockJewellerRelic),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "?????????????????", RefreshAllLevels),
            new ConsoleCommand(CommandScope.OutOfRun, "??", "??????", "?????????????????", UnlockDungeonAchievements),
        });

        _pageIndex = 0;
        RebuildCommandRows();
    }

    private static void RebuildCommandRows()
    {
        foreach (var row in CommandRows)
        {
            if (row != null)
            {
                row.SetActive(false);
                Object.Destroy(row);
            }
        }

        CommandRows.Clear();
        RefreshVisibleCommands();
        var pageStart = _pageIndex * CommandsPerPage;
        var pageEnd = Math.Min(pageStart + CommandsPerPage, VisibleCommands.Count);
        for (var commandIndex = pageStart; commandIndex < pageEnd; commandIndex++)
        {
            var command = VisibleCommands[commandIndex];
            var rowIndex = commandIndex - pageStart;
            // ????????????????? UI ??????????????
            CommandRows.Add(CreateCommandRow(_commandParent, _commandFont, rowIndex, command.IconText, command.Title, command.Description, command.Action));
        }

        UpdatePageText();
    }

    private static void RefreshVisibleCommands()
    {
        VisibleCommands.Clear();
        var scope = IsInRun() ? CommandScope.InRun : CommandScope.OutOfRun;
        VisibleCommands.AddRange(Commands.Where(command => command.Scope == scope));
        var maxPageIndex = Math.Max(0, (VisibleCommands.Count - 1) / CommandsPerPage);
        _pageIndex = Math.Min(_pageIndex, maxPageIndex);
    }

    private static GameObject CreateCommandRow(Transform parent, Font font, int index, string iconText, string title, string description, Action action)
    {
        var row = new GameObject($"CommandRow_{index}");
        row.transform.SetParent(parent, false);
        row.AddComponent<Image>().color = new Color(0.19f, 0.21f, 0.34f, 0.96f);

        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -142f - index * 64f);
        rowRect.sizeDelta = new Vector2(-96f, 54f);

        var icon = CreateText("Icon", row.transform, font, 24, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.35f, 1f));
        icon.text = iconText;
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(44f, 0f);
        iconRect.sizeDelta = new Vector2(50f, 50f);
        AddBorder(icon.transform, new Color(1f, 0.72f, 0.22f, 1f), 2f);

        var label = CreateText("Label", row.transform, font, 24, TextAnchor.MiddleLeft, Color.white);
        label.text = title;
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(90f, 8f);
        labelRect.sizeDelta = new Vector2(-320f, 30f);

        var detail = CreateText("Description", row.transform, font, 17, TextAnchor.MiddleLeft, new Color(0.82f, 0.84f, 0.94f, 1f));
        detail.text = description;
        var detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0.5f);
        detailRect.anchorMax = new Vector2(1f, 0.5f);
        detailRect.pivot = new Vector2(0f, 0.5f);
        detailRect.anchoredPosition = new Vector2(90f, -15f);
        detailRect.sizeDelta = new Vector2(-320f, 24f);

        CreateButton(row.transform, font, "??", new Vector2(-72f, 0f), new Vector2(120f, 42f), action);
        return row;
    }

    private static void CreatePageControls(Transform parent, Font font)
    {
        CreateButton(parent, font, "???", new Vector2(-170f, 92f), new Vector2(130f, 42f), PreviousPage, true);
        CreateButton(parent, font, "???", new Vector2(170f, 92f), new Vector2(130f, 42f), NextPage, true);

        _pageText = CreateText("PageText", parent, font, 18, TextAnchor.MiddleCenter, new Color(0.9f, 0.91f, 1f, 1f));
        var rect = _pageText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 92f);
        rect.sizeDelta = new Vector2(220f, 34f);
        UpdatePageText();
    }

    private static void PreviousPage()
    {
        var pageCount = GetPageCount();
        _pageIndex = (_pageIndex + pageCount - 1) % pageCount;
        RebuildCommandRows();
    }

    private static void NextPage()
    {
        var pageCount = GetPageCount();
        _pageIndex = (_pageIndex + 1) % pageCount;
        RebuildCommandRows();
    }

    private static int GetPageCount()
    {
        return Math.Max(1, (VisibleCommands.Count + CommandsPerPage - 1) / CommandsPerPage);
    }

    private static void UpdatePageText()
    {
        if (_pageText != null)
        {
            _pageText.text = $"? {_pageIndex + 1} / {GetPageCount()} ?";
        }
    }

    private static void CreateCloseButton(Transform parent, Font font)
    {
        CreateButton(parent, font, "??", new Vector2(0f, 30f), new Vector2(180f, 46f), () => SetVisible(false), true);
    }

    private static void CreateStatEditor(Transform parent, Font font)
    {
        _statEditorRoot = CreateSubPanel(parent, "StatEditorPanel", new Vector2(1060f, 820f));
        CreatePanelTitle(_statEditorRoot.transform, font, "??????");
        CreateTextBlock(_statEditorRoot.transform, font, "StatHint", "???????? 1???????????????????", -84f, 18);
        _statLevelButton = CreateButton(_statEditorRoot.transform, font, "??+1", new Vector2(-330f, 288f), new Vector2(112f, 40f), AddOneLevel);
        _statXpAddButton = CreateButton(_statEditorRoot.transform, font, "??+1", new Vector2(-210f, 288f), new Vector2(112f, 40f), AddOneXp);
        _statXpSubtractButton = CreateButton(_statEditorRoot.transform, font, "??-1", new Vector2(-90f, 288f), new Vector2(112f, 40f), SubtractOneXp);
        CreateButton(_statEditorRoot.transform, font, "???", new Vector2(-210f, 84f), new Vector2(130f, 42f), PreviousStatPage, true);
        CreateButton(_statEditorRoot.transform, font, "???", new Vector2(210f, 84f), new Vector2(130f, 42f), NextStatPage, true);
        CreateButton(_statEditorRoot.transform, font, "??", new Vector2(0f, 28f), new Vector2(180f, 46f), CloseStatEditor, true);
        _statPageText = CreateText("StatPageText", _statEditorRoot.transform, font, 18, TextAnchor.MiddleCenter, Color.white);
        var pageRect = _statPageText.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0f);
        pageRect.anchorMax = new Vector2(0.5f, 0f);
        pageRect.pivot = new Vector2(0.5f, 0.5f);
        pageRect.anchoredPosition = new Vector2(0f, 84f);
        pageRect.sizeDelta = new Vector2(220f, 34f);

        _statEditorRoot.SetActive(false);
    }

    private static void CreateSelector(Transform parent, Font font)
    {
        _selectorRoot = CreateSubPanel(parent, "CardSelectorPanel", new Vector2(1120f, 860f));
        _selectorTitleText = CreatePanelTitle(_selectorRoot.transform, font, "??/?????");
        CreateTextBlock(_selectorRoot.transform, font, "SelectorHint", "??????????????????????????????", -84f, 18);
        CreateSelectorFilters(_selectorRoot.transform, font);
        CreateButton(_selectorRoot.transform, font, "???", new Vector2(-230f, 84f), new Vector2(130f, 42f), PreviousSelectorPage, true);
        CreateButton(_selectorRoot.transform, font, "???", new Vector2(230f, 84f), new Vector2(130f, 42f), NextSelectorPage, true);
        CreateButton(_selectorRoot.transform, font, "??", new Vector2(0f, 28f), new Vector2(180f, 46f), CloseSelector, true);
        _selectorPageText = CreateText("SelectorPageText", _selectorRoot.transform, font, 18, TextAnchor.MiddleCenter, Color.white);
        var pageRect = _selectorPageText.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0f);
        pageRect.anchorMax = new Vector2(0.5f, 0f);
        pageRect.pivot = new Vector2(0.5f, 0.5f);
        pageRect.anchoredPosition = new Vector2(0f, 84f);
        pageRect.sizeDelta = new Vector2(260f, 34f);

        _selectorRoot.SetActive(false);
    }

    private static void CreateSelectorFilters(Transform parent, Font font)
    {
        _selectorCostFilterText = CreateText("SelectorCostFilterText", parent, font, 17, TextAnchor.MiddleLeft, new Color(0.9f, 0.91f, 1f, 1f));
        var costTextRect = _selectorCostFilterText.GetComponent<RectTransform>();
        costTextRect.anchorMin = new Vector2(0f, 1f);
        costTextRect.anchorMax = new Vector2(0f, 1f);
        costTextRect.pivot = new Vector2(0f, 0.5f);
        costTextRect.anchoredPosition = new Vector2(48f, -124f);
        costTextRect.sizeDelta = new Vector2(118f, 32f);

        CreateSelectorFilterButton(parent, font, "??", new Vector2(170f, -124f), new Vector2(72f, 34f), () => SetSelectorCostFilter(null));
        for (var cost = 0; cost <= 5; cost++)
        {
            var capturedCost = cost;
            CreateSelectorFilterButton(parent, font, $"{cost}?", new Vector2(252f + cost * 72f, -124f), new Vector2(64f, 34f), () => SetSelectorCostFilter(capturedCost));
        }

        CreateSelectorFilterButton(parent, font, "??", new Vector2(678f, -124f), new Vector2(72f, 34f), ClearSelectorFilters);
        UpdateSelectorCostFilterText();
    }

    private static void CreateSelectorFilterButton(Transform parent, Font font, string label, Vector2 position, Vector2 size, Action action)
    {
        var button = CreateButton(parent, font, label, position, size, action);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject CreateSubPanel(Transform parent, string name, Vector2 size)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        panel.AddComponent<Image>().color = new Color(0.28f, 0.31f, 0.48f, 0.985f);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        AddBorder(panel.transform, new Color(1f, 0.72f, 0.22f, 1f), 4f);
        return panel;
    }

    private static Text CreatePanelTitle(Transform parent, Font font, string titleText)
    {
        var title = CreateText("Title", parent, font, 30, TextAnchor.MiddleCenter, Color.white);
        title.text = titleText;
        var rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -22f);
        rect.sizeDelta = new Vector2(-70f, 54f);
        return title;
    }

    private static Text CreateTextBlock(Transform parent, Font font, string name, string content, float y, int fontSize)
    {
        var text = CreateText(name, parent, font, fontSize, TextAnchor.MiddleLeft, new Color(0.9f, 0.91f, 1f, 1f));
        text.text = content;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-96f, 36f);
        return text;
    }

    private static GameObject CreateButton(Transform parent, Font font, string label, Vector2 position, Vector2 size, Action action, bool bottom = false)
    {
        var buttonObject = new GameObject($"Button_{label}");
        buttonObject.transform.SetParent(parent, false);
        buttonObject.AddComponent<Image>().color = new Color(0.12f, 0.28f, 0.82f, 1f);
        // IL2CPP ? UnityEvent ???? Il2CppInterop ???? C# ???
        buttonObject.AddComponent<Button>().onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = bottom ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.5f);
        rect.anchorMax = bottom ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        AddBorder(buttonObject.transform, new Color(1f, 0.72f, 0.22f, 1f), 2f);

        var text = CreateText("Text", buttonObject.transform, font, 22, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return buttonObject;
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        var text = ModCore.CreateOverlayText(name, parent, font, size, anchor);
        text.color = color;
        return text;
    }

    private static void AddBorder(Transform parent, Color color, float thickness)
    {
        CreateBorderPart(parent, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, thickness));
        CreateBorderPart(parent, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness));
        CreateBorderPart(parent, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f));
        CreateBorderPart(parent, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f));
    }

    private static void CreateBorderPart(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var part = new GameObject($"Border_{name}");
        part.transform.SetParent(parent, false);
        part.AddComponent<Image>().color = color;
        var rect = part.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AddMoney(int configuredAmount)
    {
        try
        {
            var amount = Math.Max(1, configuredAmount);
            var progression = Globals.Progression;
            var coins = progression != null ? progression.TotalCoins : null;
            if (coins == null)
            {
                Logger.LogWarning("????????????? Progression.TotalCoins?");
                RefreshStatus("??????????????");
                return;
            }

            coins.ModifyValue(amount);
            Logger.LogInfo($"?????? {amount} ???");
            RefreshStatus($"??? {amount} ???");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"?????????{ex.Message}");
            RefreshStatus($"??????{ex.Message}");
        }
    }

    private static void RestorePlayerHealth()
    {
        try
        {
            var player = FindLoadedComponent<PlayerModel>();
            var health = player != null && player.PlayerStats != null ? player.PlayerStats.Health : null;
            if (health == null)
            {
                RefreshStatus("???????????????");
                Logger.LogWarning("????????????? PlayerModel ? Health?");
                return;
            }

            var maxHealth = Math.Max(1f, health.GetMaxValue());
            health.SetValue(maxHealth);
            player.CheckHealthForDeath(maxHealth);
            RefreshStatus($"?????? {FormatNumber(maxHealth)}?");
            Logger.LogInfo($"?????????? {maxHealth}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"???????{ex.Message}");
            Logger.LogWarning($"??????????{ex.Message}");
        }
    }

    private static void OpenStatEditor()
    {
        BuildStatEntries();
        _statPageIndex = 0;
        RebuildStatRows();
        _statEditorRoot.SetActive(true);
        _selectorRoot.SetActive(false);
        RefreshStatus("??????????");
    }

    private static void CloseStatEditor()
    {
        _statEditorRoot.SetActive(false);
        RefreshStatus("???????");
    }

    private static void BuildStatEntries()
    {
        StatEntries.Clear();
        StatEntries.Add(new StatEditorEntry("??", "Health", stats => stats.Health, 10f));
        StatEntries.Add(new StatEditorEntry("??", "Armor", stats => stats.Armor, 5f));
        StatEntries.Add(new StatEditorEntry("??", "Might", stats => stats.Might, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Amount", stats => stats.Amount, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Area", stats => stats.Area, 1f));
        StatEntries.Add(new StatEditorEntry("????", "Growth", stats => stats.Growth, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Greed", stats => stats.Greed, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Luck", stats => stats.Luck, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Recovery", stats => stats.Recovery, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Mana", stats => stats.Mana, 1f));
        StatEntries.Add(new StatEditorEntry("???", "DealCount", stats => stats.DealCount, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Magnet", stats => stats.Magnet, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Duration", stats => stats.Duration, 1f));
        StatEntries.Add(new StatEditorEntry("????", "Revival", stats => stats.RevivalCharge, 1f));
        StatEntries.Add(new StatEditorEntry("????", "CardSkips", stats => stats.CardSkips, 1f));
        StatEntries.Add(new StatEditorEntry("????", "Rerolls", stats => stats.Rerolls, 1f));
        StatEntries.Add(new StatEditorEntry("????", "Banishes", stats => stats.Banishes, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Curse", stats => stats.Curse, 1f));
        StatEntries.Add(new StatEditorEntry("??", "Charm", stats => stats.Charm, 1f));
    }

    private static void RebuildStatRows()
    {
        ClearRows(StatRows);
        var player = FindLoadedComponent<PlayerModel>();
        var stats = player != null ? player.PlayerStats : null;
        var inCombat = IsInCombat(player);
        SetStatCombatControlsVisible(inCombat);
        var start = _statPageIndex * StatRowsPerPage;
        var end = Math.Min(start + StatRowsPerPage, StatEntries.Count);

        for (var i = start; i < end; i++)
        {
            StatRows.Add(CreateStatRow(_statEditorRoot.transform, _commandFont, i - start, StatEntries[i], stats, inCombat));
        }

        UpdateStatPageText(player);
    }

    private static GameObject CreateStatRow(Transform parent, Font font, int rowIndex, StatEditorEntry entry, PlayerStats stats, bool inCombat)
    {
        var row = new GameObject($"StatRow_{rowIndex}");
        row.transform.SetParent(parent, false);
        row.AddComponent<Image>().color = new Color(0.19f, 0.21f, 0.34f, 0.96f);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -134f - rowIndex * 72f);
        rect.sizeDelta = new Vector2(-96f, 62f);

        var stat = stats != null ? entry.Resolve(stats) : null;
        var currentValue = stat != null ? FormatNumber(stat.Value) : "???";
        var baseValue = stats != null ? FormatNumber(GetDisplayedBaseStat(stats, entry, stat)) : "???";
        var label = CreateText("StatLabel", row.transform, font, 21, TextAnchor.MiddleLeft, Color.white);
        label.text = inCombat
            ? $"{entry.DisplayName}  ?? {currentValue}"
            : $"{entry.DisplayName}  ?? {baseValue}";
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(26f, 0f);
        labelRect.sizeDelta = new Vector2(-520f, 44f);

        if (inCombat)
        {
            CreateButton(row.transform, font, "??-", new Vector2(-170f, 0f), new Vector2(92f, 40f), () => AdjustCurrentStat(entry, -entry.Step));
            CreateButton(row.transform, font, "??+", new Vector2(-70f, 0f), new Vector2(92f, 40f), () => AdjustCurrentStat(entry, entry.Step));
        }
        else
        {
            CreateButton(row.transform, font, "??-", new Vector2(-170f, 0f), new Vector2(92f, 40f), () => AdjustBaseStat(entry, -entry.Step));
            CreateButton(row.transform, font, "??+", new Vector2(-70f, 0f), new Vector2(92f, 40f), () => AdjustBaseStat(entry, entry.Step));
        }

        return row;
    }

    private static void SetStatCombatControlsVisible(bool visible)
    {
        _statLevelButton?.SetActive(visible);
        _statXpAddButton?.SetActive(visible);
        _statXpSubtractButton?.SetActive(visible);
    }

    private static void PreviousStatPage()
    {
        _statPageIndex = (_statPageIndex + GetStatPageCount() - 1) % GetStatPageCount();
        RebuildStatRows();
    }

    private static void NextStatPage()
    {
        _statPageIndex = (_statPageIndex + 1) % GetStatPageCount();
        RebuildStatRows();
    }

    private static int GetStatPageCount()
    {
        return Math.Max(1, (StatEntries.Count + StatRowsPerPage - 1) / StatRowsPerPage);
    }

    private static void UpdateStatPageText(PlayerModel player)
    {
        if (_statPageText == null)
        {
            return;
        }

        if (IsInCombat(player))
        {
            var levelText = player != null ? player.PlayerLevel.ToString() : "???";
            var xpText = player != null ? $"{player.PlayerXp}/{player.MaxXp}" : "???";
            _statPageText.text = $"? {_statPageIndex + 1} / {GetStatPageCount()} ?  |  ???  |  ?? {levelText}  |  ?? {xpText}";
            return;
        }

        _statPageText.text = $"? {_statPageIndex + 1} / {GetStatPageCount()} ?  |  ???????????";
    }

    private static void AdjustCurrentStat(StatEditorEntry entry, float delta)
    {
        try
        {
            var player = FindLoadedComponent<PlayerModel>();
            var stat = player != null && player.PlayerStats != null ? entry.Resolve(player.PlayerStats) : null;
            if (stat == null)
            {
                RefreshStatus("???????????????");
                return;
            }

            stat.SetValue(Math.Max(1f, stat.Value + delta));
            player.SetStatsDirty();
            RebuildStatRows();
            RefreshStatus($"?????{entry.DisplayName}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"?????????{ex.Message}");
            Logger.LogWarning($"????????????{ex.Message}");
        }
    }

    private static void AdjustBaseStat(StatEditorEntry entry, float delta)
    {
        try
        {
            var player = FindLoadedComponent<PlayerModel>();
            if (player == null || player.PlayerStats == null)
            {
                RefreshStatus("?????????????????");
                return;
            }

            var stat = entry.Resolve(player.PlayerStats);
            var currentBase = stat != null ? stat.Value : GetBaseStat(player.PlayerStats, entry.StatName);
            var newBase = Math.Max(1f, currentBase + delta);
            SetBaseStat(player.PlayerStats, entry.StatName, newBase);
            if (stat != null)
            {
                // ???? GetBaseStat ?????????????? 0?
                // ?????????????????????????????????????
                stat.SetValue(newBase);
                if (string.Equals(entry.StatName, "Health", StringComparison.Ordinal))
                {
                    stat.SetMax(newBase);
                    player.CheckHealthForDeath(newBase);
                }
            }
            NotifyPlayerStatsChanged(player.PlayerStats);
            player.SetStatsDirty();
            RebuildStatRows();
            RefreshStatus($"?????{entry.DisplayName}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"?????????{ex.Message}");
            Logger.LogWarning($"????????????{ex.Message}");
        }
    }

    private static void AddOneLevel()
    {
        ExecuteOnPlayer("??+1", player =>
        {
            if (!player.IsInEncounter)
            {
                throw new InvalidOperationException("?????????????");
            }

            player.LevelUp();
            TryOpenLevelUpChoiceModal(player);
        });
        RebuildStatRows();
    }

    private static void AddOneXp()
    {
        ExecuteOnPlayer("??+1", player => player.AddOneXp());
        RebuildStatRows();
    }

    private static void SubtractOneXp()
    {
        ExecuteOnPlayer("??-1", player => player.SubtractOneXp());
        RebuildStatRows();
    }

    private static void OpenCardSelector()
    {
        var player = FindLoadedComponent<PlayerModel>();
        if (player == null || !player.IsInEncounter)
        {
            RefreshStatus("??????????????");
            Logger.LogWarning("?????????????????????");
            return;
        }

        BuildSelectorEntries();
        ApplySelectorFilters();
        _selectorPageIndex = 0;
        RebuildSelectorRows();
        _selectorRoot.SetActive(true);
        _statEditorRoot.SetActive(false);
        RefreshStatus($"???????? {FilteredSelectorEntries.Count} / {SelectorEntries.Count} ??");
    }

    private static void CloseSelector()
    {
        _selectorRoot.SetActive(false);
        RefreshStatus("???????");
    }

    private static void BuildSelectorEntries()
    {
        SelectorEntries.Clear();
        // ??????? Unity ??????????????????????
        // ???????? PlayerModel / GemInsertionSequence?????????????????
        foreach (var card in Resources.FindObjectsOfTypeAll<CardConfig>())
        {
            if (card != null && card.addToRewardPool)
            {
                var title = GetAssetTitle(card);
                SelectorEntries.Add(SelectorEntry.ForCard(
                    card,
                    title,
                    () => AddCardToPlayerHand(card)));
            }
        }

        foreach (var gem in Resources.FindObjectsOfTypeAll<GemConfig>())
        {
            if (gem != null && !gem.IsDisabled)
            {
                var title = GetAssetTitle(gem);
                SelectorEntries.Add(SelectorEntry.ForGem(
                    gem,
                    title,
                    () => ReceiveGem(gem)));
            }
        }

        SelectorEntries.Sort((left, right) => string.Compare($"{left.Kind}:{left.Title}", $"{right.Kind}:{right.Title}", StringComparison.OrdinalIgnoreCase));
    }

    private static void RebuildSelectorRows()
    {
        ClearRows(SelectorRows);
        ClampSelectorPageIndex();
        var start = _selectorPageIndex * SelectorRowsPerPage;
        var end = Math.Min(start + SelectorRowsPerPage, FilteredSelectorEntries.Count);
        for (var i = start; i < end; i++)
        {
            SelectorRows.Add(CreateSelectorRow(_selectorRoot.transform, _commandFont, i - start, FilteredSelectorEntries[i]));
        }

        if (_selectorTitleText != null)
        {
            _selectorTitleText.text = $"??/??????{FilteredSelectorEntries.Count} / {SelectorEntries.Count} ??";
        }

        if (_selectorPageText != null)
        {
            _selectorPageText.text = $"? {_selectorPageIndex + 1} / {GetSelectorPageCount()} ?";
        }
    }

    private static void ApplySelectorFilters()
    {
        FilteredSelectorEntries.Clear();
        foreach (var entry in SelectorEntries)
        {
            if (entry.Matches(_selectorCostFilter))
            {
                FilteredSelectorEntries.Add(entry);
            }
        }

        ClampSelectorPageIndex();
    }

    private static void SetSelectorCostFilter(int? cost)
    {
        _selectorCostFilter = cost;
        UpdateSelectorCostFilterText();
        _selectorPageIndex = 0;
        ApplySelectorFilters();
        RebuildSelectorRows();
    }

    private static void ClearSelectorFilters()
    {
        _selectorCostFilter = null;
        UpdateSelectorCostFilterText();
        _selectorPageIndex = 0;
        ApplySelectorFilters();
        RebuildSelectorRows();
    }

    private static void UpdateSelectorCostFilterText()
    {
        if (_selectorCostFilterText != null)
        {
            _selectorCostFilterText.text = _selectorCostFilter.HasValue ? $"???{_selectorCostFilter.Value}" : "?????";
        }
    }

    private static void ClampSelectorPageIndex()
    {
        _selectorPageIndex = Mathf.Clamp(_selectorPageIndex, 0, GetSelectorPageCount() - 1);
    }

    private static GameObject CreateSelectorRow(Transform parent, Font font, int rowIndex, SelectorEntry entry)
    {
        var row = new GameObject($"SelectorRow_{rowIndex}");
        row.transform.SetParent(parent, false);
        row.AddComponent<Image>().color = new Color(0.19f, 0.21f, 0.34f, 0.96f);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -164f - rowIndex * 70f);
        rect.sizeDelta = new Vector2(-96f, 62f);

        entry.EnsureDetails();

        CreateSelectorIcon(row.transform, entry.IconSprite);

        var label = CreateText("SelectorLabel", row.transform, font, 22, TextAnchor.MiddleLeft, Color.white);
        label.text = entry.Kind == "??"
            ? $"[{entry.Kind}] {entry.Title}  {entry.CostText}"
            : $"[{entry.Kind}] {entry.Title}";
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(92f, 18f);
        labelRect.sizeDelta = new Vector2(-360f, 26f);

        var detail = CreateText("SelectorDetail", row.transform, font, 16, TextAnchor.UpperLeft, new Color(0.88f, 0.89f, 0.97f, 1f));
        detail.horizontalOverflow = HorizontalWrapMode.Wrap;
        detail.verticalOverflow = VerticalWrapMode.Truncate;
        detail.text = entry.Description;
        var detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0.5f);
        detailRect.anchorMax = new Vector2(1f, 0.5f);
        detailRect.pivot = new Vector2(0f, 0.5f);
        detailRect.anchoredPosition = new Vector2(92f, -18f);
        detailRect.sizeDelta = new Vector2(-360f, 38f);

        var spriteName = CreateText("SelectorSpriteName", row.transform, font, 13, TextAnchor.MiddleRight, new Color(1f, 0.88f, 0.48f, 1f));
        spriteName.text = entry.SpriteFileName;
        var spriteNameRect = spriteName.GetComponent<RectTransform>();
        spriteNameRect.anchorMin = new Vector2(1f, 0.5f);
        spriteNameRect.anchorMax = new Vector2(1f, 0.5f);
        spriteNameRect.pivot = new Vector2(1f, 0.5f);
        spriteNameRect.anchoredPosition = new Vector2(-182f, -22f);
        spriteNameRect.sizeDelta = new Vector2(280f, 22f);

        CreateButton(row.transform, font, "??", new Vector2(-76f, 0f), new Vector2(120f, 42f), entry.Action);
        return row;
    }

    private static void CreateSelectorIcon(Transform parent, Sprite sprite)
    {
        var frame = new GameObject("SelectorIconFrame");
        frame.transform.SetParent(parent, false);
        frame.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.16f, 0.92f);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.anchoredPosition = new Vector2(24f, 0f);
        frameRect.sizeDelta = new Vector2(54f, 54f);
        AddBorder(frame.transform, new Color(1f, 0.72f, 0.22f, 0.9f), 1f);

        var icon = new GameObject("SelectorIcon");
        icon.transform.SetParent(frame.transform, false);
        var iconImage = icon.AddComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5f, 5f);
        iconRect.offsetMax = new Vector2(-5f, -5f);

    }

    private static void PreviousSelectorPage()
    {
        _selectorPageIndex = (_selectorPageIndex + GetSelectorPageCount() - 1) % GetSelectorPageCount();
        RebuildSelectorRows();
    }

    private static void NextSelectorPage()
    {
        _selectorPageIndex = (_selectorPageIndex + 1) % GetSelectorPageCount();
        RebuildSelectorRows();
    }

    private static int GetSelectorPageCount()
    {
        return Math.Max(1, (FilteredSelectorEntries.Count + SelectorRowsPerPage - 1) / SelectorRowsPerPage);
    }

    private static void AddCardToPlayerHand(CardConfig cardConfig)
    {
        try
        {
            var player = FindLoadedComponent<PlayerModel>();
            if (player == null || !player.IsInEncounter)
            {
                RefreshStatus("????????????????");
                return;
            }

            player.AddCardConfigToHand(cardConfig, false);
            RefreshStatus($"??????{GetAssetTitle(cardConfig)}?");
            Logger.LogInfo($"?????????{GetAssetTitle(cardConfig)}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"?????{ex.Message}");
            Logger.LogWarning($"????????{ex.Message}");
        }
    }

    private static void ReceiveGem(GemConfig gemConfig)
    {
        try
        {
            var sequence = FindLoadedComponent<GemInsertionSequence>();
            var player = FindLoadedComponent<PlayerModel>();
            if (player == null || !player.IsInEncounter)
            {
                RefreshStatus("????????????????");
                return;
            }

            if (sequence == null)
            {
                RefreshStatus("??????????????????");
                return;
            }

            sequence.OnReceiveGem(gemConfig, null, true);
            RefreshStatus($"????????{GetAssetTitle(gemConfig)}?");
            Logger.LogInfo($"???????????{GetAssetTitle(gemConfig)}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"?????{ex.Message}");
            Logger.LogWarning($"??????????{ex.Message}");
        }
    }

    private static void OpenPermanentStatMenu()
    {
        try
        {
            var modal = FindOfferingTableModal();
            if (modal == null)
            {
                RefreshStatus("??????????????????/????????");
                Logger.LogWarning("???????????????????? CardStatOfferingTableModal?");
                return;
            }

            modal.Open();
            RefreshStatus("??????????");
            Logger.LogInfo("?????????????");
        }
        catch (Exception ex)
        {
            RefreshStatus($"???????????{ex.Message}");
            Logger.LogWarning($"??????????????{ex.Message}");
        }
    }

    private static OfferingTableModal FindOfferingTableModal()
    {
        var modal = FindLoadedComponent<CardStatOfferingTableModal>();
        if (modal != null)
        {
            return modal;
        }

        var eventView = FindCurrentOfferingTableEventView();
        if (eventView == null)
        {
            return null;
        }

        var field = AccessTools.Field(typeof(OfferingTableEventView), "_offeringTableModal");
        modal = field != null ? field.GetValue(eventView) as CardStatOfferingTableModal : null;
        if (modal is PassiveEventModal passiveModal)
        {
            passiveModal.SetPassiveEventView(eventView);
        }

        return modal;
    }

    private static OfferingTableEventView FindCurrentOfferingTableEventView()
    {
        var eventView = FindLoadedComponent<OfferingTableEventView>();
        if (eventView != null)
        {
            return eventView;
        }

        var dungeon = FindLoadedComponent<DungeonModel>();
        if (dungeon?.CurrentPassiveEvent is OfferingTableEventView currentOffering)
        {
            return currentOffering;
        }

        return null;
    }

    private static void ClearRows(List<GameObject> rows)
    {
        foreach (var row in rows)
        {
            if (row != null)
            {
                row.SetActive(false);
                Object.Destroy(row);
            }
        }

        rows.Clear();
    }

    private static void TryOpenLevelUpChoiceModal(PlayerModel player)
    {
        var xpBar = FindLoadedComponent<PlayerXpBarView>();
        if (xpBar == null)
        {
            throw new InvalidOperationException("?????????????????");
        }

        // ?????????????? View ???????
        // ?????????????????? modal ???????????? UI ?????????
        xpBar.SyncView();
        var tryOpen = AccessTools.Method(typeof(PlayerXpBarView), "TryOpenChooseCardModal");
        var opened = tryOpen != null && Convert.ToBoolean(tryOpen.Invoke(xpBar, Array.Empty<object>()));
        if (!opened)
        {
            var doViewLevelUp = AccessTools.Method(typeof(PlayerXpBarView), "DoViewLevelUp");
            doViewLevelUp?.Invoke(xpBar, Array.Empty<object>());
            opened = IsChooseCardModalOpen(xpBar);
        }

        if (!opened)
        {
            OpenChooseCardModalDirectly(xpBar, player.PlayerLevel);
        }
    }

    private static bool IsChooseCardModalOpen(PlayerXpBarView xpBar)
    {
        var modalField = AccessTools.Field(typeof(PlayerXpBarView), "_chooseCardModal");
        return modalField?.GetValue(xpBar) is ChooseCardModal { IsOpen: true };
    }

    private static void OpenChooseCardModalDirectly(PlayerXpBarView xpBar, int playerLevel)
    {
        var modalField = AccessTools.Field(typeof(PlayerXpBarView), "_chooseCardModal");
        var modal = modalField?.GetValue(xpBar) as ChooseCardModal;
        if (modal == null)
        {
            throw new InvalidOperationException("????????????");
        }

        modal.SetPlayerLevel(playerLevel);
        modal.PopulateCardRewardChoices(false);
        modal.Open();
        Logger.LogInfo("??????????????????");
    }

    private static void UnlockAllAchievements()
    {
        ExecuteOnLoadedComponent<AchievementManager>("??????", manager => manager.DEBUG_ForceUnlockAllAchievements());
    }

    private static void UnlockRelicProgression()
    {
        ExecuteOnProgression("??????", progression => progression.DEBUG_UnlockRelics());
    }

    private static void ResetCardGemSlots()
    {
        ExecuteOnProgression("???????", progression => progression.DEBUG_ResetCardGemSlots());
    }

    private static void ResetGemFrequencies()
    {
        ExecuteOnProgression("??????", progression => progression.DEBUG_ResetGemFrequencies());
    }

    private static void UnlockAndDiscoverCards()
    {
        ExecuteOnLoadedComponent<CardBlacksmith>("?????????", blacksmith => blacksmith.DEBUG_UnlockAndDiscoverCards());
    }

    private static void UnlockAllGems()
    {
        ExecuteOnLoadedComponent<GemJeweller>("??????", jeweller => jeweller.DEBUG_UnlockAllGems());
    }

    private static void AddCharacterSlot()
    {
        ExecuteOnLoadedComponent<CharacterShop>("??????", shop => shop.DEBUG_AddCharacterSlot());
    }

    private static void UnlockCrawlerAchievements()
    {
        ExecuteOnLoadedComponent<CharacterShop>("??????", shop => shop.DEBUG_UnlockCrawlerAchievements());
    }

    private static void UnlockArcanaBuilding()
    {
        ExecuteOnLoadedComponent<ArcanaShop>("???????????", shop => shop.DEBUG_UnlockBuildingAndFirstArcana());
    }

    private static void UnlockArcanaAchievements()
    {
        ExecuteOnLoadedComponent<ArcanaShop>("??????", shop => shop.DEBUG_UnlockArcanaAchievements());
    }

    private static void UnlockJewellerRelic()
    {
        ExecuteOnLoadedComponent<BlacksmithShopController>("???????", controller => controller.DEBUG_UnlockJewellerRelic());
    }

    private static void RefreshAllLevels()
    {
        ExecuteOnLoadedComponent<MapLevelController>("??????", controller => controller.DEBUG_RefreshAllLevels());
    }

    private static void UnlockDungeonAchievements()
    {
        ExecuteOnLoadedComponent<MapLevelController>("??????", controller => controller.DEBUG_UnlockDungeonAchievements());
    }

    private static void ExecuteOnProgression(string actionName, Action<Nosebleed.Pancake.MetaProgression.ProgressionData> action)
    {
        try
        {
            var progression = Globals.Progression;
            if (progression == null)
            {
                Logger.LogWarning($"??????{actionName}???????? ProgressionData?");
                RefreshStatus($"{actionName}???????????");
                return;
            }

            action(progression);
            Logger.LogInfo($"???????{actionName}?");
            RefreshStatus($"????{actionName}?");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"??????{actionName}????{ex.Message}");
            RefreshStatus($"{actionName}???{ex.Message}");
        }
    }

    private static void ExecuteOnLoadedComponent<TComponent>(string actionName, Action<TComponent> action)
        where TComponent : Component
    {
        try
        {
            var component = FindLoadedComponent<TComponent>();
            if (component == null)
            {
                Logger.LogWarning($"??????{actionName}???????????? {typeof(TComponent).Name}?");
                RefreshStatus($"{actionName}???????????????");
                return;
            }

            action(component);
            Logger.LogInfo($"???????{actionName}?");
            RefreshStatus($"????{actionName}?");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"??????{actionName}????{ex.Message}");
            RefreshStatus($"{actionName}???{ex.Message}");
        }
    }

    private static void ExecuteOnPlayer(string actionName, Action<PlayerModel> action)
    {
        try
        {
            var player = FindLoadedComponent<PlayerModel>();
            if (player == null)
            {
                RefreshStatus($"{actionName}???????????");
                Logger.LogWarning($"??????{actionName}??????? PlayerModel?");
                return;
            }

            action(player);
            RefreshStatus($"????{actionName}?");
            Logger.LogInfo($"???????{actionName}?");
        }
        catch (Exception ex)
        {
            RefreshStatus($"{actionName}???{ex.Message}");
            Logger.LogWarning($"??????{actionName}????{ex.Message}");
        }
    }

    private static TComponent FindLoadedComponent<TComponent>()
        where TComponent : Component
    {
        var components = Resources.FindObjectsOfTypeAll<TComponent>();
        foreach (var component in components)
        {
            // Resources ???????????????????????????????????
            if (component != null && component.gameObject != null && component.gameObject.scene.IsValid())
            {
                return component;
            }
        }

        return null;
    }

    private static float GetDisplayedBaseStat(PlayerStats stats, StatEditorEntry entry, SimpleStat stat)
    {
        var baseValue = GetBaseStat(stats, entry.StatName);
        return baseValue > 0f || stat == null ? baseValue : stat.Value;
    }

    private static float GetBaseStat(PlayerStats stats, string statName)
    {
        var enumValue = CreateStatsEnumValue(statName);
        var method = AccessTools.Method(typeof(PlayerStats), "GetBaseStat");
        return method != null ? Convert.ToSingle(method.Invoke(stats, new[] { enumValue })) : 0f;
    }

    private static void SetBaseStat(PlayerStats stats, string statName, float value)
    {
        var enumValue = CreateStatsEnumValue(statName);
        var method = AccessTools.Method(typeof(PlayerStats), "SetBaseStat");
        if (method == null)
        {
            throw new MissingMethodException("PlayerStats.SetBaseStat");
        }

        method.Invoke(stats, new[] { enumValue, value });
    }

    private static object CreateStatsEnumValue(string statName)
    {
        // PlayerStats.Stats ???????? internal??????????
        // ????????????????????????????????
        var enumType = typeof(PlayerStats).GetNestedType("Stats", BindingFlags.Public | BindingFlags.NonPublic);
        if (enumType == null)
        {
            throw new MissingMemberException("PlayerStats.Stats");
        }

        return Enum.Parse(enumType, statName);
    }

    private static void NotifyPlayerStatsChanged(PlayerStats stats)
    {
        // SetBaseStat ???????? UI ??????? StatChanged ????/???????????
        var field = AccessTools.Field(typeof(PlayerStats), "StatChanged");
        if (field?.GetValue(stats) is Action statChanged)
        {
            statChanged.Invoke();
        }
    }

    private static string FormatNumber(float value)
    {
        return Math.Abs(value - MathF.Round(value)) < 0.001f ? MathF.Round(value).ToString("0") : value.ToString("0.##");
    }

    private static string GetAssetTitle(Object asset)
    {
        switch (asset)
        {
            case CardConfig card:
                return FirstNonEmpty(GetLocalizedTitle(card.NameLoc), card.Name, card.AssetId, card.name);
            case GemConfig gem:
                return FirstNonEmpty(gem.Name, GetPrivateLocalizedTitle(gem, "_gemName"), gem.AssetId, gem.name);
            default:
                return asset != null ? asset.name : "??";
        }
    }

    private static string GetCardSelectorDescription(CardConfig card)
    {
        var player = FindLoadedComponent<PlayerModel>();
        var description = TryGetCardEffectDescription(card, player);
        if (IsMissingLocalizationText(description))
        {
            description = TryGetCardEffectDescriptions(card, player);
        }

        if (IsMissingLocalizationText(description))
        {
            description = GetLocalizedTitle(card.cardDescription);
        }

        return IsMissingLocalizationText(description)
            ? "?????"
            : FirstNonEmpty(NormalizeSelectorText(description), "?????");
    }

    private static string TryGetCardEffectDescription(CardConfig card, PlayerModel player)
    {
        try
        {
            var playerModel = player != null ? player.Cast<IPlayerModel>() : null;
            return card.GetEffectDescription(null, playerModel, 1, true, false);
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"???????????{card?.AssetId}????{ex.Message}");
            return null;
        }
    }

    private static string TryGetCardEffectDescriptions(CardConfig card, PlayerModel player)
    {
        var parts = new List<string>();
        var playerModel = player != null ? player.Cast<IPlayerModel>() : null;
        // ???????????????????????????????????????????????????
        AppendCardEffectDescriptions(parts, GetPrivateCardEffects(card, "onPlayEffect"), playerModel, card, "??");
        AppendCardEffectDescriptions(parts, GetPrivateCardEffects(card, "onDrawEffect"), playerModel, card, "??");
        AppendCardEffectDescriptions(parts, GetPrivateCardEffects(card, "onDiscardEffect"), playerModel, card, "??");
        return parts.Count > 0 ? string.Join("?", parts) : null;
    }

    private static CardEffect[] GetPrivateCardEffects(CardConfig card, string fieldName)
    {
        var field = AccessTools.Field(typeof(CardConfig), fieldName);
        return field?.GetValue(card) as CardEffect[];
    }

    private static void AppendCardEffectDescriptions(List<string> parts, CardEffect[] effects, IPlayerModel playerModel, CardConfig card, string triggerName)
    {
        if (effects == null || effects.Length <= 0)
        {
            return;
        }

        foreach (var effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            try
            {
                var description = effect.GetEffectDescription(playerModel, null, card, 1, true, false);
                if (IsMissingLocalizationText(description))
                {
                    continue;
                }

                description = NormalizeSelectorText(description);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    parts.Add($"{triggerName}?{description}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"???????????{card?.AssetId}/{effect.GetIl2CppType().Name}????{ex.Message}");
            }
        }
    }

    private static string GetGemSelectorDescription(GemConfig gem)
    {
        try
        {
            return FirstNonEmpty(NormalizeSelectorText(gem.GemEffectDescription), NormalizeSelectorText(GetPrivateLocalizedTitle(gem, "_gemDescription")), "???????");
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"???????????{gem?.AssetId}????{ex.Message}");
            return FirstNonEmpty(NormalizeSelectorText(GetPrivateLocalizedTitle(gem, "_gemDescription")), "???????");
        }
    }

    private static string GetCardCostText(CardConfig card)
    {
        return GetCardCostText(TryGetCardManaCost(card));
    }

    private static string GetCardCostText(int? manaCost)
    {
        return manaCost.HasValue ? $"? {manaCost.Value}" : "? ?";
    }

    private static int? TryGetCardManaCost(CardConfig card)
    {
        try
        {
            return card.GetManaCost();
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"?????????{card?.AssetId}????{ex.Message}");
            return null;
        }
    }

    private static Sprite GetPrimaryCardSprite(CardConfig card)
    {
        return card != null && card.sprites != null && card.sprites.Length > 0 ? card.sprites[0] : null;
    }

    private static string GetSpriteFileName(Sprite sprite)
    {
        return sprite != null && !string.IsNullOrWhiteSpace(sprite.name)
            ? $"{GetSafeFileName(sprite.name)}.png"
            : "??: <?>";
    }

    private static string GetSafeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalidChars.Contains(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private static string NormalizeSelectorText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static bool IsMissingLocalizationText(string text)
    {
        return string.IsNullOrWhiteSpace(text) || text.Contains("No translation found", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPrivateLocalizedTitle(object instance, string fieldName)
    {
        var field = AccessTools.Field(instance.GetType(), fieldName);
        return field?.GetValue(instance) is LocalizedString localizedString ? GetLocalizedTitle(localizedString) : null;
    }

    private static string GetLocalizedTitle(LocalizedString localizedString)
    {
        if (localizedString == null)
        {
            return null;
        }

        try
        {
            return localizedString.GetLocalizedString();
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

    private static void RefreshStatus()
    {
        RefreshStatus(null);
    }

    private static void RefreshStatus(string message)
    {
        if (_statusText == null)
        {
            return;
        }

        var coins = GetCoinText();
        var pause = Time.timeScale == 0f ? "??" : "??";
        var suffix = string.IsNullOrWhiteSpace(message) ? string.Empty : $"  |  {message}";
        _statusText.text = $"???{coins}  |  ???{pause}  |  ????{Screen.width}x{Screen.height}{suffix}";
    }

    private static string GetCoinText()
    {
        try
        {
            var progression = Globals.Progression;
            return progression != null && progression.TotalCoins != null ? progression.TotalCoins.Value.ToString() : "??";
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"?????????{ex.Message}");
            return "??";
        }
    }

    private static void SetVisible(bool visible)
    {
        EnsureConsole();
        _visible = visible;
        _root.SetActive(visible);
        if (visible)
        {
            RebuildCommandRows();
            RefreshStatus();
        }
    }

    private static bool IsInRun()
    {
        var player = FindLoadedComponent<PlayerModel>();
        return player != null && FindLoadedComponent<PlayerXpBarView>() != null;
    }

    private static bool IsInCombat(PlayerModel player)
    {
        return player != null && player.IsInEncounter;
    }

    private enum CommandScope
    {
        InRun,
        OutOfRun,
    }

    private sealed class ConsoleCommand
    {
        public ConsoleCommand(CommandScope scope, string iconText, string title, string description, Action action)
        {
            Scope = scope;
            IconText = iconText;
            Title = title;
            Description = description;
            Action = action;
        }

        public CommandScope Scope { get; }

        public string IconText { get; }

        public string Title { get; }

        public string Description { get; }

        public Action Action { get; }
    }

    private sealed class StatEditorEntry
    {
        public StatEditorEntry(string displayName, string statName, Func<PlayerStats, SimpleStat> resolve, float step)
        {
            DisplayName = displayName;
            StatName = statName;
            Resolve = resolve;
            Step = step;
        }

        public string DisplayName { get; }

        public string StatName { get; }

        public Func<PlayerStats, SimpleStat> Resolve { get; }

        public float Step { get; }
    }

    private sealed class SelectorEntry
    {
        private readonly CardConfig _card;
        private readonly GemConfig _gem;
        private bool _detailsLoaded;

        private SelectorEntry(string kind, string title, CardConfig card, GemConfig gem, Action action)
        {
            Kind = kind;
            Title = title;
            _card = card;
            _gem = gem;
            Action = action;
            ManaCost = card != null ? TryGetCardManaCost(card) : null;
            Description = "???...";
            CostText = card != null ? GetCardCostText(ManaCost) : string.Empty;
            SpriteFileName = "??: <???>";
        }

        public static SelectorEntry ForCard(CardConfig card, string title, Action action)
        {
            return new SelectorEntry("??", title, card, null, action);
        }

        public static SelectorEntry ForGem(GemConfig gem, string title, Action action)
        {
            return new SelectorEntry("??", title, null, gem, action);
        }

        public void EnsureDetails()
        {
            if (_detailsLoaded)
            {
                return;
            }

            // ??????????????????????????????? 100+ ?????????????????
            if (_card != null)
            {
                var sprite = GetPrimaryCardSprite(_card);
                Description = GetCardSelectorDescription(_card);
                IconSprite = sprite;
                SpriteFileName = GetSpriteFileName(sprite);
            }
            else if (_gem != null)
            {
                Description = GetGemSelectorDescription(_gem);
                CostText = "??";
                IconSprite = _gem.GemSprite;
                SpriteFileName = GetSpriteFileName(_gem.GemSprite);
            }

            _detailsLoaded = true;
        }

        public bool Matches(int? costFilter)
        {
            return !costFilter.HasValue || (Kind == "??" && ManaCost == costFilter.Value);
        }

        public string Kind { get; }

        public string Title { get; }

        public int? ManaCost { get; }

        public string Description { get; private set; }

        public string CostText { get; private set; }

        public Sprite IconSprite { get; private set; }

        public string SpriteFileName { get; private set; }

        public Action Action { get; }
    }
}
