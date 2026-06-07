using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationStatusLineService
    {
        private const ulong StatusRefreshTicks = 30;

        private static readonly object SyncRoot = new object();
        private static readonly List<InformationStatusLine> CachedStatusLines = new List<InformationStatusLine>();

        private static ulong _lastStatusRefreshTick;
        private static string _lastStatusStyleSignature = string.Empty;
        private static long _cacheHitCount;
        private static long _cacheMissCount;

        internal static long CacheHitCount
        {
            get { return Interlocked.Read(ref _cacheHitCount); }
        }

        internal static long CacheMissCount
        {
            get { return Interlocked.Read(ref _cacheMissCount); }
        }

        internal static IList<InformationStatusLine> GetLines(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var cacheSignature = BuildCacheSignature(context, settings);
                if (CanReuseLines(_lastStatusRefreshTick, _lastStatusStyleSignature, context, cacheSignature))
                {
                    Interlocked.Increment(ref _cacheHitCount);
                    return CachedStatusLines;
                }

                Interlocked.Increment(ref _cacheMissCount);
                CachedStatusLines.Clear();
                BuildLines(CachedStatusLines, context, settings ?? AppSettings.CreateDefault());
                _lastStatusRefreshTick = context == null ? 0 : context.GameUpdateCount;
                _lastStatusStyleSignature = cacheSignature;
                return CachedStatusLines;
            }
        }

        internal static string BuildCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return BuildCacheSignature(context, settings);
        }

        internal static bool CanReuseLinesForTesting(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, AppSettings settings)
        {
            return CanReuseLines(lastRefreshTick, lastSignature, context, BuildCacheSignature(context, settings));
        }

        internal static void AddLine(ICollection<InformationStatusLine> lines, int order, string text, InformationColor color, double fontScale)
        {
            if (lines == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            lines.Add(new InformationStatusLine
            {
                Order = order,
                Text = text,
                Color = color,
                FontScale = InformationStyleHelper.NormalizeFontScale(fontScale, 0.72d)
            });
        }

        private static void BuildLines(ICollection<InformationStatusLine> lines, InformationWorldContext context, AppSettings settings)
        {
            if (settings.InformationBiomeDisplayEnabled)
            {
                InformationStatusSummaryBuilder.AddBiomeLine(lines, 10, context, settings);
            }

            if (settings.InformationWorldInfectionEnabled)
            {
                InformationStatusSummaryBuilder.AddWorldInfectionLine(lines, 20, context, settings);
            }

            if (settings.InformationLuckValueEnabled)
            {
                InformationStatusSummaryBuilder.AddLuckLines(lines, 30, context, settings);
            }

            var hasFishingBobber = false;
            var fishingFilterSonarActive = false;
            var fishingMessage = string.Empty;
            IList<FishingCatchCandidate> fishingCandidates = null;
            if (settings.InformationFishingCatchesEnabled || settings.InformationFishingFilteredCatchesEnabled)
            {
                fishingFilterSonarActive = FishingAutomationService.HasSonarBuffOnPlayer(context == null ? null : context.LocalPlayer);
                float bobberX;
                float bobberY;
                int bobberIdentity;
                hasFishingBobber = InformationBobberLocator.TryFindLocalBobber(context, out bobberX, out bobberY, out bobberIdentity);
                if (hasFishingBobber &&
                    (settings.InformationFishingCatchesEnabled || !InformationFishingStatusLineBuilder.IsFishingFilterDisabled(settings)))
                {
                    fishingCandidates = InformationFishingStatusLineBuilder.ResolveFishingCatchCandidates(
                        context,
                        bobberX,
                        bobberY,
                        bobberIdentity,
                        InformationFishingStatusLineBuilder.BuildFilterStatusSignature(settings),
                        out fishingMessage);
                }
            }

            if (settings.InformationFishingCatchesEnabled)
            {
                InformationFishingStatusLineBuilder.AddFishingCatchLines(
                    lines,
                    40,
                    hasFishingBobber,
                    fishingCandidates,
                    fishingMessage,
                    InformationColorHelper.FishingCatchesText(settings),
                    InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingCatchesFeatureId));
            }

            if (settings.InformationFishingFilteredCatchesEnabled)
            {
                InformationFishingStatusLineBuilder.AddFilteredFishingCatchLines(
                    lines,
                    45,
                    settings,
                    hasFishingBobber,
                    fishingFilterSonarActive,
                    fishingCandidates,
                    fishingMessage,
                    InformationColorHelper.FishingFilteredCatchesText(settings),
                    InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId));
            }

            if (settings.InformationAnglerQuestEnabled)
            {
                InformationStatusSummaryBuilder.AddAnglerQuestLine(lines, 50, context, settings);
            }
        }

        private static bool CanReuseLines(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, string currentSignature)
        {
            return context != null &&
                   context.GameUpdateCount != 0 &&
                   lastRefreshTick != 0 &&
                   string.Equals(lastSignature, currentSignature, StringComparison.Ordinal) &&
                   context.GameUpdateCount >= lastRefreshTick &&
                   context.GameUpdateCount - lastRefreshTick < StatusRefreshTicks;
        }

        private static string BuildCacheSignature(InformationWorldContext context, AppSettings settings)
        {
            return BuildStyleSignature(settings) + "|ctx:" +
                   (context == null ? string.Empty : context.WorldKey ?? string.Empty) + "|" +
                   BuildLocalPlayerIdentity(context == null ? null : context.LocalPlayer) + "|" +
                   CultureInfo.CurrentUICulture.Name + "|" +
                   CultureInfo.CurrentCulture.Name;
        }

        private static string BuildStyleSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return (settings.InformationBiomeDisplayEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.BiomeDisplayFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.BiomeDisplayFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationWorldInfectionEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.WorldInfectionFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.WorldInfectionFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationLuckValueEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.LuckValueFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.LuckValueFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationFishingCatchesEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingCatchesFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingCatchesFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationFishingFilteredCatchesEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   InformationFishingStatusLineBuilder.BuildFilterStatusSignature(settings) + "|" +
                   (settings.InformationAnglerQuestEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.AnglerQuestFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.AnglerQuestFeatureId).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string BuildLocalPlayerIdentity(object player)
        {
            return player == null
                ? string.Empty
                : RuntimeHelpers.GetHashCode(player).ToString(CultureInfo.InvariantCulture);
        }
    }
}
