using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    // Frame reads sample settings, player, input, and UI gates only; blocked frames stop before any takeover.
    public static partial class CombatAimFlailControlService
    {
        private static bool TryReadFlailRuntimeReady(out string blockedReason)
        {
            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                blockedReason = "runtimeTypesUnavailable";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private static bool TryReadFlailSettings(ref CombatAimFlailRuntimeFrame frame, out string blockedReason)
        {
            frame.Settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (frame.Settings.CursorAimRadius <= 0)
            {
                blockedReason = "autoAimDisabled";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private static bool TryReadFlailLocalPlayer(ref CombatAimFlailRuntimeFrame frame, out string blockedReason)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                blockedReason = "playerUnavailable";
                return false;
            }

            frame.Player = player;
            blockedReason = string.Empty;
            return true;
        }

        private static bool TryReadFlailPhysicalInput(ref CombatAimFlailRuntimeFrame frame, out string blockedReason)
        {
            bool physicalHeld;
            if (!TerrariaInputCompat.TryReadPhysicalUseItemHeld(frame.Player, out physicalHeld))
            {
                blockedReason = "physicalUseItemUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            frame.PhysicalHeld = physicalHeld;
            blockedReason = string.Empty;
            return true;
        }

        private static void ReadFlailUiContext(ref CombatAimFlailRuntimeFrame frame)
        {
            frame.UiContext = TerrariaInputCompat.ReadUiInputContext(frame.Player);
        }

        private static bool TryResolveFlailUiBlocked(ref CombatAimFlailRuntimeFrame frame, out string blockedReason)
        {
            var ui = frame.UiContext;
            if (ui == null ||
                (!ui.MainTypeUnavailable &&
                 !ui.GameMenu &&
                 !ui.ChatOpen &&
                 !ui.NpcChatOpen &&
                 !ui.ChestOpen &&
                 !ui.MouseCapturedByUi))
            {
                blockedReason = string.Empty;
                return false;
            }

            blockedReason = ui.MainTypeUnavailable
                ? "mainTypeUnavailable"
                : ui.GameMenu
                    ? "gameMenu"
                    : ui.ChatOpen
                        ? "chatOpen"
                        : ui.NpcChatOpen
                            ? "npcChatOpen"
                            : ui.ChestOpen
                                ? "chestOpen"
                                : "uiBlocked:" + (ui.MouseCaptureReason ?? string.Empty);
            return true;
        }

        private static bool TryReadFlailWeaponProfile(ref CombatAimFlailRuntimeFrame frame, out string blockedReason)
        {
            CombatAimWeaponProfile profile;
            if (!TryReadSelectedWeaponProfile(frame.Player, out profile))
            {
                blockedReason = "selectedItemUnavailable";
                return false;
            }

            frame.Profile = profile;
            blockedReason = string.Empty;
            return true;
        }

        private static void ReadFlailEligibility(ref CombatAimFlailRuntimeFrame frame)
        {
            frame.Prepared = CombatAimBallisticSolver.Prepare(frame.Player, frame.Profile);
            frame.ProjectileAiStyle = frame.Prepared == null ? 0 : frame.Prepared.ProjectileAiStyle;
            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(frame.Profile.Shoot);
            frame.Eligibility = CombatAimFlailPolicy.Evaluate(frame.Profile, frame.ProjectileAiStyle, isYoyo);
        }

        private static void ReadFlailTick(ref CombatAimFlailRuntimeFrame frame)
        {
            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            frame.Tick = tick;
        }

        private static void ReadFlailCurrentSelection(ref CombatAimFlailRuntimeFrame frame)
        {
            frame.Selection = CombatAutoAimService.CurrentSelection;
            frame.CurrentTargetAvailable = frame.Selection != null &&
                                           frame.Selection.Enabled &&
                                           frame.Selection.Target != null;
        }

        private static void ReadFlailProjectileFrame(ref CombatAimFlailRuntimeFrame frame)
        {
            FlailProjectileSnapshot projectile;
            frame.HasProjectile = ProjectileTracker.TryFindActiveFlailProjectile(frame.Player, frame.Profile.Shoot, out projectile);
            frame.RawProjectile = projectile;
            frame.Projectile = frame.HasProjectile
                ? CombatAimFlailProjectileFrame.FromSnapshot(projectile)
                : CombatAimFlailProjectileFrame.None();
        }
    }
}
