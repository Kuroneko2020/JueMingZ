using JueMingZ.Automation.Fishing;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    public static partial class InformationOverlayService
    {
        internal static float CalculateSignTextLineXForTesting(float signCenterX, int lineWidth, int screenWidth)
        {
            return InformationOverlayTestingFacade.CalculateSignTextLineX(signCenterX, lineWidth, screenWidth);
        }

        internal static bool IsTombstoneTileTypeForTesting(int tileType)
        {
            return InformationOverlayTestingFacade.IsTombstoneTileType(tileType);
        }

        internal static bool IsManaCrystalTileTypeForTesting(int tileType)
        {
            return InformationOverlayTestingFacade.IsManaCrystalTileType(tileType);
        }

        internal static string BuildTileHighlightCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return InformationOverlayTestingFacade.BuildTileHighlightCacheSignature(context, settings);
        }

        internal static bool ShouldRefreshTileHighlightCacheForTesting(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            return InformationOverlayTestingFacade.ShouldRefreshTileHighlightCache(lastScanTick, previousSignatureHash, currentTick, currentSignatureHash);
        }

        internal static int GetTileHighlightCountForTesting(InformationWorldContext context, AppSettings settings)
        {
            return InformationOverlayTestingFacade.GetTileHighlightCount(context, settings);
        }

        internal static void ResetTileHighlightCacheForTesting()
        {
            InformationOverlayTestingFacade.ResetTileHighlightCache();
        }

        internal static bool IsChestTileTypeForTesting(int tileType)
        {
            return InformationOverlayTestingFacade.IsChestTileType(tileType);
        }

        internal static bool TryNormalizeChestOriginFromFrameForTesting(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationOverlayTestingFacade.TryNormalizeChestOriginFromFrame(tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static bool TryNormalizeChestOriginFromFrameForTesting(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationOverlayTestingFacade.TryNormalizeChestOriginFromFrame(tileType, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static int BuildChestTileStyleForTesting(int tileType, int frameX)
        {
            return InformationOverlayTestingFacade.BuildChestTileStyle(tileType, frameX);
        }

        internal static string ResolveChestTileDisplayNameForTesting(int tileType, int tileStyle)
        {
            return InformationOverlayTestingFacade.ResolveChestTileDisplayName(tileType, tileStyle);
        }

        internal static string BuildChestLabelCacheSignatureForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            return InformationOverlayTestingFacade.BuildChestLabelCacheSignature(context, settings, mode);
        }

        internal static bool ShouldRefreshChestAlwaysCacheForTesting(
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
            return InformationOverlayTestingFacade.ShouldRefreshChestAlwaysCache(
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

        internal static int GetChestLabelCountForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            return InformationOverlayTestingFacade.GetChestLabelCount(context, settings, mode);
        }

        internal static void ResetChestLabelCacheForTesting()
        {
            InformationOverlayTestingFacade.ResetChestLabelCache();
        }

        internal static void SetChestAlwaysPartialScanBudgetForTesting(int budgetTiles)
        {
            InformationOverlayTestingFacade.SetChestAlwaysPartialScanBudget(budgetTiles);
        }

        internal static string BuildStatusLineCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return InformationOverlayTestingFacade.BuildStatusLineCacheSignature(context, settings);
        }

        internal static InformationWorldContextProfile BuildStatusContextProfileForTesting(AppSettings settings)
        {
            return InformationOverlayTestingFacade.BuildStatusContextProfile(settings);
        }

        internal static InformationWorldContextProfile BuildWorldOverlayContextProfileForTesting(AppSettings settings)
        {
            return InformationOverlayTestingFacade.BuildWorldOverlayContextProfile(settings);
        }

        internal static bool CanReuseStatusLinesForTesting(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, AppSettings settings)
        {
            return InformationOverlayTestingFacade.CanReuseStatusLines(lastRefreshTick, lastSignature, context, settings);
        }

        internal static bool TryUseObservedLocalBobberForTesting(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y)
        {
            return InformationOverlayTestingFacade.TryUseObservedLocalBobber(observation, myPlayer, currentGameUpdateCount, out x, out y);
        }

        internal static bool TryFindLocalBobberForTesting(InformationWorldContext context, out float x, out float y)
        {
            return InformationOverlayTestingFacade.TryFindLocalBobber(context, out x, out y);
        }

        internal static void ResetFishingBobberLookupDiagnosticsForTesting()
        {
            InformationOverlayTestingFacade.ResetFishingBobberLookupDiagnostics();
        }

        internal static int MaxChestLabelsPerFrameForTesting()
        {
            return InformationOverlayTestingFacade.MaxChestLabelsPerFrame();
        }

        internal static bool CanCacheChestLabelForTesting(InformationWorldContext context, float worldX, float worldY)
        {
            return InformationOverlayTestingFacade.CanCacheChestLabel(context, worldX, worldY);
        }

        internal static int[] SortChestLabelIndicesForTesting(InformationWorldContext context, float[] worldXs, float[] worldYs)
        {
            return InformationOverlayTestingFacade.SortChestLabelIndices(context, worldXs, worldYs);
        }

        internal static bool ShouldRefreshChestLabelSortForTesting(
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
            return InformationOverlayTestingFacade.ShouldRefreshChestLabelSort(
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

        internal static bool CanReuseNpcLabelSnapshotForTesting(
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
            return InformationOverlayTestingFacade.CanReuseNpcLabelSnapshot(
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

        internal static bool CanReuseNpcLabelHealthValuesForTesting(int labelLife, int labelLifeMax, int currentLife, int currentLifeMax)
        {
            return InformationOverlayTestingFacade.CanReuseNpcLabelHealthValues(labelLife, labelLifeMax, currentLife, currentLifeMax);
        }

        internal static string BuildEnemyHealthTextForTesting(int life, int lifeMax)
        {
            return InformationOverlayTestingFacade.BuildEnemyHealthText(life, lifeMax);
        }

        internal static float ResolveEnemyHealthFontScaleForTesting(float nameFontScale)
        {
            return InformationOverlayTestingFacade.ResolveEnemyHealthFontScale(nameFontScale);
        }

        internal static bool TryParseChestKeyForTesting(string key, string currentWorldKey, out int x, out int y)
        {
            return InformationOverlayTestingFacade.TryParseChestKey(key, currentWorldKey, out x, out y);
        }

        internal static int ImportLegacyKnownChestsForTesting(InformationWorldContext context, AppSettings settings)
        {
            return InformationOverlayTestingFacade.ImportLegacyKnownChests(context, settings);
        }

        internal static bool ShouldDrawEnemySegmentLabel(int groupSize, int neighborCount)
        {
            return InformationOverlayTestingFacade.ShouldDrawEnemySegmentLabel(groupSize, neighborCount);
        }

        internal static bool ShouldDrawEnemyNpcTypeLabelForTesting(int npcType)
        {
            return InformationOverlayTestingFacade.ShouldDrawEnemyNpcTypeLabel(npcType);
        }

        internal static bool IsNpcNameLabelCandidateForTesting(int npcType, bool townNpc)
        {
            return InformationOverlayTestingFacade.IsNpcNameLabelCandidate(npcType, townNpc);
        }

        internal static bool IsEnemyNameLabelCandidateForTesting(int npcType, bool friendly, bool critter, int life, int lifeMax)
        {
            return InformationOverlayTestingFacade.IsEnemyNameLabelCandidate(npcType, friendly, critter, life, lifeMax);
        }

        internal static string[] BuildSignTextDisplayLinesForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            return InformationOverlayTestingFacade.BuildSignTextDisplayLines(text, mode, maxLines, maxCharacters, scale);
        }

        internal static void ResetSignTextLayoutCacheForTesting()
        {
            InformationOverlayTestingFacade.ResetSignTextLayoutCache();
        }

        internal static InformationSignTextLayoutSnapshot BuildSignTextLayoutSnapshotForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            return InformationOverlayTestingFacade.BuildSignTextLayoutSnapshot(text, mode, maxLines, maxCharacters, scale);
        }
    }
}
