using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Diagnostics;
using JueMingZ.Hooks;
using JueMingZ.Records;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void PlayerWorldDeathReasonSourceSerializationCoversKnownKinds()
        {
            var pairId = "pair-death-source";
            AssertDeathSourceSerialization(pairId, CreateDeathReasonWithField("_sourceNPCIndex", 12), PlayerWorldDeathSourceKind.Npc, "\"SourceKind\":\"npc\"");
            AssertDeathSourceSerialization(pairId, CreateDeathReasonWithField("_sourceProjectileLocalIndex", 3, "_sourceProjectileType", 991), PlayerWorldDeathSourceKind.Projectile, "\"SourceProjectileType\":991");
            AssertDeathSourceSerialization(pairId, CreateDeathReasonWithField("_sourcePlayerIndex", 5), PlayerWorldDeathSourceKind.Player, "\"SourceKind\":\"player\"");
            AssertDeathSourceSerialization(pairId, CreateDeathReasonWithField("_sourceOtherIndex", 7), PlayerWorldDeathSourceKind.Other, "\"SourceOtherIndex\":7");
            AssertDeathSourceSerialization(pairId, CreateDeathReasonWithField("_sourceCustomReason", "Custom testing death"), PlayerWorldDeathSourceKind.Custom, "\"SourceCustomReason\":\"Custom testing death\"");
        }

        private static void PlayerWorldDeathRecorderWritesJsonlAndSummaryWhenMarkersDisabled()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathRecorder.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var source = new PlayerWorldDeathSourceSnapshot
                {
                    SourceKind = PlayerWorldDeathSourceKind.Custom,
                    SourceCustomReason = "Marker disabled still records"
                };
                var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                    identity.PairId,
                    source,
                    321.5f,
                    654.25f,
                    "Marker disabled still records",
                    42.5d,
                    -1,
                    false,
                    new DateTime(2026, 6, 14, 1, 2, 3, DateTimeKind.Utc));

                PlayerWorldDeathRecordResult result;
                if (!PlayerWorldDeathRecorder.TryRecordDeathForTesting(identity, deathEvent, false, out result))
                {
                    throw new InvalidOperationException("Expected death record write to succeed: " + result.Message);
                }

                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                var summaryPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathSummaryFileName);
                AssertPathUnderRoot(deathPath, root, "death jsonl path");
                AssertPathUnderRoot(summaryPath, root, "death summary path");

                if (PlayerWorldDeathRecorder.CountDeathEventLinesForTesting(deathPath) != 1)
                {
                    throw new InvalidOperationException("Expected deaths.jsonl to contain one readable event.");
                }

                var jsonl = File.ReadAllText(deathPath, Encoding.UTF8);
                AssertContains(jsonl, "\"IdentityPairId\":\"" + identity.PairId + "\"");
                AssertContains(jsonl, "\"SourceKind\":\"custom\"");
                AssertContains(jsonl, "\"PlayerTileX\":20");
                AssertContains(jsonl, "\"PlayerTileY\":40");

                var summary = ReadJsonFile<PlayerWorldDeathSummaryFile>(summaryPath);
                if (summary.DeathCount != 1 ||
                    !summary.LastWriteSucceeded ||
                    summary.DeathHistoryReadFailed ||
                    !string.Equals(summary.LastEventId, deathEvent.EventId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Death summary did not match the written jsonl event.");
                }

                var diagnostics = PlayerWorldDeathRecorder.LastDiagnostics;
                AssertStringEquals(diagnostics.LastRecordStatus, "saved", "death recorder diagnostics status");
                AssertStringEquals(diagnostics.LastPairId, identity.PairId, "death recorder diagnostics pair");
            });
        }

        private static void PlayerWorldDeathRecorderSkipsUnresolvedIdentity()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathRecorder.ResetForTesting();
                var unresolved = new PlayerWorldIdentityResolution
                {
                    IsResolved = false,
                    FailureReason = "testIdentityUnavailable"
                };
                var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                    string.Empty,
                    new PlayerWorldDeathSourceSnapshot(),
                    10f,
                    20f,
                    "unresolved",
                    1d,
                    0,
                    false,
                    DateTime.UtcNow);

                PlayerWorldDeathRecordResult result;
                if (PlayerWorldDeathRecorder.TryRecordDeathForTesting(unresolved, deathEvent, true, out result))
                {
                    throw new InvalidOperationException("Expected unresolved identity to skip death recording.");
                }

                if (Directory.Exists(Path.Combine(root, PlayerWorldFeatureDataRoot.PlayerWorldDirectoryName)))
                {
                    var files = Directory.GetFiles(Path.Combine(root, PlayerWorldFeatureDataRoot.PlayerWorldDirectoryName), "*", SearchOption.AllDirectories);
                    if (files.Length != 0)
                    {
                        throw new InvalidOperationException("Unresolved identity must not write player-world death files.");
                    }
                }

                AssertDoesNotContain(result.Message, "unknown", "unresolved death record result");
            });
        }

        private static void PlayerWorldDeathSummaryFlagsCorruptJsonlFallback()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathRecorder.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(deathPath));
                File.WriteAllText(deathPath, "{not-json" + Environment.NewLine, Encoding.UTF8);

                var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                    identity.PairId,
                    new PlayerWorldDeathSourceSnapshot { SourceKind = PlayerWorldDeathSourceKind.Other, SourceOtherIndex = 2 },
                    16f,
                    32f,
                    "corrupt fallback",
                    5d,
                    0,
                    false,
                    DateTime.UtcNow);

                PlayerWorldDeathRecordResult result;
                PlayerWorldDeathRecorder.TryRecordDeathForTesting(identity, deathEvent, true, out result);

                var summaryPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathSummaryFileName);
                var summary = ReadJsonFile<PlayerWorldDeathSummaryFile>(summaryPath);
                if (!summary.DeathHistoryReadFailed ||
                    summary.DeathHistoryReadMessage.IndexOf("invalidLine", StringComparison.OrdinalIgnoreCase) < 0 ||
                    summary.DeathCount != 1)
                {
                    throw new InvalidOperationException("Corrupt deaths.jsonl fallback should mark read failure and keep summary usable.");
                }
            });
        }

        private static void PlayerWorldDeathHookDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldDeathHookInstalled = false,
                PlayerWorldDeathHookMethod = string.Empty,
                PlayerWorldDeathHookMessage = "hook missing for test",
                PlayerWorldDeathLastRecordStatus = "identityUnavailable",
                PlayerWorldDeathLastRecordMessage = "test identity missing",
                PlayerWorldDeathLastEventId = "event-1",
                PlayerWorldDeathLastPairId = "pair-1",
                PlayerWorldDeathLastDeathCount = 3,
                PlayerWorldDeathHistoryReadFailed = true
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldDeathHookInstalled\": false");
            AssertContains(json, "\"PlayerWorldDeathHookMessage\": \"hook missing for test\"");
            AssertContains(json, "\"PlayerWorldDeathLastRecordStatus\": \"identityUnavailable\"");
            AssertContains(json, "\"PlayerWorldDeathLastDeathCount\": 3");
            AssertContains(json, "\"PlayerWorldDeathHistoryReadFailed\": true");

            HookDiagnostics.MarkPlayerDeathHookSkipped("hook skipped for test");
            AssertStringEquals(HookDiagnostics.PlayerDeathHookMessage, "hook skipped for test", "player death hook diagnostic message");
        }

        private static void PlayerWorldDeathHookSelectsKillMeSignature()
        {
            var selected = PlayerDeathHookInstaller.GetSelectedKillMeSignatureForTesting(ResolveTerrariaPlayerTypeForDeathHookTest());
            AssertContains(selected, "KillMe");
            AssertContains(selected, "Terraria.DataStructures.PlayerDeathReason");
            AssertContains(selected, "System.Double");
            AssertContains(selected, "System.Boolean");
        }

        private static void AssertDeathSourceSerialization(string pairId, object reason, string expectedKind, string expectedJson)
        {
            var source = PlayerWorldDeathEventBuilder.ReadSourceSnapshotForTesting(reason);
            AssertStringEquals(source.SourceKind, expectedKind, "death source kind");
            var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                pairId,
                source,
                160f,
                320f,
                source.SourceCustomReason,
                10d,
                1,
                false,
                new DateTime(2026, 6, 14, 2, 3, 4, DateTimeKind.Utc));
            var json = PlayerWorldDeathRecorder.SerializeEventForTesting(deathEvent);
            AssertContains(json, expectedJson);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(PlayerWorldDeathEvent));
                if (!(serializer.ReadObject(stream) is PlayerWorldDeathEvent))
                {
                    throw new InvalidOperationException("Expected death event JSON to deserialize.");
                }
            }
        }

        private static object CreateDeathReasonWithField(string firstName, object firstValue)
        {
            var reason = new TestDeathReason();
            SetPrivateField(reason, firstName, firstValue);
            return reason;
        }

        private static object CreateDeathReasonWithField(string firstName, object firstValue, string secondName, object secondValue)
        {
            var reason = CreateDeathReasonWithField(firstName, firstValue);
            SetPrivateField(reason, secondName, secondValue);
            return reason;
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Expected private field to exist: " + name);
            }

            field.SetValue(instance, value);
        }

        private static PlayerWorldIdentityResolution BuildResolvedDeathIdentity()
        {
            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityResolver.TryResolveForTesting(
                    BuildIdentityFacts(
                        @"C:\Players\DeathRecorder.plr",
                        "Death Recorder",
                        @"C:\Worlds\DeathRecorder.wld",
                        "Death World",
                        "66666666-6666-6666-6666-666666666666",
                        "death-map",
                        666),
                    out identity))
            {
                throw new InvalidOperationException("Expected death identity to resolve.");
            }

            return identity;
        }

        private static Type ResolveTerrariaPlayerTypeForDeathHookTest()
        {
            var loadedType = Type.GetType("Terraria.Player, Terraria", false);
            if (loadedType != null)
            {
                return loadedType;
            }

            var terrariaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Terraria.exe");
            if (!File.Exists(terrariaPath))
            {
                throw new InvalidOperationException("Terraria.exe test dependency was not copied to output directory.");
            }

            var assembly = Assembly.LoadFrom(terrariaPath);
            var playerType = assembly.GetType("Terraria.Player", false);
            if (playerType == null)
            {
                throw new InvalidOperationException("Terraria.Player type not found in Terraria.exe test dependency.");
            }

            return playerType;
        }

        #pragma warning disable 0169, 0414
        private sealed class TestDeathReason
        {
            private int _sourcePlayerIndex = -1;
            private int _sourceNPCIndex = -1;
            private int _sourceProjectileLocalIndex = -1;
            private int _sourceOtherIndex = -1;
            private int _sourceProjectileType;
            private string _sourceCustomReason;

            public object GetDeathText(string playerName)
            {
                return _sourceCustomReason ?? string.Empty;
            }
        }
        #pragma warning restore 0169, 0414
    }
}
