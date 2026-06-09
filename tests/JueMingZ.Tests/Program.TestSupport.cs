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
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void DependencyResolverLoadsCompressedEmbeddedHarmony()
        {
            Assembly assembly;
            if (!JueMingZ.Bootstrap.DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out assembly) ||
                assembly == null ||
                !string.Equals(assembly.GetName().Name, "0Harmony", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("0Harmony was not loaded by the embedded dependency resolver.");
            }
        }

        private static void AssertSelected(MovementSafeLandingStrategySelection selection, int priority, string strategy, string actionType)
        {
            if (selection == null || selection.SelectedPlan == null)
            {
                throw new InvalidOperationException("Expected selected plan " + strategy + ".");
            }

            if (selection.SelectedPlan.Priority != priority ||
                !string.Equals(selection.SelectedPlan.StrategyId, strategy, StringComparison.Ordinal) ||
                !string.Equals(selection.SelectedPlan.ActionType, actionType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected selected " + priority + ":" + strategy + ":" + actionType +
                    ", got " + selection.SelectedPlan.ToSummary());
            }
        }

        private static void AssertPriorityOnePlan(MovementSafeLandingAnalysis analysis, string strategy, string actionType)
        {
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 1, strategy, actionType);
            if (selection.SelectedPlan.RequestKind != InputActionKind.Jump)
            {
                throw new InvalidOperationException("Expected priority 1 plan to use Jump request kind.");
            }

            var request = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            if (request.Kind != InputActionKind.Jump)
            {
                throw new InvalidOperationException("Expected priority 1 request kind Jump.");
            }

            AssertMetadata(request, "SafeLandingStrategy", strategy);
            AssertMetadata(request, "SafeLandingActionType", actionType);
        }

        private static void AssertTemporaryApplyRequest(MovementSafeLandingEquipmentPlan plan, string expectedCategory)
        {
            var analysis = AnalysisNearGround();
            var context = MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis);
            context.TemporaryEquipmentPlan = plan;
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(context);
            AssertSelected(selection, plan.SelectedPriority, plan.StrategyId, plan.ActionType);
            var request = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentApplyRequest(plan, analysis);
            if (request.Kind != InputActionKind.InventorySlot)
            {
                throw new InvalidOperationException("Expected temporary equipment request kind InventorySlot.");
            }

            AssertMetadata(request, "SafeLandingRescueMode", "TemporaryEquipmentApply");
            AssertMetadata(request, "SafeLandingEquipmentCategory", expectedCategory);
        }

        private static MovementSafeLandingEquipmentPlan PlanForCategory(string strategy, string category, string actionType, int priority)
        {
            var source = new FakeItem { type = ResolveTestItemType(category), stack = 1, prefix = 0, Name = category };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            var plan = BuildPlan(
                strategy,
                category,
                actionType,
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                string.Equals(category, "fairy_boots", StringComparison.Ordinal) ? 2 : 3,
                source,
                target,
                !string.Equals(actionType, "equip_only", StringComparison.Ordinal));
            plan.SelectedPriority = priority;
            return plan;
        }

        private static int ResolveTestItemType(string category)
        {
            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return 5000;
            }

            if (string.Equals(category, "wings", StringComparison.Ordinal))
            {
                return 493;
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return 3770;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return 857;
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return 1131;
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return 946;
            }

            return 158;
        }

        private static void AssertMetadata(InputActionRequest request, string key, string expected)
        {
            if (request == null || request.Metadata == null)
            {
                throw new InvalidOperationException("Expected request metadata.");
            }

            string value;
            if (!request.Metadata.TryGetValue(key, out value) ||
                !string.Equals(value, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected metadata " + key + "=" + expected + ", got " + (value ?? "<missing>"));
            }
        }

        private static void AssertPreservedSafeLandingMetadata(InputActionRequest request)
        {
            string missing;
            if (!MovementSafeLandingRequestBuilder.PreservesRequiredMetadataKeys(request, out missing))
            {
                throw new InvalidOperationException("Missing preserved SafeLanding metadata key " + missing + ".");
            }
        }

        private static MovementSafeLandingEquipmentActionResult InvokeRestoreRecords(FakePlayer player, IEnumerable<MovementSafeLandingEquipmentMoveRecord> records)
        {
            var compatType = typeof(MovementSafeLandingEquipmentCompat);
            var restoreRequestType = compatType.GetNestedType("RestoreRequest", BindingFlags.NonPublic);
            if (restoreRequestType == null)
            {
                throw new InvalidOperationException("RestoreRequest reflection hook missing.");
            }

            var request = Activator.CreateInstance(restoreRequestType, true);
            restoreRequestType.GetField("Records", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(request, new List<MovementSafeLandingEquipmentMoveRecord>(records));
            restoreRequestType.GetField("Reason", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(request, "test");

            var method = compatType.GetMethod("RestoreRecords", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("RestoreRecords reflection hook missing.");
            }

            return method.Invoke(null, new[] { (object)player, request }) as MovementSafeLandingEquipmentActionResult;
        }

        private static FishingAutoEquipmentActionResult InvokeFishingAutoEquipmentRestoreRecords(FakePlayer player, IEnumerable<FishingAutoEquipmentMoveRecord> records)
        {
            var compatType = typeof(FishingAutoEquipmentCompat);
            var restoreRequestType = compatType.GetNestedType("RestoreRequest", BindingFlags.NonPublic);
            if (restoreRequestType == null)
            {
                throw new InvalidOperationException("Fishing RestoreRequest reflection hook missing.");
            }

            var request = Activator.CreateInstance(restoreRequestType, true);
            restoreRequestType.GetField("Session", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(request, new FishingAutoEquipmentSessionInfo());
            restoreRequestType.GetField("Records", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(request, new List<FishingAutoEquipmentMoveRecord>(records));
            restoreRequestType.GetField("Reason", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(request, "test");

            var method = compatType.GetMethod("RestoreRecords", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("Fishing RestoreRecords reflection hook missing.");
            }

            return method.Invoke(null, new[] { (object)player, request }) as FishingAutoEquipmentActionResult;
        }

        private static FishingAutoEquipmentMoveRecord FishingAutoEquipmentRecord(
            int targetSlot,
            FishingEquipmentContainerKind originalKind,
            int originalSlot,
            FakeItem fishingItem,
            FakeItem originalItem)
        {
            return new FishingAutoEquipmentMoveRecord
            {
                MoveId = 1,
                TargetEquipmentSlot = targetSlot,
                SourceContainerKind = originalKind,
                SourceSlot = originalSlot,
                FishingItemSignature = FishingAutoEquipmentCompat.CreateSignature(fishingItem),
                OriginalTargetWasAir = originalItem == null || originalItem.IsAir,
                OriginalTargetItemSignature = FishingAutoEquipmentCompat.CreateSignature(originalItem),
                OriginalTargetHoldingContainerKind = originalKind,
                OriginalTargetHoldingSlot = originalSlot,
                ApplyStatus = "applied",
                RestoreStatus = "pending"
            };
        }

        private static MovementSafeLandingEquipmentMoveRecord Record(string strategy, string category, string actionType)
        {
            return new MovementSafeLandingEquipmentMoveRecord
            {
                StrategyId = strategy,
                EquipmentCategory = category,
                ActionType = actionType
            };
        }

        private static MovementSafeLandingEquipmentPlan BuildPlan(
            string strategy,
            string category,
            string actionType,
            MovementSafeLandingEquipmentContainerKind sourceKind,
            int sourceSlot,
            MovementSafeLandingEquipmentContainerKind targetKind,
            int targetSlot,
            object sourceItem,
            object targetItem,
            bool applyTriggersInput)
        {
            return new MovementSafeLandingEquipmentPlan
            {
                StrategyId = strategy,
                EquipmentCategory = category,
                ActionType = actionType,
                SelectedPriority = 2,
                SourceContainerKind = sourceKind,
                SourceSlot = sourceSlot,
                TargetContainerKind = targetKind,
                TargetSlot = targetSlot,
                CandidateItemType = MovementSafeLandingEquipmentCompat.CreateSignature(sourceItem).Type,
                CandidateMountType = -1,
                CandidateSignature = MovementSafeLandingEquipmentCompat.CreateSignature(sourceItem),
                TargetSignatureAtPlan = MovementSafeLandingEquipmentCompat.CreateSignature(targetItem),
                ApplyTriggersInput = applyTriggersInput
            };
        }

        private static MovementSafeLandingEquipmentActionResult InvokeApplyPlan(FakePlayer player, MovementSafeLandingEquipmentPlan plan)
        {
            var method = typeof(MovementSafeLandingEquipmentCompat).GetMethod(
                "ApplyPlan",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ApplyPlan reflection hook missing.");
            }

            return method.Invoke(null, new object[] { player, plan }) as MovementSafeLandingEquipmentActionResult;
        }

        private static void InvokeMigrateAppSettings(AppSettings settings)
        {
            var method = typeof(ConfigService).GetMethod(
                "MigrateAppSettings",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("MigrateAppSettings reflection hook missing.");
            }

            method.Invoke(null, new object[] { settings });
        }

        private static string InvokeDiagnosticSnapshotJson(DiagnosticSnapshot snapshot)
        {
            var method = typeof(DiagnosticSnapshotWriter).GetMethod(
                "ToJson",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("DiagnosticSnapshotWriter.ToJson reflection hook missing.");
            }

            return (string)method.Invoke(null, new object[] { snapshot });
        }

        private static bool InvokeIsPlaceableFallDamageSafeTile(int createTile, int placeStyle)
        {
            var method = typeof(MovementSafeLandingCompat).GetMethod(
                "IsPlaceableFallDamageSafeTile",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("IsPlaceableFallDamageSafeTile reflection hook missing.");
            }

            return (bool)method.Invoke(null, new object[] { createTile, placeStyle });
        }

        private static void AssertContains(string text, string expected)
        {
            if ((text ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected text to contain " + expected + ", got " + text);
            }
        }

        private static void AssertDoesNotContain(string text, string unexpected)
        {
            if ((text ?? string.Empty).IndexOf(unexpected, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("Expected text not to contain " + unexpected + ", got " + text);
            }
        }

        private static void AssertNear(double actual, double expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.0001d)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AssertStringEquals(string actual, string expected, string label)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected + ", got " + actual);
            }
        }

        private static void AssertLongEquals(long actual, long expected, string label)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AssertHas(InputActionChannel actual, InputActionChannel expected, string label)
        {
            if ((actual & expected) == 0)
            {
                throw new InvalidOperationException("Expected " + label + " to include " + expected + ", got " + InputActionChannelFormatter.Format(actual));
            }
        }

        private static MovementSafeLandingAnalysis Analysis()
        {
            return new MovementSafeLandingAnalysis
            {
                PlayerControllable = true,
                Dangerous = true,
                ImpactFound = true,
                ImpactTicks = 5f,
                FallingSpeed = 10f
            };
        }

        private static MovementSafeLandingAnalysis AnalysisNearGround()
        {
            var analysis = Analysis();
            analysis.ImpactTicks = 4f;
            analysis.ImpactDistancePixels = 48;
            analysis.FallingSpeed = 10f;
            return analysis;
        }

        private static void AssertAvailable(MovementSafeLandingEquipmentMoveRecord record, MovementSafeLandingAnalysis analysis)
        {
            string reason;
            if (!MovementSafeLandingService.IsTemporaryEquipmentActivationCapabilityAvailable(record, analysis, out reason))
            {
                throw new InvalidOperationException("Expected available, got " + reason);
            }
        }

        private static void AssertUnavailable(MovementSafeLandingEquipmentMoveRecord record, MovementSafeLandingAnalysis analysis, string expectedReason)
        {
            string reason;
            if (MovementSafeLandingService.IsTemporaryEquipmentActivationCapabilityAvailable(record, analysis, out reason))
            {
                throw new InvalidOperationException("Expected unavailable.");
            }

            if (!string.Equals(reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + expectedReason + ", got " + reason);
            }
        }

        private static FakePlayer CreateFishingEquipmentPlayer()
        {
            var player = new FakePlayer();
            player.selectedItem = 0;
            player.inventory[0] = new FakeItem
            {
                type = TestFishingRod,
                stack = 1,
                Name = "Test Fishing Rod",
                fishingPole = 50
            };
            return player;
        }

        private static FakeItem Accessory(int type, string name)
        {
            return new FakeItem
            {
                type = type,
                stack = 1,
                Name = name,
                accessory = true
            };
        }

        private static FishingAutoEquipmentPlan BuildFishingAutoEquipmentPlan(FakePlayer player, FishingLiquidKind liquidKind)
        {
            FishingAutoEquipmentSessionInfo session;
            string message;
            if (!FishingAutoEquipmentCompat.TryCaptureSessionInfo(player, out session, out message))
            {
                throw new InvalidOperationException("Expected fishing session capture, got " + message);
            }

            FishingAutoEquipmentPlan plan;
            if (!FishingAutoEquipmentCompat.TryBuildApplyPlan(player, session, liquidKind, out plan, out message))
            {
                throw new InvalidOperationException("Expected fishing plan build, got " + message);
            }

            return plan;
        }

        private static void AssertPlanContains(FishingAutoEquipmentPlan plan, int itemType, string label)
        {
            if (FindMove(plan, itemType) == null)
            {
                throw new InvalidOperationException("Expected plan to contain " + label + " itemType=" + itemType + ".");
            }
        }

        private static void AssertPlanDoesNotContain(FishingAutoEquipmentPlan plan, int itemType, string label)
        {
            if (FindMove(plan, itemType) != null)
            {
                throw new InvalidOperationException("Expected plan not to contain " + label + " itemType=" + itemType + ".");
            }
        }

        private static FishingAutoEquipmentMovePlan FindMove(FishingAutoEquipmentPlan plan, int itemType)
        {
            if (plan == null || plan.Moves == null)
            {
                return null;
            }

            for (var index = 0; index < plan.Moves.Count; index++)
            {
                var move = plan.Moves[index];
                if (move != null && move.CandidateSignature != null && move.CandidateSignature.Type == itemType)
                {
                    return move;
                }
            }

            return null;
        }

        private sealed class FakePlayer
        {
            public readonly FakeItem[] armor = new FakeItem[20];
            public readonly FakeItem[] inventory = new FakeItem[59];
            public readonly FakeItem[] miscEquips = new FakeItem[5];
            public int whoAmI;
            public int selectedItem;
            public bool active = true;
            public bool dead;
            public bool ghost;
            public bool creativeGodMode;
            public int[] buffType = new int[22];
            public int[] buffTime = new int[22];
            public bool magicQuiver;
            public bool hasMoltenQuiver;
            public bool archery;
            public int grapCount;
            public bool CCed;
            public bool controlUseItem;
            public bool releaseUseItem;
            public bool channel;
            public bool mouseInterface;
            public int altFunctionUse;
            public bool controlUseTile;
            public bool tileInteractionHappened;
            public bool tileInteractAttempted;
            public int itemAnimation;
            public int itemTime;
            public int reuseDelay;
            public bool delayUseItem;
            public bool pendingItemReuse;
            public bool controlJump;
            public bool releaseJump = true;
            public bool controlMount;
            public bool releaseMount = true;
            public bool controlLeft;
            public bool controlRight;
            public bool controlDash;
            public bool releaseDash = true;
            public bool controlDown;
            public bool sliding;
            public int jump;
            public float gravDir = 1f;
            public FakeVector2 position = new FakeVector2 { X = 0f, Y = 0f };
            public FakeVector2 velocity = new FakeVector2 { X = 0f, Y = 0f };
            public int width = 20;
            public int height = 42;
            public int direction = 1;
            public int dashDelay;
            public int dashType;
            public bool noItems;
            public bool frozen;
            public bool stoned;
            public bool webbed;
            public bool canJumpAgainCloud;
            public int rocketBoots;
            public int rocketTime;
            public int rocketDelay;
            public bool canRocket = true;
            public bool rocketRelease;
            public bool carpet;
            public bool canCarpet;
            public int carpetTime;
            public bool gravControl2;
            public int wingsLogic;
            public float wingTime;
            public bool bootFlyingAbilities = true;
            public int AppliedSlot = -1;
            public FakeItem AppliedItem;
            public int RefreshDoubleJumpsCount;
            public int MaxUsableSlot = 7;

            private void changeItem(int slot)
            {
                selectedItem = slot;
            }

            private bool IsItemSlotUnlockedAndUsable(int slot)
            {
                return slot >= 0 && slot <= MaxUsableSlot;
            }

            private void ApplyEquipFunctional(int itemSlot, FakeItem currentItem)
            {
                AppliedSlot = itemSlot;
                AppliedItem = currentItem;
            }

            private void RefreshDoubleJumps()
            {
                RefreshDoubleJumpsCount++;
            }

            private bool CanUseBootFlyingAbilities()
            {
                return bootFlyingAbilities;
            }
        }

        private sealed class FakeProjectile
        {
            public int whoAmI;
            public int type;
            public int aiStyle;
            public int owner;
            public int identity;
            public bool active;
            public bool friendly;
            public bool hostile;
            public int width = 16;
            public int height = 18;
            public float[] ai = new float[] { 0f };
            public Vector2 position = new Vector2();
            public Vector2 velocity = new Vector2();
            public int[] localNPCImmunity = new int[256];
        }

        private sealed class FakeItem
        {
            public int type;
            public int stack;
            public int prefix;
            public string Name;
            public int damage;
            public float knockBack;
            public int shoot;
            public float shootSpeed;
            public int useAmmo;
            public int ammo;
            public int useStyle;
            public int useTime;
            public int useAnimation;
            public int reuseDelay;
            public int mana;
            public int buffType;
            public int createTile = -1;
            public int createWall = -1;
            public int pick;
            public int axe;
            public int hammer;
            public int fishingPole;
            public sbyte wingSlot = -1;
            public int headSlot = -1;
            public int bodySlot = -1;
            public int legSlot = -1;
            public bool melee;
            public bool ranged;
            public bool magic;
            public bool summon;
            public bool thrown;
            public bool sentry;
            public bool consumable;
            public bool autoReuse;
            public bool channel;
            public bool noMelee;
            public bool noUseGraphic;
            public bool accessory;
            public bool expertOnly;
            public bool favorited;

            public bool IsAir
            {
                get { return type <= 0 || stack <= 0; }
            }

            public void TurnToAir()
            {
                type = 0;
                stack = 0;
                prefix = 0;
                Name = string.Empty;
            }
        }

        private sealed class FakeVector2
        {
            public float X;
            public float Y;
        }

        private sealed class Vector2
        {
            public float X;
            public float Y;
        }

        private static class FakeMissingTileCollisionType
        {
            private static Vector2 NotTileCollision(Vector2 position, Vector2 velocity, int width, int height)
            {
                return velocity;
            }
        }

        private static class FakeTileCollisionType
        {
            public static int CallCount;

            private static Vector2 TileCollision(Vector2 position, Vector2 velocity, int width, int height, bool fallThrough, bool fall2, int gravDir)
            {
                CallCount++;
                return new Vector2 { X = velocity.X - 1f, Y = velocity.Y };
            }
        }

        private sealed class FakeTile
        {
            public ushort type;
            public short frameY;
            public byte liquid;
            public bool Active;
            public bool Inactive;
            public byte Slope;
            public bool HalfBrick;

            public bool active()
            {
                return Active;
            }

            public bool inActive()
            {
                return Inactive;
            }

            public byte slope()
            {
                return Slope;
            }

            public bool halfBrick()
            {
                return HalfBrick;
            }
        }

        private sealed class CountingFakeExecutor : IInputActionExecutor
        {
            private readonly InputActionKind _kind;

            public CountingFakeExecutor(InputActionKind kind)
            {
                _kind = kind;
            }

            public int StartCount { get; private set; }
            public int CancelCount { get; private set; }

            public InputActionKind Kind
            {
                get { return _kind; }
            }

            public InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                StartCount++;
                return InputActionExecutionStepResult.Running("counting fake running");
            }

            public InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Running("counting fake still running");
            }

            public InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
            {
                CancelCount++;
                return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "counting fake cancelled");
            }
        }

        private sealed class RunningFakeExecutor : IInputActionExecutor
        {
            private readonly InputActionKind _kind;

            public RunningFakeExecutor(InputActionKind kind)
            {
                _kind = kind;
            }

            public InputActionKind Kind
            {
                get { return _kind; }
            }

            public InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Running("fake running");
            }

            public InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Running("fake still running");
            }

            public InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "fake cancelled");
            }
        }

        private sealed class TerminalFakeExecutor : IInputActionExecutor
        {
            private readonly InputActionKind _kind;
            private readonly InputActionStatus _status;
            private readonly string _message;

            public TerminalFakeExecutor(InputActionKind kind, InputActionStatus status, string message)
            {
                _kind = kind;
                _status = status;
                _message = message ?? string.Empty;
            }

            public InputActionKind Kind
            {
                get { return _kind; }
            }

            public InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Complete(_status, _message);
            }

            public InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Complete(_status, _message);
            }

            public InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "terminal fake cancelled");
            }
        }

        private sealed class ThrowingStartFakeExecutor : IInputActionExecutor
        {
            private readonly InputActionKind _kind;

            public ThrowingStartFakeExecutor(InputActionKind kind)
            {
                _kind = kind;
            }

            public InputActionKind Kind
            {
                get { return _kind; }
            }

            public InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                throw new InvalidOperationException("throwing start fake");
            }

            public InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
            {
                return InputActionExecutionStepResult.Running("throwing fake update should not run");
            }

            public InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "throwing fake cancelled");
            }
        }
    }
}
