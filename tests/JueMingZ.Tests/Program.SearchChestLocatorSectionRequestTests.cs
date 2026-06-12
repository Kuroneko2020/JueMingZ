using System;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Search.ChestLocator;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void SearchChestLocatorSectionRequestSendsCurrentPlayerSection()
        {
            ChestItemLocatorSectionRequestService.ResetForTesting();
            try
            {
                var context = CreateChestLocatorContext(100);
                context.PlayerCenterX = (200 * 16) + 8;
                context.PlayerCenterY = (150 * 16) + 8;
                var probe = new ChestLocatorSectionRequestProbe();

                var result = ChestItemLocatorSectionRequestService.TryRequestForTesting(
                    context,
                    ChestItemLocatorSectionRequestOptions.Default,
                    probe.ToPorts(),
                    9);

                if (!result.Sent ||
                    !result.Attempted ||
                    result.Throttled ||
                    probe.SendCount != 1 ||
                    probe.LastSectionX != 1 ||
                    probe.LastSectionY != 1 ||
                    result.SectionKey.IndexOf("world-record-a:1:1", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Chest locator section request must send only the current player section on submit.");
                }

                var diagnostics = ChestItemLocatorSectionRequestService.GetDiagnostics();
                if (!diagnostics.Sent ||
                    diagnostics.QueryVersion != 9 ||
                    diagnostics.SectionX != 1 ||
                    diagnostics.SectionY != 1)
                {
                    throw new InvalidOperationException("Chest locator section request diagnostics must mirror the last sent request.");
                }
            }
            finally
            {
                ChestItemLocatorSectionRequestService.ResetForTesting();
            }
        }

        private static void SearchChestLocatorSectionRequestThrottlesSectionKey()
        {
            ChestItemLocatorSectionRequestService.ResetForTesting();
            try
            {
                var probe = new ChestLocatorSectionRequestProbe();
                var options = new ChestItemLocatorSectionRequestOptions(true, 300);
                var context = CreateChestLocatorContext(100);
                context.PlayerCenterX = (200 * 16) + 8;
                context.PlayerCenterY = (150 * 16) + 8;

                var first = ChestItemLocatorSectionRequestService.TryRequestForTesting(context, options, probe.ToPorts(), 1);
                context.GameUpdateCount = 120;
                var throttled = ChestItemLocatorSectionRequestService.TryRequestForTesting(context, options, probe.ToPorts(), 2);
                context.GameUpdateCount = 400;
                var cooled = ChestItemLocatorSectionRequestService.TryRequestForTesting(context, options, probe.ToPorts(), 3);
                context.GameUpdateCount = 401;
                context.PlayerCenterX = (400 * 16) + 8;
                var differentSection = ChestItemLocatorSectionRequestService.TryRequestForTesting(context, options, probe.ToPorts(), 4);

                if (!first.Sent ||
                    !throttled.Throttled ||
                    throttled.Attempted ||
                    throttled.CooldownRemainingTicks != 280 ||
                    !cooled.Sent ||
                    !differentSection.Sent ||
                    probe.SendCount != 3)
                {
                    throw new InvalidOperationException("Chest locator section request must throttle only the same world/section key.");
                }
            }
            finally
            {
                ChestItemLocatorSectionRequestService.ResetForTesting();
            }
        }

        private static void SearchChestLocatorSectionRequestSkipsSinglePlayerAndDisabled()
        {
            ChestItemLocatorSectionRequestService.ResetForTesting();
            try
            {
                var context = CreateChestLocatorContext(100);
                var probe = new ChestLocatorSectionRequestProbe { NetMode = 0 };
                var singlePlayer = ChestItemLocatorSectionRequestService.TryRequestForTesting(
                    context,
                    ChestItemLocatorSectionRequestOptions.Default,
                    probe.ToPorts(),
                    1);

                probe.NetMode = 1;
                var disabled = ChestItemLocatorSectionRequestService.TryRequestForTesting(
                    context,
                    new ChestItemLocatorSectionRequestOptions(false, 300),
                    probe.ToPorts(),
                    2);

                if (!string.Equals(singlePlayer.Status, ChestItemLocatorSectionRequestResult.StatusNotMultiplayerClient, StringComparison.Ordinal) ||
                    !string.Equals(disabled.Status, ChestItemLocatorSectionRequestResult.StatusDisabled, StringComparison.Ordinal) ||
                    probe.SendCount != 0)
                {
                    throw new InvalidOperationException("Chest locator section requests must skip single-player and disabled config states.");
                }
            }
            finally
            {
                ChestItemLocatorSectionRequestService.ResetForTesting();
            }
        }

        private static void SearchChestLocatorUiStateShowsSectionRequestStatus()
        {
            WithSearchQueryFixture(() =>
            {
                SearchChestLocatorUiState.ResetForTesting();
                SearchChestLocatorUiState.UpdateDraft("铁锭");

                ChestItemLocatorQueryResult query;
                long queryVersion;
                string message;
                if (!SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message))
                {
                    throw new InvalidOperationException("Chest locator UI state must submit the fixture query.");
                }

                SearchChestLocatorUiState.ApplySectionRequestResult(
                    queryVersion,
                    new ChestItemLocatorSectionRequestResult(
                        true,
                        true,
                        true,
                        true,
                        false,
                        ChestItemLocatorSectionRequestResult.StatusSent,
                        string.Empty,
                        1,
                        1,
                        "world-record-a:1:1",
                        queryVersion,
                        100,
                        0));

                var lines = SearchChestLocatorUiState.GetSummaryLinesForTesting();
                if (lines[1].IndexOf("多人已请求当前 section 数据", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Chest locator UI state must surface multiplayer section request status.");
                }

                SearchChestLocatorUiState.ApplySectionRequestResult(
                    queryVersion,
                    new ChestItemLocatorSectionRequestResult(
                        true,
                        false,
                        false,
                        false,
                        false,
                        ChestItemLocatorSectionRequestResult.StatusNotMultiplayerClient,
                        string.Empty,
                        -1,
                        -1,
                        string.Empty,
                        queryVersion,
                        101,
                        0));

                var singlePlayerLines = SearchChestLocatorUiState.GetSummaryLinesForTesting();
                if (singlePlayerLines[1].IndexOf("notMultiplayerClient", StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException("Chest locator UI state must not clutter single-player summaries with network status.");
                }
            });
        }

        private sealed class ChestLocatorSectionRequestProbe
        {
            public int NetMode = 1;
            public int SendCount;
            public int LastSectionX = -1;
            public int LastSectionY = -1;
            public bool SendResult = true;
            public string FailureReason = string.Empty;

            public ChestItemLocatorSectionRequestPorts ToPorts()
            {
                return new ChestItemLocatorSectionRequestPorts
                {
                    GetNetMode = () => NetMode,
                    TryRequestSectionData = TryRequestSectionData
                };
            }

            private bool TryRequestSectionData(int sectionX, int sectionY, out string failureReason)
            {
                SendCount++;
                LastSectionX = sectionX;
                LastSectionY = sectionY;
                failureReason = FailureReason;
                return SendResult;
            }
        }
    }
}
