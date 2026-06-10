using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Ui;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static FakeProjectile BuildFakeFlailProjectile(
            int whoAmI,
            int type,
            int identity,
            int owner,
            bool active,
            bool friendly,
            bool hostile)
        {
            return new FakeProjectile
            {
                whoAmI = whoAmI,
                type = type,
                identity = identity,
                owner = owner,
                active = active,
                friendly = friendly,
                hostile = hostile,
                aiStyle = 15,
                ai = new float[] { 1f },
                position = new Vector2 { X = 0f, Y = 0f },
                velocity = new Vector2 { X = 4f, Y = 0f },
                width = 16,
                height = 18,
                localNPCImmunity = new int[256]
            };
        }

        private static CombatAimFlailControlService.FlailProjectileSnapshot BuildFlailSnapshot(
            int whoAmI,
            int type,
            int identity,
            float ai0,
            float velocityX,
            float velocityY,
            object localNpcImmunity)
        {
            return new CombatAimFlailControlService.FlailProjectileSnapshot
            {
                WhoAmI = whoAmI,
                Type = type,
                AiStyle = 15,
                Owner = 7,
                Identity = identity,
                Active = true,
                Friendly = true,
                Hostile = false,
                Width = 16,
                Height = 18,
                Ai0 = ai0,
                VelocityX = velocityX,
                VelocityY = velocityY,
                Position = new Vector2 { X = 0f, Y = 0f },
                Velocity = new Vector2 { X = velocityX, Y = velocityY },
                LocalNpcImmunity = localNpcImmunity as System.Collections.IList
            };
        }

        private static void FlailCachedReleaseAimsAfterTargetSelectionLoss()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.screenPosition.X = 0f;
            Terraria.Main.screenPosition.Y = 0f;
            Terraria.Main.GameUpdateCount = 400;
            ResetFakeMainMouse(true, false);

            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var recorded = BuildFlailItemCheckDecision(player);
            CombatAimFlailControlService.SetCachedReleaseAimForTesting(recorded, 399);

            CombatAimItemCheckDecision ignored;
            CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out ignored);

            Terraria.Main.GameUpdateCount = 401;
            ResetFakeMainMouse(false, true);
            player.controlUseItem = false;
            player.releaseUseItem = true;
            player.channel = false;

            CombatAimItemCheckDecision cached;
            if (!CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out cached))
            {
                throw new InvalidOperationException("Expected flail cached release decision after current target selection was lost.");
            }

            if (cached == null ||
                cached.Target == null ||
                cached.Target.WhoAmI != 42 ||
                Math.Abs(cached.AimWorldX - recorded.AimWorldX) > 0.001f ||
                Math.Abs(cached.AimWorldY - recorded.AimWorldY) > 0.001f ||
                !cached.ReleaseDetected ||
                !cached.ReleasedThisTick ||
                !cached.WasUseItemHeldLastTick ||
                !string.Equals(cached.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal) ||
                cached.Selection == null ||
                !cached.Selection.SelectionCacheHit ||
                !string.Equals(cached.Selection.SelectionCacheKey, "flailCachedReleaseAim", StringComparison.Ordinal) ||
                !string.Equals(cached.Selection.SelectionPurpose, "FlailRelease", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cached flail release decision did not preserve recorded target, release edge, and cache metadata.");
            }

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatAimFlailControlService.TryBeginItemCheckTakeover(player, cached, out takeover))
            {
                throw new InvalidOperationException("Expected cached flail release to enter ItemCheck takeover scope.");
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(cached);
            if (diagnostics == null ||
                !diagnostics.CachedReleaseAim ||
                !string.Equals(diagnostics.InputPhase, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.TakeoverScope, "ItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected cached flail release diagnostics to mark ItemCheck release scope.");
            }

            TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
        }

        private static void FlailCachedReleaseAimRespectsAgeAndProfileBounds()
        {
            AssertCachedReleaseAge(0, true, "0 tick");
            AssertCachedReleaseAge(120, true, "120 tick");
            AssertCachedReleaseAge(121, false, "121 tick");
            AssertCachedReleaseFutureTickRejected();
            AssertCachedReleaseProfileChangeRejected(CreateFlailLikeItem(5527, 1058, "Different Flail Type"), "item type");
            AssertCachedReleaseProfileChangeRejected(CreateFlailLikeItem(5526, 1059, "Different Flail Shoot"), "shoot");
        }

        private static void AssertCachedReleaseAge(int age, bool expected, string label)
        {
            CombatAimItemCheckDecision cached;
            var releaseTick = 2000L;
            var actual = TryCreateCachedReleaseDecisionForTesting(
                releaseTick - 1,
                releaseTick,
                releaseTick - age,
                null,
                out cached);
            if (actual != expected)
            {
                var failureDiagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(null);
                throw new InvalidOperationException(
                    "Expected cached flail release age " + label + " availability to be " + expected +
                    ". LastInputError=" + TerrariaInputCompat.LastInputCompatError +
                    ", lastState=" + (failureDiagnostics == null ? "<null>" : failureDiagnostics.State) +
                    ", lastCachedReason=" + (failureDiagnostics == null ? "<null>" : failureDiagnostics.CachedReleaseAimReason) + ".");
            }

            if (!actual)
            {
                return;
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(cached);
            if (diagnostics == null ||
                !diagnostics.CachedReleaseAim ||
                diagnostics.CachedReleaseAimAgeTicks != age ||
                !string.Equals(diagnostics.CachedReleaseAimReason, "usedForPhysicalRelease", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected cached flail release age diagnostics to preserve age " + label +
                    ", got cached=" + (diagnostics != null && diagnostics.CachedReleaseAim) +
                    ", age=" + (diagnostics == null ? -1 : diagnostics.CachedReleaseAimAgeTicks) +
                    ", reason=" + (diagnostics == null ? "<null>" : diagnostics.CachedReleaseAimReason) + ".");
            }
        }

        private static void AssertCachedReleaseFutureTickRejected()
        {
            CombatAimItemCheckDecision cached;
            if (TryCreateCachedReleaseDecisionForTesting(2099, 2100, 2101, null, out cached))
            {
                throw new InvalidOperationException("Expected cached flail release to reject future cache tick.");
            }
        }

        private static void AssertCachedReleaseProfileChangeRejected(FakeItem replacement, string label)
        {
            CombatAimItemCheckDecision cached;
            if (TryCreateCachedReleaseDecisionForTesting(2199, 2200, 2199, replacement, out cached))
            {
                throw new InvalidOperationException("Expected cached flail release to reject " + label + " change.");
            }
        }

        private static bool TryCreateCachedReleaseDecisionForTesting(
            long heldTick,
            long releaseTick,
            long recordedTick,
            FakeItem replacementItem,
            out CombatAimItemCheckDecision cached)
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.screenPosition.X = 0f;
            Terraria.Main.screenPosition.Y = 0f;
            Terraria.Main.GameUpdateCount = heldTick;
            ResetFakeCombatUiUnblocked();
            ResetFakeMainMouse(true, false);

            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var recorded = BuildFlailItemCheckDecision(player);
            CombatAimFlailControlService.SetCachedReleaseAimForTesting(recorded, heldTick);

            CombatAimItemCheckDecision ignored;
            CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out ignored);
            CombatAimFlailControlService.SetCachedReleaseAimForTesting(recorded, recordedTick);

            if (replacementItem != null)
            {
                player.inventory[0] = replacementItem;
            }

            Terraria.Main.GameUpdateCount = releaseTick;
            ResetFakeCombatUiUnblocked();
            ResetFakeMainMouse(false, true);
            player.controlUseItem = false;
            player.releaseUseItem = true;
            player.channel = false;
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                return CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out cached);
            }
            finally
            {
                restoreRuntimeTypes();
            }
        }

        private static void FlailReleaseCursorTailKeepsProjectileAiScopedAim()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.GameUpdateCount = 600;
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
            decision.UseItemHeld = false;
            decision.UseItemReleased = true;
            decision.WasUseItemHeldLastTick = true;
            decision.ReleasedThisTick = true;
            decision.ReleaseDetected = true;
            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.ReleaseToTarget,
                AttackRelease = true,
                AttackSuppressed = true,
                InputMode = "controlledUseItemRelease",
                InputPhase = FlailControlStates.ReleaseToTarget,
                TakeoverScope = "ItemCheck",
                ProjectileWhoAmI = 31,
                ProjectileType = 1058,
                ProjectileAiStyle = 15,
                ProjectileIdentity = 31,
                StuckRecovery = "none",
                PhysicalReleasePending = true,
                CachedReleaseAim = true,
                CachedReleaseAimAgeTicks = 1,
                CachedReleaseAimReason = "usedForPhysicalRelease"
            });

            if (!CombatAimPersistentCursorService.RememberFlailReleaseTail(decision))
            {
                throw new InvalidOperationException("Expected flail release cursor tail to be remembered.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetFlailReleaseTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetFlailReleaseTailDecision reflection failed.");
            }

            var args = new object[] { 601L, null, null };
            var ok = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!ok || tail == null)
            {
                throw new InvalidOperationException("Expected flail release tail decision during Projectile.AI release window.");
            }

            if (tail.UseItemHeld ||
                !tail.UseItemReleased ||
                !tail.ReleasedThisTick ||
                !tail.ReleaseDetected ||
                !string.Equals(tail.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.Ordinal) ||
                !string.Equals(tail.PersistentCursorReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - decision.AimWorldX) > 0.001f ||
                Math.Abs(tail.AimWorldY - decision.AimWorldY) > 0.001f ||
                tail.WeaponProfile == null ||
                tail.WeaponProfile.ItemType != 5526)
            {
                throw new InvalidOperationException("Flail release tail did not preserve release scoped aim metadata.");
            }

            var match = CombatAimProjectileCursorCompat.MatchFlailProjectile(
                new FakeProjectile { whoAmI = 31, type = 1058, aiStyle = 15, owner = 7, active = true, friendly = true },
                player,
                tail.WeaponProfile,
                tail.BallisticSolution);
            player.controlUseItem = true;
            player.releaseUseItem = false;
            player.channel = true;
            Terraria.Main.mouseLeft = true;
            Terraria.Main.mouseLeftRelease = false;
            CombatAimFlailControlService.MarkProjectileAiScopedTakeover(tail, match);
            if (!player.controlUseItem || player.releaseUseItem || !player.channel ||
                !Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("ProjectileAI scoped diagnostics must not mutate use-item input state.");
            }

            CombatAimProjectileCursorCompat.AttachDecisionMetadata(tail, match, true, true, true);
            var json = BuildCombatAimDecisionJson(tail, true, true);
            AssertContains(json, "\"weaponFamily\":\"FlailAiStyle15\"");
            AssertContains(json, "\"persistentCursorClass\":\"flailAiStyle15\"");
            AssertContains(json, "\"persistentCursorScopedOverride\":true");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:flailAiStyle15Release\"");
            AssertContains(json, "\"flailTakeoverScope\":\"ProjectileAI\"");
            AssertContains(json, "\"flailInputPhase\":\"ReleaseToTarget\"");
        }

        private static void FlailCachedReleaseRejectsYoyoAndNormalChannel()
        {
            CombatAimFlailControlService.ResetForTesting();
            AssertCachedReleaseRejected(new FakeItem
            {
                type = 3281,
                stack = 1,
                Name = "Yoyo",
                damage = 60,
                shoot = 99,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            }, 99, "yoyo");

            CombatAimFlailControlService.ResetForTesting();
            AssertCachedReleaseRejected(new FakeItem
            {
                type = 4956,
                stack = 1,
                Name = "Channel Beam",
                damage = 120,
                shoot = 633,
                shootSpeed = 1f,
                magic = true,
                channel = true,
                useStyle = 5
            }, 75, "normal channel");
        }

        private static void AssertCachedReleaseRejected(FakeItem item, int projectileAiStyle, string label)
        {
            Terraria.Main.screenPosition.X = 0f;
            Terraria.Main.screenPosition.Y = 0f;
            Terraria.Main.GameUpdateCount = 500;
            ResetFakeMainMouse(true, false);

            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var cached = BuildCachedReleaseDecisionForItem(player, item, projectileAiStyle);
            CombatAimFlailControlService.SetCachedReleaseAimForTesting(cached, 499);

            CombatAimItemCheckDecision ignored;
            CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out ignored);

            Terraria.Main.GameUpdateCount = 501;
            ResetFakeMainMouse(false, true);
            player.controlUseItem = false;
            player.releaseUseItem = true;
            player.channel = false;

            CombatAimItemCheckDecision releaseDecision;
            if (CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out releaseDecision))
            {
                throw new InvalidOperationException("Expected cached flail release to reject " + label + ".");
            }
        }

        private static FakeItem CreateFlailLikeItem(int itemType, int shoot, string name)
        {
            return new FakeItem
            {
                type = itemType,
                stack = 1,
                Name = name,
                damage = 66,
                shoot = shoot,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            };
        }

        private static CombatAimItemCheckDecision BuildFlailItemCheckDecision(FakePlayer player)
        {
            var item = CreateFlailLikeItem(5526, 1058, "Flairon");
            player.inventory[0] = item;
            player.selectedItem = 0;

            var profile = CombatAimWeaponProfile.Read(player, item);
            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 30,
                AimRangeOrigin = CombatAimModes.RangeOriginCursor,
                AimTargetPriority = CombatAimModes.TargetPriorityNearest,
                ActiveRangeMode = CombatAimRangeResolver.RangeModeCursorSlider,
                CursorAimRadius = 30,
                PlayerAimRadius = 20,
                TrackDummy = true,
                MarkerEnabled = true,
                RangeCenterWorldX = 800f,
                RangeCenterWorldY = 600f,
                UseItemHeld = true,
                SelectedSlot = 0,
                ItemType = profile.ItemType,
                ItemName = profile.Name,
                ItemStack = profile.Stack,
                Damage = profile.Damage,
                Shoot = profile.Shoot,
                UseAmmo = profile.UseAmmo,
                Melee = profile.Melee,
                CreateTile = profile.CreateTile,
                CreateWall = profile.CreateWall,
                Pick = profile.Pick,
                Axe = profile.Axe,
                Hammer = profile.Hammer,
                FishingPole = profile.FishingPole,
                WeaponProfile = profile,
                AimWorldX = 920f,
                AimWorldY = 640f,
                AimScreenX = 920,
                AimScreenY = 640,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    ProjectileType = 1058,
                    ProjectileAiStyle = 15,
                    AimWorldX = 920f,
                    AimWorldY = 640f,
                    Mode = "centerConservative"
                },
                Selection = new CombatAimTargetSelection
                {
                    Enabled = true,
                    RadiusTiles = 30,
                    TrackDummy = true,
                    MarkerEnabled = true,
                    AimRangeOrigin = CombatAimModes.RangeOriginCursor,
                    AimTargetPriority = CombatAimModes.TargetPriorityNearest,
                    ActiveRangeMode = CombatAimRangeResolver.RangeModeCursorSlider,
                    CursorAimRadius = 30,
                    PlayerAimRadius = 20,
                    RangeCenterWorldX = 800f,
                    RangeCenterWorldY = 600f,
                    ResultCode = "TargetSelected",
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 42,
                        Type = 245,
                        Name = "Target",
                        Active = true,
                        Chaseable = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = 920f,
                        CenterY = 640f
                    },
                    SelectedSampleWorldX = 920f,
                    SelectedSampleWorldY = 640f,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    TargetScore = 10f,
                    AttackTargetWhoAmI = 42,
                    AttackTargetType = 245
                }
            };
        }

        private static CombatAimItemCheckDecision BuildCachedReleaseDecisionForItem(FakePlayer player, FakeItem item, int projectileAiStyle)
        {
            player.inventory[0] = item;
            player.selectedItem = 0;
            var profile = CombatAimWeaponProfile.Read(player, item);
            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 30,
                AimRangeOrigin = CombatAimModes.RangeOriginCursor,
                AimTargetPriority = CombatAimModes.TargetPriorityNearest,
                ActiveRangeMode = CombatAimRangeResolver.RangeModeCursorSlider,
                CursorAimRadius = 30,
                PlayerAimRadius = 20,
                TrackDummy = true,
                MarkerEnabled = true,
                RangeCenterWorldX = 800f,
                RangeCenterWorldY = 600f,
                SelectedSlot = 0,
                ItemType = profile.ItemType,
                ItemName = profile.Name,
                ItemStack = profile.Stack,
                Damage = profile.Damage,
                Shoot = profile.Shoot,
                UseAmmo = profile.UseAmmo,
                Melee = profile.Melee,
                CreateTile = profile.CreateTile,
                CreateWall = profile.CreateWall,
                Pick = profile.Pick,
                Axe = profile.Axe,
                Hammer = profile.Hammer,
                FishingPole = profile.FishingPole,
                WeaponProfile = profile,
                AimWorldX = 920f,
                AimWorldY = 640f,
                AimScreenX = 920,
                AimScreenY = 640,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    ProjectileType = profile.Shoot,
                    ProjectileAiStyle = projectileAiStyle,
                    AimWorldX = 920f,
                    AimWorldY = 640f,
                    Mode = "test"
                },
                Selection = new CombatAimTargetSelection
                {
                    Enabled = true,
                    RadiusTiles = 30,
                    TrackDummy = true,
                    MarkerEnabled = true,
                    AimRangeOrigin = CombatAimModes.RangeOriginCursor,
                    AimTargetPriority = CombatAimModes.TargetPriorityNearest,
                    ActiveRangeMode = CombatAimRangeResolver.RangeModeCursorSlider,
                    CursorAimRadius = 30,
                    PlayerAimRadius = 20,
                    RangeCenterWorldX = 800f,
                    RangeCenterWorldY = 600f,
                    ResultCode = "TargetSelected",
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 42,
                        Type = 245,
                        Name = "Target",
                        Active = true,
                        Chaseable = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = 920f,
                        CenterY = 640f
                    },
                    SelectedSampleWorldX = 920f,
                    SelectedSampleWorldY = 640f,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    AttackTargetWhoAmI = 42,
                    AttackTargetType = 245
                }
            };
        }

        private static void ResetFakeMainMouse(bool left, bool leftRelease)
        {
            TerrariaInputCompat.SetPhysicalMouseButtonOverridesForTesting(left, false);
            Terraria.Main.mouseLeft = left;
            Terraria.Main.mouseLeftRelease = leftRelease;
            Terraria.Main.mouseRight = false;
            Terraria.Main.mouseRightRelease = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = left;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = false;
        }

        private static void ResetFakeCombatUiUnblocked()
        {
            Terraria.Main.mouseInterface = false;
            Terraria.Main.blockMouse = false;
            Terraria.Main.gameMenu = false;
            Terraria.Main.chatMode = false;
            Terraria.Main.drawingPlayerChat = false;
            Terraria.Main.npcChatText = string.Empty;
            Terraria.Main.playerInventory = false;
        }

        private static Action PushFlailUpdateTestState(int cursorAimRadius, Terraria.Player player)
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreLocalPlayer = CaptureFakeLocalPlayerState();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var previousCursorAimRadius = settings.CursorAimRadius;
            var previousMouseLeft = Terraria.Main.mouseLeft;
            var previousMouseLeftRelease = Terraria.Main.mouseLeftRelease;
            var previousMouseRight = Terraria.Main.mouseRight;
            var previousMouseRightRelease = Terraria.Main.mouseRightRelease;
            var previousMouseInterface = Terraria.Main.mouseInterface;
            var previousBlockMouse = Terraria.Main.blockMouse;
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousChatMode = Terraria.Main.chatMode;
            var previousDrawingPlayerChat = Terraria.Main.drawingPlayerChat;
            var previousNpcChatText = Terraria.Main.npcChatText;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            var previousProjectiles = Terraria.Main.projectile;
            var previousGameUpdateCount = Terraria.Main.GameUpdateCount;
            var previousCurrentMouseLeft = Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft;
            var previousCurrentMouseRight = Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight;
            var previousJustPressedMouseLeft = Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft;
            var previousJustPressedMouseRight = Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight;

            settings.CursorAimRadius = cursorAimRadius;
            Terraria.Main.GameUpdateCount = 9000;
            Terraria.Main.mouseInterface = false;
            Terraria.Main.blockMouse = false;
            Terraria.Main.gameMenu = false;
            Terraria.Main.chatMode = false;
            Terraria.Main.drawingPlayerChat = false;
            Terraria.Main.npcChatText = string.Empty;
            Terraria.Main.playerInventory = false;
            Terraria.Main.projectile = new object[0];
            ResetFakeMainMouse(false, true);
            ResetFakeLocalPlayer(player);

            return () =>
            {
                settings.CursorAimRadius = previousCursorAimRadius;
                Terraria.Main.mouseLeft = previousMouseLeft;
                Terraria.Main.mouseLeftRelease = previousMouseLeftRelease;
                Terraria.Main.mouseRight = previousMouseRight;
                Terraria.Main.mouseRightRelease = previousMouseRightRelease;
                Terraria.Main.mouseInterface = previousMouseInterface;
                Terraria.Main.blockMouse = previousBlockMouse;
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.chatMode = previousChatMode;
                Terraria.Main.drawingPlayerChat = previousDrawingPlayerChat;
                Terraria.Main.npcChatText = previousNpcChatText;
                Terraria.Main.playerInventory = previousPlayerInventory;
                Terraria.Main.projectile = previousProjectiles;
                Terraria.Main.GameUpdateCount = previousGameUpdateCount;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = previousCurrentMouseLeft;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = previousCurrentMouseRight;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = previousJustPressedMouseLeft;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = previousJustPressedMouseRight;
                restoreLocalPlayer();
                restoreRuntimeTypes();
            };
        }

        private static void ResetFakeLocalPlayer(Terraria.Player player)
        {
            Terraria.Main.LocalPlayer = player;
            Terraria.Main.myPlayer = player == null ? -1 : player.whoAmI;
            if (player != null && player.whoAmI >= 0 && player.whoAmI < Terraria.Main.player.Length)
            {
                Terraria.Main.player[player.whoAmI] = player;
            }
        }

        private static void AssertFlailLastDiagnostics(string state, string blockedReason)
        {
            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(null);
            if (diagnostics == null ||
                !string.Equals(diagnostics.State, state, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected flail diagnostics state=" + state +
                    " blockedReason=" + blockedReason +
                    ", got state=" + (diagnostics == null ? "<null>" : diagnostics.State) +
                    " blockedReason=" + (diagnostics == null ? "<null>" : diagnostics.BlockedReason) + ".");
            }
        }

        private static Action CaptureFakeLocalPlayerState()
        {
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            var previousPlayers = Terraria.Main.player;
            Terraria.Main.player = new object[256];
            return () =>
            {
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                Terraria.Main.player = previousPlayers;
            };
        }

        private static Terraria.Player CreateItemUseBridgePlayer(int itemType, string itemName)
        {
            var player = new Terraria.Player
            {
                whoAmI = 0,
                selectedItem = 0,
                active = true,
                releaseUseItem = true
            };
            player.inventory[0] = new FakeItem
            {
                type = itemType,
                stack = 1,
                Name = itemName ?? string.Empty,
                useStyle = 4,
                useAnimation = 30,
                useTime = 30
            };
            return player;
        }

        private static void AssertFlailDecision(CombatAimFlailControlDecision decision, string state, bool pulse, bool suppress, bool release, string reason)
        {
            if (decision == null ||
                !string.Equals(decision.State, state, StringComparison.Ordinal) ||
                decision.AttackPulse != pulse ||
                decision.AttackSuppressed != suppress ||
                decision.AttackRelease != release ||
                !string.Equals(decision.BlockedReason, reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unexpected flail decision: state=" + (decision == null ? "<null>" : decision.State) +
                    ", pulse=" + (decision != null && decision.AttackPulse) +
                    ", suppress=" + (decision != null && decision.AttackSuppressed) +
                    ", release=" + (decision != null && decision.AttackRelease) +
                    ", reason=" + (decision == null ? "<null>" : decision.BlockedReason));
            }
        }


    }
}
