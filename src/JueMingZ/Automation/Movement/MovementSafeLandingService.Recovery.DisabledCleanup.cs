using System;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static bool TryHandleDisabledResidualState(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            long tick,
            AppSettings settings)
        {
            if (!HasSafeLandingResidualState())
            {
                return false;
            }

            var configSummary = MovementSafeLandingOptionCatalog.BuildConfigSummary(settings);
            if (queue == null)
            {
                RecordDecision(false, "disabled", "disabledPendingCleanup:queueUnavailable", tick, null, null, false, configSummary, string.Empty);
                return true;
            }

            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                var reason = snapshot == null || !snapshot.IsInWorld ? "notInWorld" : "inMainMenu";
                RecordDecision(false, "disabled", "disabledPendingCleanup:" + reason, tick, null, null, false, configSummary, string.Empty);
                return true;
            }

            var queueSnapshot = queue.GetFastState();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision(
                    false,
                    "disabled",
                    "disabledPendingCleanup:localPlayerUnavailable",
                    tick,
                    queueSnapshot,
                    null,
                    false,
                    configSummary,
                    TerrariaInputCompat.LastInputCompatError);
                return true;
            }

            if (HasTemporaryEquipmentRecordsOrInflight())
            {
                HandleTemporaryEquipmentRestore(queue, tick, queueSnapshot, player, null, settings);
                return true;
            }

            if (TryHandlePendingSafeLandingGravityRestore(queue, tick, queueSnapshot, player, null, settings))
            {
                return true;
            }

            if (TryHandlePendingSafeLandingMountCancel(queue, tick, queueSnapshot, player, null, settings))
            {
                return true;
            }

            RecordDecision(false, "disabled", "disabledPendingCleanup:idle", tick, queueSnapshot, null, false, configSummary, string.Empty);
            return true;
        }

        private static bool HasSafeLandingResidualState()
        {
            lock (SyncRoot)
            {
                return TemporaryEquipmentRecords.Count > 0 ||
                       _temporaryEquipmentApplyRequestId != Guid.Empty ||
                       _temporaryEquipmentActivationRequestId != Guid.Empty ||
                       _temporaryEquipmentRestoreRequestId != Guid.Empty ||
                       _safeLandingMountCancelPending ||
                       _safeLandingMountCancelRequestId != Guid.Empty ||
                       _safeLandingGravityRestorePending ||
                       _safeLandingGravityRestoreRequestId != Guid.Empty;
            }
        }
    }
}
