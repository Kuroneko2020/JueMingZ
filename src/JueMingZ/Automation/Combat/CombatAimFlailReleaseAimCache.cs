using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        private sealed class FlailCachedReleaseAim
        {
            public int ItemType;
            public string ItemName = string.Empty;
            public int Shoot;
            public int SelectedSlot = -1;
            public long RecordedGameUpdateCount;
            public float AimWorldX;
            public float AimWorldY;
            public int TargetWhoAmI = -1;
            public int TargetType;
            public string TargetName = string.Empty;
            public CombatAimWeaponProfile WeaponProfile;
            public CombatAimTargetSelection Selection;
            public CombatAimBallisticSolution BallisticSolution;
            public string AimRangeOrigin = string.Empty;
            public string AimTargetPriority = string.Empty;
            public int CursorAimRadius;
            public int PlayerAimRadius;
            public bool TrackDummy;
            public bool MarkerEnabled;

            public FlailCachedReleaseAim Clone()
            {
                return new FlailCachedReleaseAim
                {
                    ItemType = ItemType,
                    ItemName = ItemName ?? string.Empty,
                    Shoot = Shoot,
                    SelectedSlot = SelectedSlot,
                    RecordedGameUpdateCount = RecordedGameUpdateCount,
                    AimWorldX = AimWorldX,
                    AimWorldY = AimWorldY,
                    TargetWhoAmI = TargetWhoAmI,
                    TargetType = TargetType,
                    TargetName = TargetName ?? string.Empty,
                    WeaponProfile = WeaponProfile,
                    Selection = CloneSelection(Selection),
                    BallisticSolution = CloneBallisticSolution(BallisticSolution),
                    AimRangeOrigin = AimRangeOrigin ?? string.Empty,
                    AimTargetPriority = AimTargetPriority ?? string.Empty,
                    CursorAimRadius = CursorAimRadius,
                    PlayerAimRadius = PlayerAimRadius,
                    TrackDummy = TrackDummy,
                    MarkerEnabled = MarkerEnabled
                };
            }
        }

        private sealed class CombatAimFlailReleaseAimCache
        {
            private readonly object _syncRoot = new object();
            private FlailCachedReleaseAim _cachedReleaseAim;
            private CombatAimItemCheckDecision _flailComboPressAimDecision;
            private long _flailComboPressAimTick;

            public bool HasCachedReleaseAim
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return _cachedReleaseAim != null;
                    }
                }
            }

            public void UpdateCachedReleaseAim(
                object player,
                CombatAimWeaponProfile profile,
                CombatAimBallisticContext prepared,
                CombatAimTargetSelection selection,
                AppSettings settings,
                long tick)
            {
                if (profile == null || profile.IsEmpty || selection == null || selection.Target == null)
                {
                    return;
                }

                var ballistic = selection.BallisticSolution ?? CombatAimBallisticSolver.Solve(prepared, selection.BallisticTarget ?? selection.Target);
                var aimWorldX = ballistic == null ? selection.SelectedSampleWorldX : ballistic.AimWorldX;
                var aimWorldY = ballistic == null ? selection.SelectedSampleWorldY : ballistic.AimWorldY;

                int selectedSlot;
                TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
                lock (_syncRoot)
                {
                    _cachedReleaseAim = new FlailCachedReleaseAim
                    {
                        ItemType = profile.ItemType,
                        ItemName = profile.Name ?? string.Empty,
                        Shoot = profile.Shoot,
                        SelectedSlot = selectedSlot,
                        RecordedGameUpdateCount = tick,
                        AimWorldX = aimWorldX,
                        AimWorldY = aimWorldY,
                        TargetWhoAmI = selection.Target.WhoAmI,
                        TargetType = selection.Target.Type,
                        TargetName = selection.Target.Name ?? string.Empty,
                        WeaponProfile = profile,
                        Selection = CloneSelection(selection),
                        BallisticSolution = CloneBallisticSolution(ballistic),
                        AimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings == null ? string.Empty : settings.AimRangeOrigin),
                        AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings == null ? string.Empty : settings.AimTargetPriority),
                        CursorAimRadius = Clamp(settings == null ? 0 : settings.CursorAimRadius, 0, 50),
                        PlayerAimRadius = Clamp(settings == null ? 0 : settings.PlayerAimRadius, 0, 50),
                        TrackDummy = settings != null && settings.CombatAimTrackDummyEnabled,
                        MarkerEnabled = settings != null && settings.CombatAimMarkerEnabled
                    };
                }
            }

            public bool TryGetCachedReleaseAim(
                CombatAimWeaponProfile profile,
                long tick,
                out FlailCachedReleaseAim cached)
            {
                cached = CloneCachedReleaseAim();
                if (profile == null || cached == null)
                {
                    return false;
                }

                // Cached release aim is bounded by item, shoot type, and age; do
                // not turn it into an unbounded target memory.
                if (cached.ItemType != profile.ItemType || cached.Shoot != profile.Shoot)
                {
                    return false;
                }

                var age = ComputeCachedReleaseAimAge(cached, tick);
                return age >= 0 && age <= CachedReleaseAimMaxAgeTicks;
            }

            public string ResolveCachedReleaseAimUnavailableReason(CombatAimWeaponProfile profile, long tick)
            {
                var cached = CloneCachedReleaseAim();
                if (cached == null)
                {
                    return "missing";
                }

                if (profile == null)
                {
                    return "missingProfile";
                }

                if (cached.ItemType != profile.ItemType || cached.Shoot != profile.Shoot)
                {
                    return "itemChanged";
                }

                var age = ComputeCachedReleaseAimAge(cached, tick);
                if (age < 0)
                {
                    return "futureTick";
                }

                if (age > CachedReleaseAimMaxAgeTicks)
                {
                    return "expired:age=" + age.ToString(CultureInfo.InvariantCulture);
                }

                return "unknown";
            }

            public void ClearCachedReleaseAim()
            {
                lock (_syncRoot)
                {
                    _cachedReleaseAim = null;
                }
            }

            public void RememberFlailComboPressAim(CombatAimItemCheckDecision decision, long tick)
            {
                lock (_syncRoot)
                {
                    _flailComboPressAimDecision = decision;
                    _flailComboPressAimTick = tick;
                }
            }

            public bool TryGetRecentFlailComboPressAim(long tick, out CombatAimItemCheckDecision decision)
            {
                decision = null;
                lock (_syncRoot)
                {
                    if (_flailComboPressAimDecision == null)
                    {
                        return false;
                    }

                    if (tick > 0 &&
                        _flailComboPressAimTick > 0 &&
                        tick - _flailComboPressAimTick > FlailComboPressAimMaxAgeTicks)
                    {
                        _flailComboPressAimDecision = null;
                        _flailComboPressAimTick = 0;
                        return false;
                    }

                    decision = _flailComboPressAimDecision;
                    return true;
                }
            }

            public void ClearFlailComboPressAim()
            {
                lock (_syncRoot)
                {
                    _flailComboPressAimDecision = null;
                    _flailComboPressAimTick = 0;
                }
            }

            public void SetCachedReleaseAimForTesting(CombatAimItemCheckDecision decision, long recordedTick)
            {
                if (decision == null || decision.WeaponProfile == null)
                {
                    ClearCachedReleaseAim();
                    return;
                }

                lock (_syncRoot)
                {
                    _cachedReleaseAim = new FlailCachedReleaseAim
                    {
                        ItemType = decision.ItemType,
                        ItemName = decision.ItemName ?? string.Empty,
                        Shoot = decision.WeaponProfile.Shoot,
                        SelectedSlot = decision.SelectedSlot,
                        RecordedGameUpdateCount = recordedTick,
                        AimWorldX = decision.AimWorldX,
                        AimWorldY = decision.AimWorldY,
                        TargetWhoAmI = decision.Target == null ? -1 : decision.Target.WhoAmI,
                        TargetType = decision.Target == null ? 0 : decision.Target.Type,
                        TargetName = decision.Target == null ? string.Empty : decision.Target.Name ?? string.Empty,
                        WeaponProfile = decision.WeaponProfile,
                        Selection = CloneSelection(decision.Selection),
                        BallisticSolution = CloneBallisticSolution(decision.BallisticSolution),
                        AimRangeOrigin = decision.AimRangeOrigin ?? string.Empty,
                        AimTargetPriority = decision.AimTargetPriority ?? string.Empty,
                        CursorAimRadius = decision.CursorAimRadius,
                        PlayerAimRadius = decision.PlayerAimRadius,
                        TrackDummy = decision.TrackDummy,
                        MarkerEnabled = decision.MarkerEnabled
                    };
                }
            }

            private FlailCachedReleaseAim CloneCachedReleaseAim()
            {
                lock (_syncRoot)
                {
                    return _cachedReleaseAim == null ? null : _cachedReleaseAim.Clone();
                }
            }
        }

        private static void UpdateCachedReleaseAim(
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticContext prepared,
            CombatAimTargetSelection selection,
            AppSettings settings,
            long tick)
        {
            ReleaseAimCache.UpdateCachedReleaseAim(player, profile, prepared, selection, settings, tick);
        }

        private static bool TryGetCachedReleaseAim(
            CombatAimWeaponProfile profile,
            long tick,
            out FlailCachedReleaseAim cached)
        {
            return ReleaseAimCache.TryGetCachedReleaseAim(profile, tick, out cached);
        }

        private static int ComputeCachedReleaseAimAge(FlailCachedReleaseAim cached, long tick)
        {
            if (cached == null)
            {
                return -1;
            }

            if (tick <= 0 || cached.RecordedGameUpdateCount <= 0)
            {
                return 0;
            }

            var age = tick - cached.RecordedGameUpdateCount;
            if (age < 0)
            {
                return -1;
            }

            return age > int.MaxValue ? int.MaxValue : (int)age;
        }

        private static string ResolveCachedReleaseAimUnavailableReason(CombatAimWeaponProfile profile, long tick)
        {
            return ReleaseAimCache.ResolveCachedReleaseAimUnavailableReason(profile, tick);
        }

        private static void ClearCachedReleaseAim()
        {
            ReleaseAimCache.ClearCachedReleaseAim();
        }

        private static void ClearFlailComboPressAim()
        {
            ReleaseAimCache.ClearFlailComboPressAim();
        }

        internal static void SetCachedReleaseAimForTesting(CombatAimItemCheckDecision decision, long recordedTick)
        {
            ReleaseAimCache.SetCachedReleaseAimForTesting(decision, recordedTick);
        }
    }
}
