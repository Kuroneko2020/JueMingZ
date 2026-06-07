using JueMingZ.Automation.Fishing;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationOverlayTestingFacade
    {
        internal static float CalculateSignTextLineX(float signCenterX, int lineWidth, int screenWidth)
        {
            return InformationSignTextLayoutCache.CalculateLineX(signCenterX, lineWidth, screenWidth);
        }

        internal static bool IsTombstoneTileType(int tileType)
        {
            return InformationSignTextLabelService.IsTombstoneTileTypeForTesting(tileType);
        }

        internal static bool IsManaCrystalTileType(int tileType)
        {
            return InformationTileHighlightService.IsManaCrystalTileTypeForTesting(tileType);
        }

        internal static string BuildTileHighlightCacheSignature(InformationWorldContext context, AppSettings settings)
        {
            return InformationTileHighlightService.BuildCacheSignatureForTesting(context, settings);
        }

        internal static bool ShouldRefreshTileHighlightCache(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            return InformationTileHighlightService.ShouldRefreshCacheForTesting(lastScanTick, previousSignatureHash, currentTick, currentSignatureHash);
        }

        internal static int GetTileHighlightCount(InformationWorldContext context, AppSettings settings)
        {
            return InformationTileHighlightService.GetHighlightCountForTesting(context, settings);
        }

        internal static void ResetTileHighlightCache()
        {
            InformationTileHighlightService.ResetForTesting();
        }

        internal static bool IsChestTileType(int tileType)
        {
            return InformationChestLabelService.IsChestTileTypeForTesting(tileType);
        }

        internal static bool TryNormalizeChestOriginFromFrame(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationChestLabelService.TryNormalizeOriginFromFrameForTesting(tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static bool TryNormalizeChestOriginFromFrame(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationChestLabelService.TryNormalizeOriginFromFrameForTesting(tileType, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static int BuildChestTileStyle(int tileType, int frameX)
        {
            return InformationChestLabelService.BuildTileStyleForTesting(tileType, frameX);
        }

        internal static string ResolveChestTileDisplayName(int tileType, int tileStyle)
        {
            return InformationChestLabelService.ResolveTileDisplayNameForTesting(tileType, tileStyle);
        }

        internal static string BuildChestLabelCacheSignature(InformationWorldContext context, AppSettings settings, string mode)
        {
            return InformationChestLabelService.BuildCacheSignatureForTesting(context, settings, mode);
        }

        internal static bool ShouldRefreshChestAlwaysCache(
            ulong lastScanTick,
            InformationWorldContext previousContext,
            AppSettings previousSettings,
            string previousMode,
            ulong currentTick,
            InformationWorldContext currentContext,
            AppSettings currentSettings,
            string currentMode,
            out string dirtyReason)
        {
            return InformationChestLabelService.ShouldRefreshAlwaysCacheForTesting(
                lastScanTick,
                previousContext,
                previousSettings,
                previousMode,
                currentTick,
                currentContext,
                currentSettings,
                currentMode,
                out dirtyReason);
        }

        internal static int GetChestLabelCount(InformationWorldContext context, AppSettings settings, string mode)
        {
            return InformationChestLabelService.GetLabelCountForTesting(context, settings, mode);
        }

        internal static void ResetChestLabelCache()
        {
            InformationChestLabelService.ResetForTesting();
        }

        internal static void SetChestAlwaysPartialScanBudget(int budgetTiles)
        {
            InformationChestLabelService.SetAlwaysPartialScanBudgetForTesting(budgetTiles);
        }

        internal static string BuildStatusLineCacheSignature(InformationWorldContext context, AppSettings settings)
        {
            return InformationStatusLineService.BuildCacheSignatureForTesting(context, settings);
        }

        internal static InformationWorldContextProfile BuildStatusContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.Status;
        }

        internal static InformationWorldContextProfile BuildWorldOverlayContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.FullRecord;
        }

        internal static bool CanReuseStatusLines(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, AppSettings settings)
        {
            return InformationStatusLineService.CanReuseLinesForTesting(lastRefreshTick, lastSignature, context, settings);
        }

        internal static bool TryUseObservedLocalBobber(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y)
        {
            return InformationBobberLocator.TryUseObservedLocalBobberForTesting(observation, myPlayer, currentGameUpdateCount, out x, out y);
        }

        internal static bool TryFindLocalBobber(InformationWorldContext context, out float x, out float y)
        {
            return InformationBobberLocator.TryFindLocalBobber(context, out x, out y);
        }

        internal static void ResetFishingBobberLookupDiagnostics()
        {
            InformationBobberLocator.ResetDiagnosticsForTesting();
        }

        internal static int MaxChestLabelsPerFrame()
        {
            return InformationChestLabelService.MaxLabelsPerFrame;
        }

        internal static bool CanCacheChestLabel(InformationWorldContext context, float worldX, float worldY)
        {
            return InformationChestLabelService.CanCacheLabelForTesting(context, worldX, worldY);
        }

        internal static int[] SortChestLabelIndices(InformationWorldContext context, float[] worldXs, float[] worldYs)
        {
            return InformationChestLabelService.SortLabelIndicesForTesting(context, worldXs, worldYs);
        }

        internal static bool ShouldRefreshChestLabelSort(
            float previousPlayerCenterX,
            float previousPlayerCenterY,
            float previousScreenX,
            float previousScreenY,
            int previousScreenWidth,
            int previousScreenHeight,
            uint previousSourceSignatureHash,
            float currentPlayerCenterX,
            float currentPlayerCenterY,
            float currentScreenX,
            float currentScreenY,
            int currentScreenWidth,
            int currentScreenHeight,
            uint currentSourceSignatureHash)
        {
            return InformationChestLabelService.ShouldRefreshSortForTesting(
                previousPlayerCenterX,
                previousPlayerCenterY,
                previousScreenX,
                previousScreenY,
                previousScreenWidth,
                previousScreenHeight,
                previousSourceSignatureHash,
                currentPlayerCenterX,
                currentPlayerCenterY,
                currentScreenX,
                currentScreenY,
                currentScreenWidth,
                currentScreenHeight,
                currentSourceSignatureHash);
        }

        internal static bool CanReuseNpcLabelSnapshot(
            int labelWhoAmI,
            int labelType,
            int labelLife,
            int labelLifeMax,
            bool labelTownNpc,
            bool labelFriendly,
            bool labelHidden,
            bool labelCritter,
            int snapshotWhoAmI,
            int snapshotType,
            int snapshotLife,
            int snapshotLifeMax,
            bool snapshotTownNpc,
            bool snapshotFriendly,
            bool snapshotHidden,
            bool snapshotCritter)
        {
            return InformationNpcLabelService.CanReuseNpcLabelSnapshotForTesting(
                labelWhoAmI,
                labelType,
                labelLife,
                labelLifeMax,
                labelTownNpc,
                labelFriendly,
                labelHidden,
                labelCritter,
                snapshotWhoAmI,
                snapshotType,
                snapshotLife,
                snapshotLifeMax,
                snapshotTownNpc,
                snapshotFriendly,
                snapshotHidden,
                snapshotCritter);
        }

        internal static bool CanReuseNpcLabelHealthValues(int labelLife, int labelLifeMax, int currentLife, int currentLifeMax)
        {
            return InformationNpcLabelService.CanReuseNpcLabelHealthValuesForTesting(labelLife, labelLifeMax, currentLife, currentLifeMax);
        }

        internal static string BuildEnemyHealthText(int life, int lifeMax)
        {
            return InformationNpcLabelService.BuildEnemyHealthTextForTesting(life, lifeMax);
        }

        internal static float ResolveEnemyHealthFontScale(float nameFontScale)
        {
            return InformationNpcLabelService.ResolveEnemyHealthFontScaleForTesting(nameFontScale);
        }

        internal static bool TryParseChestKey(string key, string currentWorldKey, out int x, out int y)
        {
            return InformationChestRecordService.TryParseChestKey(key, currentWorldKey, out x, out y);
        }

        internal static int ImportLegacyKnownChests(InformationWorldContext context, AppSettings settings)
        {
            return InformationChestRecordService.ImportLegacyKnownChestsForTesting(context, settings);
        }

        internal static bool ShouldDrawEnemySegmentLabel(int groupSize, int neighborCount)
        {
            return InformationNpcSegmentService.ShouldDrawEnemySegmentLabel(groupSize, neighborCount);
        }

        internal static bool ShouldDrawEnemyNpcTypeLabel(int npcType)
        {
            return InformationNpcSegmentService.ShouldDrawEnemyNpcTypeLabelForTesting(npcType);
        }

        internal static bool IsNpcNameLabelCandidate(int npcType, bool townNpc)
        {
            return InformationNpcLabelService.IsNpcNameLabelCandidateForTesting(npcType, townNpc);
        }

        internal static bool IsEnemyNameLabelCandidate(int npcType, bool friendly, bool critter, int life, int lifeMax)
        {
            return InformationNpcLabelService.IsEnemyNameLabelCandidateForTesting(npcType, friendly, critter, life, lifeMax);
        }

        internal static string[] BuildSignTextDisplayLines(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            return InformationSignTextLayoutCache.BuildDisplayLinesForTesting(text, mode, maxLines, maxCharacters, scale);
        }

        internal static void ResetSignTextLayoutCache()
        {
            InformationSignTextLayoutCache.ResetForTesting();
        }

        internal static InformationSignTextLayoutSnapshot BuildSignTextLayoutSnapshot(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            return InformationSignTextLayoutCache.BuildSnapshotForTesting(text, mode, maxLines, maxCharacters, scale);
        }
    }
}
