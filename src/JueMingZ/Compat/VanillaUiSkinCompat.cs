using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.Compat
{
    public static class VanillaUiSkinCompat
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, object> InventoryBackTextures = new Dictionary<int, object>();
        private static readonly Dictionary<int, ItemTextureCacheEntry> ItemTextureCache = new Dictionary<int, ItemTextureCacheEntry>();
        private static bool _initialized;
        private static bool _logged;
        private static object _settingsPanelTexture;
        private static object _settingsPanel2Texture;
        private static object _colorBarTexture;
        private static object _colorSliderTexture;
        private static object _colorBlipTexture;
        private static object _colorHighlightTexture;
        private static object _scrollLeftButtonTexture;
        private static object _scrollRightButtonTexture;
        private static object _textBackTexture;
        private static object _magicPixelTexture;
        private static object _lockOnCursorTexture;
        private static object _itemTextureCollection;
        private static object _buffTextureCollection;
        private static object _legacyItemTextureCollection;
        private static LegacyUiSkinPalette _skinPalette = LegacyUiSkinPalette.CreateFallback();
        private static Type _textureAssetsType;
        private static Type _mainType;
        private static Type _assetRequestModeType;
        private static Type _xnaColorType;
        private static object _assetRequestImmediateLoadValue;
        private static string _lastError = string.Empty;
        private static DateTime _nextItemTextureUsabilitySweepUtc = DateTime.MinValue;
        private static DateTime _nextInitializeRetryUtc = DateTime.MinValue;
        private static string _resourceFingerprint = string.Empty;

        public static bool VanillaUiSkinAvailable { get; private set; }
        public static bool FallbackUsed { get; private set; } = true;
        public static string SkinSource { get; private set; } = "Fallback";
        public static bool SettingsPanelResolved { get; private set; }
        public static bool SettingsPanel2Resolved { get; private set; }
        public static bool InventoryBackResolved { get; private set; }
        public static int InventoryBackVariantCount { get; private set; }
        public static bool PanelTextureResolved { get; private set; }
        public static bool ColorBarResolved { get; private set; }
        public static bool ColorSliderResolved { get; private set; }
        public static bool ColorBlipResolved { get; private set; }
        public static bool ColorHighlightResolved { get; private set; }
        public static bool ScrollLeftButtonResolved { get; private set; }
        public static bool ScrollRightButtonResolved { get; private set; }
        public static bool TextBackResolved { get; private set; }
        public static bool MagicPixelResolved { get; private set; }
        public static bool LockOnCursorResolved { get; private set; }
        public static bool ItemTextureCollectionResolved { get; private set; }
        public static bool BuffTextureCollectionResolved { get; private set; }
        public static bool SkinPaletteResolved { get; private set; }
        public static string SkinPaletteSource { get; private set; } = "Fallback";
        public static string PanelSkin { get; private set; } = "Fallback";
        public static string ButtonSkin { get; private set; } = "Fallback";
        public static string TooltipSkin { get; private set; } = "Fallback";
        public static string LastError { get { lock (SyncRoot) { return _lastError; } } }

        public static bool PrepareForDraw(string owner)
        {
            var safeOwner = string.IsNullOrWhiteSpace(owner) ? "UnknownUiLayer" : owner;
            lock (SyncRoot)
            {
                string reason;
                if (_initialized && HasCachedTextureInvalidatedLocked(out reason))
                {
                    InvalidateLocked("cached texture invalidated: " + reason);
                    LogResourceSkipLocked(safeOwner, reason);
                    return false;
                }

                if (_initialized && HasTextureAssetFingerprintChangedLocked(out reason))
                {
                    InvalidateLocked("TextureAssets changed: " + reason);
                    LogResourceSkipLocked(safeOwner, reason);
                    return false;
                }
            }

            EnsureInitialized();

            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    LogResourceSkipLocked(safeOwner, "resources are waiting for retry window");
                    return false;
                }
            }

            return true;
        }

        public static void InvalidateCachedResources(string reason)
        {
            lock (SyncRoot)
            {
                InvalidateLocked(reason ?? "UI skin resources invalidated.");
            }
        }

        public static void EnsureInitialized()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                if (DateTime.UtcNow < _nextInitializeRetryUtc)
                {
                    return;
                }

                try
                {
                    _textureAssetsType = FindType("Terraria.GameContent.TextureAssets");
                    _mainType = FindType("Terraria.Main");
                    _assetRequestModeType = FindType("ReLogic.Content.AssetRequestMode");
                    _xnaColorType = FindType("Microsoft.Xna.Framework.Color");
                    _assetRequestImmediateLoadValue = TryResolveImmediateLoadValue(_assetRequestModeType);

                    _settingsPanelTexture = ResolveTextureAsset("SettingsPanel");
                    _settingsPanel2Texture = ResolveTextureAsset("SettingsPanel2");
                    InventoryBackTextures.Clear();
                    for (var variant = 1; variant <= 19; variant++)
                    {
                        var name = variant == 1 ? "InventoryBack" : "InventoryBack" + variant.ToString(CultureInfo.InvariantCulture);
                        var texture = ResolveTextureAsset(name);
                        if (texture != null)
                        {
                            InventoryBackTextures[variant] = texture;
                        }
                    }

                    _colorBarTexture = ResolveTextureAsset("ColorBar");
                    _colorSliderTexture = ResolveTextureAsset("ColorSlider");
                    _colorBlipTexture = ResolveTextureAsset("ColorBlip");
                    _colorHighlightTexture = ResolveTextureAsset("ColorHighlight");
                    _scrollLeftButtonTexture = ResolveTextureAsset("ScrollLeftButton");
                    _scrollRightButtonTexture = ResolveTextureAsset("ScrollRightButton");
                    _textBackTexture = ResolveTextureAsset("TextBack");
                    _magicPixelTexture = ResolveTextureAsset("MagicPixel");
                    _lockOnCursorTexture = ResolveTextureAsset("LockOnCursor");
                    _itemTextureCollection = TryGetStaticMember(_textureAssetsType, "Item");
                    _buffTextureCollection = TryGetStaticMember(_textureAssetsType, "Buff");
                    _legacyItemTextureCollection = TryGetStaticMember(_mainType, "itemTexture");

                    if (_magicPixelTexture == null)
                    {
                        _magicPixelTexture = ExtractTexture(TryGetStaticMember(_mainType, "magicPixel"));
                        if (_magicPixelTexture == null)
                        {
                            _magicPixelTexture = ExtractTexture(TryGetStaticMember(_mainType, "MagicPixel"));
                        }
                    }

                    SettingsPanelResolved = _settingsPanelTexture != null;
                    SettingsPanel2Resolved = _settingsPanel2Texture != null;
                    InventoryBackResolved = InventoryBackTextures.Count > 0;
                    InventoryBackVariantCount = InventoryBackTextures.Count;
                    PanelTextureResolved = SettingsPanelResolved || SettingsPanel2Resolved || InventoryBackResolved;
                    ColorBarResolved = _colorBarTexture != null;
                    ColorSliderResolved = _colorSliderTexture != null;
                    ColorBlipResolved = _colorBlipTexture != null;
                    ColorHighlightResolved = _colorHighlightTexture != null;
                    ScrollLeftButtonResolved = _scrollLeftButtonTexture != null;
                    ScrollRightButtonResolved = _scrollRightButtonTexture != null;
                    TextBackResolved = _textBackTexture != null;
                    MagicPixelResolved = _magicPixelTexture != null;
                    LockOnCursorResolved = _lockOnCursorTexture != null;
                    ItemTextureCollectionResolved = _itemTextureCollection != null || _legacyItemTextureCollection != null;
                    BuffTextureCollectionResolved = _buffTextureCollection != null;
                    VanillaUiSkinAvailable = PanelTextureResolved || ColorBarResolved || ColorSliderResolved || ColorBlipResolved || ColorHighlightResolved || TextBackResolved;
                    FallbackUsed = !VanillaUiSkinAvailable;
                    SkinSource = SettingsPanelResolved ? "TextureAssets.SettingsPanel" :
                        (SettingsPanel2Resolved ? "TextureAssets.SettingsPanel2" :
                        (InventoryBackResolved ? "TextureAssets.InventoryBack" :
                        (MagicPixelResolved ? "TextureAssets.MagicPixel" : "Fallback")));
                    PanelSkin = SettingsPanelResolved ? "SettingsPanel" :
                        (SettingsPanel2Resolved ? "SettingsPanel2" :
                        (InventoryBackResolved ? "InventoryBack" : "Fallback"));
                    ButtonSkin = InventoryBackResolved ? "InventoryBack" :
                        (SettingsPanel2Resolved ? "SettingsPanel2" :
                        (SettingsPanelResolved ? "SettingsPanel" : "Fallback"));
                    TooltipSkin = TextBackResolved ? "TextBack" :
                        (SettingsPanel2Resolved ? "SettingsPanel2" :
                        (InventoryBackResolved ? "InventoryBack" : "Fallback"));
                    _skinPalette = ResolveSkinPaletteLocked();
                    SkinPaletteResolved = !_skinPalette.FallbackUsed;
                    SkinPaletteSource = _skinPalette.Source;
                    _resourceFingerprint = BuildResourceFingerprintLocked();
                }
                catch (Exception error)
                {
                    _lastError = error.Message;
                    VanillaUiSkinAvailable = false;
                    FallbackUsed = true;
                    SkinSource = "Fallback";
                    PanelSkin = "Fallback";
                    ButtonSkin = "Fallback";
                    TooltipSkin = "Fallback";
                    _skinPalette = LegacyUiSkinPalette.CreateFallback();
                    SkinPaletteResolved = false;
                    SkinPaletteSource = "Fallback";
                    _resourceFingerprint = string.Empty;
                }

                _initialized = true;
                LogStatusLocked();
            }
        }

        public static bool TryGetSettingsPanelTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _settingsPanelTexture;
                return IsTextureUsableForDrawLocked(texture, "SettingsPanel");
            }
        }

        public static bool TryGetSettingsPanel2Texture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _settingsPanel2Texture;
                return IsTextureUsableForDrawLocked(texture, "SettingsPanel2");
            }
        }

        public static bool TryGetPanelTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _settingsPanelTexture ?? _settingsPanel2Texture ?? GetInventoryBackTextureLocked(1);
                return IsTextureUsableForDrawLocked(texture, "Panel");
            }
        }

        public static bool TryGetSubPanelTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _settingsPanel2Texture ?? _settingsPanelTexture ?? GetInventoryBackTextureLocked(1);
                return IsTextureUsableForDrawLocked(texture, "SubPanel");
            }
        }

        public static bool TryGetInventoryBackTexture(out object texture)
        {
            return TryGetInventoryBackTexture(1, out texture);
        }

        public static bool TryGetInventoryBackTexture(int variant, out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = GetInventoryBackTextureLocked(variant);
                return IsTextureUsableForDrawLocked(texture, "InventoryBack" + variant.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static bool TryGetInventoryBack2Texture(out object texture)
        {
            return TryGetInventoryBackTexture(2, out texture);
        }

        public static bool TryGetInventoryBack3Texture(out object texture)
        {
            return TryGetInventoryBackTexture(3, out texture);
        }

        public static bool TryGetButtonTexture(bool hovered, bool selected, out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                if (selected)
                {
                    texture = GetInventoryBackTextureLocked(3) ?? GetInventoryBackTextureLocked(4) ?? GetInventoryBackTextureLocked(2);
                }
                else if (hovered)
                {
                    texture = GetInventoryBackTextureLocked(2) ?? GetInventoryBackTextureLocked(3);
                }
                else
                {
                    texture = GetInventoryBackTextureLocked(1);
                }

                texture = texture ?? _settingsPanel2Texture ?? _settingsPanelTexture;
                return IsTextureUsableForDrawLocked(texture, "Button");
            }
        }

        public static bool TryGetTextBackTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _textBackTexture;
                return IsTextureUsableForDrawLocked(texture, "TextBack");
            }
        }

        public static bool TryGetMagicPixelTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _magicPixelTexture;
                return IsTextureUsableForDrawLocked(texture, "MagicPixel");
            }
        }

        public static bool TryGetLockOnCursorTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _lockOnCursorTexture;
                return IsTextureUsableForDrawLocked(texture, "LockOnCursor");
            }
        }

        public static bool TryGetColorBarTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _colorBarTexture;
                return IsTextureUsableForDrawLocked(texture, "ColorBar");
            }
        }

        public static bool TryGetColorSliderTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _colorSliderTexture;
                return IsTextureUsableForDrawLocked(texture, "ColorSlider");
            }
        }

        public static bool TryGetColorBlipTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _colorBlipTexture;
                return IsTextureUsableForDrawLocked(texture, "ColorBlip");
            }
        }

        public static bool TryGetColorHighlightTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _colorHighlightTexture;
                return IsTextureUsableForDrawLocked(texture, "ColorHighlight");
            }
        }

        public static bool TryGetScrollLeftButtonTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _scrollLeftButtonTexture;
                return IsTextureUsableForDrawLocked(texture, "ScrollLeftButton");
            }
        }

        public static bool TryGetScrollRightButtonTexture(out object texture)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = _scrollRightButtonTexture;
                return IsTextureUsableForDrawLocked(texture, "ScrollRightButton");
            }
        }

        public static bool TryGetItemTexture(int itemType, out object texture)
        {
            texture = null;
            if (itemType <= 0)
            {
                return false;
            }

            EnsureInitialized();
            lock (SyncRoot)
            {
                var entry = GetItemTextureCachedLocked(itemType);
                if (entry == null || !entry.Resolved || entry.Texture == null)
                {
                    return false;
                }

                texture = entry.Texture;
                var now = DateTime.UtcNow;
                if (now < _nextItemTextureUsabilitySweepUtc)
                {
                    return true;
                }

                string reason;
                if (!IsTextureUsable(texture, out reason))
                {
                    InvalidateLocked("Item." + itemType.ToString(CultureInfo.InvariantCulture) + " unavailable: " + reason);
                    texture = null;
                    return false;
                }

                _nextItemTextureUsabilitySweepUtc = now.AddMilliseconds(750);
                return true;
            }
        }

        public static bool TryEnsureItemTextureLoaded(int itemType)
        {
            object texture;
            return TryGetItemTexture(itemType, out texture);
        }

        public static bool TryGetItemTextureSize(int itemType, out int width, out int height)
        {
            width = 0;
            height = 0;
            EnsureInitialized();
            lock (SyncRoot)
            {
                var entry = GetItemTextureCachedLocked(itemType);
                if (entry == null || !entry.Resolved)
                {
                    return false;
                }

                if (!IsTextureUsableForDrawLocked(entry.Texture, "Item." + itemType.ToString(CultureInfo.InvariantCulture)))
                {
                    return false;
                }

                width = entry.Width;
                height = entry.Height;
                return width > 0 && height > 0;
            }
        }

        public static bool TryGetBuffTexture(int buffType, out object texture)
        {
            texture = null;
            if (buffType <= 0)
            {
                return false;
            }

            EnsureInitialized();
            lock (SyncRoot)
            {
                texture = TryGetCollectionTexture(_buffTextureCollection, buffType);
                return IsTextureUsableForDrawLocked(texture, "Buff." + buffType.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static LegacyUiSkinPalette GetSkinPalette()
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return _skinPalette ?? LegacyUiSkinPalette.CreateFallback();
            }
        }

        public static string BuildSkinStatusJson()
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return "{" +
                       "\"settingsPanelResolved\":" + BoolRaw(SettingsPanelResolved) + "," +
                       "\"settingsPanel2Resolved\":" + BoolRaw(SettingsPanel2Resolved) + "," +
                       "\"inventoryBackResolved\":" + BoolRaw(InventoryBackResolved) + "," +
                       "\"inventoryBackVariantCount\":" + InventoryBackVariantCount.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"colorBarResolved\":" + BoolRaw(ColorBarResolved) + "," +
                       "\"colorSliderResolved\":" + BoolRaw(ColorSliderResolved) + "," +
                       "\"colorBlipResolved\":" + BoolRaw(ColorBlipResolved) + "," +
                       "\"colorHighlightResolved\":" + BoolRaw(ColorHighlightResolved) + "," +
                       "\"scrollLeftButtonResolved\":" + BoolRaw(ScrollLeftButtonResolved) + "," +
                       "\"scrollRightButtonResolved\":" + BoolRaw(ScrollRightButtonResolved) + "," +
                       "\"textBackResolved\":" + BoolRaw(TextBackResolved) + "," +
                       "\"magicPixelResolved\":" + BoolRaw(MagicPixelResolved) + "," +
                       "\"itemTextureCollectionResolved\":" + BoolRaw(ItemTextureCollectionResolved) + "," +
                       "\"buffTextureCollectionResolved\":" + BoolRaw(BuffTextureCollectionResolved) + "," +
                       "\"skinSource\":\"" + EscapeJson(SkinSource) + "\"," +
                       "\"panelSkin\":\"" + EscapeJson(PanelSkin) + "\"," +
                       "\"buttonSkin\":\"" + EscapeJson(ButtonSkin) + "\"," +
                       "\"tooltipSkin\":\"" + EscapeJson(TooltipSkin) + "\"," +
                       "\"skinPaletteResolved\":" + BoolRaw(SkinPaletteResolved) + "," +
                       "\"skinPaletteSource\":\"" + EscapeJson(SkinPaletteSource) + "\"," +
                       "\"skinPalettePanel\":\"" + BuildPaletteColorHex(_skinPalette) + "\"," +
                       "\"fallbackUsed\":" + BoolRaw(FallbackUsed) +
                       "}";
            }
        }

        private static bool HasCachedTextureInvalidatedLocked(out string reason)
        {
            reason = string.Empty;

            if (!IsCachedTextureUsableLocked(_settingsPanelTexture, "SettingsPanel", out reason) ||
                !IsCachedTextureUsableLocked(_settingsPanel2Texture, "SettingsPanel2", out reason) ||
                !IsCachedTextureUsableLocked(_colorBarTexture, "ColorBar", out reason) ||
                !IsCachedTextureUsableLocked(_colorSliderTexture, "ColorSlider", out reason) ||
                !IsCachedTextureUsableLocked(_colorBlipTexture, "ColorBlip", out reason) ||
                !IsCachedTextureUsableLocked(_colorHighlightTexture, "ColorHighlight", out reason) ||
                !IsCachedTextureUsableLocked(_scrollLeftButtonTexture, "ScrollLeftButton", out reason) ||
                !IsCachedTextureUsableLocked(_scrollRightButtonTexture, "ScrollRightButton", out reason) ||
                !IsCachedTextureUsableLocked(_textBackTexture, "TextBack", out reason) ||
                !IsCachedTextureUsableLocked(_magicPixelTexture, "MagicPixel", out reason) ||
                !IsCachedTextureUsableLocked(_lockOnCursorTexture, "LockOnCursor", out reason))
            {
                return true;
            }

            foreach (var pair in InventoryBackTextures)
            {
                if (!IsCachedTextureUsableLocked(pair.Value, "InventoryBack" + pair.Key.ToString(CultureInfo.InvariantCulture), out reason))
                {
                    return true;
                }
            }

            foreach (var pair in ItemTextureCache)
            {
                var entry = pair.Value;
                if (entry != null && entry.Resolved &&
                    !IsCachedTextureUsableLocked(entry.Texture, "Item." + pair.Key.ToString(CultureInfo.InvariantCulture), out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTextureAssetFingerprintChangedLocked(out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(_resourceFingerprint))
            {
                return false;
            }

            var current = BuildResourceFingerprintLocked();
            if (string.IsNullOrWhiteSpace(current) ||
                string.Equals(current, _resourceFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            reason = "TextureAssets fingerprint changed";
            return true;
        }

        private static string BuildResourceFingerprintLocked()
        {
            return Identity(TryGetStaticMember(_textureAssetsType, "SettingsPanel")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "SettingsPanel2")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "InventoryBack")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "MagicPixel")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "LockOnCursor")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "Item")) + "|" +
                   Identity(TryGetStaticMember(_textureAssetsType, "Buff"));
        }

        private static string Identity(object value)
        {
            return value == null ? "null" : RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsCachedTextureUsableLocked(object texture, string name, out string reason)
        {
            reason = string.Empty;
            if (texture == null)
            {
                return true;
            }

            string textureReason;
            if (IsTextureUsable(texture, out textureReason))
            {
                return true;
            }

            reason = name + ": " + textureReason;
            return false;
        }

        private static bool IsTextureUsableForDrawLocked(object texture, string name)
        {
            if (texture == null)
            {
                return false;
            }

            string reason;
            if (IsTextureUsable(texture, out reason))
            {
                return true;
            }

            InvalidateLocked((string.IsNullOrWhiteSpace(name) ? "texture" : name) + " unavailable: " + reason);
            return false;
        }

        private static bool IsTextureUsable(object texture, out string reason)
        {
            reason = string.Empty;
            if (texture == null)
            {
                reason = "texture is null";
                return false;
            }

            try
            {
                var type = texture.GetType();
                if (ReadBoolMember(texture, type, "IsDisposed") ||
                    ReadBoolMember(texture, type, "Disposed") ||
                    ReadBoolMember(texture, type, "isDisposed"))
                {
                    reason = "texture is disposed";
                    return false;
                }

                var width = ReadTextureDimension(texture, "Width", 0);
                var height = ReadTextureDimension(texture, "Height", 0);
                if (width <= 0 || height <= 0)
                {
                    reason = "texture dimensions are unavailable";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                reason = error.Message;
                return false;
            }
        }

        private static bool ReadBoolMember(object instance, Type type, string name)
        {
            if (instance == null || type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var property = type.GetProperty(name, flags);
                if (property != null && property.CanRead && property.PropertyType == typeof(bool))
                {
                    return (bool)property.GetValue(instance, null);
                }

                var field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(instance);
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static int ReadTextureDimension(object texture, string name, int fallback)
        {
            if (texture == null || string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            try
            {
                var type = texture.GetType();
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    return Convert.ToInt32(property.GetValue(texture, null), CultureInfo.InvariantCulture);
                }

                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return Convert.ToInt32(field.GetValue(texture), CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return fallback;
            }

            return fallback;
        }

        private static void InvalidateLocked(string reason)
        {
            ResetTextureStateLocked(reason);
            UiPrimitiveRenderer.InvalidateCachedResources(reason);
            UiTextRenderer.InvalidateCachedResources(reason);
        }

        private static void ResetTextureStateLocked(string reason)
        {
            _initialized = false;
            _logged = false;
            _nextItemTextureUsabilitySweepUtc = DateTime.MinValue;
            InventoryBackTextures.Clear();
            ItemTextureCache.Clear();
            _settingsPanelTexture = null;
            _settingsPanel2Texture = null;
            _colorBarTexture = null;
            _colorSliderTexture = null;
            _colorBlipTexture = null;
            _colorHighlightTexture = null;
            _scrollLeftButtonTexture = null;
            _scrollRightButtonTexture = null;
            _textBackTexture = null;
            _magicPixelTexture = null;
            _lockOnCursorTexture = null;
            _itemTextureCollection = null;
            _buffTextureCollection = null;
            _legacyItemTextureCollection = null;
            _skinPalette = LegacyUiSkinPalette.CreateFallback();
            _textureAssetsType = null;
            _mainType = null;
            _assetRequestModeType = null;
            _xnaColorType = null;
            _assetRequestImmediateLoadValue = null;
            VanillaUiSkinAvailable = false;
            FallbackUsed = true;
            SkinSource = "Fallback";
            SettingsPanelResolved = false;
            SettingsPanel2Resolved = false;
            InventoryBackResolved = false;
            InventoryBackVariantCount = 0;
            PanelTextureResolved = false;
            ColorBarResolved = false;
            ColorSliderResolved = false;
            ColorBlipResolved = false;
            ColorHighlightResolved = false;
            ScrollLeftButtonResolved = false;
            ScrollRightButtonResolved = false;
            TextBackResolved = false;
            MagicPixelResolved = false;
            LockOnCursorResolved = false;
            ItemTextureCollectionResolved = false;
            BuffTextureCollectionResolved = false;
            SkinPaletteResolved = false;
            SkinPaletteSource = "Fallback";
            PanelSkin = "Fallback";
            ButtonSkin = "Fallback";
            TooltipSkin = "Fallback";
            _lastError = reason ?? "Vanilla UI skin resources invalidated.";
            _resourceFingerprint = string.Empty;
            _nextInitializeRetryUtc = DateTime.UtcNow.AddSeconds(1);
        }

        private static void LogResourceSkipLocked(string owner, string reason)
        {
            LogThrottle.WarnThrottled(
                "vanilla-ui-skin-resource-skip-" + owner,
                TimeSpan.FromSeconds(10),
                "VanillaUiSkinCompat",
                "Skipping JueMing-Z UI draw while Terraria UI resources settle. owner=" + owner + ", reason=" + (reason ?? string.Empty));
        }

        private static LegacyUiSkinPalette ResolveSkinPaletteLocked()
        {
            int r;
            int g;
            int b;
            string failure;
            if (TrySampleTextureColor(GetInventoryBackTextureLocked(1), "InventoryBack", out r, out g, out b, out failure))
            {
                return LegacyUiSkinPalette.CreateFromBase(r, g, b, "TextureAssets.InventoryBack", false);
            }

            if (TrySampleTextureColor(_settingsPanelTexture, "SettingsPanel", out r, out g, out b, out failure))
            {
                return LegacyUiSkinPalette.CreateFromBase(r, g, b, "TextureAssets.SettingsPanel", false);
            }

            if (TrySampleTextureColor(_settingsPanel2Texture, "SettingsPanel2", out r, out g, out b, out failure))
            {
                return LegacyUiSkinPalette.CreateFromBase(r, g, b, "TextureAssets.SettingsPanel2", false);
            }

            _lastError = string.IsNullOrWhiteSpace(failure) ? "Skin palette sampling failed." : failure;
            return LegacyUiSkinPalette.CreateFallback();
        }

        private static bool TrySampleTextureColor(object texture, string sourceName, out int r, out int g, out int b, out string failure)
        {
            r = 0;
            g = 0;
            b = 0;
            failure = string.Empty;
            if (texture == null)
            {
                failure = "Palette source texture unavailable: " + sourceName;
                return false;
            }

            int width;
            int height;
            if (!UiPrimitiveRenderer.TryReadTextureDimensions(texture, out width, out height) || width <= 0 || height <= 0)
            {
                failure = "Palette source texture dimensions unavailable: " + sourceName;
                return false;
            }

            if (_xnaColorType == null)
            {
                failure = "Microsoft.Xna.Framework.Color unavailable for skin palette sampling.";
                return false;
            }

            var pixelCount = width * height;
            if (pixelCount <= 0 || pixelCount > 1048576)
            {
                failure = "Palette source texture too large to sample safely: " + sourceName;
                return false;
            }

            Array pixels;
            if (!TryGetTexturePixels(texture, _xnaColorType, pixelCount, out pixels, out failure))
            {
                failure = sourceName + " palette GetData failed: " + failure;
                return false;
            }

            long sumR = 0;
            long sumG = 0;
            long sumB = 0;
            long weight = 0;
            var startX = Math.Max(0, width / 4);
            var endX = Math.Min(width, width - startX);
            var startY = Math.Max(0, height / 4);
            var endY = Math.Min(height, height - startY);
            var stepX = Math.Max(1, (endX - startX) / 12);
            var stepY = Math.Max(1, (endY - startY) / 12);

            for (var y = startY; y < endY; y += stepY)
            {
                for (var x = startX; x < endX; x += stepX)
                {
                    AccumulatePalettePixel(pixels.GetValue(y * width + x), ref sumR, ref sumG, ref sumB, ref weight);
                }
            }

            if (weight <= 0)
            {
                for (var index = 0; index < pixelCount; index += Math.Max(1, pixelCount / 256))
                {
                    AccumulatePalettePixel(pixels.GetValue(index), ref sumR, ref sumG, ref sumB, ref weight);
                }
            }

            if (weight <= 0)
            {
                failure = "No opaque pixels found while sampling palette source: " + sourceName;
                return false;
            }

            r = ClampColor((int)(sumR / weight));
            g = ClampColor((int)(sumG / weight));
            b = ClampColor((int)(sumB / weight));
            return true;
        }

        private static bool TryGetTexturePixels(object texture, Type colorType, int pixelCount, out Array pixels, out string failure)
        {
            pixels = null;
            failure = string.Empty;
            if (texture == null || colorType == null || pixelCount <= 0)
            {
                failure = "Invalid texture or color type.";
                return false;
            }

            pixels = Array.CreateInstance(colorType, pixelCount);
            var methods = texture.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "GetData", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsArray)
                {
                    continue;
                }

                if (TryInvokeGetData(method, texture, colorType, new object[] { pixels }, out failure))
                {
                    return true;
                }
            }

            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "GetData", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 5 || !parameters[2].ParameterType.IsArray)
                {
                    continue;
                }

                if (TryInvokeGetData(method, texture, colorType, new object[] { 0, null, pixels, 0, pixelCount }, out failure))
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(failure))
            {
                failure = "No compatible Texture2D.GetData overload found.";
            }

            return false;
        }

        private static bool TryInvokeGetData(MethodInfo method, object texture, Type colorType, object[] arguments, out string failure)
        {
            failure = string.Empty;
            try
            {
                method.MakeGenericMethod(colorType).Invoke(texture, arguments);
                return true;
            }
            catch (Exception error)
            {
                failure = error.InnerException == null ? error.Message : error.InnerException.Message;
                return false;
            }
        }

        private static void AccumulatePalettePixel(object color, ref long sumR, ref long sumG, ref long sumB, ref long weight)
        {
            if (color == null)
            {
                return;
            }

            var alpha = ReadColorChannel(color, "A", 255);
            if (alpha < 32)
            {
                return;
            }

            sumR += ReadColorChannel(color, "R", 0) * alpha;
            sumG += ReadColorChannel(color, "G", 0) * alpha;
            sumB += ReadColorChannel(color, "B", 0) * alpha;
            weight += alpha;
        }

        private static int ReadColorChannel(object color, string name, int fallback)
        {
            if (color == null)
            {
                return fallback;
            }

            try
            {
                var type = color.GetType();
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return ClampColor(Convert.ToInt32(field.GetValue(color)));
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    return ClampColor(Convert.ToInt32(property.GetValue(color, null)));
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static object ResolveTextureAsset(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            return ExtractTexture(TryGetStaticMember(_textureAssetsType, memberName));
        }

        private static object ExtractTexture(object candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            try
            {
                TryInvokeNoArg(candidate, "Wait");
                var valueProperty = candidate.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueProperty != null && valueProperty.CanRead)
                {
                    var value = valueProperty.GetValue(candidate, null);
                    return value;
                }
            }
            catch (Exception error)
            {
                _lastError = "Texture asset value unavailable: " + error.Message;
                return null;
            }

            return candidate;
        }

        private static object GetInventoryBackTextureLocked(int variant)
        {
            object texture;
            if (variant > 0 && InventoryBackTextures.TryGetValue(variant, out texture))
            {
                return texture;
            }

            if (InventoryBackTextures.TryGetValue(1, out texture))
            {
                return texture;
            }

            for (var fallback = 2; fallback <= 19; fallback++)
            {
                if (InventoryBackTextures.TryGetValue(fallback, out texture))
                {
                    return texture;
                }
            }

            return null;
        }

        private static ItemTextureCacheEntry GetItemTextureCachedLocked(int itemType)
        {
            if (itemType <= 0)
            {
                return null;
            }

            ItemTextureCacheEntry entry;
            if (ItemTextureCache.TryGetValue(itemType, out entry))
            {
                return entry;
            }

            entry = ResolveItemTextureLocked(itemType);
            ItemTextureCache[itemType] = entry;
            RecordItemTextureEvent(itemType, entry);
            return entry;
        }

        private static ItemTextureCacheEntry ResolveItemTextureLocked(int itemType)
        {
            var entry = new ItemTextureCacheEntry();
            TryInvokeLoadItem(itemType);
            var texture = TryGetCollectionTexture(_itemTextureCollection, itemType);
            if (texture == null)
            {
                texture = TryGetCollectionTexture(_legacyItemTextureCollection, itemType);
            }

            if (texture != null)
            {
                entry.Resolved = true;
                entry.Texture = texture;
                UiPrimitiveRenderer.TryReadTextureDimensions(texture, out entry.Width, out entry.Height);
                entry.Message = "Texture resolved.";
            }
            else
            {
                entry.Resolved = false;
                entry.Message = string.IsNullOrWhiteSpace(_lastError) ? "Texture unavailable." : _lastError;
            }

            return entry;
        }

        private static object TryGetCollectionTexture(object collection, int index)
        {
            return ExtractTexture(TryGetCollectionEntry(collection, index));
        }

        private static object TryGetCollectionEntry(object collection, int index)
        {
            if (collection == null || index < 0)
            {
                return null;
            }

            try
            {
                var array = collection as Array;
                if (array != null)
                {
                    return index < array.Length ? array.GetValue(index) : null;
                }

                var list = collection as IList;
                if (list != null)
                {
                    return index < list.Count ? list[index] : null;
                }

                var itemProperty = collection.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (itemProperty != null)
                {
                    return itemProperty.GetValue(collection, new object[] { index });
                }
            }
            catch (Exception error)
            {
                _lastError = "Texture collection read failed: " + error.Message;
            }

            return null;
        }

        private static void TryInvokeLoadItem(int itemType)
        {
            if (_mainType == null || itemType <= 0)
            {
                return;
            }

            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var methods = _mainType.GetMethods(flags);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "LoadItem", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int))
                    {
                        continue;
                    }

                    var instance = method.IsStatic ? null : TryGetStaticMember(_mainType, "instance");
                    if (!method.IsStatic && instance == null)
                    {
                        instance = TryGetStaticMember(_mainType, "Instance");
                    }

                    if (method.IsStatic || instance != null)
                    {
                        method.Invoke(instance, new object[] { itemType });
                        return;
                    }
                }
            }
            catch (Exception error)
            {
                _lastError = "LoadItem failed: " + error.Message;
            }
        }

        private static bool TryInvokeNoArg(object instance, string methodName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null || method.GetParameters().Length != 0)
                {
                    return false;
                }

                method.Invoke(instance, null);
                return true;
            }
            catch (Exception error)
            {
                _lastError = "Texture asset " + methodName + " failed: " + error.Message;
                return false;
            }
        }

        private static object TryResolveImmediateLoadValue(Type assetRequestModeType)
        {
            if (assetRequestModeType == null || !assetRequestModeType.IsEnum)
            {
                return null;
            }

            try
            {
                return Enum.Parse(assetRequestModeType, "ImmediateLoad", false);
            }
            catch
            {
                return null;
            }
        }

        private static object TryGetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                var field = type.GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                var property = type.GetProperty(name, flags);
                if (property != null && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch (Exception error)
            {
                _lastError = "Read TextureAssets." + name + " failed: " + error.Message;
            }

            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static void RecordItemTextureEvent(int itemType, ItemTextureCacheEntry entry)
        {
            var resolved = entry != null && entry.Resolved;
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                resolved ? "BuffPotion.TextureLoad" : "BuffPotion.TextureFallback",
                "BuffPotion",
                string.Empty,
                resolved ? "Succeeded" : "NotApplicable",
                resolved ? "Succeeded" : "TextureUnavailable",
                resolved ? "Buff potion item texture resolved." : "Buff potion item texture unavailable; fallback text will be used.",
                0,
                "{" +
                    "\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"textureResolved\":" + BoolRaw(resolved) + "," +
                    "\"textureWidth\":" + (entry == null ? "0" : entry.Width.ToString(CultureInfo.InvariantCulture)) + "," +
                    "\"textureHeight\":" + (entry == null ? "0" : entry.Height.ToString(CultureInfo.InvariantCulture)) + "," +
                    "\"drawMode\":\"" + (resolved ? "Contained" : "FallbackText") + "\"" +
                "}",
                "{" +
                    "\"textureResolved\":" + BoolRaw(resolved) +
                "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static void LogStatusLocked()
        {
            if (_logged)
            {
                return;
            }

            _logged = true;
            var json = BuildSkinStatusJsonLocked();
            Logger.Info("VanillaUiSkinCompat", "Vanilla UI skin status: " + json + (string.IsNullOrWhiteSpace(_lastError) ? string.Empty : ", error=" + _lastError));
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                FallbackUsed ? "Ui.SkinFallback" : "Ui.SkinResolved",
                "UI",
                string.Empty,
                FallbackUsed ? "NotApplicable" : "Succeeded",
                FallbackUsed ? "TextureUnavailable" : "Succeeded",
                FallbackUsed ? "Vanilla UI skin resources unavailable; fallback drawing is active." : "Vanilla UI skin resources resolved.",
                0,
                "{}",
                json,
                json,
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);

            if (!SkinPaletteResolved)
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    Guid.Empty,
                    "Ui.SkinPaletteFallback",
                    "UI",
                    string.Empty,
                    "NotApplicable",
                    "TextureUnavailable",
                    "Vanilla UI skin palette sampling failed; fallback palette is active.",
                    0,
                    "{}",
                    json,
                    "{\"skinPaletteSource\":\"" + EscapeJson(SkinPaletteSource) + "\"}",
                    "UI",
                    "LegacyMainWindow",
                    string.Empty,
                    string.Empty);
            }
        }

        private static string BuildSkinStatusJsonLocked()
        {
            return "{" +
                   "\"settingsPanelResolved\":" + BoolRaw(SettingsPanelResolved) + "," +
                   "\"settingsPanel2Resolved\":" + BoolRaw(SettingsPanel2Resolved) + "," +
                   "\"inventoryBackResolved\":" + BoolRaw(InventoryBackResolved) + "," +
                   "\"inventoryBackVariantCount\":" + InventoryBackVariantCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"colorBarResolved\":" + BoolRaw(ColorBarResolved) + "," +
                   "\"colorSliderResolved\":" + BoolRaw(ColorSliderResolved) + "," +
                   "\"colorBlipResolved\":" + BoolRaw(ColorBlipResolved) + "," +
                   "\"colorHighlightResolved\":" + BoolRaw(ColorHighlightResolved) + "," +
                   "\"scrollLeftButtonResolved\":" + BoolRaw(ScrollLeftButtonResolved) + "," +
                   "\"scrollRightButtonResolved\":" + BoolRaw(ScrollRightButtonResolved) + "," +
                   "\"textBackResolved\":" + BoolRaw(TextBackResolved) + "," +
                   "\"magicPixelResolved\":" + BoolRaw(MagicPixelResolved) + "," +
                   "\"itemTextureCollectionResolved\":" + BoolRaw(ItemTextureCollectionResolved) + "," +
                   "\"buffTextureCollectionResolved\":" + BoolRaw(BuffTextureCollectionResolved) + "," +
                   "\"skinSource\":\"" + EscapeJson(SkinSource) + "\"," +
                   "\"panelSkin\":\"" + EscapeJson(PanelSkin) + "\"," +
                   "\"buttonSkin\":\"" + EscapeJson(ButtonSkin) + "\"," +
                   "\"tooltipSkin\":\"" + EscapeJson(TooltipSkin) + "\"," +
                   "\"skinPaletteResolved\":" + BoolRaw(SkinPaletteResolved) + "," +
                   "\"skinPaletteSource\":\"" + EscapeJson(SkinPaletteSource) + "\"," +
                   "\"skinPalettePanel\":\"" + BuildPaletteColorHex(_skinPalette) + "\"," +
                   "\"fallbackUsed\":" + BoolRaw(FallbackUsed) +
                   "}";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static int ClampColor(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 255 ? 255 : value;
        }

        private static string BuildPaletteColorHex(LegacyUiSkinPalette palette)
        {
            if (palette == null)
            {
                return "#4B629A";
            }

            return "#" +
                   palette.PanelR.ToString("X2", CultureInfo.InvariantCulture) +
                   palette.PanelG.ToString("X2", CultureInfo.InvariantCulture) +
                   palette.PanelB.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private sealed class ItemTextureCacheEntry
        {
            public bool Resolved;
            public object Texture;
            public int Width;
            public int Height;
            public string Message;
        }
    }
}
