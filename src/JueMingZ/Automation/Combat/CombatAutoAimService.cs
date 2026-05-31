using System;
using System.Globalization;
using System.Text;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAutoAimService
    {
        private const long LogIntervalTicks = 300;
        private const long MarkerSelectionRefreshTicks = 6;
        private static CombatAimTargetSelection _currentSelection = new CombatAimTargetSelection();
        private static long _lastSelectionTick = -MarkerSelectionRefreshTicks;
        private static string _lastSelectionSettingsKey = string.Empty;
        private static long _lastLoggedTick = -LogIntervalTicks;
        private static int _lastLoggedTargetWhoAmI = int.MinValue;
        private static string _lastLoggedResultCode = string.Empty;
        private static int _lastLoggedRadius = -1;
        private static bool _lastLoggedTrackDummy;
        private static bool _lastLoggedMarkerEnabled;

        public static CombatAimTargetSelection CurrentSelection
        {
            get { return _currentSelection; }
        }

        public static void Tick(GameStateSnapshot gameState, RuntimeState runtimeState)
        {
            Tick(gameState, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            try
            {
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
                var radiusTiles = settingsSnapshot.CursorAimRadius;
                var trackDummy = settingsSnapshot.CombatAimTrackDummyEnabled;
                var markerEnabled = settingsSnapshot.CombatAimMarkerEnabled;
                var updateTick = runtimeState == null ? 0 : runtimeState.UpdateCount;
                var selectionSettingsKey = settingsSnapshot.CombatAimSelectionSettingsKey ?? string.Empty;
                CombatAimTargetSelection selection;

                if (gameState == null || !gameState.IsInWorld)
                {
                    selection = new CombatAimTargetSelection
                    {
                        Enabled = radiusTiles > 0,
                        RadiusTiles = radiusTiles,
                        TrackDummy = trackDummy,
                        MarkerEnabled = markerEnabled,
                        ResultCode = "NotInWorld",
                        SkipReason = "notInWorld"
                    };
                    CombatAimTargetHistoryService.Clear();
                    CombatAimTargetLockService.Clear();
                    _lastSelectionTick = updateTick;
                    _lastSelectionSettingsKey = selectionSettingsKey;
                }
                else if (radiusTiles <= 0)
                {
                    selection = new CombatAimTargetSelection
                    {
                        Enabled = false,
                        RadiusTiles = 0,
                        TrackDummy = trackDummy,
                        MarkerEnabled = markerEnabled,
                        AimRangeOrigin = settingsSnapshot.AimRangeOrigin ?? string.Empty,
                        AimTargetPriority = settingsSnapshot.AimTargetPriority ?? string.Empty,
                        CursorAimRadius = 0,
                        PlayerAimRadius = settingsSnapshot.PlayerAimRadius,
                        ActiveRangeMode = CombatAimModes.RangeOriginCursor,
                        ResultCode = "Disabled",
                        SkipReason = "radiusDisabled",
                        SelectionPurpose = "Marker"
                    };
                    CombatAimTargetLockService.Clear();
                    _lastSelectionTick = updateTick;
                    _lastSelectionSettingsKey = selectionSettingsKey;
                }
                else if (CanReuseMarkerSelection(updateTick, selectionSettingsKey))
                {
                    return;
                }
                else
                {
                    var readResult = CombatAimTargetReader.Read(trackDummy);
                    CombatAimTargetHistoryService.UpdateFromRead(readResult, runtimeState == null ? 0 : runtimeState.UpdateCount);
                    object player;
                    float playerCenterX;
                    float playerCenterY;
                    var hasPlayerCenter = CombatAimPlayerContext.TryReadLocalPlayerCenter(out player, out playerCenterX, out playerCenterY);
                    var range = CombatAimRangeResolver.Resolve(settings, readResult, hasPlayerCenter, playerCenterX, playerCenterY);
                    selection = CombatAimTargetSelector.Select(
                        readResult,
                        range.RadiusTiles,
                        trackDummy,
                        markerEnabled,
                        BuildSelectionContext(settings, player, hasPlayerCenter, playerCenterX, playerCenterY, range, "Marker"));
                    _lastSelectionTick = updateTick;
                    _lastSelectionSettingsKey = selectionSettingsKey;
                }

                _currentSelection = selection;
                MaybeRecordSelection(selection, runtimeState);
            }
            catch (Exception error)
            {
                _currentSelection = new CombatAimTargetSelection
                {
                    ResultCode = "TickFailed",
                    SkipReason = error.Message
                };
                LogThrottle.ErrorThrottled(
                    "combat-auto-aim-tick-failed",
                    TimeSpan.FromSeconds(10),
                "CombatAutoAimService",
                "Combat auto aim stage 1A tick failed; exception swallowed.", error);
            }
        }

        private static bool CanReuseMarkerSelection(long updateTick, string selectionSettingsKey)
        {
            return _currentSelection != null &&
                   updateTick >= _lastSelectionTick &&
                   updateTick - _lastSelectionTick < MarkerSelectionRefreshTicks &&
                   string.Equals(_lastSelectionSettingsKey, selectionSettingsKey, StringComparison.Ordinal);
        }

        private static string BuildSelectionSettingsKey(AppSettings settings, int radiusTiles, bool trackDummy, bool markerEnabled)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return radiusTiles.ToString(CultureInfo.InvariantCulture) + "|" +
                   (trackDummy ? "1" : "0") + "|" +
                   (markerEnabled ? "1" : "0") + "|" +
                   (settings.AimRangeOrigin ?? string.Empty) + "|" +
                   (settings.AimTargetPriority ?? string.Empty) + "|" +
                   settings.CursorAimRadius.ToString(CultureInfo.InvariantCulture) + "|" +
                   settings.PlayerAimRadius.ToString(CultureInfo.InvariantCulture);
        }

        private static void MaybeRecordSelection(CombatAimTargetSelection selection, RuntimeState runtimeState)
        {
            if (selection == null)
            {
                return;
            }

            var updateTick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            var targetWhoAmI = selection.Target == null ? -1 : selection.Target.WhoAmI;
            var changed =
                targetWhoAmI != _lastLoggedTargetWhoAmI ||
                !string.Equals(selection.ResultCode, _lastLoggedResultCode, StringComparison.Ordinal) ||
                selection.RadiusTiles != _lastLoggedRadius ||
                selection.TrackDummy != _lastLoggedTrackDummy ||
                selection.MarkerEnabled != _lastLoggedMarkerEnabled;

            var periodic = selection.Enabled &&
                           !string.Equals(selection.ResultCode, "NotInWorld", StringComparison.Ordinal) &&
                           updateTick - _lastLoggedTick >= LogIntervalTicks;

            if (!changed && !periodic)
            {
                return;
            }

            _lastLoggedTick = updateTick;
            _lastLoggedTargetWhoAmI = targetWhoAmI;
            _lastLoggedResultCode = selection.ResultCode ?? string.Empty;
            _lastLoggedRadius = selection.RadiusTiles;
            _lastLoggedTrackDummy = selection.TrackDummy;
            _lastLoggedMarkerEnabled = selection.MarkerEnabled;

            var message = selection.Target == null
                ? "Combat aim target selection: " + selection.ResultCode
                : "Combat aim target selected: npc=" + selection.Target.WhoAmI.ToString(CultureInfo.InvariantCulture) +
                  ", type=" + selection.Target.Type.ToString(CultureInfo.InvariantCulture);

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "CombatAim.TargetSelect",
                "Aim",
                string.Empty,
                "Observed",
                string.IsNullOrWhiteSpace(selection.ResultCode) ? "Unknown" : selection.ResultCode,
                message,
                0,
                "{}",
                "{}",
                BuildSelectionJson(selection),
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string BuildSelectionJson(CombatAimTargetSelection selection)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "enabled", BoolRaw(selection.Enabled), true);
            AppendRaw(builder, "radiusTiles", IntRaw(selection.RadiusTiles), true);
            AppendRaw(builder, "aimRangeRadius", IntRaw(selection.RadiusTiles), true);
            AppendString(builder, "aimRangeOrigin", selection.AimRangeOrigin, true);
            AppendString(builder, "activeRangeMode", selection.ActiveRangeMode, true);
            AppendString(builder, "aimTargetPriority", selection.AimTargetPriority, true);
            AppendRaw(builder, "cursorAimRadius", IntRaw(selection.CursorAimRadius), true);
            AppendRaw(builder, "playerAimRadius", IntRaw(selection.PlayerAimRadius), true);
            AppendRaw(builder, "playerScreenRadiusTiles", IntRaw(selection.PlayerScreenRadiusTiles), true);
            AppendRaw(builder, "playerScreenMarginTiles", IntRaw(selection.PlayerScreenMarginTiles), true);
            AppendRaw(builder, "trackDummy", BoolRaw(selection.TrackDummy), true);
            AppendRaw(builder, "markerEnabled", BoolRaw(selection.MarkerEnabled), true);
            AppendRaw(builder, "cursorWorld", BuildPointJson(selection.CursorWorldX, selection.CursorWorldY), true);
            AppendRaw(builder, "rangeCenterWorld", BuildPointJson(selection.RangeCenterWorldX, selection.RangeCenterWorldY), true);
            AppendRaw(builder, "mouseScreen", BuildIntPointJson(selection.MouseScreenX, selection.MouseScreenY), true);
            AppendRaw(builder, "screenPosition", BuildPointJson(selection.ScreenPositionX, selection.ScreenPositionY), true);
            AppendNullableInt(builder, "targetWhoAmI", selection.Target == null ? (int?)null : selection.Target.WhoAmI, true);
            AppendNullableInt(builder, "targetType", selection.Target == null ? (int?)null : selection.Target.Type, true);
            AppendString(builder, "targetName", selection.Target == null ? string.Empty : selection.Target.Name, true);
            AppendRaw(builder, "targetCenter", selection.Target == null ? "null" : BuildPointJson(selection.Target.CenterX, selection.Target.CenterY), true);
            AppendRaw(builder, "hitbox", selection.Target == null ? "null" : BuildRectJson(selection.Target.HitboxX, selection.Target.HitboxY, selection.Target.HitboxWidth, selection.Target.HitboxHeight), true);
            AppendRaw(builder, "centerDistanceTiles", FloatRaw(selection.CenterDistanceTiles), true);
            AppendRaw(builder, "hitboxDistanceTiles", FloatRaw(selection.HitboxDistanceTiles), true);
            AppendRaw(builder, "targetDistanceFromRangeCenter", FloatRaw(selection.TargetDistanceFromRangeCenterTiles), true);
            AppendRaw(builder, "targetScore", FloatRaw(selection.TargetScore), true);
            AppendRaw(builder, "lineClear", BoolRaw(selection.LineClear), true);
            AppendRaw(builder, "losCacheHit", BoolRaw(selection.LosCacheHit), true);
            AppendRaw(builder, "distanceToPlayerCursorRay", FloatRaw(selection.DistanceToPlayerCursorRay), true);
            AppendRaw(builder, "inForwardCone", BoolRaw(selection.InForwardCone), true);
            AppendRaw(builder, "previousTargetBonus", FloatRaw(selection.PreviousTargetBonus), true);
            AppendString(builder, "selectedSamplePoint", selection.SelectedSamplePoint, true);
            AppendString(builder, "attackSamplePoint", selection.AttackSamplePoint, true);
            AppendString(builder, "selectionSamplePoint", selection.SelectionSamplePoint, true);
            AppendRaw(builder, "lineOfSightRejectedSampleCount", IntRaw(selection.LineOfSightRejectedSampleCount), true);
            AppendRaw(builder, "nearestHitboxPointPenaltyApplied", BoolRaw(selection.NearestHitboxPointPenaltyApplied), true);
            AppendRaw(builder, "centerPreferred", BoolRaw(selection.CenterPreferred), true);
            AppendRaw(builder, "selectedSampleWorld", BuildPointJson(selection.SelectedSampleWorldX, selection.SelectedSampleWorldY), true);
            AppendString(builder, "selectedReason", selection.SelectedReason, true);
            AppendRaw(builder, "lockedTargetId", selection.LockedTargetId >= 0 ? IntRaw(selection.LockedTargetId) : "null", true);
            AppendRaw(builder, "lockedTargetType", selection.LockedTargetType > 0 ? IntRaw(selection.LockedTargetType) : "null", true);
            AppendRaw(builder, "lockedTargetStillValid", BoolRaw(selection.LockedTargetStillValid), true);
            AppendRaw(builder, "targetLockAgeTicks", IntRaw(selection.TargetLockAgeTicks), true);
            AppendRaw(builder, "targetHoldTicksRemaining", IntRaw(selection.TargetHoldTicksRemaining), true);
            AppendString(builder, "selectionPurpose", selection.SelectionPurpose, true);
            AppendRaw(builder, "selectionCacheHit", BoolRaw(selection.SelectionCacheHit), true);
            AppendString(builder, "selectionCacheKey", selection.SelectionCacheKey, true);
            AppendRaw(builder, "markerTargetWhoAmI", selection.MarkerTargetWhoAmI >= 0 ? IntRaw(selection.MarkerTargetWhoAmI) : "null", true);
            AppendRaw(builder, "attackTargetWhoAmI", selection.AttackTargetWhoAmI >= 0 ? IntRaw(selection.AttackTargetWhoAmI) : "null", true);
            AppendRaw(builder, "markerAttackTargetMismatch", BoolRaw(selection.MarkerAttackTargetMismatch), true);
            AppendRaw(builder, "markerTargetChangedForAttack", BoolRaw(selection.MarkerTargetChangedForAttack), true);
            AppendRaw(builder, "candidateCount", IntRaw(selection.CandidateCount), true);
            AppendRaw(builder, "cheapCandidateCount", IntRaw(selection.CheapCandidateCount), true);
            AppendRaw(builder, "expensiveCandidateCount", IntRaw(selection.ExpensiveCandidateCount), true);
            AppendRaw(builder, "evaluatedCandidateCount", IntRaw(selection.EvaluatedCandidateCount), true);
            AppendRaw(builder, "inRangeCandidateCount", IntRaw(selection.InRangeCandidateCount), true);
            AppendString(builder, "skipReason", selection.SkipReason ?? string.Empty, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static CombatAimTargetSelectionContext BuildSelectionContext(
            AppSettings settings,
            object player,
            bool hasPlayerCenter,
            float playerCenterX,
            float playerCenterY,
            CombatAimRangeResolveResult range,
            string purpose)
        {
            return new CombatAimTargetSelectionContext
            {
                AimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings == null ? string.Empty : settings.AimRangeOrigin),
                AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings == null ? string.Empty : settings.AimTargetPriority),
                CursorAimRadius = settings == null ? 0 : settings.CursorAimRadius,
                PlayerAimRadius = settings == null ? 0 : settings.PlayerAimRadius,
                Player = player,
                HasPlayerCenter = hasPlayerCenter,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY,
                HasResolvedRange = range != null,
                Range = range,
                SelectionPurpose = purpose ?? string.Empty
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) +
                   "}";
        }

        private static string BuildIntPointJson(int x, int y)
        {
            return "{" +
                   "\"x\":" + IntRaw(x) + "," +
                   "\"y\":" + IntRaw(y) +
                   "}";
        }

        private static string BuildRectJson(float x, float y, float width, float height)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) + "," +
                   "\"width\":" + FloatRaw(width) + "," +
                   "\"height\":" + FloatRaw(height) +
                   "}";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendNullableInt(StringBuilder builder, string name, int? value, bool comma)
        {
            AppendRaw(builder, name, value.HasValue ? IntRaw(value.Value) : "null", comma);
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
