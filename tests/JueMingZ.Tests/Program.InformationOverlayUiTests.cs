using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void EnemySegmentLabelsHideMiddleSegments()
        {
            if (!InformationOverlayService.ShouldDrawEnemySegmentLabel(1, 0))
            {
                throw new InvalidOperationException("Expected ordinary single enemies to draw labels.");
            }

            if (!InformationOverlayService.ShouldDrawEnemySegmentLabel(5, 1))
            {
                throw new InvalidOperationException("Expected segment endpoints to draw labels.");
            }

            if (InformationOverlayService.ShouldDrawEnemySegmentLabel(5, 2))
            {
                throw new InvalidOperationException("Expected middle segments with two neighbors to hide labels.");
            }
        }

        private static void KnownWormSegmentLabelsUseTerrariaNpcRoles()
        {
            AssertSegmentLabelRoles("Devourer", new[] { 7, 9 }, new[] { 8 });
            AssertSegmentLabelRoles("Giant Worm", new[] { 10, 12 }, new[] { 11 });
            AssertSegmentLabelRoles("Eater of Worlds", new[] { 13, 15 }, new[] { 14 });
            AssertSegmentLabelRoles("Bone Serpent", new[] { 39, 41 }, new[] { 40 });
            AssertSegmentLabelRoles("Wyvern", new[] { 87, 92 }, new[] { 88, 89, 90, 91 });
            AssertSegmentLabelRoles("Digger", new[] { 95, 97 }, new[] { 96 });
            AssertSegmentLabelRoles("Seeker", new[] { 98, 100 }, new[] { 99 });
            AssertSegmentLabelRoles("Leech", new[] { 117, 119 }, new[] { 118 });
            AssertSegmentLabelRoles("Destroyer", new[] { 134, 136 }, new[] { 135 });
            AssertSegmentLabelRoles("Stardust Worm", new[] { 402, 404 }, new[] { 403 });
            AssertSegmentLabelRoles("Solar Crawltipede", new[] { 412, 414 }, new[] { 413 });
            AssertSegmentLabelRoles("Cultist Dragon", new[] { 454, 459 }, new[] { 455, 456, 457, 458 });
            AssertSegmentLabelRoles("Dune Splicer", new[] { 510, 512 }, new[] { 511 });
            AssertSegmentLabelRoles("Tomb Crawler", new[] { 513, 515 }, new[] { 514 });
            AssertSegmentLabelRoles("Blood Eel", new[] { 621, 623 }, new[] { 622 });
        }

        private static void SkeletonMerchantCountsAsInformationNpcLabel()
        {
            const int skeletonMerchantNpcType = 453;
            const int ordinaryEnemyNpcType = 3;

            if (!InformationOverlayService.IsNpcNameLabelCandidateForTesting(skeletonMerchantNpcType, false))
            {
                throw new InvalidOperationException("Expected Skeleton Merchant to count as an NPC name label candidate.");
            }

            if (InformationOverlayService.IsEnemyNameLabelCandidateForTesting(skeletonMerchantNpcType, false, false, 250, 250))
            {
                throw new InvalidOperationException("Expected Skeleton Merchant to stay out of enemy name labels.");
            }

            if (InformationOverlayService.IsNpcNameLabelCandidateForTesting(ordinaryEnemyNpcType, false))
            {
                throw new InvalidOperationException("Expected ordinary enemies to stay out of NPC name labels.");
            }

            if (!InformationOverlayService.IsEnemyNameLabelCandidateForTesting(ordinaryEnemyNpcType, false, false, 40, 40))
            {
                throw new InvalidOperationException("Expected ordinary enemies to remain eligible for enemy name labels.");
            }
        }

        private static void EnemyHealthLabelUsesCompactHealthLine()
        {
            var healthText = InformationOverlayService.BuildEnemyHealthTextForTesting(238, 500);
            if (!string.Equals(healthText, "238/500", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected enemy health label text to use current/max format.");
            }

            var healthScale = InformationOverlayService.ResolveEnemyHealthFontScaleForTesting(0.80f);
            if (Math.Abs(healthScale - 0.67f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label scale to be 0.13 below the name scale.");
            }

            var clampedHealthScale = InformationOverlayService.ResolveEnemyHealthFontScaleForTesting(0.50f);
            if (Math.Abs(clampedHealthScale - 0.50f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label scale to respect the shared minimum font scale.");
            }

            var tightAdvance = InformationWorldLabelRenderer.ResolveTightStackedLineAdvanceForTesting(0.80f, 0.67f);
            if (Math.Abs(tightAdvance - 12.0f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label rows to leave enough tight spacing for the larger health scale.");
            }
        }

        private static void EnemyHealthLabelSnapshotTracksLifeText()
        {
            if (!InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 80, 100))
            {
                throw new InvalidOperationException("Expected enemy health label cache to reuse unchanged life text.");
            }

            if (InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 79, 100))
            {
                throw new InvalidOperationException("Expected enemy health label cache to dirty when current life changes.");
            }

            if (InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 80, 120))
            {
                throw new InvalidOperationException("Expected enemy health label cache to dirty when max life changes.");
            }
        }

        private static void AssertSegmentLabelRoles(string name, int[] headOrTailTypes, int[] bodyTypes)
        {
            foreach (var npcType in headOrTailTypes)
            {
                if (!InformationOverlayService.ShouldDrawEnemyNpcTypeLabelForTesting(npcType))
                {
                    throw new InvalidOperationException("Expected " + name + " head/tail type " + npcType.ToString(CultureInfo.InvariantCulture) + " to draw labels.");
                }
            }

            foreach (var npcType in bodyTypes)
            {
                if (InformationOverlayService.ShouldDrawEnemyNpcTypeLabelForTesting(npcType))
                {
                    throw new InvalidOperationException("Expected " + name + " body type " + npcType.ToString(CultureInfo.InvariantCulture) + " to hide labels.");
                }
            }
        }

        private static void InformationSignTextAllModeKeepsVanillaLineCap()
        {
            var text = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12";
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting(text, InformationSignTextModes.All, 99, 999, 0.7f);
            if (lines.Length != InformationSignTextModes.VanillaDisplayMaxLines)
            {
                throw new InvalidOperationException("Expected all mode to keep vanilla 10-line display cap.");
            }

            if (!lines[lines.Length - 1].EndsWith("...", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected all mode to mark text hidden past the vanilla display cap.");
            }
        }

        private static void InformationSignTextLineModeRespectsConfiguredLines()
        {
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting("alpha\nbeta\ngamma", InformationSignTextModes.Lines, 2, 999, 0.7f);
            if (lines.Length != 2 ||
                !string.Equals(lines[0], "alpha", StringComparison.Ordinal) ||
                !lines[1].StartsWith("beta", StringComparison.Ordinal) ||
                !lines[1].EndsWith("...", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected line mode to show only the configured leading lines and mark truncation.");
            }

            if (InformationSignTextModes.ClampLines(99) != InformationSignTextModes.VanillaDisplayMaxLines)
            {
                throw new InvalidOperationException("Expected sign line limit to clamp to the vanilla 10-line display cap.");
            }
        }

        private static void InformationSignTextCharacterModeTruncatesBeforeWrapping()
        {
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting("abcdef ghij", InformationSignTextModes.Characters, 10, 5, 0.7f);
            if (lines.Length != 1 ||
                !lines[0].StartsWith("abcde", StringComparison.Ordinal) ||
                !lines[0].EndsWith("...", StringComparison.Ordinal) ||
                lines[0].Contains("f"))
            {
                throw new InvalidOperationException("Expected character mode to trim by characters before wrapping.");
            }

            if (InformationSignTextModes.ClampCharacters(9999) != InformationSignTextModes.MaxCharacters)
            {
                throw new InvalidOperationException("Expected sign character limit to clamp to configured maximum.");
            }

            if (InformationSignTextModes.MaxCharacters != 1200 ||
                InformationSignTextModes.AdjustCharacters(80, 1) != 81 ||
                InformationSignTextModes.AdjustCharacters(80, -1) != 79 ||
                InformationSignTextModes.AdjustCharacters(1, -1) != 1)
            {
                throw new InvalidOperationException("Expected sign character limit to use the vanilla virtual keyboard max and adjust one character at a time.");
            }
        }

        private static void InformationSignTextCentersEachDisplayedLineOnSign()
        {
            var x = InformationOverlayService.CalculateSignTextLineXForTesting(100f, 40, 240);
            if (Math.Abs(x - 80f) > 0.001f)
            {
                throw new InvalidOperationException("Expected sign text line to center on sign center.");
            }

            var leftClamped = InformationOverlayService.CalculateSignTextLineXForTesting(10f, 40, 240);
            if (Math.Abs(leftClamped - 4f) > 0.001f)
            {
                throw new InvalidOperationException("Expected centered sign text to clamp at the left screen edge.");
            }

            var rightClamped = InformationOverlayService.CalculateSignTextLineXForTesting(230f, 40, 240);
            if (Math.Abs(rightClamped - 196f) > 0.001f)
            {
                throw new InvalidOperationException("Expected centered sign text to clamp at the right screen edge.");
            }
        }

        private static void InformationSignTextLayoutCacheReusesWrappedLines()
        {
            InformationOverlayService.ResetSignTextLayoutCacheForTesting();
            try
            {
                var first = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta gamma", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (first.RebuildCount != 1 ||
                    first.LineCount <= 0 ||
                    string.IsNullOrEmpty(first.FirstLineText) ||
                    first.FirstLineWidth <= 0 ||
                    first.LineHeight <= 0 ||
                    first.TotalHeight != first.LineHeight * first.LineCount)
                {
                    throw new InvalidOperationException("Expected initial sign text layout to cache wrapped lines, widths and height.");
                }

                var same = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta gamma", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (same.RebuildCount != first.RebuildCount ||
                    same.LineCount != first.LineCount ||
                    !string.Equals(same.FirstLineText, first.FirstLineText, StringComparison.Ordinal) ||
                    same.FirstLineWidth != first.FirstLineWidth ||
                    same.TotalHeight != first.TotalHeight)
                {
                    throw new InvalidOperationException("Expected identical sign text layout key to reuse the cached layout.");
                }

                var signDiagnostics = InformationOverlayService.GetDiagnostics();
                if (signDiagnostics.SignTextLayoutCacheMissCount != 1 ||
                    signDiagnostics.SignTextLayoutCacheHitCount != 1)
                {
                    throw new InvalidOperationException("Expected sign text layout diagnostics to count cache hit and miss paths.");
                }

                var changedText = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (changedText.RebuildCount <= same.RebuildCount)
                {
                    throw new InvalidOperationException("Expected changed sign text to invalidate the cached layout.");
                }

                var changedMode = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Characters, 3, 5, 0.7f);
                if (changedMode.RebuildCount <= changedText.RebuildCount ||
                    !changedMode.FirstLineText.EndsWith("...", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected sign text layout mode and character limit to be part of the cache key.");
                }

                var beforeFontInvalidation = changedMode.RebuildCount;
                UiTextRenderer.InvalidateCachedResources("sign text layout cache test");
                var fontInvalidated = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Characters, 3, 5, 0.7f);
                if (fontInvalidated.RebuildCount <= beforeFontInvalidation)
                {
                    throw new InvalidOperationException("Expected UI text renderer cache invalidation to rebuild sign text layout.");
                }
            }
            finally
            {
                InformationOverlayService.ResetSignTextLayoutCacheForTesting();
            }
        }

        private static void InformationPageContentHeightIncludesLimitRows()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationSignTextLabelsMode = InformationSignTextModes.Off;
            settings.InformationTombstoneTextLabelsMode = InformationSignTextModes.Off;
            var baseHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            var rowStep = LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            settings.InformationSignTextLabelsMode = InformationSignTextModes.Lines;
            var signLimitHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            if (signLimitHeight != baseHeight + rowStep)
            {
                throw new InvalidOperationException("Expected information content height to include the sign limit row.");
            }

            settings.InformationTombstoneTextLabelsMode = InformationSignTextModes.Characters;
            var bothLimitHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            if (bothLimitHeight != baseHeight + rowStep * 2)
            {
                throw new InvalidOperationException("Expected information content height to include both sign and tombstone limit rows.");
            }
        }

        private static void UiClippedSingleLineTextAvoidsBottomStick()
        {
            float drawX;
            float drawY;
            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("半露文字", 12, 86, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected top-clipped single line text to remain drawable.");
            }

            if (drawY >= 100f)
            {
                throw new InvalidOperationException("Expected top-clipped text to keep natural Y for partial clipping.");
            }

            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部可见", 12, 260, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected bottom-clipped single line text to draw when its natural position is still inside the viewport.");
            }

            var maxY = 100f + 180f - 16f * 0.86f;
            if (drawY > maxY + 0.01f)
            {
                throw new InvalidOperationException("Expected naturally visible bottom text to stay inside viewport.");
            }

            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部半露", 12, 268, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected bottom half-visible text to remain drawable.");
            }

            if (drawY <= maxY + 0.01f)
            {
                throw new InvalidOperationException("Expected bottom half-visible text to keep natural Y beyond clip edge.");
            }

            if (UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部越界", 12, 286, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected fully out-of-viewport bottom text to stay hidden.");
            }

            if (UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("顶部越界", 12, 68, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected fully out-of-viewport top text to stay hidden.");
            }
        }

        private static void InformationWorldContextCacheScopesStatusProfile()
        {
            var player = new object();
            var cachedStatus = CreateInformationContextForCacheTest(player, "world-a#1", 100, 12f, 24f, 800, 600);
            var sameProbe = CreateInformationContextForCacheTest(player, "world-a#1", 100, 12f, 24f, 800, 600);
            if (!InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.Status,
                    InformationWorldContextProfile.Status,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected same tick, screen, player and world to reuse the status context cache.");
            }

            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.Status,
                    InformationWorldContextProfile.FullRecord,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected a status-only cached context not to satisfy full record callers.");
            }

            if (!InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected a full record cached context to satisfy status callers.");
            }

            var movedProbe = CreateInformationContextForCacheTest(player, "world-a#1", 100, 16f, 24f, 800, 600);
            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    movedProbe))
            {
                throw new InvalidOperationException("Expected screen movement to dirty the information world context cache.");
            }

            var worldProbe = CreateInformationContextForCacheTest(player, "world-b#2", 100, 12f, 24f, 800, 600);
            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    worldProbe))
            {
                throw new InvalidOperationException("Expected world changes to dirty the information world context cache.");
            }

            if (InformationWorldContextProvider.RequiresFileDataForTesting(InformationWorldContextProfile.Status))
            {
                throw new InvalidOperationException("Expected status context profile to avoid full player/world file data.");
            }

            if (!InformationWorldContextProvider.RequiresFileDataForTesting(InformationWorldContextProfile.FullRecord))
            {
                throw new InvalidOperationException("Expected full record context profile to keep player/world file data.");
            }

            if (InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.Status, player, "world-a#1", 100, player, "world-a#1", 160))
            {
                throw new InvalidOperationException("Expected status profile to skip file data refresh checks.");
            }

            if (InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-a#1", 159))
            {
                throw new InvalidOperationException("Expected full record file data to reuse within the low-frequency refresh window.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-a#1", 160))
            {
                throw new InvalidOperationException("Expected full record file data to refresh at the low-frequency cadence.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, new object(), "world-a#1", 110))
            {
                throw new InvalidOperationException("Expected player changes to dirty full record file data.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-b#2", 110))
            {
                throw new InvalidOperationException("Expected world changes to dirty full record file data.");
            }
        }

        private static void InformationStatusLineCacheTracksContextIdentity()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

                var settings = AppSettings.CreateDefault();
                settings.InformationBiomeDisplayEnabled = true;
                var player = new object();
                var context = CreateInformationContextForCacheTest(player, "world-a#1", 120, 12f, 24f, 800, 600);
                var signature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                if (!InformationOverlayService.CanReuseStatusLinesForTesting(100, signature, context, settings))
                {
                    throw new InvalidOperationException("Expected unchanged status line context to reuse within the refresh cadence.");
                }

                context.GameUpdateCount = 130;
                if (InformationOverlayService.CanReuseStatusLinesForTesting(100, signature, context, settings))
                {
                    throw new InvalidOperationException("Expected status line cache to refresh at the 30 tick cadence.");
                }

                context.GameUpdateCount = 120;
                var worldChanged = CreateInformationContextForCacheTest(player, "world-b#2", 120, 12f, 24f, 800, 600);
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(worldChanged, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to track world identity.");
                }

                var playerChanged = CreateInformationContextForCacheTest(new object(), "world-a#1", 120, 12f, 24f, 800, 600);
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(playerChanged, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to track local player identity.");
                }

                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to dirty on UI language changes.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingStatusLineBuilderKeepsDisplayRows()
        {
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            settings.FishingFilterAllowExactEntries.Add(new FishingFilterExactEntry
            {
                Kind = FishingCatchKinds.Item,
                Id = 1,
                DisplayNameSnapshot = "Bass"
            });

            var candidates = new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 1,
                    DisplayName = "Bass",
                    DisplayNameSnapshot = "Bass"
                },
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 2,
                    DisplayName = "Old Shoe",
                    DisplayNameSnapshot = "Old Shoe"
                }
            };

            var color = new InformationColor(135, 206, 250, 255);
            var lines = new List<InformationStatusLine>();
            InformationFishingStatusLineBuilder.AddFishingCatchLines(lines, 40, true, candidates, string.Empty, color, 0.72d);
            InformationFishingStatusLineBuilder.AddFilteredFishingCatchLines(lines, 45, settings, true, true, candidates, string.Empty, color, 0.72d);

            if (lines.Count != 2 ||
                lines[0].Order != 40 ||
                !string.Equals(lines[0].Text, "完整鱼获: Bass、Old Shoe", StringComparison.Ordinal) ||
                lines[1].Order != 45 ||
                !string.Equals(lines[1].Text, "过滤鱼获: Bass", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing status builders to keep full and filtered display rows separate.");
            }

            settings.FishingFilterMode = FishingFilterModes.Disabled;
            lines.Clear();
            InformationFishingStatusLineBuilder.AddFilteredFishingCatchLines(lines, 45, settings, true, true, candidates, string.Empty, color, 0.72d);
            if (lines.Count != 1 || !string.Equals(lines[0].Text, "过滤鱼获: 过滤未启用", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected filtered fishing display to keep the disabled-filter row.");
            }
        }

        private static void InformationStatusPanelLayoutCacheReusesPreparedRows()
        {
            InformationStatusPanelService.ResetLayoutCacheForTesting();
            try
            {
                var lines = new List<InformationStatusLine>
                {
                    new InformationStatusLine
                    {
                        Text = "群系: 森林",
                        Color = new InformationColor(210, 235, 255, 240),
                        FontScale = 0.72d
                    },
                    new InformationStatusLine
                    {
                        Text = "幸运: 普通",
                        Color = new InformationColor(230, 230, 190, 240),
                        FontScale = 0.68d
                    }
                };

                var first = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 20, 200, false);
                if (first.RebuildCount != 1 || first.RowCount != 2 || !string.Equals(first.FirstRowText, "群系: 森林", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the initial status panel layout to build visible rows.");
                }

                var moved = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 140, 260, false);
                if (moved.RebuildCount != first.RebuildCount ||
                    moved.Width != first.Width ||
                    moved.Height != first.Height ||
                    moved.FirstRowDrawX != first.FirstRowDrawX + 120 ||
                    moved.FirstRowDrawY != first.FirstRowDrawY + 60)
                {
                    throw new InvalidOperationException("Expected position-only changes to reuse prepared row layout and only offset draw coordinates.");
                }

                if (InformationStatusPanelService.LayoutCacheMissCount != 1 ||
                    InformationStatusPanelService.LayoutCacheHitCount != 1)
                {
                    throw new InvalidOperationException("Expected status panel diagnostics to count layout cache hit and miss paths.");
                }

                lines[0].Text = "群系: 雪原";
                var changed = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 140, 260, false);
                if (changed.RebuildCount <= moved.RebuildCount || !string.Equals(changed.FirstRowText, "群系: 雪原", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status panel text changes to rebuild cached layout.");
                }

                var resized = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 900, 600, true, 140, 260, false);
                if (resized.RebuildCount <= changed.RebuildCount)
                {
                    throw new InvalidOperationException("Expected screen size changes to rebuild cached layout.");
                }

                var adjusting = InformationStatusPanelService.BuildLayoutSnapshotForTesting(new List<InformationStatusLine>(), 800, 600, true, 20, 200, true);
                if (adjusting.RowCount != 1 || !string.Equals(adjusting.FirstRowText, "信息窗", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected adjustment placeholder text to remain available when there are no status rows.");
                }

                var beforeFontInvalidation = adjusting.RebuildCount;
                UiTextRenderer.InvalidateCachedResources("status panel layout cache test");
                var fontInvalidated = InformationStatusPanelService.BuildLayoutSnapshotForTesting(new List<InformationStatusLine>(), 800, 600, true, 20, 200, true);
                if (fontInvalidated.RebuildCount <= beforeFontInvalidation)
                {
                    throw new InvalidOperationException("Expected UI text renderer cache invalidation to rebuild status panel layout.");
                }
            }
            finally
            {
                InformationStatusPanelService.ResetLayoutCacheForTesting();
            }
        }

        private static void UiTextRendererFastPathKeepsSafeFallbacks()
        {
            if (UiTextRenderer.DrawText(null, "文字", 10f, 10f, 255, 255, 255, 255, 1f))
            {
                throw new InvalidOperationException("Expected null SpriteBatch draw to fail safely.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, string.Empty, 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected empty text to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(false, true, "文字", 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected missing SpriteBatch to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, false, "文字", 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected missing font to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", 1f, 0.5f, 0f))
            {
                throw new InvalidOperationException("Expected anchored text to keep the DrawBorderString fallback.");
            }

            if (!UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", 0.01f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected tiny positive scale to remain eligible after clamping.");
            }

            if (Math.Abs(UiTextRenderer.ResolveEffectiveScaleForTesting(0.01f) - 0.1f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected tiny scale to clamp to the existing 0.1 minimum.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", float.NaN, 0f, 0f))
            {
                throw new InvalidOperationException("Expected invalid scale to keep the fallback path.");
            }
        }

        private static void UiTextRendererFontSignatureChangeClearsCaches()
        {
            if (!UiTextRenderer.FontSignatureChangeClearsCachesForTesting())
            {
                throw new InvalidOperationException("Expected font signature change to clear UI text caches and fast path suspension.");
            }
        }

        private static void InformationTombstoneTextDefaultsToRedAndSplitsTileType()
        {
            var settings = AppSettings.CreateDefault();
            if (!string.Equals(settings.InformationTombstoneTextLabelsMode, InformationSignTextModes.Off, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tombstone text labels to default to Off.");
            }

            if (!string.Equals(InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.TombstoneTextFeatureId), "#FF5555", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tombstone text labels to default to red.");
            }

            if (!InformationOverlayService.IsTombstoneTileTypeForTesting(85) ||
                InformationOverlayService.IsTombstoneTileTypeForTesting(55))
            {
                throw new InvalidOperationException("Expected tombstone text labels to split TileID.Tombstones from normal signs.");
            }
        }

        private static void InformationManaCrystalHighlightDefaultsToOffAndUsesTileId()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.InformationHighlightManaCrystalEnabled)
            {
                throw new InvalidOperationException("Expected mana crystal highlight to default to Off.");
            }

            var color = InformationColorHelper.ManaCrystal(settings);
            if (color.R != 102 || color.G != 204 || color.B != 255 || color.A != 255)
            {
                throw new InvalidOperationException("Expected mana crystal highlight to default to mana blue.");
            }

            if (!InformationOverlayService.IsManaCrystalTileTypeForTesting(639) ||
                InformationOverlayService.IsManaCrystalTileTypeForTesting(12))
            {
                throw new InvalidOperationException("Expected mana crystal highlight to use TileID.ManaCrystal=639 and stay distinct from life crystals.");
            }
        }

        private static void InformationTileAccessReadsCachedTileMembers()
        {
            bool active;
            int type;
            int frameX;
            int frameY;
            var propertyTile = new TestInformationPropertyTile
            {
                HasTile = true,
                TileType = 639,
                TileFrameX = 18,
                TileFrameY = 36
            };

            if (!InformationTileAccess.TryReadActiveTypeAndFrame(propertyTile, out active, out type, out frameX, out frameY) ||
                !active ||
                type != 639 ||
                frameX != 18 ||
                frameY != 36)
            {
                throw new InvalidOperationException("Expected cached tile accessor to read property-style tile members.");
            }

            var methodTile = new TestInformationMethodTile();
            if (!InformationTileAccess.TryReadActiveTypeAndFrame(methodTile, out active, out type, out frameX, out frameY) ||
                !active ||
                type != 236 ||
                frameX != 54 ||
                frameY != 72)
            {
                throw new InvalidOperationException("Expected cached tile accessor to read vanilla-style method and field tile members.");
            }
        }

        private static void InformationTileHighlightCacheSignatureTracksBoundsSettingsAndWorld()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationHighlightLifeCrystalEnabled = true;
            var context = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 512f, 512f, "world-a", "world-record-a");
            var signature = InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, settings);

            var sameTileBounds = CreateInformationTileHighlightContext(1035f, 1000f, 800, 600, 520f, 512f, "world-a", "world-record-a");
            var sameTileBoundsSignature = InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(sameTileBounds, settings);
            if (!string.Equals(signature, sameTileBoundsSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to survive sub-tile screen movement inside the same scan bounds and player chunk.");
            }

            var crossedTileBounds = CreateInformationTileHighlightContext(1041f, 1000f, 800, 600, 520f, 512f, "world-a", "world-record-a");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(crossedTileBounds, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when the screen tile scan bounds change.");
            }

            var crossedPlayerChunk = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 900f, 512f, "world-a", "world-record-a");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(crossedPlayerChunk, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to include the player tile chunk.");
            }

            var otherWorld = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 512f, 512f, "world-b", "world-record-b");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(otherWorld, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty across world identity changes.");
            }

            var extraHighlight = AppSettings.CreateDefault();
            extraHighlight.InformationHighlightLifeCrystalEnabled = true;
            extraHighlight.InformationHighlightLifeFruitEnabled = true;
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, extraHighlight), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when enabled highlight types change.");
            }

            var changedColor = AppSettings.CreateDefault();
            changedColor.InformationHighlightLifeCrystalEnabled = true;
            changedColor.InformationLifeCrystalHighlightColor = "#00FF00";
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, changedColor), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when highlight color configuration changes.");
            }
        }

        private static void InformationOverlayContextProfilesRouteStatusAndWorldRecord()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationBiomeDisplayEnabled = true;

            if (InformationOverlayService.BuildStatusContextProfileForTesting(settings) != InformationWorldContextProfile.Status)
            {
                throw new InvalidOperationException("Expected status panel rendering to use the lightweight information context profile.");
            }

            if (InformationOverlayService.BuildWorldOverlayContextProfileForTesting(settings) != InformationWorldContextProfile.FullRecord)
            {
                throw new InvalidOperationException("Expected world overlay rendering to keep full record context for player/world scoped records.");
            }
        }

        private static InformationWorldContext CreateInformationContextForCacheTest(object player, string worldKey, ulong updateCount, float screenX, float screenY, int screenWidth, int screenHeight)
        {
            return new InformationWorldContext
            {
                MainType = typeof(Program),
                LocalPlayer = player,
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = 240f,
                PlayerCenterY = 320f,
                GameUpdateCount = updateCount,
                WorldKey = worldKey
            };
        }

        private static void InformationTileHighlightCacheKeepsSafetyRefresh()
        {
            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(0, 1u, 10, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to scan when no previous scan tick exists.");
            }

            if (InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 159, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to reuse before the safety refresh interval.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 160, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to keep the 60 tick safety refresh when no tile generation is available.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 120, 2u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to refresh immediately when its stable signature changes.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 90, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to refresh after a tick counter rewind.");
            }
        }

        private static void InformationTileHighlightScannerGroupsAdjacentEnabledTiles()
        {
            try
            {
                FakeTileHighlightMain.ConfigureLifeCrystalGroup();
                var context = CreateInformationTileHighlightContext(0f, 0f, 320, 320, 80f, 80f, "tile-highlight-world", "tile-highlight-record");
                context.MainType = typeof(FakeTileHighlightMain);
                context.GameUpdateCount = 100;

                var settings = AppSettings.CreateDefault();
                InformationOverlayService.ResetTileHighlightCacheForTesting();
                if (InformationOverlayService.GetTileHighlightCountForTesting(context, settings) != 0)
                {
                    throw new InvalidOperationException("Expected disabled tile highlights to skip scanner results.");
                }

                settings.InformationHighlightLifeCrystalEnabled = true;
                var count = InformationOverlayService.GetTileHighlightCountForTesting(context, settings);
                if (count != 1)
                {
                    throw new InvalidOperationException("Expected adjacent life crystal tiles to be merged into one highlight group, got " + count + ".");
                }

                context.GameUpdateCount = 159;
                var cachedCount = InformationOverlayService.GetTileHighlightCountForTesting(context, settings);
                if (cachedCount != 1)
                {
                    throw new InvalidOperationException("Expected cached tile highlight group to remain stable before safety refresh, got " + cachedCount + ".");
                }
            }
            finally
            {
                FakeTileHighlightMain.Reset();
                InformationOverlayService.ResetTileHighlightCacheForTesting();
            }
        }

        private static InformationWorldContext CreateInformationTileHighlightContext(float screenX, float screenY, int screenWidth, int screenHeight, float playerCenterX, float playerCenterY, string worldKey, string worldRecordKey)
        {
            return new InformationWorldContext
            {
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY,
                WorldKey = worldKey,
                WorldRecordKey = worldRecordKey
            };
        }


    }
}
