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
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void CombatAutoClickerItemUseRequestAllowsCombatAim()
        {
            var autoClickerProfile = new CombatAutoClickerService.AutoClickerItemProfile
            {
                SelectedSlot = 0,
                ItemType = 280,
                ItemName = "Test Spear",
                UseStyle = 5,
                UseAnimation = 20,
                UseTime = 20
            };

            var request = CombatAutoClickerService.CreateItemUseRequest(autoClickerProfile);
            string allowCombatAim;
            if (!request.Metadata.TryGetValue("AllowCombatAim", out allowCombatAim) ||
                !string.Equals(allowCombatAim, "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected combat auto clicker ItemUse request to allow combat aim.");
            }

            var channelProfile = InputActionChannelResolver.Resolve(request);
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.UseItem, "auto clicker required");
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.BridgeItemUse, "auto clicker required");
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.MouseTarget, "auto clicker required");
        }

        private static void CombatPerfectRevolverItemCheckTakeoverMirrorsHelperCadence()
        {
            var profile = new CombatPerfectRevolverService.PerfectRevolverItemProfile
            {
                SelectedSlot = 0,
                ItemType = 2269,
                ItemName = "Revolver",
                UseStyle = 5,
                UseAnimation = 22,
                UseTime = 22,
                ItemStack = 1,
                ItemAnimation = 0,
                ItemTime = 0,
                ItemTimeIsZero = true,
                ReuseDelay = 0,
                DelayUseItem = false
            };

            var idle = CombatPerfectRevolverService.CreateAttackPlan(profile, 100, 0);
            if (!idle.PressAttack || !idle.Ready || idle.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to press attack while idle.");
            }

            profile.ItemAnimation = 8;
            profile.ItemTime = 8;
            profile.ItemTimeIsZero = false;
            var cooling = CombatPerfectRevolverService.CreateAttackPlan(profile, 101, 0);
            if (cooling.PressAttack || cooling.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to release attack while outside the helper fire window.");
            }

            profile.ItemAnimation = 2;
            profile.ItemTime = 2;
            var arming = CombatPerfectRevolverService.CreateAttackPlan(profile, 102, 0);
            if (arming.PressAttack || !arming.ScheduleNextTick || arming.NextScheduledPressTick != 103)
            {
                throw new InvalidOperationException("Expected perfect revolver to release this tick and schedule the next tick in the 2 tick fire window.");
            }

            var scheduled = CombatPerfectRevolverService.CreateAttackPlan(profile, 103, 103);
            if (!scheduled.PressAttack || !scheduled.ScheduledForThisTick || scheduled.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to press attack on the scheduled helper fire tick.");
            }
        }

        private static void CombatPerfectRevolverSchedulesOnlyInFireWindow()
        {
            var profile = new CombatPerfectRevolverService.PerfectRevolverItemProfile
            {
                ItemAnimation = 3,
                ItemTime = 3,
                ItemTimeIsZero = false,
                ReuseDelay = 0,
                DelayUseItem = false
            };

            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait outside the 2 tick helper fire window.");
            }

            profile.ItemAnimation = 2;
            profile.ItemTime = 2;
            if (!CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                !CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to arm next tick inside the 2 tick helper fire window.");
            }

            profile.DelayUseItem = true;
            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait while delayUseItem is active.");
            }

            profile.DelayUseItem = false;
            profile.ReuseDelay = 1;
            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait while reuseDelay is active.");
            }
        }

        private static void ChannelArbiterAcquireRelease()
        {
            var arbiter = new InputActionChannelArbiter();
            var request = new InputActionRequest { Kind = InputActionKind.UseHotbarItem, SourceFeatureId = "test.use" };
            InputActionChannelLease lease;
            InputActionChannelDecision decision;
            if (!arbiter.TryAcquire(request, out lease, out decision) || lease == null || !lease.IsAcquired)
            {
                throw new InvalidOperationException("Expected channel lease to be acquired.");
            }

            var snapshot = arbiter.GetSnapshot();
            if (snapshot.LeaseCount != 1)
            {
                throw new InvalidOperationException("Expected one channel lease.");
            }

            arbiter.Release(lease, "test");
            snapshot = arbiter.GetSnapshot();
            if (snapshot.LeaseCount != 0 || snapshot.OccupiedChannels != InputActionChannel.None)
            {
                throw new InvalidOperationException("Expected channel lease to be released.");
            }
        }

        private static void ChannelArbiterBlocksConflicts()
        {
            var arbiter = new InputActionChannelArbiter();
            InputActionChannelLease lease;
            InputActionChannelDecision decision;
            if (!arbiter.TryAcquire(new InputActionRequest { Kind = InputActionKind.InventorySlot, SourceFeatureId = "test.inventory" }, out lease, out decision))
            {
                throw new InvalidOperationException("Expected inventory lease.");
            }

            if (arbiter.CanAcquire(new InputActionRequest { Kind = InputActionKind.ItemUse, SourceFeatureId = "test.use" }, out decision))
            {
                throw new InvalidOperationException("Expected item use to be blocked by inventory slot lease.");
            }

            AssertHas(decision.BlockingChannels, InputActionChannel.InventorySlot, "blocking channels");
        }

        private static void ChannelArbiterAllowsNonConflicting()
        {
            var arbiter = new InputActionChannelArbiter();
            InputActionChannelLease lease;
            InputActionChannelDecision decision;
            if (!arbiter.TryAcquire(new InputActionRequest { Kind = InputActionKind.Jump, SourceFeatureId = "test.jump" }, out lease, out decision))
            {
                throw new InvalidOperationException("Expected jump lease.");
            }

            if (!arbiter.CanAcquire(new InputActionRequest { Kind = InputActionKind.ItemUse, SourceFeatureId = "test.use" }, out decision))
            {
                throw new InvalidOperationException("Expected pure arbiter to allow jump and item use as non-conflicting channels.");
            }
        }

        private static void TryEnqueueAcceptsNormalRequest()
        {
            var queue = new InputActionQueue();
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.try_enqueue",
                Description = "normal admission"
            }, out admission))
            {
                throw new InvalidOperationException("Expected TryEnqueue to accept a normal request: " + (admission == null ? "null" : admission.Reason));
            }

            var snapshot = queue.GetSnapshot();
            if (snapshot.PendingCount != 1 ||
                !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Accepted", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected accepted admission to appear in snapshot.");
            }
        }

        private static void TryEnqueueRejectsDuplicatePendingAdmissionKey()
        {
            var queue = new InputActionQueue();
            var first = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                SourceFeatureId = "test.duplicate",
                AdmissionKey = "duplicate-key"
            };
            var second = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                SourceFeatureId = "test.duplicate",
                AdmissionKey = "duplicate-key"
            };

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(first, out admission))
            {
                throw new InvalidOperationException("Expected first duplicate-key request to be accepted.");
            }

            if (queue.TryEnqueue(second, out admission))
            {
                throw new InvalidOperationException("Expected duplicate pending admission key to be rejected.");
            }

            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning ||
                !admission.DuplicatePendingOrRunning ||
                queue.GetSnapshot().PendingCount != 1)
            {
                throw new InvalidOperationException("Unexpected duplicate pending admission result.");
            }
        }

        private static void TryEnqueueRejectsDuplicateRunningAdmissionKey()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_duplicate",
                AdmissionKey = "running-duplicate-key"
            }, out admission))
            {
                throw new InvalidOperationException("Expected running duplicate seed request to be accepted.");
            }

            queue.Update(null);
            if (string.IsNullOrWhiteSpace(queue.GetSnapshot().RunningActionKind))
            {
                throw new InvalidOperationException("Expected seed request to be running.");
            }

            if (queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_duplicate",
                AdmissionKey = "running-duplicate-key"
            }, out admission))
            {
                throw new InvalidOperationException("Expected duplicate running admission key to be rejected.");
            }

            if (admission == null || admission.Decision != InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning)
            {
                throw new InvalidOperationException("Unexpected duplicate running admission result.");
            }
        }

        private static void TryEnqueueReportsItemUseBridgeBusy()
        {
            var bridgeRequestId = Guid.NewGuid();
            string bridgeMessage;
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                bridgeRequestId,
                "test.bridge",
                0,
                1,
                1,
                "Test Item",
                TimeSpan.FromSeconds(30),
                0,
                InputActionKind.ItemUse,
                "Test.Bridge",
                string.Empty,
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty,
                out bridgeMessage))
            {
                throw new InvalidOperationException("Failed to seed ItemUseBridge pending request: " + bridgeMessage);
            }

            try
            {
                var queue = new InputActionQueue();
                InputActionAdmissionResult admission;
                if (queue.TryEnqueue(new InputActionRequest
                {
                    Kind = InputActionKind.ItemUse,
                    SourceFeatureId = "test.use_item",
                    Description = "use item while bridge busy"
                }, out admission))
                {
                    throw new InvalidOperationException("Expected ItemUse request to be rejected while bridge is busy.");
                }

                if (admission == null ||
                    admission.Decision != InputActionAdmissionDecision.DeniedBridgeBusy ||
                    string.IsNullOrWhiteSpace(admission.BridgeBusySummary))
                {
                    throw new InvalidOperationException("Expected bridge busy admission details.");
                }
            }
            finally
            {
                ItemUseBridge.Cancel(bridgeRequestId, "test cleanup");
            }
        }

        private static void TryEnqueueBridgeBusyKeepsPendingCountUnchanged()
        {
            var bridgeRequestId = Guid.NewGuid();
            string bridgeMessage;
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                bridgeRequestId,
                "test.bridge_count",
                0,
                1,
                1,
                "Test Item",
                TimeSpan.FromSeconds(30),
                0,
                InputActionKind.ItemUse,
                "Test.BridgeCount",
                string.Empty,
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty,
                out bridgeMessage))
            {
                throw new InvalidOperationException("Failed to seed ItemUseBridge pending request: " + bridgeMessage);
            }

            try
            {
                var queue = new InputActionQueue();
                var before = queue.GetSnapshot().PendingCount;
                InputActionAdmissionResult admission;
                if (queue.TryEnqueue(new InputActionRequest
                {
                    Kind = InputActionKind.ItemUse,
                    SourceFeatureId = "test.use_item_bridge_count",
                    Description = "use item while bridge busy"
                }, out admission))
                {
                    throw new InvalidOperationException("Expected ItemUse request to be rejected while bridge is busy.");
                }

                var after = queue.GetSnapshot().PendingCount;
                if (before != after || after != 0)
                {
                    throw new InvalidOperationException("Denied bridge-busy TryEnqueue should not modify pending count.");
                }
            }
            finally
            {
                ItemUseBridge.Cancel(bridgeRequestId, "test cleanup");
            }
        }

        private static void TryEnqueueDerivesQueueExpiration()
        {
            var created = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(250);
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                SourceFeatureId = "test.queue_expiration",
                CreatedUtc = created,
                QueueTimeout = timeout
            };

            var queue = new InputActionQueue();
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                throw new InvalidOperationException("Expected request to be admitted: " + (admission == null ? "null" : admission.Reason));
            }

            if (request.QueueExpiresUtc != created + timeout)
            {
                throw new InvalidOperationException("Expected QueueExpiresUtc to be derived from CreatedUtc + QueueTimeout.");
            }
        }

        private static void TryEnqueueAllowsDistinctEmptySourceDefaultKeys()
        {
            var queue = new InputActionQueue();
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Description = "empty source one"
            }, out admission))
            {
                throw new InvalidOperationException("Expected first empty-source request to be admitted.");
            }

            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Description = "empty source two"
            }, out admission))
            {
                throw new InvalidOperationException("Expected distinct empty-source request to be admitted; got " + (admission == null ? "null" : admission.Reason));
            }

            if (queue.GetSnapshot().PendingCount != 2)
            {
                throw new InvalidOperationException("Expected both empty-source requests to remain pending.");
            }
        }

        private static void LegacyEnqueueStillAcceptsWhileChannelBusy()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.legacy.running"
            });
            queue.Update(null);

            var legacyId = queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.legacy.pending",
                AdmissionKey = "legacy-duplicate-channel"
            });
            var snapshot = queue.GetSnapshot();
            if (legacyId == Guid.Empty || snapshot.PendingCount != 1)
            {
                throw new InvalidOperationException("Expected legacy Enqueue to keep accepting pending requests.");
            }
        }

        private static void PendingQueueTimeoutExpiresBeforeStart()
        {
            var queue = new InputActionQueue();
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.expired",
                CreatedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                QueueTimeout = TimeSpan.FromMilliseconds(1),
                QueueExpiresUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(500)
            });

            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.PendingCount != 0 ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.TimedOut ||
                snapshot.ActionQueueExpiredPendingCount != 1 ||
                snapshot.RecentActionLine1.IndexOf("expired before start", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Expected expired pending request to be recorded as timed out.");
            }
        }

        private static void PendingExpirationDoesNotCancelExecutor()
        {
            var executor = new CountingFakeExecutor(InputActionKind.MouseTarget);
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = executor;
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.expired_no_cancel",
                CreatedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                QueueTimeout = TimeSpan.FromMilliseconds(1),
                QueueExpiresUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(500)
            });

            queue.Update(null);
            if (executor.StartCount != 0 || executor.CancelCount != 0)
            {
                throw new InvalidOperationException("Expired pending request should not start or cancel its executor.");
            }
        }

        private static void PendingQueueTimeoutExpiresWhileRunning()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_expiry.running"
            });
            queue.Update(null);

            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_expiry.pending",
                CreatedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                QueueTimeout = TimeSpan.FromMilliseconds(1),
                QueueExpiresUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(500)
            });

            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.PendingCount != 0 ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.TimedOut ||
                string.IsNullOrWhiteSpace(snapshot.RunningActionKind))
            {
                throw new InvalidOperationException("Expected expired pending request to be removed while the running action continues.");
            }
        }

        private static void PendingExpirationWhileRunningDoesNotStartOrCancelPendingExecutor()
        {
            var executor = new CountingFakeExecutor(InputActionKind.MouseTarget);
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = executor;
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_expiry_no_cancel.running"
            });
            queue.Update(null);

            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_expiry_no_cancel.pending",
                CreatedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                QueueTimeout = TimeSpan.FromMilliseconds(1),
                QueueExpiresUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(500)
            });
            queue.Update(null);

            if (executor.StartCount != 1 || executor.CancelCount != 0)
            {
                throw new InvalidOperationException("Expired pending request should not start or cancel while another request is running.");
            }
        }

        private static void RunningLeaseSurvivesPendingExpiration()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_lease.running"
            });
            queue.Update(null);

            var before = queue.GetSnapshot();
            if (before.ActionQueueChannelLeaseCount != 1 ||
                string.IsNullOrWhiteSpace(before.ActionQueueRunningLeaseChannels) ||
                string.Equals(before.ActionQueueRunningLeaseChannels, "None", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected running action to hold a channel lease before pending expiration.");
            }

            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.running_lease.pending",
                CreatedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                QueueTimeout = TimeSpan.FromMilliseconds(1),
                QueueExpiresUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(500)
            });
            queue.Update(null);

            var after = queue.GetSnapshot();
            if (after.ActionQueueChannelLeaseCount != 1 ||
                string.IsNullOrWhiteSpace(after.RunningActionKind) ||
                !string.Equals(before.ActionQueueRunningLeaseChannels, after.ActionQueueRunningLeaseChannels, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Pending expiration should not release the running action channel lease.");
            }
        }

        private static void SchedulerKeepsPriorityThenCreatedOrder()
        {
            var earlyLow = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Priority = InputActionPriority.Low,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            var lateHigh = new InputActionRequest
            {
                Kind = InputActionKind.Dash,
                Priority = InputActionPriority.High,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc)
            };
            var earlyHigh = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.High,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc)
            };

            var selected = InputActionScheduler.SelectNext(new[] { earlyLow, lateHigh, earlyHigh });
            if (!object.ReferenceEquals(selected, earlyHigh))
            {
                throw new InvalidOperationException("Expected scheduler to pick highest priority, then earliest CreatedUtc.");
            }
        }

        private static void PendingLowerPrioritySameChannelDoesNotBlockAdmission()
        {
            var queue = new InputActionQueue();
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = "test.low_use",
                AdmissionKey = "low-use"
            }, out admission))
            {
                throw new InvalidOperationException("Expected low priority ItemUse pending request to be accepted.");
            }

            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.High,
                SourceFeatureId = "test.high_use",
                AdmissionKey = "high-use"
            }, out admission))
            {
                throw new InvalidOperationException("Expected higher priority same-channel request to be admitted; got " + (admission == null ? "null" : admission.Reason));
            }

            if (queue.GetSnapshot().PendingCount != 2 ||
                admission == null ||
                string.IsNullOrWhiteSpace(admission.PendingConflictSummary))
            {
                throw new InvalidOperationException("Expected pending same-channel conflict to be diagnostic-only.");
            }
        }

        private static void SimulatedJumpRequestHasQueueTimeout()
        {
            var request = MovementSimulatedJumpService.CreateRequestForTesting("airJump");
            AssertPositiveQueueTimeout(request, "simulated jump");
        }

        private static void ContinuousDashRequestHasQueueTimeout()
        {
            var request = MovementContinuousDashService.CreateRequestForTesting(MovementContinuousDashModes.HoldDirection, 1);
            AssertPositiveQueueTimeout(request, "continuous dash");
        }

        private static void AutoFacingRequestHasQueueTimeout()
        {
            var request = CombatAutoFacingService.CreateRequestForTesting(1, "targetOrCursor");
            AssertPositiveQueueTimeout(request, "auto facing");
        }

        private static void AssertPositiveQueueTimeout(InputActionRequest request, string label)
        {
            if (request == null || request.QueueTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Expected " + label + " request to have a positive QueueTimeout.");
            }
        }

        private static void CombatAimDiagnosticsMetadataKeepsStableFieldNames()
        {
            var decision = BuildCombatAimDiagnosticDecision();
            var json = BuildCombatAimDecisionJson(decision, true, true);

            AssertContains(json, "\"itemType\"");
            AssertContains(json, "\"itemName\"");
            AssertContains(json, "\"damageType\"");
            AssertContains(json, "\"ammoItemType\"");
            AssertContains(json, "\"ammoItemName\"");
            AssertContains(json, "\"ammoShoot\"");
            AssertContains(json, "\"resolvedProjectileType\"");
            AssertContains(json, "\"resolvedProjectileName\"");
            AssertContains(json, "\"projectileTileCollide\"");
            AssertContains(json, "\"projectileWidth\"");
            AssertContains(json, "\"projectileFriendly\"");
            AssertContains(json, "\"aimPurpose\"");
            AssertContains(json, "\"applyPolicy\"");
            AssertContains(json, "\"lineOfSightResult\"");
            AssertContains(json, "\"markerAttackTargetMismatch\"");
            AssertContains(json, "\"itemCheckAimEntered\"");
            AssertContains(json, "\"mouseStateCaptured\"");
            AssertContains(json, "\"weaponFamily\"");
            AssertContains(json, "\"weaponFamilyReason\"");
            AssertContains(json, "\"persistentCursorTargetSet\"");
            AssertContains(json, "\"persistentCursorEligibility\"");
            AssertContains(json, "\"persistentCursorEligibilityReason\"");
            AssertContains(json, "\"persistentCursorClass\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\"");
            AssertContains(json, "\"persistentCursorProjectileAiScopedAllowed\"");
            AssertContains(json, "\"persistentCursorScopedOverride\"");
            AssertContains(json, "\"projectileCursorMatch\"");
            AssertContains(json, "\"projectileCursorMatchReason\"");
            AssertContains(json, "\"projectileCursorProjectileType\"");
            AssertContains(json, "\"projectileCursorOwner\"");
            AssertContains(json, "\"visibleCursorHijackRisk\"");
            AssertContains(json, "\"visibleCursorHijackRiskMitigated\"");
            AssertContains(json, "\"userCursorWorldAvailable\"");
            AssertContains(json, "\"userCursorWorld\"");
            AssertContains(json, "\"simulatedAimWorld\"");
            AssertContains(json, "\"cursorOwnershipMode\"");
            AssertContains(json, "\"releaseHoldTargetDummyAllowed\"");
            AssertContains(json, "\"flailControlEligible\"");
            AssertContains(json, "\"flailControlReason\"");
            AssertContains(json, "\"flailControlActive\"");
            AssertContains(json, "\"flailControlState\"");
            AssertContains(json, "\"flailInputMode\"");
            AssertContains(json, "\"flailInputPhase\"");
            AssertContains(json, "\"flailTakeoverScope\"");
            AssertContains(json, "\"flailPhysicalUseItemHeld\"");
            AssertContains(json, "\"flailPhysicalReleasePending\"");
            AssertContains(json, "\"flailProjectileWhoAmI\"");
            AssertContains(json, "\"flailProjectileType\"");
            AssertContains(json, "\"flailProjectileAiStyle\"");
            AssertContains(json, "\"flailProjectileAi0\"");
            AssertContains(json, "\"flailProjectileVelocity\"");
            AssertContains(json, "\"flailProjectileIdentity\"");
            AssertContains(json, "\"flailHitDetected\"");
            AssertContains(json, "\"flailCollisionDetected\"");
            AssertContains(json, "\"flailLocalNpcImmunityChanged\"");
            AssertContains(json, "\"flailTileCollisionDetected\"");
            AssertContains(json, "\"flailAttackPulse\"");
            AssertContains(json, "\"flailAttackRelease\"");
            AssertContains(json, "\"flailAttackSuppressed\"");
            AssertContains(json, "\"flailAttackRestored\"");
            AssertContains(json, "\"flailPulseReason\"");
            AssertContains(json, "\"flailStuckRecovery\"");
            AssertContains(json, "\"flailCachedReleaseAim\"");
            AssertContains(json, "\"flailCachedReleaseAimAgeTicks\"");
            AssertContains(json, "\"flailCachedReleaseAimReason\"");
            AssertContains(json, "\"flailReleaseSuppressedPhysicalInput\"");
            AssertContains(json, "\"flailControlBlockedReason\"");
            AssertContains(json, "\"weaponShootProjectileType\"");
            AssertContains(json, "\"weaponShootProjectileName\"");
            AssertContains(json, "\"ammoProjectileType\"");
            AssertContains(json, "\"ammoProjectileName\"");
            AssertContains(json, "\"primaryProjectileType\"");
            AssertContains(json, "\"primaryProjectileName\"");
            AssertContains(json, "\"primaryProjectileRole\"");
            AssertContains(json, "\"resolvedProjectileRole\"");
            AssertContains(json, "\"secondaryProjectileType\"");
            AssertContains(json, "\"secondaryProjectileName\"");
            AssertContains(json, "\"secondaryProjectileRole\"");
            AssertContains(json, "\"specialWeaponRuleKind\"");
            AssertContains(json, "\"specialWeaponRuleName\"");
            AssertContains(json, "\"specialWeaponRuleApplied\"");
            AssertContains(json, "\"specialWeaponAimMode\"");
            AssertContains(json, "\"specialWeaponAimPoint\"");
            AssertContains(json, "\"specialWeaponUsesCursorTarget\"");
            AssertContains(json, "\"specialWeaponUsesWeaponProjectile\"");
            AssertContains(json, "\"specialWeaponUsesAmmoProjectile\"");
            AssertContains(json, "\"specialWeaponUsesWeaponShoot\"");
            AssertContains(json, "\"specialWeaponUsesAmmoShoot\"");
        }

        private static void CombatAimWeaponFamilyResolverClassifiesRequestedFamilies()
        {
            AssertWeaponFamily(
                "Pigron flail",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 5526,
                    stack = 1,
                    Name = "Flairon",
                    damage = 66,
                    shoot = 1058,
                    shootSpeed = 12f,
                    melee = true,
                    channel = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 1058, ProjectileAiStyle = 15 },
                CombatAimWeaponFamilies.FlailAiStyle15);

            AssertWeaponFamily(
                "Xenopopper",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 2797,
                    stack = 1,
                    Name = "Xenopopper",
                    damage = 45,
                    shoot = 444,
                    shootSpeed = 24f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.SpecialCursorSpawnBurst);

            AssertWeaponFamily(
                "VortexBeater",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 3475,
                    stack = 1,
                    Name = "VortexBeater",
                    damage = 50,
                    shoot = 615,
                    shootSpeed = 14f,
                    useAmmo = 97,
                    ranged = true,
                    channel = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.SpecialDualProjectile);

            AssertWeaponFamily(
                "Spear",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 280,
                    stack = 1,
                    Name = "Spear",
                    damage = 8,
                    shoot = 49,
                    shootSpeed = 5f,
                    melee = true,
                    noMelee = true,
                    noUseGraphic = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 49, ProjectileAiStyle = 19 },
                CombatAimWeaponFamilies.SpearAiStyle19);

            AssertWeaponFamily(
                "Wooden Boomerang",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 55,
                    stack = 1,
                    Name = "Wooden Boomerang",
                    damage = 8,
                    shoot = 3,
                    shootSpeed = 10f,
                    melee = true,
                    noUseGraphic = true,
                    useStyle = 1
                }),
                new CombatAimBallisticSolution { ProjectileType = 3, ProjectileAiStyle = 3 },
                CombatAimWeaponFamilies.ReturningBoomerangAiStyle3);

            AssertWeaponFamily(
                "Ordinary Gun",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 100,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.DirectProjectile);

            AssertWeaponFamily(
                "Ordinary Bow",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 99,
                    stack = 1,
                    Name = "Ordinary Bow",
                    damage = 17,
                    shoot = 1,
                    shootSpeed = 8.5f,
                    useAmmo = 1,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 1, ProjectileAiStyle = 1, AmmoProjectileType = 1 },
                CombatAimWeaponFamilies.DirectProjectile);
        }

        private static void CombatAimWeaponFamilyDiagnosticsEmitsMetadataFields()
        {
            var decision = BuildCombatAimDiagnosticDecision();
            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"DirectProjectile\"");
            AssertContains(json, "\"weaponFamilyReason\":\"projectileSemantics:shoot=1;useAmmo=1\"");

            var player = new FakePlayer();
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;
            decision.ItemType = item.type;
            decision.ItemStack = item.stack;
            decision.ItemName = item.Name;
            decision.Damage = item.damage;
            decision.Shoot = item.shoot;
            decision.UseAmmo = item.useAmmo;
            decision.WeaponProfile = CombatAimWeaponProfile.Read(player, item);
            decision.BallisticSolution = new CombatAimBallisticSolution
            {
                Solved = true,
                Mode = "specialCursorSpawnBurst",
                ProjectileType = 14,
                ProjectileName = "Bullet",
                ProjectileAiStyle = 1,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 444,
                SpecialWeaponKind = "cursorSpawnBurst",
                SpecialWeaponName = "Xenopopper",
                SpecialWeaponRule = "cursorSpawnBubbleBullet",
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"weaponFamilyReason\":\"specialWeaponRuleKind=cursorSpawnBurst\"");
            AssertContains(json, "\"projectileAiStyle\":1");
            AssertContains(json, "\"specialWeaponRuleKind\":\"cursorSpawnBurst\"");
        }

        private static void CombatAimSkipReasonsNormalizeToStableStrings()
        {
            AssertCombatAimSkipReason("radiusOff", "disabled");
            AssertCombatAimSkipReason("itemUseBridgePending", "bridgeBusy");
            AssertCombatAimSkipReason("playerMouseInterface", "mouseCaptured");
            AssertCombatAimSkipReason("placementItem", "placementItem");
            AssertCombatAimSkipReason("toolOrFishingItem", "toolOrFishingRod");
            AssertCombatAimSkipReason("sentryPlacementWeapon", "sentryOrSummonPlacement");
            AssertCombatAimSkipReason("summonPlacementWeapon", "sentryOrSummonPlacement");
            AssertCombatAimSkipReason("notProjectileAmmoOrMelee", "noProjectile");
            AssertCombatAimSkipReason("targetUnavailable:NoTarget:noCandidates", "noTarget");
            AssertCombatAimSkipReason("targetUnavailable:NoTarget:blockedByLineOfSight", "lineOfSightBlocked");
            AssertCombatAimSkipReason("notEligible:notChannelProjectile", "persistentCursorNotEligible");
            AssertCombatAimSkipReason("releaseHoldTargetInvalid:targetDummyDisabled", "noTarget");
        }

        private static void PersistentCursorPolicyRejectsOrdinaryProjectileWeapons()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 100,
                stack = 1,
                Name = "Ordinary Gun",
                damage = 20,
                shoot = 14,
                shootSpeed = 10f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, false, "notEligible:notChannelProjectile", "none");
        }

        private static void PersistentCursorPolicyAllowsChannelProjectileScopedOnly()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, true, "eligible:projectileAiScoped", "channelProjectileWeapon");
            if (!eligibility.VisibleCursorHijackRisk ||
                !eligibility.VisibleCursorHijackRiskMitigated ||
                !string.Equals(eligibility.CursorOwnershipMode, "projectileAiScoped", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to mitigate visible cursor risk through Projectile.AI scoped ownership.");
            }

            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to block Main.Update fallback.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to allow only generic Projectile.AI scoped override.");
            }
        }

        private static void PersistentCursorPolicyAllowsSpecialProjectileScopedOnly()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, true, "eligible:specialProjectileAiScoped", "specialProjectileWeapon");
            if (!eligibility.VisibleCursorHijackRisk ||
                !eligibility.VisibleCursorHijackRiskMitigated ||
                !eligibility.AllowsAnimationScopedWithoutHeld ||
                !string.Equals(eligibility.CursorOwnershipMode, "projectileAiScoped", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected special projectile weapons to use scoped projectile ownership and allow animation-scoped followup.");
            }

            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected special projectile weapons to block Main.Update fallback.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected special projectile weapons to allow only generic Projectile.AI scoped override.");
            }

            var vortex = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 3475,
                stack = 1,
                Name = "VortexBeater",
                damage = 50,
                shoot = 615,
                shootSpeed = 14f,
                useAmmo = 97,
                ranged = true,
                channel = true,
                useStyle = 5
            });
            var vortexEligibility = CombatAimPersistentCursorPolicy.Evaluate(vortex, false, false, string.Empty);
            AssertPersistentCursorEligibility(vortexEligibility, true, "eligible:specialProjectileAiScoped", "specialProjectileWeapon");

            var ordinaryShotgun = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 534,
                stack = 1,
                Name = "Shotgun",
                damage = 24,
                shoot = 14,
                shootSpeed = 9f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            var ordinaryEligibility = CombatAimPersistentCursorPolicy.Evaluate(ordinaryShotgun, false, false, string.Empty);
            AssertPersistentCursorEligibility(ordinaryEligibility, false, "notEligible:notChannelProjectile", "none");

            var boomstick = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 964,
                stack = 1,
                Name = "Boomstick",
                damage = 14,
                shoot = 14,
                shootSpeed = 5.35f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            var boomstickEligibility = CombatAimPersistentCursorPolicy.Evaluate(boomstick, false, false, string.Empty);
            AssertPersistentCursorEligibility(boomstickEligibility, false, "notEligible:notChannelProjectile", "none");

            CombatAimSpecialWeaponRule boomstickRule;
            if (CombatAimSpecialWeaponRuleResolver.TryResolve(boomstick, out boomstickRule))
            {
                throw new InvalidOperationException("Expected Boomstick to stay out of special projectile scoped weapon rules.");
            }
        }

        private static void PersistentCursorPolicyRejectsPlacementSummonsAndSentries()
        {
            var summon = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 102,
                stack = 1,
                Name = "Summon Placement",
                damage = 20,
                shoot = 1,
                buffType = 1,
                summon = true,
                channel = true
            });
            AssertPersistentCursorEligibility(
                CombatAimPersistentCursorPolicy.Evaluate(summon, false, false, string.Empty),
                false,
                "notEligible:placementOrSummon",
                "none");

            var sentry = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 103,
                stack = 1,
                Name = "Sentry Placement",
                damage = 20,
                shoot = 1,
                sentry = true,
                channel = true
            });
            AssertPersistentCursorEligibility(
                CombatAimPersistentCursorPolicy.Evaluate(sentry, false, false, string.Empty),
                false,
                "notEligible:placementOrSummon",
                "none");
        }

        private static void PersistentCursorPolicyPreservesYoyoEligibility()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 104,
                stack = 1,
                Name = "Yoyo",
                damage = 30,
                shoot = 99,
                melee = true,
                channel = true
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, true, true, "activeYoyoProjectile");
            AssertPersistentCursorEligibility(eligibility, true, "yoyo", "yoyo");
            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo to keep projectile hook ownership when AI099 is installed.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility) ||
                !CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo to allow projectile-scoped hooks.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo fallback to remain available when projectile hook is unavailable.");
            }
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalChannelProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution { ProjectileType = 633 };

            var match = CombatAimProjectileCursorCompat.MatchChannelProjectile(
                new FakeProjectile { whoAmI = 10, type = 633, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:channelProjectileWeapon", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned channel projectile to match, got " + match.Reason);
            }

            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 11, type = 633, owner = 7, active = true, friendly = true, hostile = true },
                player,
                profile,
                solution,
                "notEligible:hostileProjectile");
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 12, type = 633, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 13, type = 999, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:projectileMismatch");

            var ordinaryGun = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
                {
                    type = 102,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                });
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 14, type = 14, owner = 7, active = true, friendly = true },
                player,
                ordinaryGun,
                new CombatAimBallisticSolution { ProjectileType = 14 },
                "notEligible:notChannelProjectileWeapon");
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalSpecialWeaponProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 444,
                SecondaryProjectileType = 444,
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned special weapon projectile to match, got " + match.Reason);
            }

            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 21, type = 444, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 22, type = 14, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:projectileMismatch");

            var vortex = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
                {
                    type = 3475,
                    stack = 1,
                    Name = "VortexBeater",
                    damage = 50,
                    shoot = 615,
                    shootSpeed = 14f,
                    useAmmo = 97,
                    ranged = true,
                    channel = true,
                    useStyle = 5
                });
            var vortexSolution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 615,
                SecondaryProjectileType = 615,
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };
            var vortexController = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 25, type = 615, owner = 7, active = true, friendly = false },
                player,
                vortex,
                vortexSolution);
            if (!vortexController.Matches)
            {
                throw new InvalidOperationException("Expected VortexBeater non-friendly controller projectile to match special scoped rule, got " + vortexController.Reason);
            }

            var vortexRocket = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 24, type = 616, owner = 7, active = true, friendly = true },
                player,
                vortex,
                vortexSolution);
            if (!vortexRocket.Matches)
            {
                throw new InvalidOperationException("Expected VortexBeater assist rocket projectile to match special scoped rule, got " + vortexRocket.Reason);
            }

            var ordinaryGun = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
                {
                    type = 100,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                });
            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 23, type = 14, owner = 7, active = true, friendly = true },
                player,
                ordinaryGun,
                new CombatAimBallisticSolution { ProjectileType = 14 },
                "notEligible:notSpecialProjectileWeapon");
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalFlailReleaseProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 5526,
                stack = 1,
                Name = "Flairon",
                damage = 66,
                shoot = 1058,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 1058,
                ProjectileAiStyle = 15
            };

            var match = CombatAimProjectileCursorCompat.MatchFlailProjectile(
                new FakeProjectile { whoAmI = 31, type = 1058, aiStyle = 15, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:flailAiStyle15Release", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned aiStyle 15 flail projectile to match, got " + match.Reason);
            }

            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 32, type = 1058, aiStyle = 15, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 33, type = 1058, aiStyle = 99, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notFlailAiStyle15");
            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 34, type = 633, aiStyle = 75, owner = 7, active = true, friendly = true },
                player,
                CombatAimWeaponProfile.Read(
                    player,
                    new FakeItem
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
                    }),
                new CombatAimBallisticSolution { ProjectileType = 633, ProjectileAiStyle = 75 },
                "notEligible:notFlailAiStyle15");
        }

        private static void AssertProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchChannelProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void AssertSpecialProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected special projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void AssertFlailProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchFlailProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void CombatAimScopedCursorDiagnosticsKeepsOwnershipFields()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            };

            var decision = BuildCombatAimDiagnosticDecision();
            decision.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            decision.PersistentCursorActive = true;
            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorReason = "channelProjectileWeapon";
            decision.ItemType = item.type;
            decision.ItemStack = item.stack;
            decision.ItemName = item.Name;
            decision.Damage = item.damage;
            decision.Shoot = item.shoot;
            decision.UseAmmo = item.useAmmo;
            decision.WeaponProfile = CombatAimWeaponProfile.Read(player, item);
            decision.BallisticSolution.ProjectileType = 633;
            decision.AimWorldX = 320f;
            decision.AimWorldY = 360f;
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                decision,
                CombatAimProjectileCursorMatch.Result(true, "matched:channelProjectileWeapon", 10, 633, 7, 75),
                true,
                true,
                true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"persistentHook\":\"ProjectileAI\"");
            AssertContains(json, "\"persistentCursorEligibility\":true");
            AssertContains(json, "\"persistentCursorEligibilityReason\":\"eligible:projectileAiScoped\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\":false");
            AssertContains(json, "\"persistentCursorProjectileAiScopedAllowed\":true");
            AssertContains(json, "\"persistentCursorScopedOverride\":true");
            AssertContains(json, "\"persistentCursorTargetSet\":true");
            AssertContains(json, "\"projectileCursorMatch\":true");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:channelProjectileWeapon\"");
            AssertContains(json, "\"projectileCursorProjectileType\":633");
            AssertContains(json, "\"projectileCursorOwner\":7");
            AssertContains(json, "\"visibleCursorHijackRiskMitigated\":true");
            AssertContains(json, "\"cursorOwnershipMode\":\"projectileAiScoped\"");
        }

        private static void FlailPolicyOnlyAcceptsNonYoyoChannelAiStyle15()
        {
            var flail = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 5526,
                stack = 1,
                Name = "Flairon",
                damage = 66,
                shoot = 1058,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            });
            var eligible = CombatAimFlailPolicy.Evaluate(flail, 15, false);
            if (!eligible.Eligible || !string.Equals(eligible.Reason, "eligible:flailAiStyle15", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected channel aiStyle 15 flail to be eligible, got " + eligible.Reason);
            }

            AssertFlailRejected(flail, 15, true, "notFlail:yoyo");
            AssertFlailRejected(flail, 75, false, "notFlail:notFlailAiStyle15");

            var channelProjectile = BuildCombatAimWeaponProfile(new FakeItem
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
            });
            AssertFlailRejected(channelProjectile, 75, false, "notFlail:notFlailAiStyle15");

            var gun = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 1553,
                stack = 1,
                Name = "SDMG",
                damage = 85,
                shoot = 10,
                shootSpeed = 13f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            AssertFlailRejected(gun, 1, false, "notFlail:notChannel");

            var sentry = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 3826,
                stack = 1,
                Name = "Ballista Staff",
                damage = 27,
                shoot = 679,
                sentry = true
            });
            AssertFlailRejected(sentry, 15, false, "notFlail:placementOrSummon");
        }

        private static void AssertFlailRejected(CombatAimWeaponProfile profile, int projectileAiStyle, bool isYoyo, string expectedReason)
        {
            var eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
            if (eligibility.Eligible || !string.Equals(eligibility.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail rejection " + expectedReason + ", got " + eligibility.Reason);
            }
        }

        private static void FlailControlPreservesHoldSpinAndReleasesOnPhysicalRelease()
        {
            var noProjectileHeld = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.None(),
                true,
                false,
                false,
                false,
                false,
                true,
                false,
                false);
            AssertFlailDecision(noProjectileHeld, FlailControlStates.SpinHold, false, false, false, "spinHoldNoProjectile");

            var spinHeld = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                false,
                true,
                false,
                false);
            AssertFlailDecision(spinHeld, FlailControlStates.SpinHold, false, false, false, "spinHold");

            var release = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                false,
                false,
                true,
                true);
            AssertFlailDecision(release, FlailControlStates.ReleaseToTarget, false, true, true, "physicalRelease");

            var hit = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 1f, 6f, 0f),
                true,
                false,
                true,
                false,
                false,
                false,
                false,
                true);
            AssertFlailDecision(hit, FlailControlStates.ProjectileActive, false, false, false, "hitDetected");

            var flying = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 1f, 6f, 0f),
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                true);
            AssertFlailDecision(flying, FlailControlStates.ProjectileFlying, false, false, false, "projectileFlying");
        }

        private static void FlailItemCheckTakeoverSkipsHoldSpin()
        {
            ResetFakeMainMouse(true, false);
            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var decision = BuildFlailItemCheckDecision(player);
            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.SpinHold,
                InputMode = "observe",
                InputPhase = FlailControlStates.SpinHold,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = true
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
                throw new InvalidOperationException("Expected flail hold spin to avoid ItemCheck use-item takeover.");
            }

            if (!player.controlUseItem || player.releaseUseItem || !player.channel ||
                !Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected flail hold spin to preserve physical held inputs.");
            }
        }

        private static void FlailReleaseHoldItemCheckTakeoverArmsProjectileTailBeforeRuntimeUpdate()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.GameUpdateCount = 700;
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
            decision.AimApplyMode = CombatAimApplyModes.ReleaseHold;
            decision.UseItemHeld = false;
            decision.UseItemReleased = false;
            decision.WasUseItemHeldLastTick = true;
            decision.ReleasedThisTick = true;
            decision.ReleaseDetected = true;
            decision.ReleaseHoldPending = true;
            decision.ReleaseHoldActive = true;
            decision.ReleaseHoldState = ReleaseHoldStates.ReleasedPending;
            decision.ReleaseHoldValidationReason = "targetDummyAllowed:strictRecomputed";

            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.SpinHold,
                InputMode = "observe",
                InputPhase = FlailControlStates.SpinHold,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = true,
                PhysicalReleasePending = false,
                CachedReleaseAim = true,
                CachedReleaseAimAgeTicks = 0,
                CachedReleaseAimReason = "available"
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                throw new InvalidOperationException("Expected ReleaseHold flail decision to enter ItemCheck release takeover before RuntimeUpdate observes the release.");
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                takeover == null || !takeover.Applied || takeover.Pressed)
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover to apply release state without pressing use item.");
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(decision);
            if (diagnostics == null ||
                !diagnostics.AttackRelease ||
                !diagnostics.AttackSuppressed ||
                !diagnostics.PhysicalReleasePending ||
                !string.Equals(diagnostics.State, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.TakeoverScope, "ItemCheck", StringComparison.Ordinal) ||
                !string.Equals(diagnostics.BlockedReason, "releaseHoldItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover diagnostics to promote stale SpinHold into ReleaseToTarget.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetFlailReleaseTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetFlailReleaseTailDecision reflection failed.");
            }

            var args = new object[] { 700L, null, null };
            var tailAvailable = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!tailAvailable || tail == null ||
                !string.Equals(tail.PersistentCursorReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - decision.AimWorldX) > 0.001f ||
                Math.Abs(tail.AimWorldY - decision.AimWorldY) > 0.001f)
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover to arm flail Projectile.AI release tail immediately.");
            }

            TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
        }

        private static void FlailItemCheckTakeoverAppliesPhysicalReleaseScope()
        {
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
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
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = false,
                PhysicalReleasePending = true
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                throw new InvalidOperationException("Expected flail physical release takeover to apply in ItemCheck scope: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                takeover == null || !takeover.Applied || takeover.Pressed ||
                !string.Equals(takeover.ScopeName, "ItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected physical release takeover to apply release state without pressing use item.");
            }

            TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected physical release takeover restore to recover original release inputs.");
            }
        }

        private static void FlailStuckProjectileRetriesReleaseAfterPhysicalRelease()
        {
            var stuck = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                true,
                false,
                false,
                true);
            AssertFlailDecision(stuck, FlailControlStates.StuckRecoveryRelease, false, true, true, "stuckRecoveryRelease:ai0ZeroVelocity");
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
                !cached.WasUseItemHeldLastTick)
            {
                throw new InvalidOperationException("Cached flail release decision did not preserve the recorded target and release edge.");
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
            CombatAimFlailControlService.MarkProjectileAiScopedTakeover(tail, match);
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

        private static CombatAimItemCheckDecision BuildFlailItemCheckDecision(FakePlayer player)
        {
            var item = new FakeItem
            {
                type = 5526,
                stack = 1,
                Name = "Flairon",
                damage = 66,
                shoot = 1058,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            };
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
            Terraria.Main.mouseLeft = left;
            Terraria.Main.mouseLeftRelease = leftRelease;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = left;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = false;
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

        private static void SpecialProjectileRulesDistinguishWeaponAndAmmoProjectiles()
        {
            var xenopopper = ResolveSpreadRuleForTesting(2797);
            AssertPrivateStringField(xenopopper, "Kind", "cursorSpawnBurst");
            AssertPrivateStringField(xenopopper, "Name", "Xenopopper");
            AssertPrivateStringField(xenopopper, "Rule", "cursorSpawnBubbleBullet");
            AssertPrivateBoolField(xenopopper, "CursorTarget", true);
            AssertPrivateBoolField(xenopopper, "UsesWeaponShoot", true);
            AssertPrivateBoolField(xenopopper, "UsesAmmoShoot", true);

            var vortex = ResolveSpreadRuleForTesting(3475);
            AssertPrivateStringField(vortex, "Kind", "dualProjectileSpread");
            AssertPrivateStringField(vortex, "Name", "VortexBeater");
            AssertPrivateBoolField(vortex, "UsesWeaponShoot", true);
            AssertPrivateBoolField(vortex, "UsesAmmoShoot", true);

            var onyx = ResolveSpreadRuleForTesting(3788);
            AssertPrivateStringField(onyx, "Kind", "spreadMultiShot");
            AssertPrivateStringField(onyx, "Name", "OnyxBlaster");
            AssertPrivateBoolField(onyx, "UsesWeaponShoot", true);
            AssertPrivateBoolField(onyx, "UsesAmmoShoot", true);

            var player = new FakePlayer();
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 10f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                ProjectileName = "Bullet",
                AmmoProjectileType = 14,
                AmmoItemType = 97,
                AmmoItemName = "Bullet"
            };

            InvokePrivateStatic("ApplyProjectileRoleMetadata", solution, profile);
            if (solution.WeaponShootProjectileType != 444 ||
                !string.Equals(solution.ResolvedProjectileRole, "ammoProjectile", StringComparison.Ordinal) ||
                solution.PrimaryProjectileType != 14 ||
                !string.Equals(solution.PrimaryProjectileRole, "ammoPrimary", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ammo projectile to remain primary while weapon.shoot is retained.");
            }

            InvokePrivateStatic("ApplySpecialMetadata", solution, xenopopper);
            if (!solution.SpecialWeaponUsesWeaponShoot ||
                !solution.SpecialWeaponUsesAmmoShoot ||
                solution.SecondaryProjectileType != 444 ||
                !string.Equals(solution.SecondaryProjectileRole, "weaponAssist", StringComparison.Ordinal) ||
                !solution.SpecialCursorTarget)
            {
                throw new InvalidOperationException("Expected Xenopopper special metadata to retain weapon and ammo projectile roles.");
            }

            CombatAimSpecialWeaponRule resolvedRule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out resolvedRule) ||
                !string.Equals(resolvedRule.Kind, "cursorSpawnBurst", StringComparison.Ordinal) ||
                !resolvedRule.UsesWeaponProjectile ||
                !resolvedRule.UsesAmmoProjectile)
            {
                throw new InvalidOperationException("Expected special weapon rule resolver to classify Xenopopper by rule kind.");
            }
        }

        private static void SpecialDualProjectileRejectsVortexAmmoBulletScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildVortexTailDecision(player, 100f, 120f);

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    14,
                    decision.WeaponProfile,
                    decision.BallisticSolution,
                    out role) ||
                !string.Equals(role, "ammoPrimary", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Vortex ammo bullet to resolve as ammoPrimary and stay out of special scoped cursor.");
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 30, type = 14, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (ammoBullet.Matches || !string.Equals(ammoBullet.Reason, "notEligible:projectileMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Vortex ammo bullet to be rejected, got " + ammoBullet.Reason);
            }
        }

        private static void SpecialDualProjectileMatchesVortexControllerAndRocketScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildVortexTailDecision(player, 180f, 220f);

            var controller = AssertSpecialWeaponAssistMatch(
                "Vortex weapon controller",
                new FakeProjectile { whoAmI = 31, type = 615, owner = 7, active = true, friendly = false },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            AssertSpecialWeaponAssistMatch(
                "Vortex rocket assist",
                new FakeProjectile { whoAmI = 32, type = 616, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, controller, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialDualProjectile\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"dualProjectileSpread\"");
            AssertContains(json, "\"primaryProjectileRole\":\"ammoPrimary\"");
            AssertContains(json, "\"secondaryProjectileRole\":\"weaponAssist\"");
            AssertContains(json, "\"projectileCursorProjectileType\":615");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void OnyxBlasterStaysItemCheckSpreadPathWithoutSpecialScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 3788,
                stack = 1,
                Name = "Onyx Blaster",
                damage = 28,
                shoot = 661,
                shootSpeed = 14f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                Solved = true,
                Mode = "specialSpreadMultiShot",
                ProjectileType = 14,
                ProjectileName = "Bullet",
                ResolvedProjectileRole = "ammoProjectile",
                AmmoProjectileType = 14,
                AmmoProjectileName = "Bullet",
                PrimaryProjectileType = 14,
                PrimaryProjectileName = "Bullet",
                PrimaryProjectileRole = "ammoPrimary",
                WeaponShootProjectileType = 661,
                WeaponShootProjectileName = "Black Bolt",
                SecondaryProjectileType = 661,
                SecondaryProjectileName = "Black Bolt",
                SecondaryProjectileRole = "weaponAssist",
                SpecialWeaponKind = "spreadMultiShot",
                SpecialWeaponName = "OnyxBlaster",
                SpecialWeaponRule = "spreadBulletWithDarkBolt",
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            if (CombatAimPersistentCursorPolicy.IsSpecialProjectileScopedWeapon(profile))
            {
                throw new InvalidOperationException("Expected Onyx Blaster to stay out of specialProjectileWeapon policy.");
            }

            CombatAimSpecialWeaponRule scopedRule;
            if (CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out scopedRule))
            {
                throw new InvalidOperationException("Expected Onyx Blaster not to resolve as special projectile scoped weapon.");
            }

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    661,
                    profile,
                    solution,
                    out role))
            {
                throw new InvalidOperationException("Expected Onyx dark bolt to stay in ItemCheck spread path, got scoped role " + role);
            }

            var darkBolt = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 33, type = 661, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (darkBolt.Matches || !string.Equals(darkBolt.Reason, "notEligible:notSpecialProjectileWeapon", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Onyx dark bolt to be rejected by special scoped cursor, got " + darkBolt.Reason);
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 34, type = 14, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (ammoBullet.Matches)
            {
                throw new InvalidOperationException("Expected Onyx Blaster ordinary ammo bullet to stay out of special scoped cursor.");
            }

            var decision = new CombatAimItemCheckDecision
            {
                Enabled = true,
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorActive = true,
                PersistentCursorReason = "specialProjectileWeapon",
                ItemType = item.type,
                ItemName = item.Name,
                ItemStack = item.stack,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = profile,
                AimWorldX = 240f,
                AimWorldY = 260f,
                BallisticSolution = solution
            };
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, darkBolt, false, false, true);
            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"specialWeaponRuleKind\":\"spreadMultiShot\"");
            AssertContains(json, "\"primaryProjectileRole\":\"ammoPrimary\"");
            AssertContains(json, "\"secondaryProjectileRole\":\"weaponAssist\"");
            AssertContains(json, "\"projectileCursorMatchReason\":\"notEligible:notSpecialProjectileWeapon\"");
            if (json.IndexOf("\"weaponFamily\":\"SpecialDualProjectile\"", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("Expected Onyx Blaster not to report SpecialDualProjectile family.");
            }
        }

        private static void OrdinaryShotgunFamilyStaysOutOfSpecialProjectileScopedCursor()
        {
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Shotgun", 534, 14);
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Boomstick", 964, 14);
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Quad-Barrel Shotgun", 4703, 14);
        }

        private static void SpecialProjectileTailKeepsScopedAimAfterUseWindow()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var decision = new CombatAimItemCheckDecision
            {
                Enabled = true,
                AimApplyMode = CombatAimApplyModes.InstantItemCheck,
                ItemType = 2797,
                ItemName = "Xenopopper",
                ItemStack = 1,
                Damage = 45,
                Shoot = 444,
                UseAmmo = 97,
                WeaponProfile = profile,
                AimWorldX = 1234f,
                AimWorldY = 5678f,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    ProjectileType = 14,
                    AmmoProjectileType = 14,
                    WeaponShootProjectileType = 444,
                    SecondaryProjectileType = 444,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true
                }
            };

            if (!CombatAimPersistentCursorService.RememberSpecialProjectileTail(decision))
            {
                throw new InvalidOperationException("Expected special projectile tail to be remembered.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetSpecialProjectileTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetSpecialProjectileTailDecision reflection failed.");
            }

            var args = new object[] { 1L, null, null };
            var ok = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!ok || tail == null)
            {
                throw new InvalidOperationException("Expected special projectile tail decision after use window.");
            }

            if (tail.UseItemHeld ||
                !tail.UseItemReleased ||
                !string.Equals(tail.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.Ordinal) ||
                !string.Equals(tail.PersistentCursorReason, "specialProjectileWeapon", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - 1234f) > 0.001f ||
                Math.Abs(tail.AimWorldY - 5678f) > 0.001f ||
                tail.WeaponProfile == null ||
                tail.WeaponProfile.ItemType != 2797)
            {
                throw new InvalidOperationException("Special projectile tail did not preserve scoped aim metadata.");
            }
        }

        private static void SpecialProjectileTailMatchesXenopopperBubbleScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Xenopopper bubble type 444 to enter special projectile scoped cursor, got " + match.Reason);
            }

            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, match, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"cursorSpawnBurst\"");
            AssertContains(json, "\"projectileCursorProjectileType\":444");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailActive\":true");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
            AssertContains(json, "\"specialProjectileTailExpiredReason\":\"none\"");
        }

        private static void SpecialProjectileTailUsesXenopopperBubbleProjectileKillScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 2234f, 6678f);
            var hookMethod = typeof(CombatAimPersistentCursorService).GetMethod("IsSpecialProjectileTailScopedHook", BindingFlags.Static | BindingFlags.NonPublic);
            if (hookMethod == null)
            {
                throw new InvalidOperationException("IsSpecialProjectileTailScopedHook reflection failed.");
            }

            if (!(bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.ProjectileAI }) ||
                !(bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.ProjectileKill }) ||
                (bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.MainUpdateFallback }))
            {
                throw new InvalidOperationException("Expected special projectile tail to be scoped only to Projectile.AI and Projectile.Kill.");
            }

            var guardMethod = typeof(CombatAimPersistentCursorService).GetMethod("ShouldAttemptSpecialProjectileTailOverride", BindingFlags.Static | BindingFlags.NonPublic);
            if (guardMethod == null)
            {
                throw new InvalidOperationException("ShouldAttemptSpecialProjectileTailOverride reflection failed.");
            }

            var projectile = new FakeProjectile { whoAmI = 22, type = 444, owner = 7, active = true, friendly = true };
            if (!(bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.ProjectileAI }) ||
                !(bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.ProjectileKill }) ||
                (bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.MainUpdateFallback }) ||
                (bool)guardMethod.Invoke(null, new object[] { null, PersistentCursorHooks.ProjectileKill }))
            {
                throw new InvalidOperationException("Expected special projectile tail begin guard to include Projectile.Kill and reject fallback/null scopes.");
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                projectile,
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!match.Matches)
            {
                throw new InvalidOperationException("Expected Xenopopper bubble type 444 to match Projectile.Kill tail scope, got " + match.Reason);
            }

            decision.PersistentHook = PersistentCursorHooks.ProjectileKill;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, match, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"persistentHook\":\"ProjectileKill\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\":false");
            AssertContains(json, "\"projectileCursorProjectileType\":444");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailActive\":true");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void SpecialProjectileTailActiveBubbleRefreshesFixedTailWindow()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var rememberMethod = typeof(CombatAimPersistentCursorService).GetMethod(
                "RememberSpecialProjectileTail",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(CombatAimItemCheckDecision), typeof(long) },
                null);
            var getMethod = typeof(CombatAimPersistentCursorService).GetMethod("TryGetSpecialProjectileTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            var refreshMethod = typeof(CombatAimPersistentCursorService).GetMethod("RefreshSpecialProjectileTailLease", BindingFlags.Static | BindingFlags.NonPublic);
            if (rememberMethod == null || getMethod == null || refreshMethod == null)
            {
                throw new InvalidOperationException("Special projectile tail lease reflection failed.");
            }

            rememberMethod.Invoke(null, new object[] { decision, 100L });

            var beforeRefreshArgs = new object[] { 219L, null, null };
            var beforeRefreshOk = (bool)getMethod.Invoke(null, beforeRefreshArgs);
            if (!beforeRefreshOk)
            {
                throw new InvalidOperationException("Expected Xenopopper tail to still exist before the original fixed tail window ends.");
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 24, type = 444, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!match.Matches)
            {
                throw new InvalidOperationException("Expected local active Xenopopper bubble to refresh the tail lease, got " + match.Reason);
            }

            refreshMethod.Invoke(null, new object[] { match, 219L });

            var refreshedArgs = new object[] { 339L, null, null };
            var refreshedOk = (bool)getMethod.Invoke(null, refreshedArgs);
            if (!refreshedOk)
            {
                throw new InvalidOperationException("Expected active Xenopopper bubble to keep special projectile tail alive beyond the original fixed window.");
            }

            var expiredArgs = new object[] { 340L, null, null };
            var expiredOk = (bool)getMethod.Invoke(null, expiredArgs);
            if (expiredOk)
            {
                throw new InvalidOperationException("Expected refreshed Xenopopper tail to expire after the active-projectile lease window.");
            }
        }

        private static void SpecialProjectileTailUsesRecomputedAimAfterTargetMoves()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var stale = BuildXenopopperTailDecision(player, 100f, 120f);
            var recomputed = BuildXenopopperTailDecision(player, 220f, 260f);
            recomputed.Selection.Target.CenterX = 220f;
            recomputed.Selection.Target.CenterY = 260f;

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryChooseSpecialProjectileTailAimDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryChooseSpecialProjectileTailAimDecision reflection failed.");
            }

            var args = new object[] { stale, recomputed, true, false, "targetMovedWithoutRecompute", null, false, null };
            var ok = (bool)method.Invoke(null, args);
            var selected = args[5] as CombatAimItemCheckDecision;
            var recomputedAim = args[6] is bool && (bool)args[6];
            var expiredReason = args[7] as string;
            if (!ok ||
                !ReferenceEquals(selected, recomputed) ||
                !recomputedAim ||
                !string.Equals(expiredReason, "none", StringComparison.Ordinal) ||
                Math.Abs(selected.AimWorldX - stale.AimWorldX) < 0.001f ||
                Math.Abs(selected.AimWorldY - stale.AimWorldY) < 0.001f)
            {
                throw new InvalidOperationException("Expected special projectile tail to prefer recomputed aim after target movement.");
            }

            args = new object[] { stale, null, false, false, "targetMovedWithoutRecompute", null, false, null };
            ok = (bool)method.Invoke(null, args);
            expiredReason = args[7] as string;
            if (ok || !string.Equals(expiredReason, "targetMovedWithoutRecompute", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected moved target without recompute to expire instead of reusing stale aim.");
            }
        }

        private static void SpecialDualProjectileTailRecomputesAimForMovingAssistTarget()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var stale = BuildVortexTailDecision(player, 100f, 120f);
            var recomputed = BuildVortexTailDecision(player, 260f, 300f);
            recomputed.Selection.Target.CenterX = 260f;
            recomputed.Selection.Target.CenterY = 300f;

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryChooseSpecialProjectileTailAimDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryChooseSpecialProjectileTailAimDecision reflection failed.");
            }

            var controller = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 35, type = 615, owner = 7, active = true, friendly = false },
                player,
                recomputed.WeaponProfile,
                recomputed.BallisticSolution);
            if (!controller.Matches)
            {
                throw new InvalidOperationException("Expected Vortex controller projectile to match before recompute assertion, got " + controller.Reason);
            }

            var args = new object[] { stale, recomputed, true, false, "targetMovedWithoutRecompute", null, false, null };
            var ok = (bool)method.Invoke(null, args);
            var selected = args[5] as CombatAimItemCheckDecision;
            var recomputedAim = args[6] is bool && (bool)args[6];
            var expiredReason = args[7] as string;
            if (!ok ||
                !ReferenceEquals(selected, recomputed) ||
                !recomputedAim ||
                !string.Equals(expiredReason, "none", StringComparison.Ordinal) ||
                Math.Abs(selected.AimWorldX - stale.AimWorldX) < 0.001f ||
                Math.Abs(selected.AimWorldY - stale.AimWorldY) < 0.001f)
            {
                throw new InvalidOperationException("Expected Vortex controller scoped tail to prefer recomputed aim after target movement.");
            }

            selected.PersistentHook = PersistentCursorHooks.ProjectileAI;
            selected.PersistentCursorActive = true;
            selected.PersistentCursorReason = "specialProjectileWeapon";
            selected.SpecialProjectileTailActive = true;
            selected.SpecialProjectileTailRecomputedAim = recomputedAim;
            selected.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(selected, controller, true, true, true);

            var json = BuildCombatAimDecisionJson(selected, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialDualProjectile\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"dualProjectileSpread\"");
            AssertContains(json, "\"projectileCursorProjectileType\":615");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void SpecialProjectileTailExpiresInactiveBubbleAndIgnoresAmmoBullet()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var inactiveBubble = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = false, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            var method = typeof(CombatAimPersistentCursorService).GetMethod("ShouldExpireSpecialProjectileTailForMatchFailure", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("ShouldExpireSpecialProjectileTailForMatchFailure reflection failed.");
            }

            var args = new object[] { decision, inactiveBubble, null };
            var shouldExpire = (bool)method.Invoke(null, args);
            var expiredReason = args[2] as string;
            if (!shouldExpire || !string.Equals(expiredReason, "projectileInactive", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected inactive Xenopopper bubble to expire the special projectile tail.");
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 21, type = 14, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (ammoBullet.Matches || !string.Equals(ammoBullet.Reason, "notEligible:projectileMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ordinary ammo bullet to stay out of Xenopopper bubble tail, got " + ammoBullet.Reason);
            }

            args = new object[] { decision, ammoBullet, null };
            shouldExpire = (bool)method.Invoke(null, args);
            if (shouldExpire)
            {
                throw new InvalidOperationException("Expected ordinary ammo bullet mismatch not to expire Xenopopper bubble tail.");
            }
        }

        private static void CombatAimItemCheckLogThrottleKeepsIndependentKeys()
        {
            CombatAimItemCheckService.ResetLogThrottleForTesting();
            var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now))
            {
                throw new InvalidOperationException("Expected first instant no-target log key to record.");
            }

            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("PersistentCursor:noTarget:5526", now.AddMilliseconds(20)))
            {
                throw new InvalidOperationException("Expected first persistent no-target log key to record independently.");
            }

            if (CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now.AddSeconds(1)))
            {
                throw new InvalidOperationException("Expected instant no-target log key to stay throttled even after an alternating key.");
            }

            if (CombatAimItemCheckService.ShouldRecordLogForTesting("PersistentCursor:noTarget:5526", now.AddSeconds(1)))
            {
                throw new InvalidOperationException("Expected persistent no-target log key to stay throttled even after an alternating key.");
            }

            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now.AddSeconds(6)))
            {
                throw new InvalidOperationException("Expected itemcheck log key to record after the throttle interval.");
            }
        }

        private static CombatAimProjectileCursorMatch AssertSpecialWeaponAssistMatch(
            string label,
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            string role;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    projectile.type,
                    profile,
                    solution,
                    out role) ||
                !string.Equals(role, "weaponAssist", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " to resolve as weaponAssist scoped projectile, got " + role);
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                projectile,
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " to match special scoped cursor, got " + match.Reason);
            }

            return match;
        }

        private static void AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor(
            string name,
            int itemType,
            int shoot)
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = itemType,
                stack = 1,
                Name = name,
                damage = 24,
                shoot = shoot,
                shootSpeed = 9f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);

            if (CombatAimPersistentCursorPolicy.IsSpecialProjectileScopedWeapon(profile))
            {
                throw new InvalidOperationException("Expected " + name + " to stay out of specialProjectileWeapon policy.");
            }

            CombatAimSpecialWeaponRule rule;
            if (CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out rule))
            {
                throw new InvalidOperationException("Expected " + name + " not to resolve as special projectile scoped weapon.");
            }

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    14,
                    profile,
                    new CombatAimBallisticSolution
                    {
                        ProjectileType = 14,
                        AmmoProjectileType = 14,
                        PrimaryProjectileType = 14,
                        PrimaryProjectileRole = "ammoPrimary"
                    },
                    out role))
            {
                throw new InvalidOperationException("Expected " + name + " ammo projectile not to resolve as scoped special projectile.");
            }
        }

        private static object ResolveSpreadRuleForTesting(int itemType)
        {
            var method = typeof(CombatAimBallisticSolver).GetMethod("TryResolveSpreadMultiShotRule", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryResolveSpreadMultiShotRule not found.");
            }

            var args = new object[] { itemType, null };
            var matched = (bool)method.Invoke(null, args);
            if (!matched || args[1] == null)
            {
                throw new InvalidOperationException("Expected special spread rule for item " + itemType + ".");
            }

            return args[1];
        }

        private static void InvokePrivateStatic(string name, params object[] args)
        {
            var method = typeof(CombatAimBallisticSolver).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException(name + " not found.");
            }

            method.Invoke(null, args);
        }

        private static void AssertPrivateStringField(object instance, string fieldName, string expected)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var actual = field == null ? null : field.GetValue(instance) as string;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + fieldName + "=" + expected + ", got " + actual);
            }
        }

        private static void AssertPrivateBoolField(object instance, string fieldName, bool expected)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var actual = field != null && field.GetValue(instance) is bool && (bool)field.GetValue(instance);
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + fieldName + "=" + expected + ", got " + actual);
            }
        }

        private static void ReleaseHoldTargetDummyValidationRespectsTrackDummy()
        {
            var dummy = new CombatTargetSnapshot
            {
                Active = true,
                IsTargetDummy = true,
                Friendly = true,
                TownNpc = true
            };

            string reason;
            if (!CombatAimReleaseHoldService.IsTargetValidForReleaseHold(dummy, true, out reason) ||
                !string.Equals(reason, "targetDummyAllowed", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected target dummy to be valid when TrackDummy is enabled, got " + reason);
            }

            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(dummy, false, out reason) ||
                !string.Equals(reason, "targetDummyDisabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected target dummy to be invalid when TrackDummy is disabled, got " + reason);
            }

            var friendly = new CombatTargetSnapshot { Active = true, Life = 100, Friendly = true };
            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(friendly, true, out reason) ||
                !string.Equals(reason, "friendly", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected friendly NPC to remain invalid, got " + reason);
            }

            var townNpc = new CombatTargetSnapshot { Active = true, Life = 100, TownNpc = true };
            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(townNpc, true, out reason) ||
                !string.Equals(reason, "townNpc", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected town NPC to remain invalid, got " + reason);
            }
        }

        private static CombatAimItemCheckDecision BuildCombatAimDiagnosticDecision()
        {
            var item = new FakeItem
            {
                type = 99,
                stack = 1,
                Name = "Diagnostic Bow",
                damage = 17,
                shoot = 1,
                shootSpeed = 8.5f,
                useAmmo = 1,
                useStyle = 5,
                useTime = 20,
                useAnimation = 20,
                ranged = true
            };
            var player = new FakePlayer();
            player.inventory[0] = item;

            var selection = new CombatAimTargetSelection
            {
                Target = new CombatTargetSnapshot
                {
                    WhoAmI = 3,
                    Type = 4,
                    Name = "Diagnostic Target",
                    CenterX = 120f,
                    CenterY = 140f,
                    HitboxX = 100f,
                    HitboxY = 120f,
                    HitboxWidth = 40f,
                    HitboxHeight = 40f
                },
                SelectedSamplePoint = "center",
                AttackSamplePoint = "center",
                SelectionSamplePoint = "center",
                SelectedSampleWorldX = 120f,
                SelectedSampleWorldY = 140f,
                LineClear = true,
                LineClearAvailable = true,
                MarkerTargetWhoAmI = 2,
                AttackTargetWhoAmI = 3,
                MarkerAttackTargetMismatch = true,
                SelectionPurpose = "Attack"
            };

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = "ProjectileAI",
                PersistentCursorReason = "diagnostic",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                Selection = selection,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "linearBasic",
                    ProjectileType = 12,
                    ProjectileName = "Diagnostic Projectile",
                    ProjectileAiStyle = 1,
                    ProjectileExtraUpdates = 1,
                    ProjectileDefaultsAvailable = true,
                    ProjectileTileCollide = true,
                    ProjectileWidth = 10,
                    ProjectileHeight = 12,
                    ProjectileFriendly = true,
                    AmmoAvailable = true,
                    AmmoItemType = 40,
                    AmmoItemName = "Diagnostic Arrow",
                    AmmoProjectileType = 12,
                    AmmoShootSpeed = 2f,
                    ProjectileSpeed = 10.5f
                }
            };
        }

        private static CombatAimItemCheckDecision BuildXenopopperTailDecision(FakePlayer player, float aimWorldX, float aimWorldY)
        {
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorReason = "specialProjectileWeapon",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                AimWorldX = aimWorldX,
                AimWorldY = aimWorldY,
                Selection = new CombatAimTargetSelection
                {
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 42,
                        Type = 245,
                        Name = "Xenopopper Target",
                        Active = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = aimWorldX,
                        CenterY = aimWorldY,
                        HitboxX = aimWorldX - 20f,
                        HitboxY = aimWorldY - 20f,
                        HitboxWidth = 40f,
                        HitboxHeight = 40f
                    },
                    SelectedSampleWorldX = aimWorldX,
                    SelectedSampleWorldY = aimWorldY,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    AttackTargetWhoAmI = 42,
                    AttackTargetType = 245,
                    LineClear = true,
                    LineClearAvailable = true,
                    SelectionPurpose = "PersistentCursor"
                },
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "specialCursorSpawnBurst",
                    ProjectileType = 14,
                    ProjectileName = "Bullet",
                    AmmoProjectileType = 14,
                    AmmoProjectileName = "Bullet",
                    PrimaryProjectileType = 14,
                    PrimaryProjectileName = "Bullet",
                    PrimaryProjectileRole = "ammoPrimary",
                    AmmoItemType = 97,
                    AmmoItemName = "Bullet",
                    WeaponShootProjectileType = 444,
                    WeaponShootProjectileName = "Xenopopper Bubble",
                    SecondaryProjectileType = 444,
                    SecondaryProjectileName = "Xenopopper Bubble",
                    SecondaryProjectileRole = "weaponAssist",
                    SpecialWeaponKind = "cursorSpawnBurst",
                    SpecialWeaponName = "Xenopopper",
                    SpecialWeaponRule = "cursorSpawnBubbleBullet",
                    SpecialCursorTarget = true,
                    SpecialAimApplied = true,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true,
                    ProjectileSpeed = 24f,
                    AimWorldX = aimWorldX,
                    AimWorldY = aimWorldY
                }
            };
        }

        private static CombatAimItemCheckDecision BuildVortexTailDecision(FakePlayer player, float aimWorldX, float aimWorldY)
        {
            var item = new FakeItem
            {
                type = 3475,
                stack = 1,
                Name = "VortexBeater",
                damage = 50,
                shoot = 615,
                shootSpeed = 14f,
                useAmmo = 97,
                ranged = true,
                channel = true,
                useStyle = 5
            };
            player.inventory[0] = item;

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorReason = "specialProjectileWeapon",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                AimWorldX = aimWorldX,
                AimWorldY = aimWorldY,
                Selection = new CombatAimTargetSelection
                {
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 43,
                        Type = 245,
                        Name = "Vortex Target",
                        Active = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = aimWorldX,
                        CenterY = aimWorldY,
                        HitboxX = aimWorldX - 20f,
                        HitboxY = aimWorldY - 20f,
                        HitboxWidth = 40f,
                        HitboxHeight = 40f
                    },
                    SelectedSampleWorldX = aimWorldX,
                    SelectedSampleWorldY = aimWorldY,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    AttackTargetWhoAmI = 43,
                    AttackTargetType = 245,
                    LineClear = true,
                    LineClearAvailable = true,
                    SelectionPurpose = "PersistentCursor"
                },
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "specialSpreadMultiShot",
                    ProjectileType = 14,
                    ProjectileName = "Bullet",
                    ResolvedProjectileRole = "ammoProjectile",
                    AmmoProjectileType = 14,
                    AmmoProjectileName = "Bullet",
                    PrimaryProjectileType = 14,
                    PrimaryProjectileName = "Bullet",
                    PrimaryProjectileRole = "ammoPrimary",
                    AmmoItemType = 97,
                    AmmoItemName = "Bullet",
                    WeaponShootProjectileType = 615,
                    WeaponShootProjectileName = "Vortex Beater",
                    SecondaryProjectileType = 615,
                    SecondaryProjectileName = "Vortex Beater",
                    SecondaryProjectileRole = "weaponAssist",
                    SpecialWeaponKind = "dualProjectileSpread",
                    SpecialWeaponName = "VortexBeater",
                    SpecialWeaponRule = "bulletSpreadWithRocketAssist",
                    SpecialAimApplied = true,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true,
                    ProjectileSpeed = 14f,
                    AimWorldX = aimWorldX,
                    AimWorldY = aimWorldY
                }
            };
        }

        private static string BuildCombatAimDecisionJson(CombatAimItemCheckDecision decision, bool mouseOverrideApplied, bool restored)
        {
            var method = typeof(CombatAimItemCheckService).GetMethod("BuildDecisionJson", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("BuildDecisionJson not found.");
            }

            return (string)method.Invoke(null, new object[] { decision, mouseOverrideApplied, restored });
        }

        private static void AssertCombatAimSkipReason(string raw, string expected)
        {
            var method = typeof(CombatAimItemCheckService).GetMethod("NormalizeSkipReason", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NormalizeSkipReason not found.");
            }

            var actual = (string)method.Invoke(null, new object[] { new CombatAimItemCheckDecision(), raw });
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + raw + " -> " + expected + ", got " + actual);
            }
        }

        private static CombatAimWeaponProfile BuildCombatAimWeaponProfile(FakeItem item)
        {
            var player = new FakePlayer();
            player.inventory[0] = item;
            return CombatAimWeaponProfile.Read(player, item);
        }

        private static void AssertWeaponFamily(
            string label,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedFamily)
        {
            var result = CombatAimWeaponFamilyResolver.Resolve(profile, solution);
            if (result == null ||
                !string.Equals(result.Family, expectedFamily, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.Reason))
            {
                throw new InvalidOperationException(
                    "Expected " + label + " family " + expectedFamily +
                    ", got " + (result == null ? "<null>" : result.Family + " / " + result.Reason));
            }
        }

        private static void AssertPersistentCursorEligibility(
            CombatAimPersistentCursorEligibility eligibility,
            bool expectedEligible,
            string expectedReason,
            string expectedClass)
        {
            if (eligibility == null)
            {
                throw new InvalidOperationException("Expected persistent cursor eligibility result.");
            }

            if (eligibility.Eligible != expectedEligible ||
                !string.Equals(eligibility.Reason, expectedReason, StringComparison.Ordinal) ||
                !string.Equals(eligibility.Class, expectedClass, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected eligibility " + expectedEligible + " / " + expectedReason + " / " + expectedClass +
                    ", got " + eligibility.Eligible + " / " + eligibility.Reason + " / " + eligibility.Class);
            }
        }

        private static void InputActionQueueReleasesChannelAfterTerminalStart()
        {
            var queue = new InputActionQueue();
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                SourceFeatureId = "test.terminal",
                Description = "unrecognized raw input terminal"
            });
            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.IsNullOrWhiteSpace(snapshot.RunningActionKind))
            {
                throw new InvalidOperationException("Expected terminal start path to release channel lease.");
            }
        }

        private static void InputActionQueueReleasesChannelAfterStartException()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new ThrowingStartFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.start_exception",
                Description = "throwing start action"
            });

            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.IsNullOrWhiteSpace(snapshot.RunningActionKind) ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.Failed)
            {
                throw new InvalidOperationException("Expected start exception path to release channel lease and record failed result.");
            }
        }

        private static void InputActionQueueReleasesChannelAfterCancel()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.cancel",
                Description = "fake running action"
            });

            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 1)
            {
                throw new InvalidOperationException("Expected running fake action to hold one channel lease.");
            }

            queue.CancelBySource("test.cancel");
            snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.IsNullOrWhiteSpace(snapshot.RunningActionKind))
            {
                throw new InvalidOperationException("Expected cancelled running action to release channel lease.");
            }
        }

        private static void InputActionQueueClearReleasesPendingAndRunningLeases()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new RunningFakeExecutor(InputActionKind.MouseTarget);
            var queue = new InputActionQueue(executors);
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.clear.running"
            });
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                SourceFeatureId = "test.clear.pending"
            });
            queue.Update(null);

            var before = queue.GetSnapshot();
            if (before.ActionQueueChannelLeaseCount != 1 || before.PendingCount != 1)
            {
                throw new InvalidOperationException("Expected setup to include one running lease and one pending request.");
            }

            queue.Clear();
            var after = queue.GetSnapshot();
            if (after.ActionQueueChannelLeaseCount != 0 ||
                after.PendingCount != 0 ||
                !string.IsNullOrWhiteSpace(after.RunningActionKind))
            {
                throw new InvalidOperationException("Expected Clear to release running lease and remove pending requests.");
            }
        }

        private static void DiagnosticNoopDoesNotAcquireChannelLease()
        {
            var queue = new InputActionQueue();
            queue.Enqueue(InputActionRequest.CreateDiagnosticNoop("test.noop", "diagnostic noop"));
            queue.Update(null);

            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.Equals(snapshot.ActionQueueRunningLeaseChannels, "None", StringComparison.Ordinal) ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.Succeeded)
            {
                throw new InvalidOperationException("Expected DiagnosticNoop to complete without acquiring a channel lease.");
            }
        }
    }
}
