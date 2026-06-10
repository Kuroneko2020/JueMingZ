using JueMingZ.Automation.Fishing;
using JueMingZ.Config;
using JueMingZ.GameState;

namespace JueMingZ.Runtime
{
    internal static class RuntimeGameStateReadOptionsBuilder
    {
        public static GameStateReadOptions Build(RuntimeSettingsSnapshot settingsSnapshot, bool diagnosticSnapshotDue)
        {
            // Read profiles are the runtime cost budget; full reads stay behind feature and snapshot gates.
            var settings = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var sourceSettings = settings.SourceSettings ?? AppSettings.CreateDefault();
            if (diagnosticSnapshotDue)
            {
                return GameStateReadOptions.Full;
            }

            var inventoryProfile = InventoryReadProfile.None;
            var npcProfile = NpcReadProfile.None;

            if (settings.LegacyMiscUiNeedsInventorySnapshot)
            {
                inventoryProfile |= InventoryReadProfile.Full;
            }

            if (settings.RecoveryAnyEnabled)
            {
                inventoryProfile |= InventoryReadProfile.RecoveryItems;
            }

            if (settings.FishingAutoFishEnabled)
            {
                inventoryProfile |= InventoryReadProfile.SignatureOnly;
            }

            if (settings.InventoryAutoStackEnabled)
            {
                inventoryProfile |= InventoryReadProfile.StackCandidates;
            }

            if (settings.InventoryAutoSellEnabled || settings.InventoryAutoDiscardEnabled)
            {
                inventoryProfile |= InventoryReadProfile.SellDiscardCandidates;
            }

            if (settings.InventoryAutoDepositCoinsEnabled)
            {
                inventoryProfile |= InventoryReadProfile.CoinsOnly;
            }

            if (settings.InventoryAutoExtractinatorEnabled)
            {
                inventoryProfile |= InventoryReadProfile.ExtractinatorItems;
            }

            if (settings.InventoryKeepFavoritedEnabled)
            {
                inventoryProfile |= InventoryReadProfile.KeepFavorited;
            }

            if (settings.WorldAutomationAutoCaptureCritterEnabled)
            {
                inventoryProfile |= InventoryReadProfile.BugNetOnly;
                npcProfile |= NpcReadProfile.CatchableCrittersOnly;
            }

            if (settings.WorldAutomationAutoHarvestEnabled)
            {
                inventoryProfile |= InventoryReadProfile.ToolsAndSeeds;
            }

            var fishingFilterNeedsActiveBuffs =
                settings.FishingAutoFishEnabled &&
                FishingAutomationService.IsFishingFilterEnabled(sourceSettings);

            return new GameStateReadOptions
            {
                InventoryProfile = inventoryProfile,
                IncludeActiveBuffs = settings.AutoBuffEnabled ||
                                     (settings.AutoRecovery != null && settings.AutoRecovery.AutoStationBuffEnabled) ||
                                     fishingFilterNeedsActiveBuffs,
                NpcProfile = npcProfile,
                TileProfile = TileReadProfile.None,
                IncludeWorldSummary = false
            };
        }
    }
}
