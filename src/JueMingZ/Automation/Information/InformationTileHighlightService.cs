using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationTileHighlightService
    {
        private const int TileSize = 16;
        private const int ScanMarginTiles = 4;
        private const int PlayerChunkTiles = 16;
        private const int LifeCrystalMask = 1 << 0;
        private const int ManaCrystalMask = 1 << 1;
        private const int DigtoiseMask = 1 << 2;
        private const int LifeFruitMask = 1 << 3;
        private const int DragonEggMask = 1 << 4;
        private const ulong ScanIntervalTicks = 60;

        private static readonly object SyncRoot = new object();
        private static readonly TileHighlight[] EmptyHighlights = new TileHighlight[0];
        private static readonly List<TileHighlight> BuildBuffer = new List<TileHighlight>();
        private static readonly List<TileHighlight> CurrentHighlightFilterBuffer = new List<TileHighlight>();

        private static TileHighlight[] _cachedHighlights = EmptyHighlights;
        private static ulong _lastScanTick;
        private static uint _lastSignatureHash;

        internal static int Draw(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            if (!HasAnyEnabled(settings) ||
                context == null ||
                !InformationPlayerDetectionService.HasMetalDetector(context.LocalPlayer))
            {
                return 0;
            }

            var highlights = GetHighlights(context, settings);
            return InformationTileHighlightRenderer.Draw(spriteBatch, context, highlights);
        }

        internal static string BuildCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return BuildScanSignature(context, settings).Hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        internal static bool ShouldRefreshCacheForTesting(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            return ShouldRefreshCore(lastScanTick, previousSignatureHash, currentTick, currentSignatureHash);
        }

        internal static bool IsManaCrystalTileTypeForTesting(int tileType)
        {
            return InformationTileHighlightScanner.IsManaCrystalTileTypeForTesting(tileType);
        }

        internal static int GetHighlightCountForTesting(InformationWorldContext context, AppSettings settings)
        {
            return HasAnyEnabled(settings) ? GetHighlights(context, settings).Length : 0;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                BuildBuffer.Clear();
                CurrentHighlightFilterBuffer.Clear();
                _cachedHighlights = EmptyHighlights;
                _lastScanTick = 0;
                _lastSignatureHash = 0;
                InformationTileHighlightScanner.ResetForTesting();
            }
        }

        private static TileHighlight[] GetHighlights(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var signature = BuildScanSignature(context, settings);
                // Signature plus scan interval gate the cache; do not replace
                // this with unconditional per-frame tile scans.
                if (!ShouldRefresh(context, signature.Hash))
                {
                    return FilterCurrentHighlights(context, _cachedHighlights);
                }

                BuildBuffer.Clear();
                InformationTileHighlightScanner.Scan(context, settings, signature.Bounds, BuildColors(settings), BuildBuffer);
                _cachedHighlights = BuildBuffer.Count == 0 ? EmptyHighlights : BuildBuffer.ToArray();
                _lastScanTick = context == null ? 0 : context.GameUpdateCount;
                _lastSignatureHash = signature.Hash;
                return FilterCurrentHighlights(context, _cachedHighlights);
            }
        }

        private static TileHighlight[] FilterCurrentHighlights(InformationWorldContext context, TileHighlight[] highlights)
        {
            if (highlights == null || highlights.Length == 0)
            {
                return EmptyHighlights;
            }

            CurrentHighlightFilterBuffer.Clear();
            var changed = false;
            for (var index = 0; index < highlights.Length; index++)
            {
                var highlight = highlights[index];
                // Cached highlight groups are draw candidates only; the bounded
                // rectangle must still contain at least one matching active tile.
                if (!InformationTileHighlightScanner.ContainsActiveTileType(context, highlight))
                {
                    if (!changed)
                    {
                        CopyHighlightsToFilterBuffer(highlights, index);
                        changed = true;
                    }

                    continue;
                }

                if (changed)
                {
                    CurrentHighlightFilterBuffer.Add(highlight);
                }
            }

            if (!changed)
            {
                return highlights;
            }

            return CurrentHighlightFilterBuffer.Count == 0 ? EmptyHighlights : CurrentHighlightFilterBuffer.ToArray();
        }

        private static void CopyHighlightsToFilterBuffer(TileHighlight[] highlights, int count)
        {
            for (var index = 0; index < count; index++)
            {
                CurrentHighlightFilterBuffer.Add(highlights[index]);
            }
        }

        private static bool ShouldRefresh(InformationWorldContext context, uint currentSignatureHash)
        {
            return ShouldRefreshCore(
                _lastScanTick,
                _lastSignatureHash,
                context == null ? 0 : context.GameUpdateCount,
                currentSignatureHash);
        }

        private static bool ShouldRefreshCore(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            if (lastScanTick == 0 || previousSignatureHash != currentSignatureHash)
            {
                return true;
            }

            if (currentTick == 0)
            {
                return false;
            }

            if (currentTick < lastScanTick)
            {
                return true;
            }

            return currentTick - lastScanTick >= ScanIntervalTicks;
        }

        private static TileHighlightScanSignature BuildScanSignature(InformationWorldContext context, AppSettings settings)
        {
            // The signature scopes visible-range scans; range, player chunk,
            // enabled mask, world, and colors are part of the highlight cache.
            var bounds = BuildScanBounds(context);
            var enabledMask = BuildEnabledMask(settings);
            unchecked
            {
                var hash = 2166136261u;
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldKey);
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldRecordKey);
                AddHashInt(ref hash, bounds.MinX);
                AddHashInt(ref hash, bounds.MinY);
                AddHashInt(ref hash, bounds.MaxX);
                AddHashInt(ref hash, bounds.MaxY);
                AddHashInt(ref hash, BuildPlayerChunkX(context));
                AddHashInt(ref hash, BuildPlayerChunkY(context));
                AddHashInt(ref hash, enabledMask);
                if ((enabledMask & LifeCrystalMask) != 0)
                {
                    AddHashValue(ref hash, BuildColorSignature(settings == null ? null : settings.InformationLifeCrystalHighlightColor));
                }

                if ((enabledMask & ManaCrystalMask) != 0)
                {
                    AddHashValue(ref hash, BuildColorSignature(settings == null ? null : settings.InformationManaCrystalHighlightColor));
                }

                if ((enabledMask & LifeFruitMask) != 0)
                {
                    AddHashValue(ref hash, BuildColorSignature(settings == null ? null : settings.InformationLifeFruitHighlightColor));
                }

                if ((enabledMask & DragonEggMask) != 0)
                {
                    AddHashValue(ref hash, BuildColorSignature(settings == null ? null : settings.InformationDragonEggHighlightColor));
                }

                return new TileHighlightScanSignature(hash, bounds);
            }
        }

        private static TileHighlightScanBounds BuildScanBounds(InformationWorldContext context)
        {
            var screenX = context == null ? 0f : context.ScreenX;
            var screenY = context == null ? 0f : context.ScreenY;
            var screenWidth = context == null ? 0 : context.ScreenWidth;
            var screenHeight = context == null ? 0 : context.ScreenHeight;
            var minX = Math.Max(0, (int)Math.Floor(screenX / TileSize) - ScanMarginTiles);
            var minY = Math.Max(0, (int)Math.Floor(screenY / TileSize) - ScanMarginTiles);
            var maxX = Math.Max(minX, (int)Math.Ceiling((screenX + screenWidth) / TileSize) + ScanMarginTiles);
            var maxY = Math.Max(minY, (int)Math.Ceiling((screenY + screenHeight) / TileSize) + ScanMarginTiles);
            return new TileHighlightScanBounds(minX, minY, maxX, maxY);
        }

        private static int BuildEnabledMask(AppSettings settings)
        {
            var mask = 0;
            if (settings == null)
            {
                return mask;
            }

            if (settings.InformationHighlightLifeCrystalEnabled) mask |= LifeCrystalMask;
            if (settings.InformationHighlightManaCrystalEnabled) mask |= ManaCrystalMask;
            if (settings.InformationHighlightDigtoiseEnabled) mask |= DigtoiseMask;
            if (settings.InformationHighlightLifeFruitEnabled) mask |= LifeFruitMask;
            if (settings.InformationHighlightDragonEggEnabled) mask |= DragonEggMask;
            return mask;
        }

        private static bool HasAnyEnabled(AppSettings settings)
        {
            return settings != null &&
                   (settings.InformationHighlightLifeCrystalEnabled ||
                    settings.InformationHighlightManaCrystalEnabled ||
                    settings.InformationHighlightDigtoiseEnabled ||
                    settings.InformationHighlightLifeFruitEnabled ||
                    settings.InformationHighlightDragonEggEnabled);
        }

        private static int BuildPlayerChunkX(InformationWorldContext context)
        {
            var tileX = context == null ? 0 : (int)Math.Floor(context.PlayerCenterX / TileSize);
            return FloorDiv(tileX, PlayerChunkTiles);
        }

        private static int BuildPlayerChunkY(InformationWorldContext context)
        {
            var tileY = context == null ? 0 : (int)Math.Floor(context.PlayerCenterY / TileSize);
            return FloorDiv(tileY, PlayerChunkTiles);
        }

        private static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            if (value >= 0)
            {
                return value / divisor;
            }

            return -(((-value) + divisor - 1) / divisor);
        }

        private static string BuildColorSignature(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static TileHighlightColors BuildColors(AppSettings settings)
        {
            return new TileHighlightColors(
                InformationColorHelper.LifeCrystal(settings),
                InformationColorHelper.ManaCrystal(settings),
                InformationColorHelper.Digtoise(settings),
                InformationColorHelper.LifeFruit(settings),
                InformationColorHelper.DragonEgg(settings));
        }

        private static void AddHashValue(ref uint hash, string value)
        {
            unchecked
            {
                var text = value ?? string.Empty;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                hash ^= 31u;
                hash *= 16777619u;
            }
        }

        private static void AddHashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                hash ^= (uint)(value >> 16);
                hash *= 16777619u;
                hash ^= 31u;
                hash *= 16777619u;
            }
        }
    }
}
