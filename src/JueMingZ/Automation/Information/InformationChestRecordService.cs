using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Records;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationChestRecordService
    {
        internal static void RecordOpenChest(InformationWorldContext context)
        {
            // Opened-chest records are per player/world behavior state; recording
            // must not inspect or mutate chest contents.
            int chestIndex;
            var typedPlayer = context == null ? null : context.LocalPlayer as Player;
            if (typedPlayer != null)
            {
                chestIndex = TerrariaPlayerReadCompat.ChestIndex(typedPlayer);
            }
            else if (!InformationReflection.TryReadInt(context == null ? null : context.LocalPlayer, "chest", out chestIndex))
            {
                return;
            }

            if (chestIndex < 0)
            {
                return;
            }

            int x;
            int y;
            Chest typedChest;
            if (TerrariaMainCompat.TryGetChest(chestIndex, out typedChest))
            {
                x = typedChest.x;
                y = typedChest.y;
            }
            else
            {
                var chests = InformationReflection.GetStaticMember(context.MainType, "chest");
                var chest = InformationReflection.GetIndexedValue(chests, chestIndex);
                if (chest == null ||
                    !InformationReflection.TryReadInt(chest, "x", out x) ||
                    !InformationReflection.TryReadInt(chest, "y", out y))
                {
                    return;
                }
            }

            if (x <= 0 || y <= 0)
            {
                return;
            }

            bool added;
            string message;
            if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                    BuildBehaviorContext(context),
                    x,
                    y,
                    "Information.OpenChest",
                    out added,
                    out message))
            {
                LogThrottle.WarnThrottled(
                    "information-opened-chest-record-failed",
                    TimeSpan.FromSeconds(30),
                    "InformationChestRecordService",
                    "Opened chest record skipped: " + message);
                return;
            }

            if (added)
            {
                InformationChestLabelService.Invalidate();
            }
        }

        internal static int ImportLegacyKnownChests(InformationWorldContext context, AppSettings settings)
        {
            return ImportLegacyKnownChestsCore(context, settings, true);
        }

        internal static int ImportLegacyKnownChestsForTesting(InformationWorldContext context, AppSettings settings)
        {
            return ImportLegacyKnownChestsCore(context, settings, false);
        }

        internal static PlayerWorldBehaviorContext BuildBehaviorContext(InformationWorldContext context)
        {
            if (context == null)
            {
                return new PlayerWorldBehaviorContext();
            }

            return new PlayerWorldBehaviorContext
            {
                PlayerKey = context.PlayerRecordKey ?? string.Empty,
                WorldKey = context.WorldRecordKey ?? string.Empty,
                PlayerName = context.PlayerName ?? string.Empty,
                WorldName = context.WorldName ?? string.Empty
            };
        }

        internal static bool TryParseChestKey(string key, string currentWorldKey, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var lastSeparator = key.LastIndexOf('|');
            var secondSeparator = lastSeparator <= 0 ? -1 : key.LastIndexOf('|', lastSeparator - 1);
            if (secondSeparator <= 0 || lastSeparator <= secondSeparator + 1 || lastSeparator >= key.Length - 1)
            {
                return false;
            }

            var worldKey = key.Substring(0, secondSeparator);
            if (!WorldKeysMatch(worldKey, currentWorldKey))
            {
                return false;
            }

            return int.TryParse(key.Substring(secondSeparator + 1, lastSeparator - secondSeparator - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                   int.TryParse(key.Substring(lastSeparator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        private static int ImportLegacyKnownChestsCore(InformationWorldContext context, AppSettings settings, bool saveConfig)
        {
            if (context == null ||
                settings == null ||
                settings.InformationKnownChestKeys == null ||
                settings.InformationKnownChestKeys.Count == 0)
            {
                return 0;
            }

            var behaviorContext = BuildBehaviorContext(context);
            if (!PlayerWorldBehaviorStore.IsUsable(behaviorContext))
            {
                return 0;
            }

            var imported = new List<PlayerWorldOpenedChestRecord>();
            var remaining = new List<string>();
            for (var index = 0; index < settings.InformationKnownChestKeys.Count; index++)
            {
                var legacyKey = settings.InformationKnownChestKeys[index];
                int x;
                int y;
                if (TryParseChestKey(legacyKey, context.WorldKey, out x, out y))
                {
                    imported.Add(new PlayerWorldOpenedChestRecord
                    {
                        X = x,
                        Y = y,
                        Source = "LegacyInformationKnownChestKeys"
                    });
                    continue;
                }

                remaining.Add(legacyKey);
            }

            if (imported.Count == 0)
            {
                return 0;
            }

            var added = PlayerWorldBehaviorStore.ImportOpenedChests(
                behaviorContext,
                imported,
                "LegacyInformationKnownChestKeys");

            settings.InformationKnownChestKeys = remaining;
            if (saveConfig)
            {
                ConfigService.SaveAll();
            }

            if (added > 0)
            {
                InformationChestLabelService.Invalidate();
            }

            return added;
        }

        private static bool WorldKeysMatch(string storedWorldKey, string currentWorldKey)
        {
            if (string.Equals(storedWorldKey ?? string.Empty, currentWorldKey ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }

            var storedId = ExtractWorldId(storedWorldKey);
            var currentId = ExtractWorldId(currentWorldKey);
            return !string.IsNullOrWhiteSpace(storedId) &&
                   !string.IsNullOrWhiteSpace(currentId) &&
                   string.Equals(storedId, currentId, StringComparison.Ordinal);
        }

        private static string ExtractWorldId(string worldKey)
        {
            if (string.IsNullOrWhiteSpace(worldKey))
            {
                return string.Empty;
            }

            var marker = worldKey.LastIndexOf('#');
            if (marker < 0 || marker >= worldKey.Length - 1)
            {
                return string.Empty;
            }

            return worldKey.Substring(marker + 1).Trim();
        }
    }
}
