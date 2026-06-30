using System;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class UiDrawLifecycleGuard
    {
        private static readonly object SyncRoot = new object();
        private static Type _spriteBatchStateType;
        private static FieldInfo _beginStateField;
        private static PropertyInfo _beginStateProperty;
        private static bool _beginStateLookupDone;

        public static bool TryEnterWorldDraw(string owner, out object spriteBatch)
        {
            var safeOwner = string.IsNullOrWhiteSpace(owner) ? "UnknownWorldLayer" : owner;
            Microsoft.Xna.Framework.Graphics.SpriteBatch worldSpriteBatch;
            if (!TerrariaDrawCompat.TryGetSpriteBatch(out worldSpriteBatch))
            {
                spriteBatch = null;
                LogWorldSkip(safeOwner, "spriteBatchUnavailable", "Terraria.Main.spriteBatch is unavailable for world drawing.");
                return false;
            }

            spriteBatch = worldSpriteBatch;
            bool begun;
            if (TryReadSpriteBatchBegun(spriteBatch, out begun) && !begun)
            {
                LogWorldSkip(safeOwner, "spriteBatchNotBegun", "Terraria world layer is not inside a SpriteBatch.Begin/End pair; JueMing-Z will not Begin here.");
                return false;
            }

            // World-layer hooks run inside Terraria's own world SpriteBatch
            // lifecycle. Keep this guard separate from interface overlays so a
            // wall ghost cannot silently drift back into the late UI layer.
            if (!UiPrimitiveRenderer.EnsureReady(spriteBatch))
            {
                LogWorldSkip(safeOwner, "primitiveRendererNotReady", "UI primitive renderer is not ready for world drawing: " + UiPrimitiveRenderer.LastError);
                return false;
            }

            return true;
        }

        public static bool TryEnterInterfaceDraw(string owner, bool requireVanillaResources, out object spriteBatch)
        {
            // Interface overlays must draw inside Terraria's active SpriteBatch; starting
            // our own Begin here would corrupt vanilla draw state.
            var safeOwner = string.IsNullOrWhiteSpace(owner) ? "UnknownUiLayer" : owner;
            spriteBatch = UiTextRenderer.GetSpriteBatch();
            if (spriteBatch == null)
            {
                LogSkip(safeOwner, "spriteBatchUnavailable", "Terraria.Main.spriteBatch is unavailable.");
                return false;
            }

            bool begun;
            if (TryReadSpriteBatchBegun(spriteBatch, out begun) && !begun)
            {
                LogSkip(safeOwner, "spriteBatchNotBegun", "Terraria interface layer is not inside a SpriteBatch.Begin/End pair; JueMing-Z will not Begin here.");
                return false;
            }

            if (requireVanillaResources && !VanillaUiSkinCompat.PrepareForDraw(safeOwner))
            {
                LogSkip(safeOwner, "resourcesNotReady", "Vanilla UI resources are not ready: " + VanillaUiSkinCompat.LastError);
                return false;
            }

            if (!UiPrimitiveRenderer.EnsureReady(spriteBatch))
            {
                LogSkip(safeOwner, "primitiveRendererNotReady", "UI primitive renderer is not ready: " + UiPrimitiveRenderer.LastError);
                return false;
            }

            return true;
        }

        internal static string GetWorldDrawLifecycleContractForTesting()
        {
            return "world-spritebatch-active+no-begin-end+primitive-renderer-ready";
        }

        public static void RecordDrawException(string owner, Exception error)
        {
            if (error == null)
            {
                return;
            }

            if (!LooksLikeResourceFailure(error))
            {
                return;
            }

            var safeOwner = string.IsNullOrWhiteSpace(owner) ? "UnknownUiLayer" : owner;
            VanillaUiSkinCompat.InvalidateCachedResources(safeOwner + " draw exception: " + GetRootMessage(error));
            UiPrimitiveRenderer.InvalidateCachedResources(safeOwner + " draw exception.");
            UiTextRenderer.InvalidateCachedResources(safeOwner + " draw exception.");
            UiEmbeddedTextureLoader.InvalidateCachedResources(safeOwner + " draw exception.");
        }

        private static bool TryReadSpriteBatchBegun(object spriteBatch, out bool begun)
        {
            begun = false;
            if (spriteBatch == null)
            {
                return false;
            }

            var type = spriteBatch.GetType();
            lock (SyncRoot)
            {
                if (_spriteBatchStateType != type)
                {
                    _spriteBatchStateType = type;
                    _beginStateField = null;
                    _beginStateProperty = null;
                    _beginStateLookupDone = false;
                }

                if (!_beginStateLookupDone)
                {
                    ResolveBeginStateMemberLocked(type);
                    _beginStateLookupDone = true;
                }
            }

            try
            {
                object raw = null;
                if (_beginStateField != null)
                {
                    raw = _beginStateField.GetValue(spriteBatch);
                }
                else if (_beginStateProperty != null && _beginStateProperty.CanRead)
                {
                    raw = _beginStateProperty.GetValue(spriteBatch, null);
                }

                if (raw is bool)
                {
                    begun = (bool)raw;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void ResolveBeginStateMemberLocked(Type spriteBatchType)
        {
            if (spriteBatchType == null)
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var exactNames = new[] { "_beginCalled", "beginCalled", "BeginCalled", "_hasBegun", "HasBegun" };
            for (var index = 0; index < exactNames.Length; index++)
            {
                var field = spriteBatchType.GetField(exactNames[index], flags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    _beginStateField = field;
                    return;
                }

                var property = spriteBatchType.GetProperty(exactNames[index], flags);
                if (property != null && property.PropertyType == typeof(bool))
                {
                    _beginStateProperty = property;
                    return;
                }
            }

            var fields = spriteBatchType.GetFields(flags);
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (field.FieldType != typeof(bool))
                {
                    continue;
                }

                var name = field.Name ?? string.Empty;
                if (name.IndexOf("begin", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("called", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("begun", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _beginStateField = field;
                    return;
                }
            }
        }

        private static bool LooksLikeResourceFailure(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                var text = (current.GetType().FullName + " " + current.Message).ToLowerInvariant();
                if (text.IndexOf("disposed", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("texture", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("asset", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("content", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("spritebatch", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("render", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRootMessage(Exception error)
        {
            var current = error;
            while (current != null && current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current == null ? string.Empty : current.Message;
        }

        private static void LogSkip(string owner, string reasonKey, string message)
        {
            LogThrottle.WarnThrottled(
                "ui-draw-skip-" + owner + "-" + reasonKey,
                TimeSpan.FromSeconds(10),
                owner,
                "JueMing-Z UI draw skipped this frame. " + message);
        }

        private static void LogWorldSkip(string owner, string reasonKey, string message)
        {
            LogThrottle.WarnThrottled(
                "world-draw-skip-" + owner + "-" + reasonKey,
                TimeSpan.FromSeconds(10),
                owner,
                "JueMing-Z world draw skipped this frame. " + message);
        }
    }
}
