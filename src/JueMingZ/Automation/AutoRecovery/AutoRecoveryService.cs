using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.AutoRecovery
{
    public static partial class AutoRecoveryService
    {
        // Shared state and public snapshot entrypoint for the partial AutoRecoveryService implementation.
        public const long ForceDueTick = -1000000;
        private const string AutoHealFeatureId = "buff.auto_heal";
        private const string AutoManaFeatureId = "buff.auto_mana";
        private const string AutoBuffFeatureId = "buff.auto_buff";
        private const string AutoNurseFeatureId = "buff.nurse_auto_heal";
        private const string AutoStationBuffFeatureId = "buff.auto_station_buff";
        private const int BlockedEventThrottleTicks = 60;
        private const int AutoNurseCooldownTicks = 90;
        private const int AutoStationBuffCooldownTicks = 10;
        private const int AutoStationBuffFastSkipResultThrottleTicks = 60;
        private const int AutoBuffFailedRetryThrottleTicks = 45;
        private const int AutoBuffInflightExpiryTicks = 600;
        private const int ImmediateAutoBuffInventorySignatureIntervalTicks = 15;
        private static readonly object SyncRoot = new object();
        private static readonly AutoRecoveryState State = new AutoRecoveryState();
        private static readonly HashSet<int> AutoBuffInflightItemTypes = new HashSet<int>();
        private static readonly Dictionary<int, long> AutoBuffInflightTicks = new Dictionary<int, long>();
        private static readonly Dictionary<Guid, int> AutoBuffRequestItems = new Dictionary<Guid, int>();
        private static readonly Dictionary<int, long> AutoBuffLastFailedTicks = new Dictionary<int, long>();
        private static DateTime _lastF5ControlUtc = DateTime.MinValue;
        private static long _lastAutoHealBlockedEventTick = ForceDueTick;
        private static long _lastAutoManaBlockedEventTick = ForceDueTick;
        private static long _lastAutoBuffBlockedEventTick = ForceDueTick;
        private static long _lastAutoBuffMissingEventTick = ForceDueTick;
        private static long _lastAutoStationBuffFastSkipResultTick = ForceDueTick;
        private static bool _immediateBuffReconcileRequested;
        private static string _immediateBuffTriggerReason = string.Empty;
        private static bool _hasLastInventorySignature;
        private static bool _hasLastBuffSignature;
        private static long _lastInventorySignatureTick = ForceDueTick;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastBuffSignature = string.Empty;

        public static AutoRecoveryState GetStateSnapshot()
        {
            lock (SyncRoot)
            {
                var snapshot = RuntimeSettingsSnapshotProvider.GetCurrent();
                ApplySettingsLocked(snapshot == null ? AutoRecoverySettings.FromConfig() : snapshot.AutoRecovery);
                return State.Clone();
            }
        }
    }
}
