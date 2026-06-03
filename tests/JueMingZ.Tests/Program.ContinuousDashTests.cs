using System;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void ContinuousDashDoubleTapHoldSurvivesBriefDirectionGap()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate("doubleTapNotArmed", DashProfile(-1), 100, "first tap");
            ExpectDashGate("directionNotHeld", DashProfile(0), 101, "release after first tap");
            ExpectDashGate(string.Empty, DashProfile(-1), 105, "second tap arms");
            AssertContinuousDashArmed(-1, "second tap");

            ExpectDashGate("directionNotHeldGrace", DashProfile(0), 106, "one tick transient gap");
            AssertContinuousDashArmed(-1, "brief direction gap");

            ExpectDashGate(string.Empty, DashProfile(-1), 107, "held direction resumes after gap");
            AssertContinuousDashArmed(-1, "held direction resumes");
        }

        private static void ContinuousDashDoubleTapHoldCancelsAfterReleaseGrace()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate("doubleTapNotArmed", DashProfile(1), 200, "first tap");
            ExpectDashGate("directionNotHeld", DashProfile(0), 201, "release after first tap");
            ExpectDashGate(string.Empty, DashProfile(1), 205, "second tap arms");
            AssertContinuousDashArmed(1, "second tap");

            ExpectDashGate("directionNotHeld", DashProfile(0), 208, "release beyond grace");
            AssertContinuousDashArmed(0, "release beyond grace");

            ExpectDashGate("doubleTapNotArmed", DashProfile(1), 209, "single press after cancel");
            AssertContinuousDashArmed(0, "single press after cancel");
        }

        private static void ContinuousDashDoubleTapHoldCancelsOnDirectionSwitch()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate("doubleTapNotArmed", DashProfile(-1), 300, "first left tap");
            ExpectDashGate("directionNotHeld", DashProfile(0), 301, "release after first left tap");
            ExpectDashGate(string.Empty, DashProfile(-1), 305, "second left tap arms");
            AssertContinuousDashArmed(-1, "second left tap");

            ExpectDashGate("doubleTapNotArmed", DashProfile(1), 306, "switch to right");
            AssertContinuousDashArmed(0, "direction switch");
        }

        private static void ContinuousDashDoubleTapHoldCancelsWhenUncontrollable()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate("doubleTapNotArmed", DashProfile(-1), 400, "first tap");
            ExpectDashGate("directionNotHeld", DashProfile(0), 401, "release after first tap");
            ExpectDashGate(string.Empty, DashProfile(-1), 405, "second tap arms");
            AssertContinuousDashArmed(-1, "second tap");

            var profile = DashProfile(0);
            profile.PlayerNoItems = true;
            ExpectDashGate("playerNotControllable", profile, 406, "uncontrollable gap");
            AssertContinuousDashArmed(0, "uncontrollable gap");
        }

        private static void ContinuousDashDoubleTapRequiresDoubleTapForOppositeDirection()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate("doubleTapNotArmed", DashProfile(1), 500, "first right tap");
            ExpectDashGate("directionNotHeld", DashProfile(0), 501, "release after first right tap");
            ExpectDashGate(string.Empty, DashProfile(1), 505, "second right tap arms");
            AssertContinuousDashArmed(1, "second right tap");

            ExpectDashGate("doubleTapNotArmed", DashProfile(true, true), 506, "left pressed while right is still held", -1);
            AssertContinuousDashArmed(0, "single opposite press");

            ExpectDashGate("doubleTapNotArmed", DashProfile(true, true), 507, "both held without another left edge", -1);
            AssertContinuousDashArmed(0, "both held without another left edge");

            ExpectDashGate("doubleTapNotArmed", DashProfile(1), 508, "left released while right remains held", 1);
            AssertContinuousDashArmed(0, "right remains held after left release");

            ExpectDashGate(string.Empty, DashProfile(true, true), 510, "second left press arms left", -1);
            AssertContinuousDashArmed(-1, "second left press");
        }

        private static void ContinuousDashHoldModeUsesLaterDirectionWhenBothHeld()
        {
            MovementContinuousDashService.ResetArmingForTesting();

            ExpectDashGate(string.Empty, DashProfile(1), MovementContinuousDashModes.HoldDirection, 600, "right held", 1);
            ExpectDashGate(string.Empty, DashProfile(true, true), MovementContinuousDashModes.HoldDirection, 601, "left pressed while right is held", -1);
        }

        private static void ContinuousDashDirectionHeldAcceptsBothKeys()
        {
            var profile = DashProfile(true, true);
            profile.HasDashAbility = true;
            profile.DashCooldownReady = true;

            if (!profile.IsDirectionHeld(-1) ||
                !profile.IsDirectionHeld(1) ||
                !profile.CanDashInDirection(-1) ||
                !profile.CanDashInDirection(1))
            {
                throw new InvalidOperationException("Expected requested dash direction to remain valid while both horizontal keys are held.");
            }
        }

        private static DashInputProfile DashProfile(int direction)
        {
            return new DashInputProfile
            {
                PlayerActive = true,
                ControlLeft = direction < 0,
                ControlRight = direction > 0,
                HeldDirection = direction < 0 ? -1 : direction > 0 ? 1 : 0
            };
        }

        private static DashInputProfile DashProfile(bool leftHeld, bool rightHeld)
        {
            return new DashInputProfile
            {
                PlayerActive = true,
                ControlLeft = leftHeld,
                ControlRight = rightHeld,
                HeldDirection = leftHeld == rightHeld ? 0 : rightHeld ? 1 : -1
            };
        }

        private static void ExpectDashGate(string expected, DashInputProfile profile, long tick, string label)
        {
            ExpectDashGate(expected, profile, MovementContinuousDashModes.DoubleTapAndHold, tick, label, profile == null ? 0 : profile.HeldDirection);
        }

        private static void ExpectDashGate(string expected, DashInputProfile profile, long tick, string label, int expectedDirection)
        {
            ExpectDashGate(expected, profile, MovementContinuousDashModes.DoubleTapAndHold, tick, label, expectedDirection);
        }

        private static void ExpectDashGate(string expected, DashInputProfile profile, string mode, long tick, string label, int expectedDirection)
        {
            int actualDirection;
            var actual = MovementContinuousDashService.EvaluateDirectionAndModeForTesting(
                profile,
                mode,
                tick,
                out actualDirection);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected continuous dash gate '" + expected + "' for " + label + ", got '" + actual + "'.");
            }

            if (actualDirection != expectedDirection)
            {
                throw new InvalidOperationException(
                    "Expected continuous dash effective direction " + expectedDirection + " for " + label + ", got " + actualDirection + ".");
            }
        }

        private static void AssertContinuousDashArmed(int expected, string label)
        {
            var actual = MovementContinuousDashService.ArmedDirectionForTesting;
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    "Expected continuous dash armed direction " + expected + " for " + label + ", got " + actual + ".");
            }
        }
    }
}
