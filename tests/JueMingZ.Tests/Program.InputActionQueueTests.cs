using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Movement;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Input;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
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
                Description = "normal admission",
                AdmissionKey = "try-enqueue-normal",
                Metadata = { { ActionMetadataKeys.Scenario, "Test.TryEnqueue.Normal" } }
            }, out admission))
            {
                throw new InvalidOperationException("Expected TryEnqueue to accept a normal request: " + (admission == null ? "null" : admission.Reason));
            }

            var snapshot = queue.GetSnapshot();
            if (snapshot.PendingCount != 1 ||
                !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Accepted", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionKind, "MouseTarget", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionSource, "test.try_enqueue", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionScenario, "Test.TryEnqueue.Normal", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionKey, "try-enqueue-normal", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(snapshot.ActionQueueLastAdmissionRequiredChannels))
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

        private static void TryEnqueueSupersedesPendingUserRequest()
        {
            var queue = new InputActionQueue();
            var first = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                DuplicatePolicy = InputActionDuplicatePolicy.SupersedePending,
                SourceFeatureId = FeatureIds.InventoryQuickItemHotkeys,
                AdmissionKey = "quick-hotkey|F1|1",
                Metadata = { { ActionMetadataKeys.Scenario, "Hotkey.QuickItemHotkeys" } }
            };
            var second = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                DuplicatePolicy = InputActionDuplicatePolicy.SupersedePending,
                SourceFeatureId = FeatureIds.InventoryQuickItemHotkeys,
                AdmissionKey = "quick-hotkey|F1|1",
                Metadata = { { ActionMetadataKeys.Scenario, "Hotkey.QuickItemHotkeys" } }
            };

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(first, out admission))
            {
                throw new InvalidOperationException("Expected first user request to be accepted.");
            }

            if (!queue.TryEnqueue(second, out admission))
            {
                throw new InvalidOperationException("Expected second user request to supersede pending request: " + (admission == null ? "null" : admission.Reason));
            }

            var snapshot = queue.GetSnapshot();
            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.SupersededPending ||
                admission.SupersededRequestId != first.RequestId ||
                snapshot.PendingCount != 1 ||
                snapshot.LastResult == null ||
                snapshot.LastResult.RequestId != first.RequestId ||
                snapshot.LastResult.Status != InputActionStatus.Cancelled ||
                snapshot.ActionQueueSupersededPendingCount != 1 ||
                !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Superseded", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected superseded pending request to record terminal result and snapshot state.");
            }
        }

        private static void TryEnqueueCoalescesPendingBackgroundRequest()
        {
            var queue = new InputActionQueue();
            var first = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.InventoryAutoStack,
                AdmissionKey = FeatureIds.InventoryAutoStack,
                QueueTimeout = TimeSpan.FromSeconds(2),
                Metadata =
                {
                    { ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoStack },
                    { "InventorySignature", "old" }
                }
            };
            var second = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.InventoryAutoStack,
                AdmissionKey = FeatureIds.InventoryAutoStack,
                QueueTimeout = TimeSpan.FromSeconds(2),
                Metadata =
                {
                    { ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoStack },
                    { "InventorySignature", "new" }
                }
            };

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(first, out admission))
            {
                throw new InvalidOperationException("Expected first background request to be accepted.");
            }

            if (!queue.TryEnqueue(second, out admission))
            {
                throw new InvalidOperationException("Expected second background request to coalesce pending request: " + (admission == null ? "null" : admission.Reason));
            }

            var pending = queue.GetPendingRequestsForTesting();
            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.CoalescedPending ||
                admission.CoalescedRequestId != first.RequestId ||
                queue.GetSnapshot().PendingCount != 1 ||
                pending.Count != 1 ||
                pending[0].RequestId != first.RequestId ||
                !string.Equals(pending[0].Metadata["InventorySignature"], "new", StringComparison.Ordinal) ||
                !string.Equals(pending[0].Metadata["AdmissionCoalescedCount"], "1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected coalesced pending request to keep one updated pending request.");
            }
        }

        private static void TryEnqueueUserRequestSupersedesBackgroundPending()
        {
            var queue = new InputActionQueue();
            var background = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoHarvest,
                AdmissionKey = FeatureIds.WorldAutomationAutoHarvest + ".harvest.sustained",
                QueueTimeout = TimeSpan.FromMilliseconds(250),
                Metadata =
                {
                    { ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvest },
                    { ActionMetadataKeys.RawInputMode, "AutoHarvestSustainedUse" },
                    { ActionMetadataKeys.SourceKind, "Automation" }
                }
            };
            var user = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.InventoryQuickItemHotkeys,
                AdmissionKey = "quick-hotkey|F2|1326",
                QueueTimeout = TimeSpan.FromMilliseconds(400),
                Metadata =
                {
                    { ActionMetadataKeys.Scenario, "Hotkey.QuickItemHotkeys" },
                    { ActionMetadataKeys.SourceKind, "Hotkey" },
                    { ActionMetadataKeys.TargetSlot, "1" }
                }
            };

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(background, out admission))
            {
                throw new InvalidOperationException("Expected background pending request to be accepted.");
            }

            if (!queue.TryEnqueue(user, out admission))
            {
                throw new InvalidOperationException("Expected user request to supersede background pending: " + (admission == null ? "null" : admission.Reason));
            }

            var pending = queue.GetPendingRequestsForTesting();
            var snapshot = queue.GetSnapshot();
            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.SupersededPending ||
                admission.SupersededRequestId != background.RequestId ||
                pending.Count != 1 ||
                pending[0].RequestId != user.RequestId ||
                snapshot.LastResult == null ||
                snapshot.LastResult.RequestId != background.RequestId ||
                snapshot.ActionQueueSupersededPendingCount != 1 ||
                snapshot.SchedulerLastSupersededRequest.IndexOf(FeatureIds.WorldAutomationAutoHarvest, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected user command to cancel conflicting background pending request with diagnostics.");
            }
        }

        private static void InputActionQueueFindsTerminalResultByRequestId()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.Chest] = new TerminalFakeExecutor(
                InputActionKind.Chest,
                InputActionStatus.AttemptedButUnverified,
                "quick stack invoked but not verified");
            var queue = new InputActionQueue(executors);
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                SourceFeatureId = FeatureIds.InventoryAutoStack,
                AdmissionKey = FeatureIds.InventoryAutoStack,
                Metadata = { { ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoStack } }
            };

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                throw new InvalidOperationException("Expected request to be accepted.");
            }

            queue.Update(null);

            InputActionResult result;
            if (!queue.TryGetResultByRequestId(request.RequestId, out result) ||
                result == null ||
                result.RequestId != request.RequestId ||
                result.Status != InputActionStatus.AttemptedButUnverified ||
                result.Message.IndexOf("not verified", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Expected queue to return terminal result by request id.");
            }
        }

        private static void CleanupLeaseBlocksSameResourceAdmission()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new TerminalFakeExecutor(
                InputActionKind.MouseTarget,
                InputActionStatus.AttemptedButUnverified,
                "restore unverified");
            var queue = new InputActionQueue(executors);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.cleanup.seed",
                AdmissionKey = "cleanup-seed"
            }, out admission))
            {
                throw new InvalidOperationException("Expected cleanup seed request to be accepted.");
            }

            queue.Update(null);
            if (!queue.IsAnyChannelBusy(InputActionChannel.MouseTarget))
            {
                throw new InvalidOperationException("Expected attempted-but-unverified result to hold cleanup lease.");
            }

            if (queue.TryEnqueue(new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.cleanup.next",
                AdmissionKey = "cleanup-next"
            }, out admission))
            {
                throw new InvalidOperationException("Expected cleanup lease to block same-resource admission.");
            }

            var snapshot = queue.GetSnapshot();
            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.DeniedCleanupLease ||
                snapshot.ActionQueueCleanupLeaseCount != 1 ||
                string.IsNullOrWhiteSpace(snapshot.ActionQueueLastCleanupOwner) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionDecision, "DeniedCleanupLease", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected cleanup lease denial to be visible in admission snapshot.");
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

        private static void TryEnqueueSnapshotRecordsDeniedAdmissionDetails()
        {
            var bridgeRequestId = Guid.NewGuid();
            string bridgeMessage;
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                bridgeRequestId,
                "test.bridge_denied_snapshot",
                0,
                1,
                1,
                "Test Item",
                TimeSpan.FromSeconds(30),
                0,
                InputActionKind.ItemUse,
                "Test.BridgeDeniedSnapshot",
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
                    SourceFeatureId = "test.denied_snapshot",
                    Description = "denied snapshot",
                    AdmissionKey = "denied-snapshot",
                    Metadata = { { ActionMetadataKeys.Scenario, "Test.TryEnqueue.DeniedSnapshot" } }
                }, out admission))
                {
                    throw new InvalidOperationException("Expected ItemUse request to be rejected while bridge is busy.");
                }

                var snapshot = queue.GetSnapshot();
                if (snapshot.PendingCount != 0 ||
                    !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Denied", StringComparison.Ordinal) ||
                    !string.Equals(snapshot.ActionQueueLastAdmissionKind, "ItemUse", StringComparison.Ordinal) ||
                    !string.Equals(snapshot.ActionQueueLastAdmissionSource, "test.denied_snapshot", StringComparison.Ordinal) ||
                    !string.Equals(snapshot.ActionQueueLastAdmissionScenario, "Test.TryEnqueue.DeniedSnapshot", StringComparison.Ordinal) ||
                    !string.Equals(snapshot.ActionQueueLastAdmissionKey, "denied-snapshot", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(snapshot.ActionQueueLastAdmissionBlockingChannels) ||
                    string.IsNullOrWhiteSpace(snapshot.ActionQueueLastAdmissionBridgeBusySummary))
                {
                    throw new InvalidOperationException("Expected denied admission details to remain visible in snapshot.");
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

        private static void LegacyEnqueueRecordsDirectEntryDiagnostics()
        {
            var queue = new InputActionQueue();
            queue.Enqueue(new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                SourceFeatureId = "test.legacy.direct",
                AdmissionKey = "legacy-direct",
                Metadata = { { ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoStack } }
            });

            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueDirectEnqueueCount != 1 ||
                !string.Equals(snapshot.ActionQueueLastDirectEnqueueKind, "Chest", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastDirectEnqueueSource, "test.legacy.direct", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastDirectEnqueueScenario, ScenarioNames.InventoryAutoStack, StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastDirectEnqueueAdmissionKey, "legacy-direct", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(snapshot.ActionQueueLastDirectEnqueueRequiredChannels))
            {
                throw new InvalidOperationException("Expected legacy Enqueue diagnostics to record the latest direct entry.");
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

        private static void SchedulerPrefersUserBucketOverEarlierBackground()
        {
            var earlyBackground = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.WorldAutomationAutoHarvest,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Metadata =
                {
                    { ActionMetadataKeys.SourceKind, "Automation" },
                    { ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvest }
                }
            };
            var laterUser = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.InventoryQuickItemHotkeys,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc),
                Metadata =
                {
                    { ActionMetadataKeys.SourceKind, "Hotkey" },
                    { ActionMetadataKeys.Scenario, "Hotkey.QuickItemHotkeys" }
                }
            };

            var selected = InputActionScheduler.SelectNext(new[] { earlyBackground, laterUser });
            if (!object.ReferenceEquals(selected, laterUser) ||
                !string.Equals(InputActionScheduler.ResolveBucketName(laterUser), "P2:UserExplicitCommand", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected scheduler bucket ordering to prefer explicit user commands over earlier background automation at the same priority.");
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

        private static void DiagnosticActionRequestHasAdmissionContract()
        {
            var request = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(
                InputActionKind.QuickHeal,
                "Button.QuickHeal",
                DiagnosticActionSource.ForButton("diagnostics.quick_heal", "QuickHeal 回血"));

            AssertPositiveQueueTimeout(request, "diagnostic action");
            if (request.QueueTimeout > TimeSpan.FromMilliseconds(500) ||
                request.DuplicatePolicy != InputActionDuplicatePolicy.CoalescePending ||
                string.IsNullOrWhiteSpace(request.AdmissionKey) ||
                !string.Equals(request.Metadata[ActionMetadataKeys.SourceKind], "Button", StringComparison.Ordinal) ||
                !string.Equals(request.Metadata[ActionMetadataKeys.Scenario], "Button.QuickHeal", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected diagnostic action request to carry a short queue timeout, coalesce policy, stable key, and source metadata.");
            }
        }

        private static void RepeatedDiagnosticActionCoalescesPending()
        {
            var queue = new InputActionQueue();
            var source = DiagnosticActionSource.ForButton("diagnostics.quick_heal", "QuickHeal 回血");
            var first = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(InputActionKind.QuickHeal, "Button.QuickHeal", source);
            var second = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(InputActionKind.QuickHeal, "Button.QuickHeal", source);

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(first, out admission))
            {
                throw new InvalidOperationException("Expected first diagnostic action to be admitted.");
            }

            if (!queue.TryEnqueue(second, out admission))
            {
                throw new InvalidOperationException("Expected repeated diagnostic action to coalesce pending request: " + (admission == null ? "null" : admission.Reason));
            }

            var pending = queue.GetPendingRequestsForTesting();
            var snapshot = queue.GetSnapshot();
            if (admission == null ||
                admission.Decision != InputActionAdmissionDecision.CoalescedPending ||
                pending.Count != 1 ||
                pending[0].RequestId != first.RequestId ||
                !string.Equals(pending[0].Metadata["AdmissionCoalescedCount"], "1", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Coalesced", StringComparison.Ordinal) ||
                snapshot.ActionQueueCoalescedPendingCount != 1)
            {
                throw new InvalidOperationException("Expected repeated diagnostic action to update one pending request and expose coalesced admission diagnostics.");
            }
        }

        private static void DiagnosticAdmissionDeniedFeedbackIncludesSummary()
        {
            var request = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(
                InputActionKind.ItemUse,
                "Button.UseSelectedItem",
                DiagnosticActionSource.ForButton("diagnostics.use_selected", "使用手上物品"));
            var admission = new InputActionAdmissionResult
            {
                Accepted = false,
                Decision = InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning,
                Kind = request.Kind,
                SourceFeatureId = request.SourceFeatureId,
                AdmissionKey = request.AdmissionKey,
                RequiredChannels = InputActionChannel.UseItem,
                BlockingChannels = InputActionChannel.UseItem,
                Reason = "duplicateRunning:test"
            };

            var feedback = DiagnosticActionDispatcher.BuildAdmissionFeedbackForTesting("使用手上物品", request, admission, false);
            if (feedback.IndexOf("动作未提交", StringComparison.Ordinal) < 0 ||
                feedback.IndexOf("Denied", StringComparison.Ordinal) < 0 ||
                feedback.IndexOf("DeniedDuplicatePendingOrRunning", StringComparison.Ordinal) < 0 ||
                feedback.IndexOf("duplicateRunning:test", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected denied diagnostic admission feedback to include the admission summary.");
            }
        }

        private static void SchedulerTreatsDiagnosticButtonAsUserCommand()
        {
            var earlyBackground = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.WorldAutomationAutoHarvest,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Metadata =
                {
                    { ActionMetadataKeys.SourceKind, "Automation" },
                    { ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvest }
                }
            };
            var laterButton = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(
                InputActionKind.QuickHeal,
                "Button.QuickHeal",
                DiagnosticActionSource.ForButton("diagnostics.quick_heal", "QuickHeal 回血"));
            laterButton.CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc);

            var selected = InputActionScheduler.SelectNext(new[] { earlyBackground, laterButton });
            if (!object.ReferenceEquals(selected, laterButton) ||
                !string.Equals(InputActionScheduler.ResolveBucketName(laterButton), "P2:UserExplicitCommand", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected diagnostic button actions to be treated as explicit user commands.");
            }
        }

        private static void AssertPositiveQueueTimeout(InputActionRequest request, string label)
        {
            if (request == null || request.QueueTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Expected " + label + " request to have a positive QueueTimeout.");
            }
        }


        private static void InputActionQueueReleasesChannelAfterTerminalStart()
        {
            // Channel-release tests guard cleanup regressions, not player-facing
            // feature success. Keep leases and terminal results observable.
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

        private static void InputActionQueueReleasesChannelAfterUpdateException()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.MouseTarget] = new ThrowingUpdateFakeExecutor(InputActionKind.MouseTarget);
            executors[InputActionKind.Jump] = new TerminalFakeExecutor(
                InputActionKind.Jump,
                InputActionStatus.Succeeded,
                "next action completed");
            var queue = new InputActionQueue(executors);
            var throwingRequest = new InputActionRequest
            {
                Kind = InputActionKind.MouseTarget,
                SourceFeatureId = "test.update_exception",
                Description = "throwing update action"
            };
            var nextRequest = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                SourceFeatureId = "test.update_exception.next",
                Description = "next action after update exception"
            };
            queue.Enqueue(throwingRequest);
            queue.Enqueue(nextRequest);

            queue.Update(null);
            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.IsNullOrWhiteSpace(snapshot.RunningActionKind) ||
                snapshot.PendingCount != 1 ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.Failed ||
                snapshot.LastResult.Error == null ||
                snapshot.LastResult.Message.IndexOf("Action update failed", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Expected update exception path to fail, release channel lease, clear running action, and keep pending work.");
            }

            queue.Update(null);
            snapshot = queue.GetSnapshot();
            InputActionResult failedResult;
            if (!queue.TryGetResultByRequestId(throwingRequest.RequestId, out failedResult) ||
                failedResult.Status != InputActionStatus.Failed ||
                failedResult.Error == null ||
                snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.IsNullOrWhiteSpace(snapshot.RunningActionKind) ||
                snapshot.LastResult == null ||
                snapshot.LastResult.RequestId != nextRequest.RequestId ||
                snapshot.LastResult.Status != InputActionStatus.Succeeded)
            {
                throw new InvalidOperationException("Expected queue to preserve failed update result and continue with the next action.");
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
            var request = DiagnosticActionDispatcher.CreateDiagnosticRequestForTesting(
                InputActionKind.DiagnosticNoop,
                "Button.DiagnosticNoop",
                DiagnosticActionSource.ForButton("diagnostics.noop", "空动作"));
            AssertPositiveQueueTimeout(request, "diagnostic noop");
            if (request.DuplicatePolicy != InputActionDuplicatePolicy.CoalescePending)
            {
                throw new InvalidOperationException("Expected DiagnosticNoop diagnostic request to use coalescing admission policy.");
            }

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                throw new InvalidOperationException("Expected DiagnosticNoop admission to succeed: " + (admission == null ? "null" : admission.Reason));
            }

            queue.Update(null);

            var snapshot = queue.GetSnapshot();
            if (snapshot.ActionQueueChannelLeaseCount != 0 ||
                !string.Equals(snapshot.ActionQueueRunningLeaseChannels, "None", StringComparison.Ordinal) ||
                !string.Equals(snapshot.ActionQueueLastAdmissionStatus, "Accepted", StringComparison.Ordinal) ||
                snapshot.LastResult == null ||
                snapshot.LastResult.Status != InputActionStatus.Succeeded)
            {
                throw new InvalidOperationException("Expected DiagnosticNoop to complete without acquiring a channel lease.");
            }
        }
    }
}
