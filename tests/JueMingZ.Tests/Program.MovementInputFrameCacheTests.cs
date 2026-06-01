using System;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void MovementInputFrameCacheReusesProfilesWithinFrame()
        {
            MovementInputFrameCache.ResetForTesting();
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlJump = true,
                controlLeft = true,
                dashDelay = 0,
                dashType = 2,
                position = new FakeVector2 { X = 120f, Y = 320f },
                velocity = new FakeVector2 { X = -2f, Y = 5f },
                width = 22,
                height = 44
            };
            var frame = MovementInputFrameCache.CreateForTesting(100, 7, player, player.whoAmI);

            JumpInputProfile jumpA;
            JumpInputProfile jumpB;
            string failure;
            if (!frame.TryGetJumpProfile(out jumpA, out failure) ||
                !frame.TryGetJumpProfile(out jumpB, out failure) ||
                !object.ReferenceEquals(jumpA, jumpB))
            {
                throw new InvalidOperationException("Expected one cached jump profile instance within a movement input frame.");
            }

            DashInputProfile dashA;
            DashInputProfile dashB;
            if (!frame.TryGetDashProfile(out dashA, out failure) ||
                !frame.TryGetDashProfile(out dashB, out failure) ||
                !object.ReferenceEquals(dashA, dashB))
            {
                throw new InvalidOperationException("Expected one cached dash profile instance within a movement input frame.");
            }

            var motionA = frame.GetBasicMotion(out failure);
            var motionB = frame.GetBasicMotion(out failure);
            if (motionA == null || motionB == null || !object.ReferenceEquals(motionA, motionB))
            {
                throw new InvalidOperationException("Expected one cached basic motion snapshot within a movement input frame.");
            }

            if (MovementInputFrameCache.JumpProfileReadCountForTesting != 1 ||
                MovementInputFrameCache.DashProfileReadCountForTesting != 1 ||
                MovementInputFrameCache.BasicMotionReadCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected movement frame cache to read each compat profile once.");
            }

            if (!frame.Matches(100, 7, player, player.whoAmI, true))
            {
                throw new InvalidOperationException("Expected movement frame cache key to match the same tick, settings, and player.");
            }

            var replacementPlayer = new FakePlayer { whoAmI = player.whoAmI };
            if (frame.Matches(100, 7, replacementPlayer, replacementPlayer.whoAmI, true) ||
                frame.Matches(100, 7, player, player.whoAmI + 1, true) ||
                frame.Matches(101, 7, player, player.whoAmI, true))
            {
                throw new InvalidOperationException("Expected movement frame cache key to reject changed player identity or tick.");
            }

            AssertNear(motionA.PositionX, 120f, "cached motion position X");
            AssertNear(motionA.VelocityY, 5f, "cached motion velocity Y");
            if (motionA.Width != 22 || motionA.Height != 44 || motionA.WhoAmI != 7)
            {
                throw new InvalidOperationException("Expected cached motion dimensions and player identity to be preserved.");
            }
        }

        private static void SafeLandingCheapPrecheckUsesCachedMotionWithinFrame()
        {
            MovementInputFrameCache.ResetForTesting();
            var player = new FakePlayer
            {
                whoAmI = 8,
                gravDir = 1f,
                velocity = new FakeVector2 { X = 0f, Y = 2f }
            };
            var frame = MovementInputFrameCache.CreateForTesting(101, 4, player, player.whoAmI);

            MovementSafeLandingAnalysis analysis;
            bool shouldRunFullAnalysis;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, frame, out analysis, out shouldRunFullAnalysis) ||
                shouldRunFullAnalysis ||
                Math.Abs(analysis.FallingSpeed - 2f) > 0.001f)
            {
                throw new InvalidOperationException("Expected cached slow falling motion to skip full SafeLanding analysis.");
            }

            player.velocity.Y = 9f;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, frame, out analysis, out shouldRunFullAnalysis) ||
                shouldRunFullAnalysis ||
                Math.Abs(analysis.FallingSpeed - 2f) > 0.001f)
            {
                throw new InvalidOperationException("Expected SafeLanding cheap precheck to reuse same-frame cached motion.");
            }

            if (MovementInputFrameCache.BasicMotionReadCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected SafeLanding cheap precheck to read basic motion once per frame.");
            }
        }
    }
}
