using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VampireCrawlers.RuntimeMod;

public static class ModCore
{
    public const string RuntimePluginGuid = "com.local.vampirecrawlers.runtime";
    public const string RuntimePluginName = "Vampire Crawlers Runtime BaseLib";
    public const string RuntimePluginVersion = "0.2.0";
    private const float ReferenceAspect = 16f / 9f;

    public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        // ?????????????????????????????????????
        var canvasObject = new GameObject(name);
        Object.DontDestroyOnLoad(canvasObject);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvasObject.AddComponent<CanvasScaler>();
        return canvas;
    }

    public static Text CreateOverlayText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(1f, 0.92f, 0.35f, 1f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static Rect GetReferenceGameRect()
    {
        var screenWidth = Screen.width;
        var screenHeight = Screen.height;
        var screenAspect = screenHeight > 0 ? screenWidth / (float)screenHeight : ReferenceAspect;

        if (screenAspect > ReferenceAspect)
        {
            var width = screenHeight * ReferenceAspect;
            return new Rect((screenWidth - width) * 0.5f, 0f, width, screenHeight);
        }

        var height = screenWidth / ReferenceAspect;
        return new Rect(0f, (screenHeight - height) * 0.5f, screenWidth, height);
    }

    public static Rect GetBattleWindowRect(float leftRatio, float rightRatio, float topRatio, float bottomRatio)
    {
        // ???????/? 16:9 ??????????? 16:9 ??????????????????
        var gameRect = GetReferenceGameRect();
        var left = gameRect.xMin + gameRect.width * leftRatio;
        var right = gameRect.xMin + gameRect.width * rightRatio;
        var top = gameRect.yMax - gameRect.height * topRatio;
        var bottom = gameRect.yMax - gameRect.height * bottomRatio;
        return Rect.MinMaxRect(left, bottom, right, top);
    }
}
