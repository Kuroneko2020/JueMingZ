using System;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TrySetControlDown(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot set controlDown: player unavailable.");
            }

            // Controlled input write: safe landing may suppress down for a few ticks during an emergency jump.
            return SetMember(player, "controlDown", pressed)
                ? ClearInputError()
                : Fail("Cannot set player.controlDown.");
        }

        public static bool TryPrimeJumpReleaseForNextTick(object player, bool applyRocketRelease, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot prime jump release: player unavailable.";
                return Fail(message);
            }

            // Controlled input write: simulated multi-jump only toggles vanilla jump input state.
            var ok = SetMember(player, "controlJump", false);
            ok &= SetMember(player, "releaseJump", true);

            var rocketReleaseApplied = false;
            if (applyRocketRelease)
            {
                // Optional controlled input write: rocketRelease exists in Terraria 1.4.x and is ignored if absent.
                rocketReleaseApplied = TrySetMemberIfExists(player, "rocketRelease", true);
            }

            var triggersSynced = TrySyncPlayerInputJumpTriggers(false, false, true, out var triggerMessage);

            message = ok
                ? "Jump release primed" + (rocketReleaseApplied ? " with rocket release." : ".")
                : "Jump release prime failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryPressPrimedJumpForNextTick(object player, bool applyRocketRelease, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot press primed jump: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", true);
            ok &= SetMember(player, "releaseJump", true);

            var rocketReleaseApplied = false;
            if (applyRocketRelease)
            {
                rocketReleaseApplied = TrySetMemberIfExists(player, "rocketRelease", true);
            }

            var triggersSynced = TrySyncPlayerInputJumpTriggers(true, true, false, out var triggerMessage);

            message = ok
                ? "Primed jump press armed" + (rocketReleaseApplied ? " with rocket release." : ".")
                : "Primed jump press failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryHoldJumpInput(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot hold jump input: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", true);
            var triggersSynced = TrySyncPlayerInputJumpTriggers(true, false, false, out var triggerMessage);
            message = ok ? "Jump input held." : "Jump input hold failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryReleaseJumpInput(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot release jump input: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", false);
            ok &= SetMember(player, "releaseJump", true);
            var triggersSynced = TrySyncPlayerInputJumpTriggers(false, false, true, out var triggerMessage);
            message = ok ? "Jump input released." : "Jump input release failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryPrimeQuickMountReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", false, false, true, "Quick mount release primed.", "Quick mount release prime failed", out message);
        }

        public static bool TryPressPrimedQuickMountForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", true, true, false, "Primed quick mount press armed.", "Primed quick mount press failed", out message);
        }

        public static bool TryHoldQuickMountInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", true, false, false, "Quick mount input held.", "Quick mount input hold failed", out message);
        }

        public static bool TryReleaseQuickMountInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", false, false, true, "Quick mount input released.", "Quick mount input release failed", out message);
        }

        public static bool TryPrimeGrappleReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", false, false, true, "Grapple release primed.", "Grapple release prime failed", out message);
        }

        public static bool TryPressPrimedGrappleForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", true, true, false, "Primed grapple press armed.", "Primed grapple press failed", out message);
        }

        public static bool TryHoldGrappleInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", true, false, false, "Grapple input held.", "Grapple input hold failed", out message);
        }

        public static bool TryReleaseGrappleInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", false, false, true, "Grapple input released.", "Grapple input release failed", out message);
        }

        public static bool TryPrimeUpReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", false, false, true, "Up input release primed.", "Up input release prime failed", out message);
        }

        public static bool TryPressPrimedUpForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", true, true, false, "Primed up input press armed.", "Primed up input press failed", out message);
        }

        public static bool TryHoldUpInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", true, false, false, "Up input held.", "Up input hold failed", out message);
        }

        public static bool TryReleaseUpInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", false, false, true, "Up input released.", "Up input release failed", out message);
        }

        public static bool TryReleaseSafeLandingControlInputs(object player, out string message)
        {
            string jumpMessage;
            string mountMessage;
            string grappleMessage;
            string upMessage;
            var jumpOk = TryReleaseJumpInput(player, out jumpMessage);
            var mountOk = TryReleaseQuickMountInput(player, out mountMessage);
            var grappleOk = TryReleaseGrappleInput(player, out grappleMessage);
            var upOk = TryReleaseUpInput(player, out upMessage);
            message = (jumpMessage ?? string.Empty) + " " +
                      (mountMessage ?? string.Empty) + " " +
                      (grappleMessage ?? string.Empty) + " " +
                      (upMessage ?? string.Empty);
            return jumpOk || mountOk || grappleOk || upOk;
        }

        private static bool TrySetNamedControlInput(object player, string controlFieldName, string playerInputTriggerName, bool current, bool justPressed, bool justReleased, string successMessage, string failurePrefix, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = failurePrefix + ": player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, controlFieldName, current);
            var releaseFieldName = ResolveReleaseFieldName(controlFieldName);
            var releaseSynced = false;
            if (!string.IsNullOrWhiteSpace(releaseFieldName) && (justPressed || justReleased || !current))
            {
                // Quick mount / grapple are one-shot vanilla control inputs. Their Terraria handlers also gate on
                // releaseMount / releaseHook in several versions, so keep those release flags primed when emitting
                // a synthetic press or release. Missing fields are allowed: PlayerInput trigger sync remains the
                // fallback path.
                releaseSynced = TrySetMemberIfExists(player, releaseFieldName, true);
            }

            var triggersSynced = TrySyncPlayerInputTrigger(playerInputTriggerName, current, justPressed, justReleased, out var triggerMessage);
            message = ok ? successMessage : failurePrefix + ": " + LastInputCompatError;
            if (releaseSynced)
            {
                message += " " + releaseFieldName + " primed.";
            }

            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        private static string ResolveReleaseFieldName(string controlFieldName)
        {
            if (string.Equals(controlFieldName, "controlMount", StringComparison.Ordinal))
            {
                return "releaseMount";
            }

            if (string.Equals(controlFieldName, "controlHook", StringComparison.Ordinal))
            {
                return "releaseHook";
            }

            if (string.Equals(controlFieldName, "controlUp", StringComparison.Ordinal))
            {
                return "releaseUp";
            }

            if (string.Equals(controlFieldName, "controlDown", StringComparison.Ordinal))
            {
                return "releaseDown";
            }

            return string.Empty;
        }
    }
}
