using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Search;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    internal static class SearchItemQueryUiState
    {
        public const string InputId = "search-query:input";
        public const string PickItemButtonId = "search-query:pick-item";
        public const int CandidateMaxResults = 24;
        private const int RecentItemHistoryLimit = 8;

        private static readonly object SyncRoot = new object();
        private static readonly List<ItemQueryCandidate> Candidates = new List<ItemQueryCandidate>();
        private static readonly List<int> RecentItemTypes = new List<int>();
        private static string _queryText = string.Empty;
        private static string _candidateMessage = string.Empty;
        private static ItemQueryResult _selectedResult;
        private static int _selectedItemType;
        private static int _candidateScrollOffset;
        private static int _candidateMaxScroll;
        private static LegacyUiRect _candidateViewport;
        private static int _hoverItemType;
        private static int _hoverItemStack;
        private static string _hoverItemName = string.Empty;
        private static string _hoverItemSource = string.Empty;
        private static ulong _hoverItemGameUpdateCount;
        private static SearchItemPickSelectionState _selectionState = SearchItemPickSelectionState.Idle;
        private static bool _selectionPending;
        private static ulong _selectionStartGameUpdateCount;
        private static bool _selectionWaitingForMouseRelease;
        private static string _selectionHintText = string.Empty;
        private static string _selectionSourceSummary = string.Empty;

        public static string QueryText
        {
            get { lock (SyncRoot) { return _queryText; } }
        }

        public static string CandidateMessage
        {
            get { lock (SyncRoot) { return _candidateMessage; } }
        }

        public static int CandidateCount
        {
            get { lock (SyncRoot) { return Candidates.Count; } }
        }

        public static int CandidateScrollOffset
        {
            get { lock (SyncRoot) { return _candidateScrollOffset; } }
        }

        public static int SelectedItemType
        {
            get { lock (SyncRoot) { return _selectedItemType; } }
        }

        public static bool HasSelectedResult
        {
            get { lock (SyncRoot) { return _selectedResult != null; } }
        }

        public static bool HasHoverItem
        {
            get { lock (SyncRoot) { return _hoverItemType > 0; } }
        }

        public static int HoverItemType
        {
            get { lock (SyncRoot) { return _hoverItemType; } }
        }

        public static bool IsSelectionPending
        {
            get { lock (SyncRoot) { return _selectionPending; } }
        }

        public static SearchItemPickSelectionState SelectionState
        {
            get { lock (SyncRoot) { return _selectionState; } }
        }

        public static ulong SelectionStartGameUpdateCount
        {
            get { lock (SyncRoot) { return _selectionStartGameUpdateCount; } }
        }

        public static bool SelectionWaitingForMouseRelease
        {
            get { lock (SyncRoot) { return _selectionWaitingForMouseRelease; } }
        }

        public static string SelectionHintText
        {
            get { lock (SyncRoot) { return _selectionHintText ?? string.Empty; } }
        }

        public static string SelectionSourceSummary
        {
            get { lock (SyncRoot) { return _selectionSourceSummary ?? string.Empty; } }
        }

        public static int RecentItemHistoryCount
        {
            get { lock (SyncRoot) { return RecentItemTypes.Count; } }
        }

        public static void UpdateDraft(string query)
        {
            var text = NormalizeQuery(query);
            lock (SyncRoot)
            {
                if (string.Equals(_queryText, text, StringComparison.Ordinal))
                {
                    return;
                }

                _queryText = text;
                _selectedResult = null;
                _selectedItemType = 0;
                ClearPendingSelectionLocked();
                RebuildCandidatesLocked();
            }
        }

        public static bool SelectItem(int itemType)
        {
            var result = ItemQueryService.BuildQuery(itemType);
            lock (SyncRoot)
            {
                var found = ApplySelectedItemLocked(itemType, result);
                ClearPendingSelectionLocked();
                return found;
            }
        }

        public static bool SelectHoverItem(out int itemType)
        {
            lock (SyncRoot)
            {
                itemType = _hoverItemType;
            }

            return itemType > 0 && SelectItem(itemType);
        }

        public static void BeginPendingSelection(ulong startGameUpdateCount, string sourceSummary)
        {
            lock (SyncRoot)
            {
                _selectionPending = true;
                _selectionState = SearchItemPickSelectionState.WaitingButtonRelease;
                _selectionStartGameUpdateCount = startGameUpdateCount;
                // The button click already used the left mouse button; target
                // picking must wait for that press to be released before arming.
                _selectionWaitingForMouseRelease = true;
                _selectionHintText = "已进入选择物品模式：松开按钮后，再左键点击要查询的目标。";
                _selectionSourceSummary = NormalizeSelectionSource(sourceSummary);
                Candidates.Clear();
                _candidateMessage = "等待左键选择物品";
                ClearCandidateViewportLocked();
            }
        }

        public static bool MarkSelectionArmedForNextLeftClick(ulong currentGameUpdateCount)
        {
            lock (SyncRoot)
            {
                if (!_selectionPending ||
                    _selectionState != SearchItemPickSelectionState.WaitingButtonRelease)
                {
                    return false;
                }

                _selectionState = SearchItemPickSelectionState.ArmedForNextLeftClick;
                _selectionWaitingForMouseRelease = false;
                _selectionHintText = "选择物品模式已就绪：左键点击背包物品、掉落物、物块或墙。";
                if (currentGameUpdateCount > 0)
                {
                    _selectionStartGameUpdateCount = currentGameUpdateCount;
                }

                return true;
            }
        }

        public static bool CompletePendingSelectionWithItem(int itemType, string sourceSummary)
        {
            var result = ItemQueryService.BuildQuery(itemType);
            lock (SyncRoot)
            {
                var found = ApplySelectedItemLocked(itemType, result);
                _selectionPending = false;
                _selectionState = found
                    ? SearchItemPickSelectionState.Resolved
                    : SearchItemPickSelectionState.CancelledOrFailed;
                _selectionWaitingForMouseRelease = false;
                _selectionHintText = found
                    ? "已选择物品并展示资料。"
                    : "未找到对应物品资料，请点击“选择物品”重试。";
                _selectionSourceSummary = NormalizeSelectionSource(sourceSummary);
                return found;
            }
        }

        public static void CompletePendingSelectionFailed(string hintText, string sourceSummary)
        {
            lock (SyncRoot)
            {
                _queryText = string.Empty;
                _selectedResult = null;
                _selectedItemType = 0;
                Candidates.Clear();
                ClearCandidateViewportLocked();
                _candidateMessage = string.IsNullOrWhiteSpace(hintText)
                    ? "未识别到可查询物品"
                    : hintText.Trim();
                _selectionPending = false;
                _selectionState = SearchItemPickSelectionState.CancelledOrFailed;
                _selectionStartGameUpdateCount = 0;
                _selectionWaitingForMouseRelease = false;
                _selectionHintText = _candidateMessage;
                _selectionSourceSummary = NormalizeSelectionSource(sourceSummary);
            }
        }

        public static bool TryRefreshHoverItemFromFreshSnapshot(ulong currentGameUpdateCount, int mouseX, int mouseY)
        {
            TerrariaUiHoverItemSnapshot snapshot;
            if (!TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(currentGameUpdateCount, mouseX, mouseY, out snapshot) ||
                snapshot == null ||
                snapshot.ItemType <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                // The vanilla ItemSlot hover bridge is only fresh while the mouse
                // stays over the slot. Store value facts here so the later F5
                // button click does not need to reread or mutate inventory state.
                _hoverItemType = snapshot.ItemType;
                _hoverItemStack = Math.Max(1, snapshot.Stack);
                _hoverItemName = NormalizeDisplayName(snapshot.Name, snapshot.ItemType);
                _hoverItemSource = snapshot.Source ?? string.Empty;
                _hoverItemGameUpdateCount = snapshot.GameUpdateCount;
            }

            return true;
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _queryText = string.Empty;
                _candidateMessage = string.Empty;
                _selectedResult = null;
                _selectedItemType = 0;
                Candidates.Clear();
                ClearPendingSelectionLocked();
                ClearCandidateViewportLocked();
            }
        }

        public static List<ItemQueryCandidate> GetCandidates()
        {
            lock (SyncRoot)
            {
                var snapshot = new List<ItemQueryCandidate>(Candidates.Count);
                for (var index = 0; index < Candidates.Count; index++)
                {
                    snapshot.Add(Clone(Candidates[index]));
                }

                return snapshot;
            }
        }

        public static ItemQueryResult GetSelectedResult()
        {
            lock (SyncRoot)
            {
                return Clone(_selectedResult);
            }
        }

        public static string GetHoverItemLabel()
        {
            lock (SyncRoot)
            {
                return BuildHoverItemLabelLocked();
            }
        }

        public static string GetHoverItemSource()
        {
            lock (SyncRoot)
            {
                return _hoverItemSource ?? string.Empty;
            }
        }

        public static List<int> GetRecentItemTypes()
        {
            lock (SyncRoot)
            {
                return new List<int>(RecentItemTypes);
            }
        }

        public static void SetCandidateViewport(LegacyUiRect viewport, int contentHeight)
        {
            lock (SyncRoot)
            {
                _candidateViewport = viewport;
                _candidateMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                _candidateScrollOffset = Clamp(_candidateScrollOffset, 0, _candidateMaxScroll);
            }
        }

        public static void ClearCandidateViewport()
        {
            lock (SyncRoot)
            {
                ClearCandidateViewportLocked();
            }
        }

        public static bool TryConsumeCandidateScroll(LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            lock (SyncRoot)
            {
                if (_candidateViewport.Width <= 0 ||
                    _candidateViewport.Height <= 0 ||
                    mouse == null ||
                    rawScrollDelta == 0 ||
                    !_candidateViewport.Contains(mouse.X, mouse.Y))
                {
                    return false;
                }

                var next = Clamp(_candidateScrollOffset + ConvertWheelDelta(rawScrollDelta), 0, _candidateMaxScroll);
                if (next == _candidateScrollOffset)
                {
                    return false;
                }

                _candidateScrollOffset = next;
                return true;
            }
        }

        public static int BuildStateSignature()
        {
            unchecked
            {
                lock (SyncRoot)
                {
                    var hash = 17;
                    AddHash(ref hash, _queryText);
                    AddHash(ref hash, _candidateMessage);
                    AddHash(ref hash, Candidates.Count);
                    for (var index = 0; index < Candidates.Count; index++)
                    {
                        AddCandidateHash(ref hash, Candidates[index]);
                    }

                    AddHash(ref hash, _candidateScrollOffset);
                    AddHash(ref hash, _selectedItemType);
                    AddResultLayoutHash(ref hash, _selectedResult);
                    AddHash(ref hash, _hoverItemType);
                    AddHash(ref hash, _hoverItemName);
                    AddHash(ref hash, _hoverItemSource);
                    AddHash(ref hash, (int)(_hoverItemGameUpdateCount & 0x7fffffff));
                    AddHash(ref hash, (int)_selectionState);
                    AddHash(ref hash, _selectionPending);
                    AddHash(ref hash, (int)(_selectionStartGameUpdateCount & 0x7fffffff));
                    AddHash(ref hash, (int)((_selectionStartGameUpdateCount >> 31) & 0x7fffffff));
                    AddHash(ref hash, _selectionWaitingForMouseRelease);
                    AddHash(ref hash, _selectionHintText);
                    AddHash(ref hash, _selectionSourceSummary);
                    AddHash(ref hash, RecentItemTypes.Count);
                    return hash;
                }
            }
        }

        private static void AddResultLayoutHash(ref int hash, ItemQueryResult result)
        {
            // The page/content-height cache depends on this compact result hash.
            // Keep every height-affecting result detail here when adding search sections.
            AddHash(ref hash, result == null ? 0 : result.ItemType);
            AddHash(ref hash, result != null && result.Found);
            if (result == null)
            {
                return;
            }

            AddHash(ref hash, result.Status);
            AddItemReferenceHash(ref hash, result.Item);
            AddAcquisitionSourceListHash(ref hash, result.AcquisitionSources);
            AddRecipeListHash(ref hash, result.CraftingSources);
            AddRecipeListHash(ref hash, result.CraftingUses);
            AddShimmerHash(ref hash, result.Shimmer);
        }

        private static void AddCandidateHash(ref int hash, ItemQueryCandidate candidate)
        {
            if (candidate == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, candidate.ItemType);
            AddHash(ref hash, candidate.DisplayName);
            AddHash(ref hash, candidate.InternalName);
        }

        private static void AddAcquisitionSourceListHash(ref int hash, IList<ItemAcquisitionSourceSummary> sources)
        {
            var count = sources == null ? 0 : sources.Count;
            AddHash(ref hash, count);
            for (var index = 0; index < count; index++)
            {
                AddAcquisitionSourceHash(ref hash, sources[index]);
            }
        }

        private static void AddAcquisitionSourceHash(ref int hash, ItemAcquisitionSourceSummary source)
        {
            if (source == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, source.SourceType);
            AddHash(ref hash, source.SourceTag);
            AddHash(ref hash, source.Title);
            AddHash(ref hash, source.SourceName);
            AddHash(ref hash, source.QuantityText);
            AddHash(ref hash, source.ProbabilityText);
            AddHash(ref hash, source.ConditionText);
            AddHash(ref hash, source.ContextText);
            AddHash(ref hash, source.ItemType);
            AddHash(ref hash, source.NpcNetId);
            AddHash(ref hash, source.RelatedItemType);
        }

        private static void AddRecipeListHash(ref int hash, IList<ItemQueryRecipeSummary> recipes)
        {
            var count = recipes == null ? 0 : recipes.Count;
            AddHash(ref hash, count);
            for (var index = 0; index < count; index++)
            {
                AddRecipeHash(ref hash, recipes[index]);
            }
        }

        private static void AddRecipeHash(ref int hash, ItemQueryRecipeSummary recipe)
        {
            if (recipe == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, recipe.RecipeIndex);
            AddItemReferenceHash(ref hash, recipe.CreateItem);
            AddHash(ref hash, recipe.CreateStack);
            AddHash(ref hash, recipe.MatchKind);
            AddHash(ref hash, recipe.MatchedRecipeGroupId);
            var ingredientCount = recipe.Ingredients == null ? 0 : recipe.Ingredients.Count;
            AddHash(ref hash, ingredientCount);
            for (var index = 0; index < ingredientCount; index++)
            {
                AddIngredientHash(ref hash, recipe.Ingredients[index]);
            }
        }

        private static void AddIngredientHash(ref int hash, ItemQueryIngredientSummary ingredient)
        {
            if (ingredient == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, ingredient.IsRecipeGroup);
            AddHash(ref hash, ingredient.RecipeGroupId);
            AddHash(ref hash, ingredient.RecipeGroupName);
            AddItemReferenceHash(ref hash, ingredient.Item);
            AddHash(ref hash, ingredient.Stack);
            AddHash(ref hash, ingredient.MatchesQueriedItem);
            var acceptedCount = ingredient.AcceptedItems == null ? 0 : ingredient.AcceptedItems.Count;
            AddHash(ref hash, acceptedCount);
            for (var index = 0; index < acceptedCount; index++)
            {
                AddItemReferenceHash(ref hash, ingredient.AcceptedItems[index]);
            }
        }

        private static void AddShimmerHash(ref int hash, ItemQueryShimmerSummary shimmer)
        {
            if (shimmer == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddItemReferenceHash(ref hash, shimmer.ForwardResult);
            var reverseCount = shimmer.ReverseSources == null ? 0 : shimmer.ReverseSources.Count;
            AddHash(ref hash, reverseCount);
            for (var index = 0; index < reverseCount; index++)
            {
                AddItemReferenceHash(ref hash, shimmer.ReverseSources[index]);
            }
        }

        private static void AddItemReferenceHash(ref int hash, ItemQueryReference item)
        {
            if (item == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, item.ItemType);
            AddHash(ref hash, item.DisplayName);
            AddHash(ref hash, item.InternalName);
            AddHash(ref hash, item.Stack);
            AddHash(ref hash, item.MaxStack);
            AddHash(ref hash, item.Rare);
            AddHash(ref hash, item.Value);
            AddHash(ref hash, item.IsMaterial);
            AddHash(ref hash, item.IsConsumable);
            AddHash(ref hash, item.CreateTile);
            AddHash(ref hash, item.CreateWall);
        }

        public static string BuildUiStateJson()
        {
            lock (SyncRoot)
            {
                return "{" +
                       "\"query\":\"" + EscapeJson(_queryText) + "\"," +
                       "\"candidateCount\":" + Candidates.Count.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectedItemType\":" + _selectedItemType.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"hasResult\":" + ((_selectedResult != null && _selectedResult.Found) ? "true" : "false") + "," +
                       "\"hoverItemType\":" + _hoverItemType.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"hoverSource\":\"" + EscapeJson(_hoverItemSource) + "\"," +
                       "\"hoverGameUpdateCount\":" + _hoverItemGameUpdateCount.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectionState\":\"" + _selectionState.ToString() + "\"," +
                       "\"selectionPending\":" + (_selectionPending ? "true" : "false") + "," +
                       "\"selectionStartGameUpdateCount\":" + _selectionStartGameUpdateCount.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectionWaitingForMouseRelease\":" + (_selectionWaitingForMouseRelease ? "true" : "false") + "," +
                       "\"selectionHint\":\"" + EscapeJson(_selectionHintText) + "\"," +
                       "\"selectionSource\":\"" + EscapeJson(_selectionSourceSummary) + "\"," +
                       "\"recentItemCount\":" + RecentItemTypes.Count.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"candidateScrollOffset\":" + _candidateScrollOffset.ToString(CultureInfo.InvariantCulture) +
                       "}";
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _queryText = string.Empty;
                _candidateMessage = string.Empty;
                _selectedResult = null;
                _selectedItemType = 0;
                Candidates.Clear();
                RecentItemTypes.Clear();
                _hoverItemType = 0;
                _hoverItemStack = 0;
                _hoverItemName = string.Empty;
                _hoverItemSource = string.Empty;
                _hoverItemGameUpdateCount = 0;
                ClearPendingSelectionLocked();
                ClearCandidateViewportLocked();
            }
        }

        internal static void SetSelectedResultForTesting(ItemQueryResult result)
        {
            lock (SyncRoot)
            {
                _selectedResult = Clone(result);
                _selectedItemType = result != null && result.Found ? result.ItemType : 0;
                _queryText = BuildSelectedQueryText(result, result == null ? 0 : result.ItemType);
                Candidates.Clear();
                _candidateMessage = string.Empty;
                ClearCandidateViewportLocked();
            }
        }

        private static void RebuildCandidatesLocked()
        {
            Candidates.Clear();
            ClearCandidateViewportLocked();

            if (_queryText.Length <= 0)
            {
                _candidateMessage = string.Empty;
                return;
            }

            IList<ItemQueryCandidate> candidates;
            try
            {
                candidates = ItemQueryService.ResolveCandidates(_queryText, CandidateMaxResults);
            }
            catch
            {
                _candidateMessage = "物品候选读取失败";
                return;
            }

            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = NormalizeCandidate(candidates[index]);
                    if (candidate != null)
                    {
                        Candidates.Add(candidate);
                    }
                }
            }

            _candidateMessage = Candidates.Count <= 0 ? "未找到匹配物品" : string.Empty;
        }

        private static ItemQueryCandidate NormalizeCandidate(ItemQueryCandidate candidate)
        {
            if (candidate == null || candidate.ItemType <= 0)
            {
                return null;
            }

            return new ItemQueryCandidate
            {
                ItemType = candidate.ItemType,
                DisplayName = NormalizeDisplayName(candidate.DisplayName, candidate.ItemType),
                InternalName = candidate.InternalName ?? string.Empty
            };
        }

        private static string BuildSelectedQueryText(ItemQueryResult result, int itemType)
        {
            if (result != null && result.Item != null && !string.IsNullOrWhiteSpace(result.Item.DisplayName))
            {
                return result.Item.DisplayName.Trim();
            }

            return "#" + itemType.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildHoverItemLabelLocked()
        {
            if (_hoverItemType <= 0)
            {
                return string.Empty;
            }

            var label = NormalizeDisplayName(_hoverItemName, _hoverItemType);
            if (_hoverItemStack > 1)
            {
                label += " x" + _hoverItemStack.ToString(CultureInfo.InvariantCulture);
            }

            return label + " #" + _hoverItemType.ToString(CultureInfo.InvariantCulture);
        }

        private static bool ApplySelectedItemLocked(int itemType, ItemQueryResult result)
        {
            var found = result != null && result.Found;
            _selectedResult = result;
            _selectedItemType = found ? itemType : 0;
            _queryText = BuildSelectedQueryText(result, itemType);
            Candidates.Clear();
            _candidateMessage = found ? string.Empty : "未找到对应物品";
            ClearCandidateViewportLocked();
            if (found)
            {
                RecordRecentItemLocked(itemType);
            }

            return found;
        }

        private static void RecordRecentItemLocked(int itemType)
        {
            if (itemType <= 0)
            {
                return;
            }

            if (RecentItemTypes.Count > 0 && RecentItemTypes[0] == itemType)
            {
                return;
            }

            RecentItemTypes.Remove(itemType);
            RecentItemTypes.Insert(0, itemType);
            while (RecentItemTypes.Count > RecentItemHistoryLimit)
            {
                RecentItemTypes.RemoveAt(RecentItemTypes.Count - 1);
            }
        }

        private static string NormalizeDisplayName(string displayName, int itemType)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? "#" + itemType.ToString(CultureInfo.InvariantCulture)
                : displayName.Trim();
        }

        private static void ClearCandidateViewportLocked()
        {
            _candidateScrollOffset = 0;
            _candidateMaxScroll = 0;
            _candidateViewport = new LegacyUiRect();
        }

        private static void ClearPendingSelectionLocked()
        {
            _selectionState = SearchItemPickSelectionState.Idle;
            _selectionPending = false;
            _selectionStartGameUpdateCount = 0;
            _selectionWaitingForMouseRelease = false;
            _selectionHintText = string.Empty;
            _selectionSourceSummary = string.Empty;
        }

        private static string NormalizeSelectionSource(string sourceSummary)
        {
            if (string.IsNullOrWhiteSpace(sourceSummary))
            {
                return "unknown";
            }

            var text = sourceSummary.Trim();
            return text.Length > 80 ? text.Substring(0, 80) : text;
        }

        private static int ConvertWheelDelta(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static ItemQueryCandidate Clone(ItemQueryCandidate candidate)
        {
            return candidate == null ? null : new ItemQueryCandidate
            {
                ItemType = candidate.ItemType,
                DisplayName = candidate.DisplayName,
                InternalName = candidate.InternalName
            };
        }

        private static ItemQueryResult Clone(ItemQueryResult result)
        {
            if (result == null)
            {
                return null;
            }

            var clone = new ItemQueryResult
            {
                ItemType = result.ItemType,
                Found = result.Found,
                Status = result.Status,
                Item = Clone(result.Item),
                Shimmer = Clone(result.Shimmer)
            };
            AddAcquisitionSources(clone.AcquisitionSources, result.AcquisitionSources);
            AddRecipes(clone.CraftingSources, result.CraftingSources);
            AddRecipes(clone.CraftingUses, result.CraftingUses);
            return clone;
        }

        private static ItemQueryReference Clone(ItemQueryReference item)
        {
            return item == null ? null : new ItemQueryReference
            {
                ItemType = item.ItemType,
                DisplayName = item.DisplayName,
                InternalName = item.InternalName,
                Stack = item.Stack,
                MaxStack = item.MaxStack,
                Rare = item.Rare,
                Value = item.Value,
                IsMaterial = item.IsMaterial,
                IsConsumable = item.IsConsumable,
                CreateTile = item.CreateTile,
                CreateWall = item.CreateWall
            };
        }

        private static ItemQueryShimmerSummary Clone(ItemQueryShimmerSummary shimmer)
        {
            var clone = new ItemQueryShimmerSummary();
            if (shimmer == null)
            {
                return clone;
            }

            clone.ForwardResult = Clone(shimmer.ForwardResult);
            for (var index = 0; index < shimmer.ReverseSources.Count; index++)
            {
                clone.ReverseSources.Add(Clone(shimmer.ReverseSources[index]));
            }

            return clone;
        }

        private static void AddAcquisitionSources(IList<ItemAcquisitionSourceSummary> target, IList<ItemAcquisitionSourceSummary> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                target.Add(Clone(source[index]));
            }
        }

        private static ItemAcquisitionSourceSummary Clone(ItemAcquisitionSourceSummary source)
        {
            return source == null ? null : new ItemAcquisitionSourceSummary
            {
                SourceType = source.SourceType,
                SourceTag = source.SourceTag,
                Title = source.Title,
                SourceName = source.SourceName,
                QuantityText = source.QuantityText,
                ProbabilityText = source.ProbabilityText,
                ConditionText = source.ConditionText,
                ContextText = source.ContextText,
                ItemType = source.ItemType,
                NpcNetId = source.NpcNetId,
                RelatedItemType = source.RelatedItemType
            };
        }

        private static void AddRecipes(IList<ItemQueryRecipeSummary> target, IList<ItemQueryRecipeSummary> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                target.Add(Clone(source[index]));
            }
        }

        private static ItemQueryRecipeSummary Clone(ItemQueryRecipeSummary recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            var clone = new ItemQueryRecipeSummary
            {
                RecipeIndex = recipe.RecipeIndex,
                CreateItem = Clone(recipe.CreateItem),
                CreateStack = recipe.CreateStack,
                MatchKind = recipe.MatchKind,
                MatchedRecipeGroupId = recipe.MatchedRecipeGroupId
            };
            for (var index = 0; index < recipe.Ingredients.Count; index++)
            {
                clone.Ingredients.Add(Clone(recipe.Ingredients[index]));
            }

            return clone;
        }

        private static ItemQueryIngredientSummary Clone(ItemQueryIngredientSummary ingredient)
        {
            if (ingredient == null)
            {
                return null;
            }

            var clone = new ItemQueryIngredientSummary
            {
                IsRecipeGroup = ingredient.IsRecipeGroup,
                RecipeGroupId = ingredient.RecipeGroupId,
                RecipeGroupName = ingredient.RecipeGroupName,
                Item = Clone(ingredient.Item),
                Stack = ingredient.Stack,
                MatchesQueriedItem = ingredient.MatchesQueriedItem
            };
            for (var index = 0; index < ingredient.AcceptedItems.Count; index++)
            {
                clone.AcceptedItems.Add(Clone(ingredient.AcceptedItems[index]));
            }

            return clone;
        }

        private static string NormalizeQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        }

        private static void AddHash(ref int hash, int value)
        {
            hash = hash * 31 + value;
        }

        private static void AddHash(ref int hash, bool value)
        {
            hash = hash * 31 + (value ? 1 : 0);
        }

        private static void AddHash(ref int hash, string value)
        {
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
