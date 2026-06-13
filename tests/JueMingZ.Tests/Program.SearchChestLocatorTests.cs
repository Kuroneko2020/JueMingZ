using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Search;
using JueMingZ.Automation.Search.ChestLocator;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.GameState.Ui;
using JueMingZ.Input;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void SearchChestLocatorResolvesLocalizedAndInternalNames()
        {
            WithSearchQueryFixture(() =>
            {
                var localized = ChestItemLocatorQueryResolver.Resolve("铁", 10);
                AssertChestLocatorSuccessContains(localized, 100, "localized display name");

                var internalName = ChestItemLocatorQueryResolver.Resolve("wood", 10);
                AssertChestLocatorSuccessContains(internalName, 104, "internal name");
            });
        }

        private static void SearchChestLocatorResolvesNumericIds()
        {
            WithSearchQueryFixture(() =>
            {
                var hashId = ChestItemLocatorQueryResolver.Resolve("#100", 10);
                AssertSingleChestLocatorMatch(hashId, 100, "hash item id");

                var plainId = ChestItemLocatorQueryResolver.Resolve("104", 10);
                AssertSingleChestLocatorMatch(plainId, 104, "plain numeric item id");
            });
        }

        private static void SearchChestLocatorRejectsEmptyAndUnknownQueries()
        {
            WithSearchQueryFixture(() =>
            {
                var empty = ChestItemLocatorQueryResolver.Resolve("   ", 10);
                AssertStringEquals(empty.Status, ChestItemLocatorQueryResult.StatusEmptyInput, "empty chest locator query status");
                if (empty.Succeeded || empty.CandidateCount != 0 || empty.CreateMatchSet().Count != 0)
                {
                    throw new InvalidOperationException("Empty chest locator query must not produce candidates or match types.");
                }

                var unknownName = ChestItemLocatorQueryResolver.Resolve("不存在的物品", 10);
                AssertStringEquals(unknownName.Status, ChestItemLocatorQueryResult.StatusNoMatch, "unknown chest locator query status");
                if (unknownName.Succeeded || unknownName.CandidateCount != 0 || unknownName.MatchesItemType(100))
                {
                    throw new InvalidOperationException("Unknown chest locator text must not produce a match set.");
                }

                var unknownId = ChestItemLocatorQueryResolver.Resolve("#9999", 10);
                AssertStringEquals(unknownId.Status, ChestItemLocatorQueryResult.StatusUnknownItemId, "unknown chest locator id status");
                if (unknownId.Succeeded || unknownId.CandidateCount != 0)
                {
                    throw new InvalidOperationException("Unknown chest locator item id must not produce candidates.");
                }
            });
        }

        private static void SearchChestLocatorCapsCandidatesBeforeScanning()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ChestItemLocatorQueryResolver.Resolve("锭", 1);
                AssertStringEquals(result.Status, ChestItemLocatorQueryResult.StatusTooManyCandidates, "chest locator over-limit status");
                if (result.Succeeded || !result.IsTruncated || result.CandidateCount != 1)
                {
                    throw new InvalidOperationException("Over-limit chest locator query must degrade with a bounded candidate list.");
                }

                if (result.MatchesItemType(100) || result.CreateMatchSet().Count != 0)
                {
                    throw new InvalidOperationException("Over-limit chest locator query must not expose a scanning match set.");
                }
            });
        }

        private static void SearchChestLocatorMatchSetUsesItemTypesOnly()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ChestItemLocatorQueryResolver.Resolve("铁砧", 10);
                AssertChestLocatorSuccessContains(result, 200, "chest locator item type match set");

                var matchSet = result.CreateMatchSet();
                if (!matchSet.Contains(200) || matchSet.Contains(100))
                {
                    throw new InvalidOperationException("Chest locator match set must contain only resolved item types.");
                }

                matchSet.Add(100);
                if (result.MatchesItemType(100))
                {
                    throw new InvalidOperationException("Mutating a copied chest locator match set must not alter the query result.");
                }
            });
        }

        private static void SearchChestLocatorCandidateLimitIsClamped()
        {
            WithSearchQueryFixture(() =>
            {
                var normalized = ChestItemLocatorQueryResolver.NormalizeCandidateLimit(9999);
                if (normalized != ChestItemLocatorQueryResolver.MaxCandidateLimit)
                {
                    throw new InvalidOperationException("Chest locator candidate limit must be clamped to a stable maximum.");
                }

                var fallback = ChestItemLocatorQueryResolver.NormalizeCandidateLimit(0);
                if (fallback != ChestItemLocatorQueryResolver.DefaultCandidateLimit)
                {
                    throw new InvalidOperationException("Chest locator non-positive candidate limit must use the default limit.");
                }
            });
        }

        private static void SearchChestLocatorSnapshotHandlesEmptyAndNoMatchChests()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40));

                    var query = ChestItemLocatorQueryResolver.Resolve("铁锭", 10);
                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        1,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    AssertStringEquals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, "empty chest locator snapshot status");
                    if (snapshot.HitCount != 0 ||
                        snapshot.ScannedChestCount != 1 ||
                        snapshot.UnreadableChestCount != 0 ||
                        snapshot.MatchedSlotCount != 0)
                    {
                        throw new InvalidOperationException("Empty chest scan should read one candidate without producing hits.");
                    }

                    ports.Replace(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(104, 9)));
                    var noMatch = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        2,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);
                    if (noMatch.HitCount != 0 || noMatch.ScannedChestCount != 1)
                    {
                        throw new InvalidOperationException("Non-matching chest contents must not produce locator hits.");
                    }
                });
            });
        }

        private static void SearchChestLocatorSnapshotAccumulatesMatchedSlots()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(
                        0,
                        CreateChestLocatorFakeChest(
                            0,
                            5,
                            6,
                            40,
                            CreateChestLocatorItem(100, 3),
                            CreateChestLocatorItem(104, 5),
                            CreateChestLocatorItem(100, 7)));

                    var query = ChestItemLocatorQueryResolver.Resolve("铁锭", 10);
                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        1,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    if (snapshot.HitCount != 1 ||
                        snapshot.MatchedChestCount != 1 ||
                        snapshot.MatchedSlotCount != 2 ||
                        snapshot.TotalStack != 10)
                    {
                        throw new InvalidOperationException("Chest locator snapshot must aggregate matched slots by item type.");
                    }

                    var hit = snapshot.Hits[0];
                    if (hit.ChestIndex != 0 ||
                        hit.ChestX != 5 ||
                        hit.ChestY != 6 ||
                        hit.Items.Count != 1 ||
                        hit.Items[0].ItemType != 100 ||
                        hit.Items[0].TotalStack != 10 ||
                        hit.Items[0].SlotCount != 2 ||
                        hit.Items[0].FirstSlot != 0)
                    {
                        throw new InvalidOperationException("Chest locator hit should copy stable origin and matched item facts.");
                    }
                });
            });
        }

        private static void SearchChestLocatorSnapshotLimitsCandidateChests()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ConfigureChestLocatorChest(8, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 1)));
                    ports.Add(1, CreateChestLocatorFakeChest(1, 8, 6, 40, CreateChestLocatorItem(100, 1)));

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        1,
                        new ChestItemLocatorScanOptions(1, 24, 300),
                        ports.ToPorts(),
                        true);

                    if (!snapshot.CandidateLimitReached ||
                        snapshot.CandidateChestCount != 1 ||
                        snapshot.ScannedChestCount != 1 ||
                        snapshot.HitCount != 1)
                    {
                        throw new InvalidOperationException("Chest locator scan must stop at the configured candidate chest limit.");
                    }
                });
            });
        }

        private static void SearchChestLocatorSnapshotCacheInvalidatesByQueryVersionAndWorld()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    var chest = CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 1));
                    ports.Add(0, chest);
                    var query = ChestItemLocatorQueryResolver.Resolve("铁锭", 10);

                    var first = ChestItemLocatorService.GetSnapshotForTesting(query, context, 1, ChestItemLocatorScanOptions.Default, ports.ToPorts(), false);
                    chest.item[0] = CreateChestLocatorItem(100, 9);
                    context.GameUpdateCount = 101;
                    var cached = ChestItemLocatorService.GetSnapshotForTesting(query, context, 1, ChestItemLocatorScanOptions.Default, ports.ToPorts(), false);
                    if (!object.ReferenceEquals(first, cached) || cached.TotalStack != 1)
                    {
                        throw new InvalidOperationException("Chest locator snapshot should reuse cache while query version and world signature are unchanged.");
                    }

                    context.GameUpdateCount = 102;
                    var versionChanged = ChestItemLocatorService.GetSnapshotForTesting(query, context, 2, ChestItemLocatorScanOptions.Default, ports.ToPorts(), false);
                    if (object.ReferenceEquals(cached, versionChanged) ||
                        versionChanged.TotalStack != 9 ||
                        !string.Equals(versionChanged.RefreshReason, "queryVersionChanged", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Chest locator snapshot must refresh when query version changes.");
                    }

                    chest.item[0] = CreateChestLocatorItem(100, 4);
                    var otherWorld = CreateChestLocatorContext(103, "world-b", "world-record-b");
                    var worldChanged = ChestItemLocatorService.GetSnapshotForTesting(query, otherWorld, 2, ChestItemLocatorScanOptions.Default, ports.ToPorts(), false);
                    if (object.ReferenceEquals(versionChanged, worldChanged) ||
                        worldChanged.TotalStack != 4 ||
                        !string.Equals(worldChanged.RefreshReason, "worldChanged", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Chest locator snapshot must refresh when world identity changes.");
                    }
                });
            });
        }

        private static void SearchChestLocatorSnapshotDegradesUnreadableChests()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.MapOnly(0, 5, 6);

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        1,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    if (snapshot.HitCount != 0 ||
                        snapshot.ScannedChestCount != 0 ||
                        snapshot.UnreadableChestCount != 1 ||
                        !string.Equals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unreadable chest records should degrade without failing the whole scan.");
                    }
                });
            });
        }

        private static void SearchChestLocatorSnapshotDoesNotRetainMutableItems()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    var item = CreateChestLocatorItem(100, 3);
                    var chest = CreateChestLocatorFakeChest(0, 5, 6, 40, item);
                    ports.Add(0, chest);

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        1,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    item.type = 104;
                    item.stack = 99;
                    chest.x = 20;
                    chest.y = 20;

                    if (snapshot.HitCount != 1 ||
                        snapshot.Hits[0].ChestX != 5 ||
                        snapshot.Hits[0].ChestY != 6 ||
                        snapshot.Hits[0].Items[0].ItemType != 100 ||
                        snapshot.Hits[0].Items[0].TotalStack != 3)
                    {
                        throw new InvalidOperationException("Chest locator snapshot must copy values instead of retaining mutable chest or item state.");
                    }
                });
            });
        }

        private static void SearchChestLocatorUiStateSubmitsSnapshotAndClearRemovesHighlight()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3)));

                    SearchChestLocatorUiState.ResetForTesting();
                    SearchChestLocatorUiState.UpdateDraft("铁锭");

                    ChestItemLocatorQueryResult query;
                    long queryVersion;
                    string message;
                    if (!SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message) ||
                        queryVersion != 1 ||
                        query == null ||
                        !query.Succeeded)
                    {
                        throw new InvalidOperationException("Chest locator UI state should prepare a successful submitted query.");
                    }

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        queryVersion,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);
                    SearchChestLocatorUiState.ApplySnapshot(queryVersion, snapshot);
                    var lines = SearchChestLocatorUiState.GetSummaryLinesForTesting();
                    if (SearchChestLocatorUiState.GetSnapshot().HitCount != 1 ||
                        lines[0].IndexOf("命中 1 个箱子", StringComparison.Ordinal) < 0 ||
                        !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.GetNoticeMessageForTesting()))
                    {
                        throw new InvalidOperationException("Chest locator UI state should keep a successful submitted snapshot without a no-result notice.");
                    }

                    SearchChestLocatorUiState.Clear();
                    if (SearchChestLocatorUiState.GetSnapshot().HitCount != 0 ||
                        !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText) ||
                        SearchChestLocatorUiState.QueryVersion <= queryVersion)
                    {
                        throw new InvalidOperationException("Clearing chest locator UI state must remove query text and any highlight snapshot.");
                    }
                });
            });
        }

        private static void SearchChestLocatorNoResultNoticeDoesNotCloseWindow()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(104, 3)));

                    SearchChestLocatorUiState.ResetForTesting();
                    SearchChestLocatorUiState.UpdateDraft("铁锭");

                    ChestItemLocatorQueryResult query;
                    long queryVersion;
                    string message;
                    if (!SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message))
                    {
                        throw new InvalidOperationException("Expected chest locator no-result test to submit a valid query.");
                    }

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        queryVersion,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);
                    SearchChestLocatorUiState.ApplySnapshot(queryVersion, snapshot);

                    AssertStringEquals(
                        SearchChestLocatorUiState.GetNoticeMessageForTesting(),
                        SearchChestLocatorUiState.SearchRangeNoResultMessage,
                        "chest locator no-result notice");
                    if (LegacyUiActionService.ShouldCloseSearchChestLocatorWindowForTesting(snapshot))
                    {
                        throw new InvalidOperationException("Chest locator submit must not close the F5 window when the scan has no hits.");
                    }

                    SearchChestLocatorUiState.ResetForTesting();
                    if (SearchChestLocatorUiState.HasNotice)
                    {
                        throw new InvalidOperationException("Chest locator notice must not be a persistent summary panel.");
                    }
                });
            });
        }

        private static void SearchChestLocatorHitClosesWindowAndChestOpenClearsHighlight()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3)));

                    SearchChestLocatorUiState.ResetForTesting();
                    SearchChestLocatorUiState.UpdateDraft("铁锭");

                    ChestItemLocatorQueryResult query;
                    long queryVersion;
                    string message;
                    if (!SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message))
                    {
                        throw new InvalidOperationException("Expected chest locator hit test to submit a valid query.");
                    }

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        query,
                        context,
                        queryVersion,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);
                    SearchChestLocatorUiState.ApplySnapshot(queryVersion, snapshot);

                    if (!LegacyUiActionService.ShouldCloseSearchChestLocatorWindowForTesting(snapshot))
                    {
                        throw new InvalidOperationException("Chest locator submit should close the F5 window only after a real hit.");
                    }

                    if (!SearchChestLocatorUiState.ClearVisibleStateAfterHitClose() ||
                        SearchChestLocatorUiState.GetSnapshot().HitCount != 1 ||
                        !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText) ||
                        SearchChestLocatorUiState.CandidateCount != 0 ||
                        SearchChestLocatorUiState.SelectedItemType != 0)
                    {
                        throw new InvalidOperationException("Closing F5 after a hit should clear visible query state while keeping the world highlight snapshot.");
                    }

                    var beforeClearVersion = SearchChestLocatorUiState.QueryVersion;
                    var cleared = JueMingZRuntime.ClearSearchChestLocatorHighlightIfChestOpenForTesting(
                        new GameStateSnapshot { Ui = new UiStateSnapshot { ChestOpen = true } });
                    if (!cleared ||
                        SearchChestLocatorUiState.GetSnapshot().HitCount != 0 ||
                        SearchChestLocatorUiState.QueryVersion <= beforeClearVersion ||
                        !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText))
                    {
                        throw new InvalidOperationException("Opening a chest should clear only the chest locator highlight snapshot.");
                    }

                    var closedChest = JueMingZRuntime.ClearSearchChestLocatorHighlightIfChestOpenForTesting(
                        new GameStateSnapshot { Ui = new UiStateSnapshot { ChestOpen = false } });
                    if (closedChest)
                    {
                        throw new InvalidOperationException("Chest locator highlight cleanup must only run while a chest is open.");
                    }
                });
            });
        }

        private static void SearchChestLocatorUiStateRejectsEmptyAndTooManyWithoutScanning()
        {
            WithSearchQueryFixture(() =>
            {
                SearchChestLocatorUiState.ResetForTesting();

                ChestItemLocatorQueryResult query;
                long queryVersion;
                string message;
                if (SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message) ||
                    queryVersion != 1 ||
                    query == null ||
                    !string.Equals(query.Status, ChestItemLocatorQueryResult.StatusEmptyInput, StringComparison.Ordinal) ||
                    SearchChestLocatorUiState.GetSnapshot().HitCount != 0 ||
                    !string.Equals(SearchChestLocatorUiState.GetNoticeMessageForTesting(), SearchChestLocatorUiState.SearchRangeNoResultMessage, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Empty chest locator UI submit must fail before scanning and clear the snapshot.");
                }

                for (var index = 0; index < 30; index++)
                {
                    AddSearchItem(700 + index, "过多锭" + index, "TooManyBar" + index, 999, 1, 0, true, false, -1, -1);
                }

                ItemQueryService.ResetForTesting();
                SearchChestLocatorUiState.UpdateDraft("过多锭");
                if (SearchChestLocatorUiState.CandidateCount != ChestItemLocatorQueryResolver.MaxCandidateLimit ||
                    SearchChestLocatorUiState.TryBeginSubmit(out query, out queryVersion, out message) ||
                    query == null ||
                    !string.Equals(query.Status, ChestItemLocatorQueryResult.StatusTooManyCandidates, StringComparison.Ordinal) ||
                    message.IndexOf("候选过多", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Over-broad chest locator UI submit must expose bounded candidates and skip scanning.");
                }
            });
        }

        private static void SearchChestLocatorCommandsFocusAndClearDedicatedState()
        {
            WithSearchQueryFixture(() =>
            {
                ResetSearchQueryCommandState();
                try
                {
                    EnqueueSearchQueryCommandForTesting(SearchChestLocatorUiState.InputId);
                    LegacyUiActionService.Update(null, null);
                    if (LegacyUiActionService.DispatchedCommandCountLast != 1 ||
                        !LegacyTextInput.IsFocused(SearchChestLocatorUiState.InputId))
                    {
                        throw new InvalidOperationException("Chest locator input command should focus its own LegacyTextInput id.");
                    }

                    SearchChestLocatorUiState.UpdateDraft("铁锭");
                    EnqueueSearchQueryCommandForTesting(SearchChestLocatorUiState.ClearButtonId);
                    LegacyUiActionService.Update(null, null);
                    if (LegacyUiActionService.DispatchedCommandCountLast != 1 ||
                        LegacyTextInput.IsAnyFocused ||
                        !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText) ||
                        SearchChestLocatorUiState.CandidateCount != 0)
                    {
                        throw new InvalidOperationException("Chest locator clear command should clear only the locator state and text focus.");
                    }
                }
                finally
                {
                    ResetSearchQueryCommandState();
                }
            });
        }

        private static void SearchChestLocatorLayoutStaysAboveSearchQueryAndTracksHeight()
        {
            WithSearchQueryFixture(() =>
            {
                SearchChestLocatorUiState.ResetForTesting();
                SearchItemQueryUiState.ResetForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();

                var order = LegacyMainWindow.GetSearchPageBlockOrderForTesting();
                if (order.Length != 2 ||
                    !string.Equals(order[0], "chestLocator", StringComparison.Ordinal) ||
                    !string.Equals(order[1], "querySearch", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("F5 search page must keep chest locator above the search query block.");
                }

                var labels = LegacyMainWindow.GetSearchChestLocatorInputRowTextForTesting();
                AssertStringEquals(labels[0], "定位物品", "chest locator input label");
                AssertStringEquals(labels[1], "定位", "chest locator submit button label");
                if (LegacyMainWindow.CalculateSearchChestLocatorBlockHeightForTesting(540) != 36)
                {
                    throw new InvalidOperationException("Chest locator should not reserve an extra section title row above the input line.");
                }

                var settings = AppSettings.CreateDefault();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var shell = LegacyMainWindowShell.Create(window);
                var content = shell.ContentRect;
                var initial = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);

                for (var index = 0; index < 10; index++)
                {
                    AddSearchItem(800 + index, "双列候选" + index, "ChestGridCandidate" + index, 999, 1, 0, true, false, -1, -1);
                }

                ItemQueryService.ResetForTesting();
                SearchChestLocatorUiState.UpdateDraft("双列候选");
                if (SearchChestLocatorUiState.CandidateCount != 10 ||
                    LegacyMainWindow.GetSearchChestLocatorCandidateRowsForTesting() != 5)
                {
                    throw new InvalidOperationException("Chest locator candidates should render every candidate in a two-column grid without the old four-row cap.");
                }

                SearchChestLocatorUiState.ResetForTesting();
                SearchChestLocatorUiState.UpdateDraft("铁");
                var candidates = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);
                if (candidates.PageStateSignature == initial.PageStateSignature ||
                    candidates.ContentHeight <= initial.ContentHeight ||
                    LegacyMainWindow.CalculateSearchChestLocatorBlockHeightForTesting(content.Width) <= 0)
                {
                    throw new InvalidOperationException("Chest locator candidates must dirty search page layout and increase content height.");
                }
            });
        }

        private static void SearchChestLocatorOverlayBuildsViewFromSnapshotOnly()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    var chest = CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3));
                    chest.name = "Ore Box";
                    ports.Add(0, chest);

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        7,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    chest.item[0] = CreateChestLocatorItem(104, 99);
                    var drawContext = CreateChestLocatorContext(108);
                    var view = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, drawContext);
                    if (!view.Enabled ||
                        view.QueryVersion != 7 ||
                        view.CandidateChestCount != 1 ||
                        view.ScannedChestCount != 1 ||
                        view.HitCount != 1 ||
                        view.Hits.Count != 1)
                    {
                        throw new InvalidOperationException("Chest locator overlay should build a drawable view from the submitted snapshot.");
                    }

                    var hit = view.Hits[0];
                    if (hit.ChestX != 5 ||
                        hit.ChestY != 6 ||
                        hit.ScreenX != 80 ||
                        hit.ScreenY != 96 ||
                        hit.PixelWidth != 32 ||
                        hit.PixelHeight != 32 ||
                        hit.TotalStack != 3)
                    {
                        throw new InvalidOperationException("Chest locator overlay should use snapshot hit facts while only validating the current container tile.");
                    }
                });
            });
        }

        private static void SearchChestLocatorOverlayFiltersRemovedContainer()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3)));

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        8,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    ChestLocatorFakeMain.ClearChest(5, 6);
                    var view = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, CreateChestLocatorContext(101));
                    if (view.Enabled ||
                        view.Hits.Count != 0 ||
                        view.HitCount != 1 ||
                        !string.Equals(view.SkipReason, "invalidContainer", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Chest locator overlay should hide a cached hit when its current container tile is gone.");
                    }
                });
            });
        }

        private static void SearchChestLocatorOverlayKeepsValidHitsWhenOneContainerRemoved()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ConfigureChestLocatorChest(8, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3)));
                    ports.Add(1, CreateChestLocatorFakeChest(1, 8, 6, 40, CreateChestLocatorItem(100, 5)));

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        9,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    ChestLocatorFakeMain.ClearChest(5, 6);
                    var view = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, CreateChestLocatorContext(101));
                    if (!view.Enabled ||
                        view.HitCount != 2 ||
                        view.Hits.Count != 1 ||
                        view.Hits[0].ChestX != 8 ||
                        view.Hits[0].ChestY != 6 ||
                        view.Hits[0].TotalStack != 5)
                    {
                        throw new InvalidOperationException("Chest locator overlay should skip only the removed container and keep other valid hits visible.");
                    }
                });
            });
        }

        private static void SearchChestLocatorOverlaySkipsStaleForeignAndOffscreenSnapshots()
        {
            WithSearchQueryFixture(() =>
            {
                WithChestLocatorScanFixture(() =>
                {
                    var context = CreateChestLocatorContext(100);
                    var ports = new ChestLocatorFakePorts();
                    ConfigureChestLocatorChest(5, 6);
                    ports.Add(0, CreateChestLocatorFakeChest(0, 5, 6, 40, CreateChestLocatorItem(100, 3)));

                    var snapshot = ChestItemLocatorService.GetSnapshotForTesting(
                        ChestItemLocatorQueryResolver.Resolve("铁锭", 10),
                        context,
                        2,
                        ChestItemLocatorScanOptions.Default,
                        ports.ToPorts(),
                        true);

                    var expired = CreateChestLocatorContext(100 + ChestItemLocatorOverlayService.SnapshotMaxAgeTicksForTesting + 1);
                    var expiredView = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, expired);
                    if (expiredView.Enabled ||
                        !string.Equals(expiredView.SkipReason, "snapshotExpired", StringComparison.Ordinal) ||
                        expiredView.SnapshotAgeTicks != ChestItemLocatorOverlayService.SnapshotMaxAgeTicksForTesting + 1)
                    {
                        throw new InvalidOperationException("Chest locator overlay should skip stale submitted snapshots.");
                    }

                    var otherWorld = CreateChestLocatorContext(108, "world-b", "world-record-b");
                    var otherWorldView = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, otherWorld);
                    if (otherWorldView.Enabled ||
                        !string.Equals(otherWorldView.SkipReason, "worldChanged", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Chest locator overlay should skip snapshots from a different world.");
                    }

                    var offscreen = CreateChestLocatorContext(108);
                    offscreen.ScreenX = 10000f;
                    var offscreenView = ChestItemLocatorOverlayService.BuildViewForTesting(snapshot, offscreen);
                    if (offscreenView.Enabled ||
                        !string.Equals(offscreenView.SkipReason, "offscreen", StringComparison.Ordinal) ||
                        offscreenView.HitCount != 1)
                    {
                        throw new InvalidOperationException("Chest locator overlay should skip drawing when snapshot hits are outside the viewport.");
                    }
                });
            });
        }

        private static void AssertChestLocatorSuccessContains(
            ChestItemLocatorQueryResult result,
            int itemType,
            string label)
        {
            if (result == null ||
                !result.Succeeded ||
                !string.Equals(result.Status, ChestItemLocatorQueryResult.StatusOk, StringComparison.Ordinal) ||
                !result.MatchesItemType(itemType))
            {
                throw new InvalidOperationException("Expected chest locator candidate " + itemType + " for " + label + ".");
            }
        }

        private static void AssertSingleChestLocatorMatch(
            ChestItemLocatorQueryResult result,
            int itemType,
            string label)
        {
            AssertChestLocatorSuccessContains(result, itemType, label);
            if (result.CandidateCount != 1 ||
                result.MatchItemTypes.Count != 1 ||
                result.Candidates[0].ItemType != itemType)
            {
                throw new InvalidOperationException("Expected exactly one chest locator match for " + label + ".");
            }
        }

        private static void WithChestLocatorScanFixture(Action test)
        {
            ChestItemLocatorService.ResetForTesting();
            ChestLocatorFakeMain.Reset();
            try
            {
                ChestLocatorFakeMain.Reset(64, 64);
                test();
            }
            finally
            {
                ChestItemLocatorService.ResetForTesting();
                ChestLocatorFakeMain.Reset();
            }
        }

        private static InformationWorldContext CreateChestLocatorContext(ulong tick)
        {
            return CreateChestLocatorContext(tick, "world-a", "world-record-a");
        }

        private static InformationWorldContext CreateChestLocatorContext(ulong tick, string worldKey, string worldRecordKey)
        {
            return new InformationWorldContext
            {
                MainType = typeof(ChestLocatorFakeMain),
                LocalPlayer = new object(),
                ScreenX = 0f,
                ScreenY = 0f,
                ScreenWidth = 640,
                ScreenHeight = 480,
                PlayerCenterX = 128f,
                PlayerCenterY = 128f,
                WorldKey = worldKey,
                WorldRecordKey = worldRecordKey,
                PlayerRecordKey = "player-a",
                GameUpdateCount = tick
            };
        }

        private static void ConfigureChestLocatorChest(int chestX, int chestY)
        {
            ChestLocatorFakeMain.SetChest(chestX, chestY, 21, 0);
        }

        private static ChestLocatorFakeChest CreateChestLocatorFakeChest(
            int index,
            int x,
            int y,
            int maxItems,
            params Terraria.TestRecipeItem[] items)
        {
            var chest = new ChestLocatorFakeChest
            {
                index = index,
                x = x,
                y = y,
                maxItems = maxItems,
                item = new object[maxItems]
            };

            for (var slot = 0; slot < chest.item.Length; slot++)
            {
                chest.item[slot] = CreateChestLocatorItem(0, 0);
            }

            if (items != null)
            {
                for (var slot = 0; slot < items.Length && slot < chest.item.Length; slot++)
                {
                    chest.item[slot] = items[slot] ?? CreateChestLocatorItem(0, 0);
                }
            }

            return chest;
        }

        private static Terraria.TestRecipeItem CreateChestLocatorItem(int itemType, int stack)
        {
            return new Terraria.TestRecipeItem
            {
                type = itemType,
                stack = stack
            };
        }

        private sealed class ChestLocatorFakeChest
        {
            public int index;
            public int x;
            public int y;
            public string name = string.Empty;
            public object[] item = new object[0];
            public int maxItems;
        }

        private sealed class ChestLocatorFakePorts
        {
            private readonly Dictionary<long, int> _indexes = new Dictionary<long, int>();
            private readonly Dictionary<int, object> _chests = new Dictionary<int, object>();

            public void Add(int index, ChestLocatorFakeChest chest)
            {
                if (chest == null)
                {
                    return;
                }

                _indexes[BuildChestLocatorKey(chest.x, chest.y)] = index;
                _chests[index] = chest;
            }

            public void MapOnly(int index, int x, int y)
            {
                _indexes[BuildChestLocatorKey(x, y)] = index;
            }

            public void Replace(int index, ChestLocatorFakeChest chest)
            {
                _chests[index] = chest;
            }

            public ChestItemLocatorScanPorts ToPorts()
            {
                return new ChestItemLocatorScanPorts
                {
                    TryFindChestIndex = TryFindChestIndex,
                    TryGetChest = TryGetChest
                };
            }

            private bool TryFindChestIndex(int chestX, int chestY, out int chestIndex)
            {
                return _indexes.TryGetValue(BuildChestLocatorKey(chestX, chestY), out chestIndex);
            }

            private bool TryGetChest(int chestIndex, out object chest)
            {
                return _chests.TryGetValue(chestIndex, out chest) && chest != null;
            }
        }

        private static long BuildChestLocatorKey(int x, int y)
        {
            unchecked
            {
                return ((long)x << 32) ^ (uint)y;
            }
        }

        private static class ChestLocatorFakeMain
        {
            public static ChestLocatorFakeTile[,] tile = new ChestLocatorFakeTile[1, 1];
            public static int maxTilesX = 1;
            public static int maxTilesY = 1;

            public static void Reset()
            {
                Reset(1, 1);
            }

            public static void Reset(int width, int height)
            {
                maxTilesX = Math.Max(1, width);
                maxTilesY = Math.Max(1, height);
                tile = new ChestLocatorFakeTile[maxTilesX, maxTilesY];
            }

            public static void SetChest(int chestX, int chestY, int tileType, int style)
            {
                EnsureSize(chestX + 3, chestY + 3);
                SetTile(chestX, chestY, tileType, style * 36, 0);
                SetTile(chestX + 1, chestY, tileType, style * 36 + 18, 0);
                SetTile(chestX, chestY + 1, tileType, style * 36, 18);
                SetTile(chestX + 1, chestY + 1, tileType, style * 36 + 18, 18);
            }

            public static void ClearChest(int chestX, int chestY)
            {
                EnsureSize(chestX + 3, chestY + 3);
                for (var x = chestX; x < chestX + 2; x++)
                {
                    for (var y = chestY; y < chestY + 2; y++)
                    {
                        tile[x, y] = null;
                    }
                }
            }

            private static void EnsureSize(int width, int height)
            {
                if (width <= maxTilesX && height <= maxTilesY)
                {
                    return;
                }

                var newWidth = Math.Max(width, maxTilesX);
                var newHeight = Math.Max(height, maxTilesY);
                var next = new ChestLocatorFakeTile[newWidth, newHeight];
                for (var x = 0; x < maxTilesX; x++)
                {
                    for (var y = 0; y < maxTilesY; y++)
                    {
                        next[x, y] = tile[x, y];
                    }
                }

                tile = next;
                maxTilesX = newWidth;
                maxTilesY = newHeight;
            }

            private static void SetTile(int x, int y, int tileType, int frameX, int frameY)
            {
                tile[x, y] = new ChestLocatorFakeTile
                {
                    active = true,
                    type = tileType,
                    frameX = frameX,
                    frameY = frameY
                };
            }
        }

        private sealed class ChestLocatorFakeTile
        {
            public bool active;
            public int type;
            public int frameX;
            public int frameY;
        }
    }
}
