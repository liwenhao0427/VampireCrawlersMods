using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Modal;
using Nosebleed.Pancake.Shops.Blacksmith;
using Nosebleed.Pancake.View;
using Nosebleed.Pancake.View.UI;
using System.Buffers.Binary;
using System.IO.Compression;
using UnityEngine;
using VampireCrawlers.RuntimeMod;

namespace VampireCrawlers.ImageReplacer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(ModCore.RuntimePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private const string PluginGuid = "com.local.vampirecrawlers.imagereplacer";
    private const string PluginName = "Vampire Crawlers ????";
    private const string PluginVersion = "0.2.3";
    private const string PluginFolderName = "VampireCrawlers.ImageReplacer";
    private const string ManifestFileName = "manifest.txt";

    private static readonly Dictionary<string, ReplacementEntry> CardReplacements = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ReplacementEntry> GemReplacements = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> AutoImagePaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> SpriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedMissingFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedInvalidTargets = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedObservedSprites = new(StringComparer.OrdinalIgnoreCase);
    private static ManualLogSource Logger;
    private static ConfigEntry<bool> EnableReplacer;
    private static ConfigEntry<bool> EnableDebugLogs;
    private static ConfigEntry<bool> LogObservedSprites;
    private static ConfigEntry<string> ImageFolder;
    private static ConfigEntry<string> ManifestPath;

    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        EnableReplacer = Config.Bind("General", "EnableImageReplacer", true, "??????????");
        EnableDebugLogs = Config.Bind("General", "EnableDebugLogs", false, "??????????? BepInEx/LogOutput.log?");
        LogObservedSprites = Config.Bind("General", "LogObservedSprites", true, "???? Hook ????????? Sprite ??????????????");
        ImageFolder = Config.Bind("Path", "ImageFolder", Path.Combine(Paths.PluginPath, PluginFolderName, "images"), "???? PNG ?????????? Sprite ??????? PNG?");
        ManifestPath = Config.Bind("Path", "ManifestPath", Path.Combine(Paths.PluginPath, PluginFolderName, ManifestFileName), "????????????????????????????");

        EnsureModFolder();
        if (!EnableReplacer.Value)
        {
            Logger.LogInfo("????????");
            return;
        }

        LoadManifest();
        LoadAutoImages();
        _harmony = new Harmony(PluginGuid);
        PatchCardConfigOnEnable();
        PatchViewEntrypoints();
        Logger.LogInfo($"???????????? PNG {AutoImagePaths.Count} ??manifest ?? {CardReplacements.Count} ???? {GemReplacements.Count} ??");
    }

    private static void PatchCardConfigOnEnable()
    {
        var method = AccessTools.Method(typeof(CardConfig), "OnEnable");
        if (method == null)
        {
            Logger.LogWarning("??? CardConfig.OnEnable???????????????????");
            return;
        }

        new Harmony(PluginGuid + ".cardconfig").Patch(method, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterCardConfigOnEnable)));
    }

    private void PatchViewEntrypoints()
    {
        PatchMethod(typeof(CardView), "SetCardConfig", nameof(BeforeSetCardConfig), new[] { typeof(CardConfig), typeof(bool) });
        PatchMethod(typeof(GemChoiceView), "SetGem", nameof(BeforeSetGem), new[] { typeof(GemConfig) });
        PatchMethod(typeof(GemSelectionView), "SetGem", nameof(BeforeSetGem), new[] { typeof(GemConfig) });
        PatchMethod(typeof(AnimatedGemView), "SetGem", nameof(BeforeSetGem), new[] { typeof(GemConfig) });
        PatchMethod(typeof(InsertGemModal), "SetGem", nameof(BeforeSetGem), new[] { typeof(GemConfig) });
        PatchMethod(typeof(JewellerGemView), "SetupGemView", nameof(BeforeJewellerSetupGemView), new[] { typeof(GemConfig), typeof(GemJeweller) });
    }

    private void PatchMethod(Type type, string methodName, string patchName, Type[] argumentTypes)
    {
        var method = AccessTools.Method(type, methodName, argumentTypes);
        if (method == null)
        {
            Logger.LogWarning($"??? {type.FullName}.{methodName}?????????? Hook ???");
            return;
        }

        _harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin), patchName));
    }

    private static void AfterCardConfigOnEnable(CardConfig __instance)
    {
        ApplyCardConfig(__instance);
    }

    private static void BeforeSetCardConfig(CardConfig cardConfig)
    {
        ApplyCardConfig(cardConfig);
    }

    private static void BeforeSetGem(GemConfig gemConfig)
    {
        ApplyGemConfig(gemConfig);
    }

    private static void BeforeJewellerSetupGemView(GemConfig gemConfig)
    {
        ApplyGemConfig(gemConfig);
    }

    private static void LoadManifest()
    {
        CardReplacements.Clear();
        GemReplacements.Clear();
        SpriteCache.Clear();
        LoggedMissingFiles.Clear();
        LoggedInvalidTargets.Clear();
        LoggedObservedSprites.Clear();

        var manifestPath = ManifestPath.Value;
        if (!File.Exists(manifestPath))
        {
            Logger.LogInfo($"????????????{manifestPath}??????? PNG ?????");
            return;
        }

        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(manifestPath))
        {
            lineNumber++;
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!TrySplitManifestLine(line, out var target, out var relativeImagePath))
            {
                Logger.LogWarning($"??????? {lineNumber} ??????{rawLine}");
                continue;
            }

            AddReplacement(lineNumber, target, relativeImagePath);
        }
    }

    private static void LoadAutoImages()
    {
        AutoImagePaths.Clear();

        var imageFolder = ImageFolder.Value;
        if (!Directory.Exists(imageFolder))
        {
            Logger.LogWarning($"????????????{imageFolder}");
            return;
        }

        foreach (var path in Directory.EnumerateFiles(imageFolder, "*.png", SearchOption.AllDirectories))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            if (AutoImagePaths.ContainsKey(key))
            {
                Logger.LogWarning($"??????????????????{key}");
                continue;
            }

            AutoImagePaths[key] = path;
        }
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf('#');
        return index >= 0 ? line[..index] : line;
    }

    private static bool TrySplitManifestLine(string line, out string target, out string relativeImagePath)
    {
        var separatorIndex = line.IndexOf("=>", StringComparison.Ordinal);
        var separatorLength = 2;
        if (separatorIndex < 0)
        {
            separatorIndex = line.IndexOf('=');
            separatorLength = 1;
        }

        if (separatorIndex < 0)
        {
            target = string.Empty;
            relativeImagePath = string.Empty;
            return false;
        }

        target = line[..separatorIndex].Trim();
        relativeImagePath = line[(separatorIndex + separatorLength)..].Trim();
        return target.Length > 0 && relativeImagePath.Length > 0;
    }

    private static void AddReplacement(int lineNumber, string target, string relativeImagePath)
    {
        var parts = target.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            Logger.LogWarning($"??????? {lineNumber} ??????{target}");
            return;
        }

        var imagePath = Path.IsPathRooted(relativeImagePath)
            ? relativeImagePath
            : Path.Combine(ImageFolder.Value, relativeImagePath);

        switch (parts[0].ToLowerInvariant())
        {
            case "card":
                if (parts.Length != 3 || !int.TryParse(parts[2], out var spriteIndex) || spriteIndex < 0)
                {
                    Logger.LogWarning($"??????????? card:<AssetId>:<index>?{target}");
                    return;
                }

                CardReplacements[$"{parts[1]}:{spriteIndex}"] = new ReplacementEntry(parts[1], spriteIndex, imagePath);
                break;

            case "gem":
                if (parts.Length != 2)
                {
                    Logger.LogWarning($"??????????? gem:<AssetId>?{target}");
                    return;
                }

                GemReplacements[parts[1]] = new ReplacementEntry(parts[1], null, imagePath);
                break;

            default:
                Logger.LogWarning($"????????????{parts[0]}");
                break;
        }
    }

    private static void ApplyCardConfig(CardConfig cardConfig)
    {
        if (cardConfig == null)
        {
            return;
        }

        var assetId = cardConfig.AssetId;
        if (cardConfig.sprites == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(assetId))
        {
            foreach (var pair in CardReplacements)
            {
                var replacement = pair.Value;
                if (!string.Equals(replacement.AssetId, assetId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var index = replacement.Index!.Value;
                if (index >= cardConfig.sprites.Length)
                {
                    LogInvalidTargetOnce(pair.Key, $"?? {assetId} ??? {index} ? Sprite?");
                    continue;
                }

                var original = cardConfig.sprites[index];
                var replacementSprite = LoadReplacementSprite(replacement.ImagePath, replacement.CacheKey, original);
                if (replacementSprite == null)
                {
                    continue;
                }

                cardConfig.sprites[index] = replacementSprite;
                LogDebug($"?? manifest ???????{assetId}[{index}] -> {replacement.ImagePath}");
            }
        }

        for (var i = 0; i < cardConfig.sprites.Length; i++)
        {
            var original = cardConfig.sprites[i];
            LogObservedSpriteOnce("??", assetId, i, original);
            if (!TryLoadAutoReplacement(original, out var replacementSprite, out var imagePath))
            {
                continue;
            }

            cardConfig.sprites[i] = replacementSprite;
            LogDebug($"???? PNG ???????{assetId}[{i}] {original.name} -> {imagePath}");
        }
    }

    private static void ApplyGemConfig(GemConfig gemConfig)
    {
        if (gemConfig == null)
        {
            return;
        }

        var assetId = gemConfig.AssetId;
        if (!string.IsNullOrWhiteSpace(assetId) && GemReplacements.TryGetValue(assetId, out var replacement))
        {
            var manifestSprite = LoadReplacementSprite(replacement.ImagePath, replacement.CacheKey, gemConfig.GemSprite);
            if (manifestSprite != null)
            {
                gemConfig.GemSprite = manifestSprite;
                LogDebug($"?? manifest ???????{assetId} -> {replacement.ImagePath}");
            }

            return;
        }

        if (TryLoadAutoReplacement(gemConfig.GemSprite, out var autoSprite, out var imagePath))
        {
            gemConfig.GemSprite = autoSprite;
            LogDebug($"???? PNG ???????{assetId} {gemConfig.GemSprite.name} -> {imagePath}");
        }
        else
        {
            LogObservedSpriteOnce("??", assetId, null, gemConfig.GemSprite);
        }
    }

    private static bool TryLoadAutoReplacement(Sprite original, out Sprite replacementSprite, out string imagePath)
    {
        replacementSprite = null;
        imagePath = null;

        if (original == null || original.name.StartsWith("VC_Replaced_", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var key in GetAutoImageKeys(original))
        {
            if (!AutoImagePaths.TryGetValue(key, out imagePath))
            {
                continue;
            }

            replacementSprite = LoadReplacementSprite(imagePath, $"auto:{key}:{imagePath}", original);
            return replacementSprite != null;
        }

        return false;
    }

    private static IEnumerable<string> GetAutoImageKeys(Sprite original)
    {
        yield return original.name;
        var safeName = GetSafeFileName(original.name);
        if (!string.Equals(safeName, original.name, StringComparison.Ordinal))
        {
            yield return safeName;
        }

        if (original.texture != null && !string.IsNullOrWhiteSpace(original.texture.name))
        {
            yield return original.texture.name;
            var safeTextureName = GetSafeFileName(original.texture.name);
            if (!string.Equals(safeTextureName, original.texture.name, StringComparison.Ordinal))
            {
                yield return safeTextureName;
            }
        }
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

    private static Sprite LoadReplacementSprite(string imagePath, string cacheKey, Sprite original)
    {
        if (SpriteCache.TryGetValue(cacheKey, out var cachedSprite))
        {
            return cachedSprite;
        }

        if (!File.Exists(imagePath))
        {
            if (LoggedMissingFiles.Add(imagePath))
            {
                Logger.LogWarning($"??????????{imagePath}");
            }
            return null;
        }

        try
        {
            var decoded = DecodePngRgba(imagePath);
            var texture = new Texture2D(decoded.Width, decoded.Height, TextureFormat.RGBA32, false);
            texture.name = $"VC_Replaced_{Path.GetFileNameWithoutExtension(imagePath)}";

            for (var y = 0; y < decoded.Height; y++)
            {
                for (var x = 0; x < decoded.Width; x++)
                {
                    var offset = ((y * decoded.Width) + x) * 4;
                    var color = new Color32(
                        decoded.Rgba[offset],
                        decoded.Rgba[offset + 1],
                        decoded.Rgba[offset + 2],
                        decoded.Rgba[offset + 3]);
                    texture.SetPixel(x, decoded.Height - 1 - y, color);
                }
            }

            texture.Apply(false, false);

            var rect = GetSpriteRect(texture);
            var pivot = GetSpritePivot(original, rect);
            var pixelsPerUnit = original != null ? original.pixelsPerUnit : 100f;
            var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
            sprite.name = $"VC_Replaced_{Path.GetFileNameWithoutExtension(imagePath)}";
            SpriteCache[cacheKey] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"?????????{imagePath}????{ex.Message}");
            return null;
        }
    }

    private static DecodedPng DecodePngRgba(string imagePath)
    {
        var data = File.ReadAllBytes(imagePath);
        if (data.Length < 33 || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            throw new InvalidDataException("????? PNG ???");
        }

        var idat = new MemoryStream();
        int width = 0;
        int height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte compressionMethod = 0;
        byte filterMethod = 0;
        byte interlaceMethod = 0;
        var offset = 8;
        while (offset + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
            var chunkType = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
            var chunkDataOffset = offset + 8;
            if (chunkDataOffset + length + 4 > data.Length)
            {
                throw new InvalidDataException("PNG chunk ?????");
            }

            switch (chunkType)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(chunkDataOffset, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(chunkDataOffset + 4, 4));
                    bitDepth = data[chunkDataOffset + 8];
                    colorType = data[chunkDataOffset + 9];
                    compressionMethod = data[chunkDataOffset + 10];
                    filterMethod = data[chunkDataOffset + 11];
                    interlaceMethod = data[chunkDataOffset + 12];
                    break;

                case "IDAT":
                    idat.Write(data, chunkDataOffset, length);
                    break;

                case "IEND":
                    offset = data.Length;
                    continue;
            }

            offset = chunkDataOffset + length + 4;
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG ???????");
        }

        if (bitDepth != 8 || colorType is not (0 or 2 or 6))
        {
            throw new NotSupportedException($"???? 8-bit ??/RGB/RGBA PNG??? bitDepth={bitDepth}, colorType={colorType}?");
        }

        if (compressionMethod != 0 || filterMethod != 0 || interlaceMethod != 0)
        {
            throw new NotSupportedException($"????????? PNG??? compression={compressionMethod}, filter={filterMethod}, interlace={interlaceMethod}?");
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        var bytesPerPixel = colorType switch
        {
            0 => 1,
            2 => 3,
            6 => 4,
            _ => throw new NotSupportedException($"???? PNG colorType={colorType}?")
        };
        var stride = width * bytesPerPixel;
        var decompressed = raw.ToArray();
        var expected = (stride + 1) * height;
        if (decompressed.Length < expected)
        {
            throw new InvalidDataException($"PNG ???????{decompressed.Length} < {expected}?");
        }

        // Unity ? ImageConversion ??? IL2CPP ?????? ReadOnlySpan ???
        // ?????? PNG filter ????????????????????
        var scanlines = new byte[stride * height];
        var previous = new byte[stride];
        var current = new byte[stride];
        var sourceOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = decompressed[sourceOffset++];
            Buffer.BlockCopy(decompressed, sourceOffset, current, 0, stride);
            sourceOffset += stride;
            UnfilterScanline(filter, current, previous, bytesPerPixel);
            Buffer.BlockCopy(current, 0, scanlines, y * stride, stride);
            var swap = previous;
            previous = current;
            current = swap;
            Array.Clear(current, 0, current.Length);
        }

        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var source = i * bytesPerPixel;
            var target = i * 4;
            switch (colorType)
            {
                case 0:
                    rgba[target] = scanlines[source];
                    rgba[target + 1] = scanlines[source];
                    rgba[target + 2] = scanlines[source];
                    rgba[target + 3] = 255;
                    break;
                case 2:
                    rgba[target] = scanlines[source];
                    rgba[target + 1] = scanlines[source + 1];
                    rgba[target + 2] = scanlines[source + 2];
                    rgba[target + 3] = 255;
                    break;
                case 6:
                    rgba[target] = scanlines[source];
                    rgba[target + 1] = scanlines[source + 1];
                    rgba[target + 2] = scanlines[source + 2];
                    rgba[target + 3] = scanlines[source + 3];
                    break;
            }
        }

        return new DecodedPng(width, height, rgba);
    }

    private static void UnfilterScanline(byte filter, byte[] current, byte[] previous, int bytesPerPixel)
    {
        for (var i = 0; i < current.Length; i++)
        {
            var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
            var up = previous[i];
            var upperLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            var predictor = filter switch
            {
                0 => 0,
                1 => left,
                2 => up,
                3 => (left + up) / 2,
                4 => PaethPredictor(left, up, upperLeft),
                _ => throw new InvalidDataException($"???? PNG filter?{filter}?")
            };
            current[i] = unchecked((byte)(current[i] + predictor));
        }
    }

    private static int PaethPredictor(int left, int up, int upperLeft)
    {
        var p = left + up - upperLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upperLeft);
        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        return pb <= pc ? up : upperLeft;
    }

    private static Rect GetSpriteRect(Texture2D texture)
    {
        // ??????? PNG?????? Sprite ??????????
        // ?????????????????????????????
        return new Rect(0f, 0f, texture.width, texture.height);
    }

    private static Vector2 GetSpritePivot(Sprite original, Rect rect)
    {
        if (original == null || rect.width <= 0f || rect.height <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(
            Mathf.Clamp01(original.pivot.x / original.rect.width),
            Mathf.Clamp01(original.pivot.y / original.rect.height));
    }

    private static void EnsureModFolder()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath.Value)!);
        Directory.CreateDirectory(ImageFolder.Value);
    }

    private static void LogInvalidTargetOnce(string key, string message)
    {
        if (LoggedInvalidTargets.Add(key))
        {
            Logger.LogWarning(message);
        }
    }

    private static void LogObservedSpriteOnce(string targetType, string assetId, int? index, Sprite sprite)
    {
        if (!LogObservedSprites.Value || sprite == null)
        {
            return;
        }

        var textureName = sprite.texture != null ? sprite.texture.name : "<???>";
        var indexText = index.HasValue ? $"[{index.Value}]" : string.Empty;
        var key = $"{targetType}:{assetId}:{index}:{sprite.name}:{textureName}";
        if (LoggedObservedSprites.Add(key))
        {
            Logger.LogInfo($"???{targetType}???AssetId='{assetId}'{indexText}, Sprite='{sprite.name}', Texture='{textureName}'??????????? '{GetSafeFileName(sprite.name)}.png'?");
        }
    }

    private static void LogDebug(string message)
    {
        if (EnableDebugLogs.Value)
        {
            Logger.LogInfo(message);
        }
    }

    private sealed class ReplacementEntry
    {
        public ReplacementEntry(string assetId, int? index, string imagePath)
        {
            AssetId = assetId;
            Index = index;
            ImagePath = imagePath;
            CacheKey = index.HasValue
                ? $"{assetId}:{index.Value}:{imagePath}"
                : $"{assetId}:{imagePath}";
        }

        public string AssetId { get; }
        public int? Index { get; }
        public string ImagePath { get; }
        public string CacheKey { get; }
    }

    private sealed class DecodedPng
    {
        public DecodedPng(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Rgba { get; }
    }
}
