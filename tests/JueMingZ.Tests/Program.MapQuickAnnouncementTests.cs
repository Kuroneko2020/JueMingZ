using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.Information;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesMapQuickAnnouncementConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapQuickAnnouncement, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected map quick announcement feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Map quick announcement config must be visible in the map enhancement UI.");
            }

            if (feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Map quick announcement must default to disabled.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.Information ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement)
            {
                throw new InvalidOperationException("Map quick announcement must keep Information code-domain and MapEnhancement UI category.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.InlineHotkey)
            {
                throw new InvalidOperationException("Map quick announcement must expose inline hotkey-style config metadata.");
            }

            if (feature.RequiredActions.Count != 1 || feature.RequiredActions[0] != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Map quick announcement must declare raw input as its future trigger boundary.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
            {
                throw new InvalidOperationException("Map quick announcement multiplayer metadata must stay pending until chat send is implemented and verified.");
            }
        }

        private static void MapQuickAnnouncementSettingsNormalizeSlotsAndDefaults()
        {
            var defaults = MapQuickAnnouncementSettings.CreateDefaultHotkey();
            AssertStringEquals(defaults.Slot1, MapQuickAnnouncementSettings.DefaultHotkeySlot1, "map quick announcement default slot1");
            AssertStringEquals(defaults.Slot2, MapQuickAnnouncementSettings.DefaultHotkeySlot2, "map quick announcement default slot2");
            AssertStringEquals(defaults.TriggerKey, MapQuickAnnouncementSettings.DefaultTriggerKey, "map quick announcement default trigger");

            var normalized = MapQuickAnnouncementSettings.NormalizeHotkey("shift", "MOUSE4", "leftmouse");
            AssertStringEquals(normalized.Slot1, "Shift", "map quick announcement keyboard slot normalization");
            AssertStringEquals(normalized.Slot2, string.Empty, "map quick announcement mouse key rejected from slot2");
            AssertStringEquals(normalized.TriggerKey, "MouseLeft", "map quick announcement trigger mouse normalization");

            var duplicate = MapQuickAnnouncementSettings.NormalizeHotkey("H", "h", "H");
            AssertStringEquals(duplicate.Slot1, "H", "map quick announcement duplicate slot1 kept");
            AssertStringEquals(duplicate.Slot2, string.Empty, "map quick announcement duplicate slot2 cleared");
            AssertStringEquals(duplicate.TriggerKey, string.Empty, "map quick announcement duplicate trigger cleared");

            AssertStringEquals(MapQuickAnnouncementSettings.DisplayKey("MouseLeft"), "左键", "map quick announcement display left mouse");
            AssertStringEquals(MapQuickAnnouncementSettings.NormalizeColorHex("ffd966"), MapQuickAnnouncementSettings.DefaultAnnouncementColorHex, "map quick announcement color upper");
            AssertStringEquals(MapQuickAnnouncementSettings.NormalizeColorHex("not-a-color"), MapQuickAnnouncementSettings.DefaultAnnouncementColorHex, "map quick announcement invalid color default");
            if (MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(0, MapQuickAnnouncementSettings.DefaultCooldownMilliseconds) !=
                MapQuickAnnouncementSettings.DefaultCooldownMilliseconds)
            {
                throw new InvalidOperationException("Map quick announcement cooldown must use fallback for missing values.");
            }
        }

        private static void MapQuickAnnouncementCaptureRulesRejectInvalidMouseWheelAndDuplicates()
        {
            var settings = AppSettings.CreateDefault();
            string resultCode;
            if (LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "1", "MouseLeft", out resultCode) ||
                !string.Equals(resultCode, "invalidKeyboardKey", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement prefix slots must reject mouse capture.");
            }

            AssertStringEquals(settings.MapQuickAnnouncementHotkeySlot1, "Alt", "map quick announcement slot1 preserved after invalid mouse capture");
            if (LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "trigger", "MouseWheelUp", out resultCode) ||
                !string.Equals(resultCode, "invalidTriggerKey", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement trigger slot must reject mouse wheel capture.");
            }

            if (LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "2", "Alt", out resultCode) ||
                !string.Equals(resultCode, "duplicateKey", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement capture must reject duplicate keys.");
            }

            if (!LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "1", "H", out resultCode) ||
                !LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "2", "J", out resultCode) ||
                !LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "trigger", "K", out resultCode))
            {
                throw new InvalidOperationException("Map quick announcement pure keyboard H+J+K capture must be accepted.");
            }

            AssertStringEquals(settings.MapQuickAnnouncementHotkeySlot1, "H", "map quick announcement captured slot1 H");
            AssertStringEquals(settings.MapQuickAnnouncementHotkeySlot2, "J", "map quick announcement captured slot2 J");
            AssertStringEquals(settings.MapQuickAnnouncementTriggerKey, "K", "map quick announcement captured trigger K");

            settings = AppSettings.CreateDefault();
            if (!LegacyMainWindow.TryApplyMapQuickAnnouncementCapturedTokenForTesting(settings, "trigger", "XButton1", out resultCode))
            {
                throw new InvalidOperationException("Map quick announcement trigger slot must accept side mouse button 1.");
            }

            AssertStringEquals(settings.MapQuickAnnouncementTriggerKey, "Mouse4", "map quick announcement side mouse token normalized");
        }

        private static void MapQuickAnnouncementHotkeyStateMachineFiresOnTriggerEdgeOnceUntilRelease()
        {
            var hotkey = new MapQuickAnnouncementHotkey("H", "J", "K");
            var held = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var machine = new MapQuickAnnouncementHotkeyStateMachine();

            var state = machine.Update(hotkey, held.Contains);
            if (state.Triggered || state.CombinationHeld)
            {
                throw new InvalidOperationException("Map quick announcement must not trigger before the chord is held.");
            }

            held.Add("K");
            state = machine.Update(hotkey, held.Contains);
            if (state.Triggered)
            {
                throw new InvalidOperationException("Map quick announcement must not trigger when the trigger key is pressed before prefixes.");
            }

            held.Add("H");
            held.Add("J");
            state = machine.Update(hotkey, held.Contains);
            if (state.Triggered)
            {
                throw new InvalidOperationException("Map quick announcement must require a trigger-key press edge while prefixes are held.");
            }

            held.Remove("K");
            machine.Update(hotkey, held.Contains);
            held.Add("K");
            state = machine.Update(hotkey, held.Contains);
            if (!state.Triggered || !state.CombinationHeld || !state.LatchedUntilRelease)
            {
                throw new InvalidOperationException("Map quick announcement must fire on the trigger-key edge when prefixes are held.");
            }

            state = machine.Update(hotkey, held.Contains);
            if (state.Triggered)
            {
                throw new InvalidOperationException("Map quick announcement must fire only once while the same chord remains held.");
            }

            held.Remove("H");
            state = machine.Update(hotkey, held.Contains);
            if (state.LatchedUntilRelease)
            {
                throw new InvalidOperationException("Map quick announcement latch must reset when any required key is released.");
            }

            held.Add("H");
            state = machine.Update(hotkey, held.Contains);
            if (state.Triggered)
            {
                throw new InvalidOperationException("Map quick announcement must not refire until the trigger key gets a new press edge.");
            }

            held.Remove("K");
            machine.Update(hotkey, held.Contains);
            held.Add("K");
            state = machine.Update(hotkey, held.Contains);
            if (!state.Triggered)
            {
                throw new InvalidOperationException("Map quick announcement must fire again after release and a new trigger edge.");
            }
        }

        private static void MapQuickAnnouncementHotkeyStateMachineSupportsMouseTrigger()
        {
            var hotkey = new MapQuickAnnouncementHotkey("Alt", "Shift", "Mouse5");
            var held = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var machine = new MapQuickAnnouncementHotkeyStateMachine();

            held.Add("Alt");
            held.Add("Shift");
            machine.Update(hotkey, held.Contains);
            held.Add("Mouse5");
            var state = machine.Update(hotkey, held.Contains);
            if (!state.Triggered || !state.TriggerPressedEdge)
            {
                throw new InvalidOperationException("Map quick announcement must support mouse side button trigger edges.");
            }
        }

        private static void RuntimeSettingsSnapshotCarriesMapQuickAnnouncementConfig()
        {
            var settings = AppSettings.CreateDefault();
            settings.MapQuickAnnouncementEnabled = true;
            settings.MapQuickAnnouncementHotkeySlot1 = "H";
            settings.MapQuickAnnouncementHotkeySlot2 = "Mouse4";
            settings.MapQuickAnnouncementTriggerKey = "Mouse5";
            settings.MapQuickAnnouncementColorHex = "ffd966";
            settings.MapQuickAnnouncementCooldownMilliseconds = 1;
            settings.MapQuickAnnouncementAirCooldownMilliseconds = 0;

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!snapshot.MapQuickAnnouncementEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose map quick announcement enabled flag.");
            }

            AssertStringEquals(snapshot.MapQuickAnnouncementHotkeySlot1, "H", "runtime map quick announcement slot1");
            AssertStringEquals(snapshot.MapQuickAnnouncementHotkeySlot2, string.Empty, "runtime map quick announcement slot2");
            AssertStringEquals(snapshot.MapQuickAnnouncementTriggerKey, "Mouse5", "runtime map quick announcement trigger");
            AssertStringEquals(snapshot.MapQuickAnnouncementColorHex, MapQuickAnnouncementSettings.DefaultAnnouncementColorHex, "runtime map quick announcement color");
            if (snapshot.MapQuickAnnouncementCooldownMilliseconds != MapQuickAnnouncementSettings.MinCooldownMilliseconds ||
                snapshot.MapQuickAnnouncementAirCooldownMilliseconds != MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds)
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize map quick announcement cooldowns.");
            }
        }

        private static void LegacyMapEnhancementPageLayoutTracksQuickAnnouncementState()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 128);
            var expectedHeight =
                LegacyUiMetrics.RowHeight * 2 +
                LegacyUiMetrics.SettingRowGap +
                24;

            if (LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting() != expectedHeight)
            {
                throw new InvalidOperationException("Map enhancement content height must include the inline quick announcement row and future placeholder.");
            }

            AssertStringEquals(
                LegacyMainWindow.MapEnhancementFuturePlaceholderText,
                "更多功能正在开发中",
                "map enhancement future placeholder");

            if (expectedHeight > content.Height)
            {
                throw new InvalidOperationException("Map enhancement placeholder should fit in the default content viewport without requiring scroll.");
            }

            var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
            settings.MapQuickAnnouncementEnabled = true;
            var changed = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
            if (changed.PageStateSignature == first.PageStateSignature ||
                changed.RebuildCount <= first.RebuildCount)
            {
                throw new InvalidOperationException("Map enhancement page layout must dirty when quick announcement state changes.");
            }

            AssertStringEquals(
                LegacyMainWindow.BuildMapQuickAnnouncementHotkeyDisplayForTesting(AppSettings.CreateDefault()),
                "Alt|Shift|左键",
                "map quick announcement default UI display");
        }

        private static void LegacyMapQuickAnnouncementButtonTooltipsMatchRequestedWording()
        {
            var tooltips = LegacyMainWindow.GetMapQuickAnnouncementButtonTooltipsForTesting();
            if (tooltips == null || tooltips.Length != 5)
            {
                throw new InvalidOperationException("Map quick announcement tooltip test contract must expose five button slots.");
            }

            AssertStringEquals(tooltips[0], "双击进行改键，不支持鼠标按键", "map quick announcement slot1 tooltip");
            AssertStringEquals(tooltips[1], "双击进行改键，不支持鼠标按键", "map quick announcement slot2 tooltip");
            AssertStringEquals(tooltips[2], "双击进行改键，支持鼠标按键", "map quick announcement trigger tooltip");
            AssertStringEquals(tooltips[3], "按下快捷键对鼠标位置内容进行广播", "map quick announcement on tooltip");
            AssertStringEquals(tooltips[4], string.Empty, "map quick announcement off tooltip");
        }

        private static void MapQuickAnnouncementHoverSnapshotTracksItemSlotFreshness()
        {
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var item = new TestQuickAnnouncementHoverItem
                {
                    type = 9,
                    stack = 42,
                    prefix = 1,
                    Name = "木材"
                };

                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 8, 3, 123, 64, 96))
                {
                    throw new InvalidOperationException("Expected ItemSlot hover item snapshot capture to succeed.");
                }

                TerrariaUiHoverItemSnapshot snapshot;
                if (!TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(123, 64, 96, out snapshot) || snapshot == null)
                {
                    throw new InvalidOperationException("Expected same-frame ItemSlot hover snapshot to be fresh.");
                }

                if (snapshot.ItemType != 9 ||
                    snapshot.Stack != 42 ||
                    snapshot.Prefix != 1 ||
                    snapshot.Context != 8 ||
                    snapshot.Slot != 3 ||
                    snapshot.GameUpdateCount != 123 ||
                    snapshot.MouseX != 64 ||
                    snapshot.MouseY != 96)
                {
                    throw new InvalidOperationException("ItemSlot hover snapshot must preserve item, source, frame, and mouse facts.");
                }

                AssertStringEquals(snapshot.Name, "木材", "map quick announcement hover snapshot name");
                AssertStringEquals(snapshot.Source, "ItemSlot:8:3", "map quick announcement hover snapshot source");

                if (!TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(124, 64, 96, out snapshot))
                {
                    throw new InvalidOperationException("Expected previous UI draw hover snapshot to remain fresh for the next update prefix.");
                }

                if (!TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(129, 67, 99, out snapshot))
                {
                    throw new InvalidOperationException("Expected ItemSlot hover snapshot to bridge short draw/update jitter and tiny mouse drift.");
                }

                if (TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(130, 64, 96, out snapshot))
                {
                    throw new InvalidOperationException("ItemSlot hover snapshot must expire after the short bridge window.");
                }

                if (TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(123, 70, 96, out snapshot))
                {
                    throw new InvalidOperationException("ItemSlot hover snapshot must not match after the mouse leaves the recorded position.");
                }
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementItemSlotHookCandidateSummaryCoversForwardingOverloads()
        {
            var summary = MapQuickAnnouncementItemSlotHoverHookInstaller.GetMouseHoverCandidateSummaryForTesting(
                typeof(TestItemSlotMouseHoverOverloads));
            AssertContains(summary, "MouseHover(System.Int32 context)");
            AssertContains(summary, "MouseHover(JueMingZ.Tests.Program+TestQuickAnnouncementHoverItem item, System.Int32 context)");
            AssertContains(summary, "MouseHover(JueMingZ.Tests.Program+TestQuickAnnouncementHoverItem[] inv, System.Int32 context, System.Int32 slot)");

            var selected = MapQuickAnnouncementItemSlotHoverHookInstaller.GetSelectedMouseHoverSignatureForTesting(
                typeof(TestItemSlotMouseHoverOverloads));
            AssertContains(selected, "MouseHover(JueMingZ.Tests.Program+TestQuickAnnouncementHoverItem[] inv, System.Int32 context, System.Int32 slot)");
        }

        private static void MapQuickAnnouncementHoverSnapshotReadStatusDistinguishesFailureModes()
        {
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                TerrariaUiHoverSlotSnapshot slotSnapshot;
                TerrariaUiHoverSlotReadResult readResult;
                if (TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(100, 30, 40, out slotSnapshot, out readResult) ||
                    readResult == null ||
                    !string.Equals(readResult.Status, "noSnapshot", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected hover slot read status to report noSnapshot before any ItemSlot hit.");
                }

                var item = new TestQuickAnnouncementHoverItem
                {
                    type = 9,
                    stack = 1,
                    Name = "木材"
                };
                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 8, 3, 200, 30, 40) ||
                    !TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(201, 30, 40, out slotSnapshot, out readResult) ||
                    readResult == null ||
                    !string.Equals(readResult.Status, "freshItem", StringComparison.Ordinal) ||
                    !readResult.HasActiveItem ||
                    readResult.AgeUpdates != 1)
                {
                    throw new InvalidOperationException("Expected hover slot read status to report a fresh active item.");
                }

                var emptyItem = new TestQuickAnnouncementHoverItem();
                if (TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(emptyItem, 8, 5, 300, 30, 40))
                {
                    throw new InvalidOperationException("Empty slot capture should save slot proof without returning an active item.");
                }

                if (!TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(301, 30, 40, out slotSnapshot, out readResult) ||
                    readResult == null ||
                    !string.Equals(readResult.Status, "freshEmptySlot", StringComparison.Ordinal) ||
                    readResult.HasActiveItem)
                {
                    throw new InvalidOperationException("Expected hover slot read status to report a fresh empty UI slot.");
                }

                if (TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(301, 40, 40, out slotSnapshot, out readResult) ||
                    readResult == null ||
                    !string.Equals(readResult.Status, "mouseLeft", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected hover slot read status to report mouseLeft when the pointer leaves the recorded slot.");
                }

                if (TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(307, 30, 40, out slotSnapshot, out readResult) ||
                    readResult == null ||
                    !string.Equals(readResult.Status, "expired", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected hover slot read status to report expired after the bridge window.");
                }
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementResolverUsesFreshUiHoverSnapshots()
        {
            var uiContexts = new[] { 0, 3, 8, 9, 11 };
            for (var index = 0; index < uiContexts.Length; index++)
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                var contextId = uiContexts[index];
                var itemName = "UI物品" + contextId.ToString(CultureInfo.InvariantCulture);
                var item = new TestQuickAnnouncementHoverItem
                {
                    type = 100 + contextId,
                    stack = 2,
                    Name = itemName
                };

                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, contextId, index, 200, 30, 40))
                {
                    throw new InvalidOperationException("Expected UI hover snapshot capture for context " + contextId + ".");
                }

                var context = CreateQuickAnnouncementContext(20f, 20f);
                context.GameUpdateCount = 200;
                context.MouseScreenX = 30;
                context.MouseScreenY = 40;
                context.Tile = new MapQuickAnnouncementTileTarget
                {
                    Active = true,
                    TileName = "石块"
                };

                if (!MapQuickAnnouncementTargetResolver.TryAddUiHoverItemForTesting(context))
                {
                    throw new InvalidOperationException("Expected fresh UI hover snapshot to populate context " + contextId + ".");
                }

                var result = MapQuickAnnouncementTargetResolver.Resolve(context);
                if (result.Kind != MapQuickAnnouncementTargetKind.UiItem)
                {
                    throw new InvalidOperationException("Fresh UI hover snapshot must win over world tile for context " + contextId + ".");
                }

                var expectedName = MapQuickAnnouncementNameResolver.ResolveItemName(item.type, itemName);
                AssertStringEquals(result.Body, "这里有 2 个 " + expectedName, "map quick announcement fresh UI hover body");
            }

            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
        }

        private static void MapQuickAnnouncementStaleHoverSnapshotFallsBackToTile()
        {
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var item = new TestQuickAnnouncementHoverItem
                {
                    type = 9,
                    stack = 2,
                    Name = "木材"
                };

                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 0, 0, 200, 30, 40))
                {
                    throw new InvalidOperationException("Expected stale fallback setup snapshot capture to succeed.");
                }

                var context = CreateQuickAnnouncementContext(20f, 20f);
                context.GameUpdateCount = 207;
                context.MouseScreenX = 30;
                context.MouseScreenY = 40;
                context.Tile = new MapQuickAnnouncementTileTarget
                {
                    Active = true,
                    TileName = "石块"
                };

                if (MapQuickAnnouncementTargetResolver.TryAddUiHoverItemForTesting(context))
                {
                    throw new InvalidOperationException("Stale UI hover snapshot must not populate quick announcement context.");
                }

                var result = MapQuickAnnouncementTargetResolver.Resolve(context);
                if (result.Kind != MapQuickAnnouncementTargetKind.Tile)
                {
                    throw new InvalidOperationException("Stale UI hover snapshot must fall back to the existing world target resolution.");
                }

                AssertStringEquals(result.Body, "这里有 石块", "map quick announcement stale UI hover fallback tile body");
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementResolverIgnoresSearchVisibleSlotFallback()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreUiState = CaptureSearchPickFakeUiState();
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var player = CreateSearchPickFakePlayer();
                player.inventory[0] = new Terraria.TestRecipeItem { type = 101, stack = 4 };
                ResetSearchPickFakeUiState(player);

                var context = new MapQuickAnnouncementResolveContext
                {
                    MouseScreenX = SearchPickInventorySlotCenterX(0),
                    MouseScreenY = SearchPickInventorySlotCenterY(0),
                    MouseWorldX = 160f,
                    MouseWorldY = 160f,
                    MouseTileX = 10,
                    MouseTileY = 10,
                    GameUpdateCount = 500
                };

                TerrariaUiHoverSlotSnapshot visibleSlot;
                if (!TerrariaUiMouseCompat.TryReadVisibleItemSlotSnapshot(
                        context.MouseScreenX,
                        context.MouseScreenY,
                        context.GameUpdateCount,
                        out visibleSlot) ||
                    visibleSlot == null ||
                    !visibleSlot.HasActiveItem)
                {
                    throw new InvalidOperationException("Expected search visible-slot test setup to expose an inventory item.");
                }

                if (MapQuickAnnouncementTargetResolver.TryAddUiHoverItemForTesting(context) ||
                    context.UiItem != null ||
                    context.UiSlot != null)
                {
                    throw new InvalidOperationException("Map quick announcement must keep using ItemSlot hover snapshots and must not adopt search visible-slot fallback.");
                }
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                restoreUiState();
                restoreRuntimeTypes();
            }
        }

        private static void MapQuickAnnouncementPlacementNamesPreferItemLocalization()
        {
            ResetQuickAnnouncementPlacementNameFakes();
            try
            {
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[11] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 6, tileStyle = 0 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[170] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 54, tileStyle = 0 };
                Terraria.ID.ContentSamples.ItemsByType[171] = new Terraria.ID.TestContentSampleItem
                {
                    type = 171,
                    createWall = 21
                };

                Terraria.Lang.ItemNames[11] = "铁矿";
                Terraria.Lang.ItemNames[170] = "玻璃";
                Terraria.Lang.ItemNames[171] = "玻璃墙";
                Terraria.Map.MapHelper.TileLookups[Terraria.Map.MapHelper.BuildTileKey(238, 0)] = 2380;
                Terraria.Lang.MapObjectNames[2380] = "世纪之花球茎";
                MapQuickAnnouncementPlacementNameCache.ResetForTesting();

                string source;
                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(6, 0, string.Empty, out source),
                    "铁矿",
                    "map quick announcement iron ore placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement iron ore name source");

                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(54, 0, string.Empty, out source),
                    "玻璃",
                    "map quick announcement glass tile placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement glass tile name source");

                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveWallName(21, string.Empty, out source),
                    "玻璃墙",
                    "map quick announcement glass wall placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement glass wall name source");

                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(238, 0, string.Empty, out source),
                    "世纪之花球茎",
                    "map quick announcement plantera bulb map object fallback");
                AssertStringEquals(source, "mapObject", "map quick announcement plantera bulb name source");

                var context = CreateQuickAnnouncementContext(20f, 20f);
                context.Tile = new MapQuickAnnouncementTileTarget
                {
                    Active = true,
                    TileType = 6,
                    TileName = "铁矿",
                    NameSource = "placementItem"
                };

                var result = MapQuickAnnouncementTargetResolver.Resolve(context);
                AssertContains(result.Detail, "tile:placementItem");
                AssertContains(result.Detail, "type=6");
            }
            finally
            {
                ResetQuickAnnouncementPlacementNameFakes();
            }
        }

        private static void MapQuickAnnouncementMultiTileFurnitureStylesResolveConsistently()
        {
            ResetQuickAnnouncementPlacementNameFakes();
            try
            {
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[1366] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 240, tileStyle = 6 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[2865] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 241, tileStyle = 0 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[1474] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 245, tileStyle = 0 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[4934] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 617, tileStyle = 10 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[4945] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 617, tileStyle = 21 };

                Terraria.Lang.ItemNames[1366] = "毁灭者纪念章";
                Terraria.Lang.ItemNames[2865] = "火星伯爵城";
                Terraria.Lang.ItemNames[1474] = "小幅挂画";
                Terraria.Lang.ItemNames[4934] = "世纪之花圣物";
                Terraria.Lang.ItemNames[4945] = "圣诞坦克圣物";
                Terraria.Map.MapHelper.TileLookups[Terraria.Map.MapHelper.BuildTileKey(240, 99)] = 2499;
                Terraria.Lang.MapObjectNames[2499] = "纪念章";
                MapQuickAnnouncementPlacementNameCache.ResetForTesting();
                MapQuickAnnouncementTileStyleResolver.ResetForTesting();

                var trophyStyleTopLeft = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(240, 6 * 54, 0);
                var trophyStyleMiddle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(240, 6 * 54 + 18, 18);
                var trophyStyleBottomRight = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(240, 6 * 54 + 36, 36);
                if (trophyStyleTopLeft != 6 || trophyStyleMiddle != 6 || trophyStyleBottomRight != 6)
                {
                    throw new InvalidOperationException("Map quick announcement must normalize all cells of the same trophy to the same placement style.");
                }

                string source;
                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(240, trophyStyleMiddle, string.Empty, out source),
                    "毁灭者纪念章",
                    "map quick announcement trophy placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement trophy name source");

                var painting4x3Style = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(241, 54, 36);
                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(241, painting4x3Style, string.Empty, out source),
                    "火星伯爵城",
                    "map quick announcement 4x3 painting placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement 4x3 painting name source");

                var painting2x3Style = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(245, 18, 36);
                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(245, painting2x3Style, string.Empty, out source),
                    "小幅挂画",
                    "map quick announcement 2x3 painting placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement 2x3 painting name source");

                var relicStyleTopLeft = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(617, 10 * 54, 0);
                var relicStyleRightFacingTop = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(617, 10 * 54, 72);
                var relicStyleRightFacingMiddle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(617, 10 * 54 + 18, 72 + 36);
                if (relicStyleTopLeft != 10 || relicStyleRightFacingTop != 10 || relicStyleRightFacingMiddle != 10)
                {
                    throw new InvalidOperationException("Map quick announcement must divide master relic placement frames back to the item placeStyle.");
                }

                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(617, relicStyleRightFacingMiddle, string.Empty, out source),
                    "世纪之花圣物",
                    "map quick announcement master relic placement item name");
                AssertStringEquals(source, "placementItem", "map quick announcement master relic name source");

                AssertStringEquals(
                    MapQuickAnnouncementNameResolver.ResolveTileName(240, 99, string.Empty, out source),
                    "纪念章",
                    "map quick announcement unresolved furniture map object fallback");
                AssertStringEquals(source, "mapObject", "map quick announcement unresolved furniture fallback source");
            }
            finally
            {
                ResetQuickAnnouncementPlacementNameFakes();
            }
        }

        private static void MapQuickAnnouncementResolverPrefersUiItemOverWorldTargets()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.UiItem = new MapQuickAnnouncementItemTarget { ItemType = 9, Stack = 42, Name = "木材" };
            context.Actors.Add(new MapQuickAnnouncementActorTarget
            {
                IsPlayer = true,
                Name = "Alice",
                Life = 400,
                LifeMax = 400,
                HitboxX = 0,
                HitboxY = 0,
                HitboxWidth = 40,
                HitboxHeight = 60
            });
            context.Tile = new MapQuickAnnouncementTileTarget { Active = true, TileName = "石块" };

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.UiItem)
            {
                throw new InvalidOperationException("Map quick announcement must prefer UI hover item over world targets.");
            }

            AssertStringEquals(result.Body, "这里有 42 个 木材", "map quick announcement UI item text");
        }

        private static void MapQuickAnnouncementResolverListsPlayersAndNpcsAtMouse()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.Actors.Add(new MapQuickAnnouncementActorTarget
            {
                IsPlayer = true,
                Name = "Alice",
                Life = 400,
                LifeMax = 400,
                HitboxX = 0,
                HitboxY = 0,
                HitboxWidth = 40,
                HitboxHeight = 60
            });
            context.Actors.Add(new MapQuickAnnouncementActorTarget
            {
                IsPlayer = true,
                IsLocalPlayer = true,
                Name = "Me",
                Life = 400,
                LifeMax = 400,
                Mana = 200,
                ManaMax = 200,
                HitboxX = 10,
                HitboxY = 0,
                HitboxWidth = 40,
                HitboxHeight = 60
            });
            context.Actors.Add(new MapQuickAnnouncementActorTarget
            {
                IsPlayer = false,
                TypeName = "史莱姆",
                Name = "史莱姆",
                Life = 35,
                LifeMax = 35,
                HitboxX = 0,
                HitboxY = 0,
                HitboxWidth = 40,
                HitboxHeight = 40
            });
            context.Actors.Add(new MapQuickAnnouncementActorTarget
            {
                IsPlayer = false,
                IsTownNpc = true,
                TypeName = "向导",
                Name = "Steve",
                Life = 250,
                LifeMax = 250,
                HitboxX = 0,
                HitboxY = 0,
                HitboxWidth = 40,
                HitboxHeight = 40
            });

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.Actor)
            {
                throw new InvalidOperationException("Map quick announcement must resolve players and NPCs before items and tiles.");
            }

            AssertStringEquals(
                result.Body,
                "这里有 Alice 400/400，Me 400/400 200/200，史莱姆 35/35，向导/Steve 250/250",
                "map quick announcement actor text");
        }

        private static void MapQuickAnnouncementResolverAggregatesNearbyDroppedItems()
        {
            var context = CreateQuickAnnouncementContext(165f, 165f);
            context.WorldItems.Add(CreateQuickAnnouncementWorldItem(10, 1, "铁矿", 160f, 160f, 10, 10));
            context.WorldItems.Add(CreateQuickAnnouncementWorldItem(10, 2, "铁矿", 176f, 160f, 11, 10));
            context.WorldItems.Add(CreateQuickAnnouncementWorldItem(10, 7, "铁矿", 192f, 160f, 12, 10));
            context.WorldItems.Add(CreateQuickAnnouncementWorldItem(11, 99, "铜矿", 160f, 176f, 10, 11));

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.WorldItem)
            {
                throw new InvalidOperationException("Map quick announcement must resolve dropped items after actors.");
            }

            AssertStringEquals(result.Body, "这里有 3 个 铁矿", "map quick announcement dropped item stack text");
        }

        private static void MapQuickAnnouncementResolverCombinesTileAndCircuitLayers()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.Tile = new MapQuickAnnouncementTileTarget
            {
                Active = true,
                TileName = "石块",
                RedWire = true,
                BlueWire = true,
                Actuator = true
            };
            context.Wall = new MapQuickAnnouncementWallTarget
            {
                Active = true,
                WallName = "石墙"
            };

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.Tile)
            {
                throw new InvalidOperationException("Map quick announcement must resolve tile and circuit before wall.");
            }

            AssertStringEquals(result.Body, "这里有 石块，红线、蓝线，执行器", "map quick announcement tile circuit text");
        }

        private static void MapQuickAnnouncementResolverTreatsLiquidAsTileLayer()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.Tile = new MapQuickAnnouncementTileTarget
            {
                LiquidAmount = 255,
                LiquidType = 0
            };
            context.Wall = new MapQuickAnnouncementWallTarget
            {
                Active = true,
                WallName = "石墙"
            };

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.Tile)
            {
                throw new InvalidOperationException("Map quick announcement must resolve liquid at tile priority before wall and air.");
            }

            AssertStringEquals(result.Body, "这里有 水", "map quick announcement water text");

            context.Tile = new MapQuickAnnouncementTileTarget
            {
                Active = true,
                TileName = "石块",
                LiquidAmount = 128,
                LiquidType = 1,
                RedWire = true
            };
            result = MapQuickAnnouncementTargetResolver.Resolve(context);
            AssertStringEquals(result.Body, "这里有 石块，熔岩，红线", "map quick announcement tile liquid wire text");

            AssertStringEquals(
                MapQuickAnnouncementTextBuilder.BuildTileText(new MapQuickAnnouncementTileTarget { LiquidAmount = 1, LiquidType = 2 }),
                "这里有 蜂蜜",
                "map quick announcement honey text");
            AssertStringEquals(
                MapQuickAnnouncementTextBuilder.BuildTileText(new MapQuickAnnouncementTileTarget { LiquidAmount = 1, LiquidType = 3 }),
                "这里有 微光",
                "map quick announcement shimmer text");
        }

        private static void MapQuickAnnouncementResolverUsesWallBeforeAir()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.Wall = new MapQuickAnnouncementWallTarget
            {
                Active = true,
                WallName = "石墙"
            };

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.Wall)
            {
                throw new InvalidOperationException("Map quick announcement must resolve wall before air.");
            }

            AssertStringEquals(result.Body, "这里有 石墙", "map quick announcement wall text");
        }

        private static void MapQuickAnnouncementResolverFallsBackToAirPhrase()
        {
            var context = CreateQuickAnnouncementContext(20f, 20f);
            context.AirPhraseIndex = 1;

            var result = MapQuickAnnouncementTargetResolver.Resolve(context);
            if (result.Kind != MapQuickAnnouncementTargetKind.Air)
            {
                throw new InvalidOperationException("Map quick announcement must produce air text when no target exists.");
            }

            AssertStringEquals(result.Body, "这里只有空气", "map quick announcement air text");
        }

        private static void MapQuickAnnouncementTextSafetyWrapsColorAndBlocksInjection()
        {
            var sanitized = MapQuickAnnouncementTextSafety.SanitizeBody("/tp\r\n[c/FF0000:坏消息]");
            AssertStringEquals(sanitized, "tp c/FF0000:坏消息", "map quick announcement sanitized body");

            var colored = MapQuickAnnouncementTextSafety.BuildColoredAnnouncement("/tp\r\n[c/FF0000:坏消息]", "#ffe066");
            AssertStringEquals(colored, "[c/FFE066:tp c/FF0000:坏消息]", "map quick announcement controlled color tag");
            if (colored.IndexOf("[c/FF0000", StringComparison.Ordinal) >= 0 ||
                colored.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement must not preserve injected tags or command-shaped messages.");
            }

            var longBody = new string('A', MapQuickAnnouncementTextSafety.MaxBodyLength + 20);
            if (MapQuickAnnouncementTextSafety.SanitizeBody(longBody).Length != MapQuickAnnouncementTextSafety.MaxBodyLength)
            {
                throw new InvalidOperationException("Map quick announcement text safety must clamp long chat bodies.");
            }
        }

        private static void MapQuickAnnouncementDeliveryHonorsCooldownsAndPromptThrottle()
        {
            var sink = new RecordingQuickAnnouncementSink();
            var service = new MapQuickAnnouncementDeliveryService(sink, sink);
            var options = MapQuickAnnouncementDeliveryOptions.Create("#FFD966", 500, 2000);
            var now = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);
            var tile = CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Tile, "这里有 石块");
            var air = CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Air, "这里只有空气");

            var first = service.TryDeliver(tile, options, now);
            if (!first.Sent || sink.ChatMessages.Count != 1)
            {
                throw new InvalidOperationException("Map quick announcement first normal send must pass.");
            }

            AssertStringEquals(sink.ChatMessages[0], "[c/FFD966:这里有 石块]", "map quick announcement sent chat text");

            var blocked = service.TryDeliver(tile, options, now.AddMilliseconds(100));
            if (!blocked.CooldownBlocked ||
                !blocked.CooldownPromptAttempted ||
                !blocked.CooldownPromptShown ||
                sink.PromptMessages.Count != 1 ||
                sink.ChatMessages.Count != 1)
            {
                throw new InvalidOperationException("Map quick announcement normal cooldown must block chat and show one prompt.");
            }

            blocked = service.TryDeliver(tile, options, now.AddMilliseconds(200));
            if (!blocked.CooldownBlocked ||
                blocked.CooldownPromptAttempted ||
                sink.PromptMessages.Count != 1)
            {
                throw new InvalidOperationException("Map quick announcement cooldown prompt must be throttled.");
            }

            var second = service.TryDeliver(tile, options, now.AddMilliseconds(600));
            if (!second.Sent || sink.ChatMessages.Count != 2)
            {
                throw new InvalidOperationException("Map quick announcement normal cooldown must reopen after 500ms.");
            }

            var airSent = service.TryDeliver(air, options, now.AddMilliseconds(1200));
            if (!airSent.Sent || sink.ChatMessages.Count != 3)
            {
                throw new InvalidOperationException("Map quick announcement air send must pass when global cooldown is open.");
            }

            var ordinaryAfterAir = service.TryDeliver(tile, options, now.AddMilliseconds(1800));
            if (!ordinaryAfterAir.Sent || sink.ChatMessages.Count != 4)
            {
                throw new InvalidOperationException("Map quick announcement air cooldown must not block later non-air targets after global cooldown.");
            }

            var airBlocked = service.TryDeliver(air, options, now.AddMilliseconds(2500));
            if (!airBlocked.CooldownBlocked ||
                !airBlocked.CooldownPromptAttempted ||
                sink.PromptMessages.Count != 2 ||
                sink.ChatMessages.Count != 4)
            {
                throw new InvalidOperationException("Map quick announcement air cooldown must block repeated air announcements.");
            }
        }

        private static void MapQuickAnnouncementDeliveryDoesNotCooldownFailedSend()
        {
            var sink = new RecordingQuickAnnouncementSink();
            var service = new MapQuickAnnouncementDeliveryService(sink, sink);
            var options = MapQuickAnnouncementDeliveryOptions.Create("#FFD966", 500, 2000);
            var now = new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc);
            var tile = CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Tile, "这里有 石块");

            sink.FailNextChat = true;
            var failed = service.TryDeliver(tile, options, now);
            if (failed.Sent ||
                !string.Equals(failed.ResultCode, "sendFailed", StringComparison.Ordinal) ||
                sink.ChatMessages.Count != 0)
            {
                throw new InvalidOperationException("Map quick announcement failed sends must be reported without recording chat.");
            }

            var sent = service.TryDeliver(tile, options, now);
            if (!sent.Sent || sink.ChatMessages.Count != 1)
            {
                throw new InvalidOperationException("Map quick announcement failed sends must not start cooldown.");
            }

            var blocked = service.TryDeliver(tile, options, now.AddMilliseconds(100));
            if (!blocked.CooldownBlocked || sink.ChatMessages.Count != 1)
            {
                throw new InvalidOperationException("Map quick announcement successful retry must start cooldown.");
            }
        }

        private static void MapQuickAnnouncementRuntimeSkipsDisabledAndBlockedContexts()
        {
            var disabledInput = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            disabledInput.FeatureEnabled = false;
            var probe = new RecordingQuickAnnouncementRuntimeProbe();

            var result = MapQuickAnnouncementRuntimeService.TickForTesting(disabledInput, probe.ToPorts());
            if (result.Triggered ||
                probe.ResolveCount != 0 ||
                probe.ConsumeCount != 0 ||
                !string.Equals(result.SkipReason, "disabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement runtime must skip disabled feature before resolving or consuming input.");
            }

            AssertRuntimeBlockedContext("legacyUiVisible", blockedInput =>
            {
                blockedInput.LegacyUiVisible = true;
            });
            AssertRuntimeBlockedContext("legacyUiInteraction", blockedInput =>
            {
                blockedInput.LegacyUiActiveInteraction = true;
            });
            AssertRuntimeBlockedContext("legacyTextInput", blockedInput =>
            {
                blockedInput.LegacyTextInputFocused = true;
            });
            AssertRuntimeBlockedContext("terrariaTextInput:chat", blockedInput =>
            {
                blockedInput.TerrariaTextInputFocused = true;
                blockedInput.TerrariaTextInputReason = "chat";
            });
            AssertRuntimeBlockedContext("npcChat", blockedInput =>
            {
                blockedInput.NpcChatOpen = true;
            });
        }

        private static void MapQuickAnnouncementRuntimeTriggersConsumesAndDelivers()
        {
            var held = new HashSet<string>(StringComparer.Ordinal);
            var machine = new MapQuickAnnouncementHotkeyStateMachine();
            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var ports = probe.ToPorts();
            ports.IsTokenDown = held.Contains;

            held.Add("Alt");
            held.Add("Shift");
            var waiting = MapQuickAnnouncementRuntimeService.TickForTesting(input, machine, ports);
            if (waiting.Triggered || probe.ResolveCount != 0 || probe.ConsumeCount != 0)
            {
                throw new InvalidOperationException("Map quick announcement runtime must wait for the trigger key edge.");
            }

            held.Add("MouseLeft");
            var triggered = MapQuickAnnouncementRuntimeService.TickForTesting(input, machine, ports);
            if (!triggered.Triggered ||
                !triggered.InputConsumeAttempted ||
                !triggered.InputConsumed ||
                !triggered.Delivered ||
                probe.ResolveCount != 1 ||
                probe.ConsumeCount != 1 ||
                probe.DeliverCount != 1 ||
                !string.Equals(probe.LastConsumedToken, "MouseLeft", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement runtime must consume the mouse trigger, resolve the target, and deliver chat.");
            }

            var latched = MapQuickAnnouncementRuntimeService.TickForTesting(input, machine, ports);
            if (latched.Triggered || probe.ResolveCount != 1 || probe.ConsumeCount != 1 || probe.DeliverCount != 1)
            {
                throw new InvalidOperationException("Map quick announcement runtime must not repeat while the same chord remains held.");
            }
        }

        private static void MapQuickAnnouncementRuntimeMousePendingWaitsForNextUiHoverSnapshot()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
                input.GameUpdateCount = 200;
                var state = new MapQuickAnnouncementRuntimeState();
                var probe = new RecordingQuickAnnouncementRuntimeProbe
                {
                    TriggerContext = CreateQuickAnnouncementTriggerContext(30, 40, 200),
                    ResolvePendingUiOverride = (pending, currentGameUpdateCount) =>
                        MapQuickAnnouncementTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                    ResolvePendingFallbackOverride = (pending, currentGameUpdateCount) =>
                        MapQuickAnnouncementResolveAttempt.Failed("fallbackShouldNotRun")
                };
                var ports = probe.ToPorts();

                var first = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!first.Triggered ||
                    !first.InputConsumed ||
                    !first.PendingRequestActive ||
                    first.ResolveAttempted ||
                    probe.DeliverCount != 0 ||
                    state.PendingRequest == null)
                {
                    throw new InvalidOperationException("Mouse trigger must consume input and wait when UI hover has not been produced yet.");
                }

                var pendingSnapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
                AssertStringEquals(pendingSnapshot.LastResultCode, "pendingUiHover", "map quick announcement pending diagnostic result");
                AssertStringEquals(pendingSnapshot.LastPendingState, "waitingForUiHover", "map quick announcement pending diagnostic state");
                AssertStringEquals(pendingSnapshot.LastUiHoverState, "hookNotInstalled", "map quick announcement pending diagnostic hover state");

                var hoverItem = new TestQuickAnnouncementHoverItem
                {
                    type = 9,
                    stack = 7,
                    Name = "木材"
                };
                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(hoverItem, 8, 3, 200, 30, 40))
                {
                    throw new InvalidOperationException("Expected delayed UI hover snapshot capture to succeed.");
                }

                input.GameUpdateCount = 201;
                var second = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!second.Triggered ||
                    !second.ResolveAttempted ||
                    !second.Delivered ||
                    !string.Equals(second.TargetKind, MapQuickAnnouncementTargetKind.UiItem.ToString(), StringComparison.Ordinal) ||
                    probe.DeliverCount != 1 ||
                    state.PendingRequest != null)
                {
                    throw new InvalidOperationException("Pending mouse trigger must resolve to the delayed UI item and then clear.");
                }

                AssertContains(second.ResolveDetail, "uiItem;source=ItemSlot");
                var resolvedSnapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
                AssertStringEquals(resolvedSnapshot.LastPendingState, "resolvedUiHover", "map quick announcement resolved pending diagnostic state");
                AssertStringEquals(resolvedSnapshot.LastUiHoverState, "freshItem", "map quick announcement resolved hover state");
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementRuntimeMousePendingEmptyUiSlotDoesNotFallThrough()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
                input.GameUpdateCount = 300;
                var fallbackCalled = false;
                var state = new MapQuickAnnouncementRuntimeState();
                var probe = new RecordingQuickAnnouncementRuntimeProbe
                {
                    TriggerContext = CreateQuickAnnouncementTriggerContext(30, 40, 300),
                    ResolvePendingUiOverride = (pending, currentGameUpdateCount) =>
                        MapQuickAnnouncementTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                    ResolvePendingFallbackOverride = (pending, currentGameUpdateCount) =>
                    {
                        fallbackCalled = true;
                        return MapQuickAnnouncementResolveAttempt.Success(
                            CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Tile, "这里有 石块"));
                    }
                };
                var ports = probe.ToPorts();

                var first = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!first.PendingRequestActive || state.PendingRequest == null)
                {
                    throw new InvalidOperationException("Expected empty-slot scenario to start as a pending mouse trigger.");
                }

                var emptyItem = new TestQuickAnnouncementHoverItem
                {
                    type = 0,
                    stack = 0,
                    Name = string.Empty
                };
                TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(emptyItem, 8, 5, 300, 30, 40);
                TerrariaUiHoverSlotSnapshot slotSnapshot;
                if (!TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(301, 30, 40, out slotSnapshot) ||
                    slotSnapshot == null ||
                    slotSnapshot.HasActiveItem)
                {
                    throw new InvalidOperationException("Expected empty ItemSlot hover proof to be readable.");
                }

                input.GameUpdateCount = 301;
                var second = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!second.Triggered ||
                    !second.ResolveAttempted ||
                    second.Delivered ||
                    probe.DeliverCount != 0 ||
                    fallbackCalled ||
                    state.PendingRequest != null ||
                    !string.Equals(second.ResultCode, "uiEmptySlot", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Confirmed empty UI slot must suppress delivery and must not fall through to world targets.");
                }

                AssertContains(second.ResolveDetail, "uiSlot:empty;source=ItemSlot");
                var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
                AssertStringEquals(snapshot.LastPendingState, "uiEmptySlot", "map quick announcement empty slot pending state");
                AssertStringEquals(snapshot.LastUiHoverState, "freshEmptySlot", "map quick announcement empty slot hover state");
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementRuntimeMousePendingExpiresThenFallsBackToWorld()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
                input.GameUpdateCount = 400;
                var state = new MapQuickAnnouncementRuntimeState();
                var fallbackCount = 0;
                var probe = new RecordingQuickAnnouncementRuntimeProbe
                {
                    TriggerContext = CreateQuickAnnouncementTriggerContext(64, 96, 400),
                    ResolvePendingUiOverride = (pending, currentGameUpdateCount) =>
                        MapQuickAnnouncementTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                    ResolvePendingFallbackOverride = (pending, currentGameUpdateCount) =>
                    {
                        fallbackCount++;
                        return MapQuickAnnouncementResolveAttempt.Success(
                            CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Tile, "这里有 石块"));
                    }
                };
                var ports = probe.ToPorts();

                var first = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!first.PendingRequestActive || first.ResolveAttempted || fallbackCount != 0 || state.PendingRequest == null)
                {
                    throw new InvalidOperationException("Mouse pending must not resolve world target before the UI hover wait expires.");
                }

                input.GameUpdateCount = 406;
                var expired = MapQuickAnnouncementRuntimeService.TickForTesting(input, state, ports);
                if (!expired.Triggered ||
                    !expired.ResolveAttempted ||
                    !expired.Delivered ||
                    fallbackCount != 1 ||
                    state.PendingRequest != null ||
                    !string.Equals(expired.TargetKind, MapQuickAnnouncementTargetKind.Tile.ToString(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expired mouse pending request must fall back to the captured world target and clear.");
                }

                var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
                AssertStringEquals(snapshot.LastPendingState, "expiredFallback", "map quick announcement expired pending state");
                AssertStringEquals(snapshot.LastUiHoverState, "hookNotInstalled", "map quick announcement expired hover state");
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            }
        }

        private static void MapQuickAnnouncementRuntimeConsumesBeforeCooldownBlock()
        {
            var input = CreateQuickAnnouncementRuntimeInput("MouseRight");
            var probe = new RecordingQuickAnnouncementRuntimeProbe
            {
                DeliveryResult = new MapQuickAnnouncementDeliveryResult
                {
                    CooldownBlocked = true,
                    ResultCode = "cooldown",
                    CooldownRemainingMilliseconds = 400
                }
            };
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (!result.Triggered ||
                !result.InputConsumeAttempted ||
                !result.InputConsumed ||
                result.Delivered ||
                !string.Equals(result.ResultCode, "cooldown", StringComparison.Ordinal) ||
                probe.ConsumeCount != 1 ||
                probe.ResolveCount != 1 ||
                probe.DeliverCount != 1)
            {
                throw new InvalidOperationException("Map quick announcement runtime must consume mouse input even when delivery is cooldown-blocked.");
            }
        }

        private static void MapQuickAnnouncementRuntimeRecordsRecentDiagnostics()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();

            var input = CreateQuickAnnouncementRuntimeInput("MouseRight");
            var probe = new RecordingQuickAnnouncementRuntimeProbe
            {
                ResolveResult = CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Air, "这里只有空气"),
                DeliveryResult = new MapQuickAnnouncementDeliveryResult
                {
                    CooldownBlocked = true,
                    ResultCode = "cooldown",
                    FailureReason = "cooldown remaining",
                    CooldownRemainingMilliseconds = 321
                }
            };

            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
            if (!result.Triggered ||
                !snapshot.LastTriggered ||
                !snapshot.LastInputConsumed ||
                !snapshot.LastCooldownBlocked ||
                snapshot.LastSendSucceeded ||
                !snapshot.LastIsAir ||
                snapshot.LastTargetCount != 0 ||
                !snapshot.LastDecisionUtc.HasValue)
            {
                throw new InvalidOperationException("Map quick announcement runtime must publish the latest triggered diagnostic state.");
            }

            AssertStringEquals(snapshot.LastResultCode, "cooldown", "map quick announcement diagnostic result code");
            AssertStringEquals(snapshot.LastTargetKind, "Air", "map quick announcement diagnostic target kind");
            AssertStringEquals(snapshot.LastTargetName, "这里只有空气", "map quick announcement diagnostic target name");
            AssertStringEquals(snapshot.LastTargetSummary, "这里只有空气", "map quick announcement diagnostic target summary");
            AssertStringEquals(snapshot.LastFailureReason, "cooldown remaining", "map quick announcement diagnostic failure reason");
            AssertStringEquals(snapshot.LastHotkeySummary, "Alt|Shift|MouseRight", "map quick announcement diagnostic hotkey");
            AssertContains(snapshot.LastInputConsumeResult, "consumed");
        }

        private static void MapQuickAnnouncementRuntimeDiagnosticsExplainTargetSources()
        {
            ResetQuickAnnouncementPlacementNameFakes();
            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
            try
            {
                var hoverItem = new TestQuickAnnouncementHoverItem
                {
                    type = 9,
                    stack = 2,
                    Name = "木材"
                };
                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(hoverItem, 10, 3, 200, 30, 40))
                {
                    throw new InvalidOperationException("Expected UI hover snapshot capture for diagnostic source test.");
                }

                var uiContext = CreateQuickAnnouncementContext(20f, 20f);
                uiContext.GameUpdateCount = 201;
                uiContext.MouseScreenX = 30;
                uiContext.MouseScreenY = 40;
                if (!MapQuickAnnouncementTargetResolver.TryAddUiHoverItemForTesting(uiContext))
                {
                    throw new InvalidOperationException("Expected fresh UI hover snapshot for diagnostic source test.");
                }

                var uiSnapshot = RecordQuickAnnouncementDiagnosticsForResult(
                    MapQuickAnnouncementTargetResolver.Resolve(uiContext));
                AssertStringEquals(uiSnapshot.LastTargetSource, "uiItem", "map quick announcement diagnostic UI source kind");
                AssertStringEquals(uiSnapshot.LastUiHoverSource, "ItemSlot", "map quick announcement diagnostic UI hover source");
                if (uiSnapshot.LastHoverCacheAgeUpdates != 1)
                {
                    throw new InvalidOperationException("Map quick announcement diagnostic must record UI hover cache age.");
                }

                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[11] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 6, tileStyle = 0 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[1366] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 240, tileStyle = 6 };
                Terraria.ID.ItemID.Sets.DerivedPlacementDetails[1474] =
                    new Terraria.DataStructures.PlacementDetails { tileType = 245, tileStyle = 0 };
                Terraria.ID.ContentSamples.ItemsByType[171] = new Terraria.ID.TestContentSampleItem
                {
                    type = 171,
                    createWall = 21
                };
                Terraria.Lang.ItemNames[11] = "铁矿";
                Terraria.Lang.ItemNames[1366] = "毁灭者纪念章";
                Terraria.Lang.ItemNames[1474] = "小幅挂画";
                Terraria.Lang.ItemNames[171] = "玻璃墙";
                Terraria.Map.MapHelper.TileLookups[Terraria.Map.MapHelper.BuildTileKey(238, 0)] = 2380;
                Terraria.Lang.MapObjectNames[2380] = "世纪之花球茎";
                MapQuickAnnouncementPlacementNameCache.ResetForTesting();

                var ironSnapshot = RecordQuickAnnouncementDiagnosticsForResult(CreateTileResolveResult(6, 0, 0, 0));
                AssertStringEquals(ironSnapshot.LastTargetSource, "tile", "map quick announcement iron diagnostic target source");
                AssertStringEquals(ironSnapshot.LastPlacementLookupSource, "placementItem", "map quick announcement iron diagnostic placement source");
                AssertStringEquals(ironSnapshot.LastFallbackReason, string.Empty, "map quick announcement iron diagnostic fallback reason");

                var wallSnapshot = RecordQuickAnnouncementDiagnosticsForResult(CreateWallResolveResult(21));
                AssertStringEquals(wallSnapshot.LastTargetSource, "wall", "map quick announcement glass wall diagnostic target source");
                AssertStringEquals(wallSnapshot.LastPlacementLookupSource, "placementItem", "map quick announcement glass wall diagnostic placement source");

                var trophyStyle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(240, 6 * 54 + 18, 18);
                var trophySnapshot = RecordQuickAnnouncementDiagnosticsForResult(CreateTileResolveResult(240, trophyStyle, 6 * 54 + 18, 18));
                AssertStringEquals(trophySnapshot.LastPlacementLookupSource, "placementItem", "map quick announcement trophy diagnostic placement source");

                var paintingStyle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(245, 18, 36);
                var paintingSnapshot = RecordQuickAnnouncementDiagnosticsForResult(CreateTileResolveResult(245, paintingStyle, 18, 36));
                AssertStringEquals(paintingSnapshot.LastPlacementLookupSource, "placementItem", "map quick announcement painting diagnostic placement source");

                var bulbSnapshot = RecordQuickAnnouncementDiagnosticsForResult(CreateTileResolveResult(238, 0, 0, 0));
                AssertStringEquals(bulbSnapshot.LastPlacementLookupSource, "mapObject", "map quick announcement bulb diagnostic fallback source");
                AssertStringEquals(bulbSnapshot.LastFallbackReason, "placementItemMiss:mapObject", "map quick announcement bulb diagnostic fallback reason");

                var finalFallback = RecordQuickAnnouncementDiagnosticsForResult(CreateTileResultWithSource("Tile#999", "tileId"));
                AssertStringEquals(finalFallback.LastPlacementLookupSource, "tileId", "map quick announcement final diagnostic fallback source");
                AssertStringEquals(finalFallback.LastFallbackReason, "placementItemMiss:tileId", "map quick announcement final diagnostic fallback reason");
            }
            finally
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                ResetQuickAnnouncementPlacementNameFakes();
            }
        }

        private static void MapQuickAnnouncementRuntimeIdlePathDoesNotResolveOrRecordDiagnostics()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();

            var held = new HashSet<string>(StringComparer.Ordinal);
            held.Add("Alt");
            held.Add("Shift");
            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var ports = probe.ToPorts();
            ports.IsTokenDown = held.Contains;

            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, new MapQuickAnnouncementHotkeyStateMachine(), ports);
            var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
            if (result.Triggered ||
                probe.ResolveCount != 0 ||
                probe.ConsumeCount != 0 ||
                probe.DeliverCount != 0 ||
                snapshot.LastDecisionUtc.HasValue ||
                !string.IsNullOrEmpty(snapshot.LastResultCode) ||
                !string.IsNullOrEmpty(snapshot.LastResolveDetail) ||
                !string.IsNullOrEmpty(snapshot.LastTargetSource) ||
                snapshot.LastHoverCacheAgeUpdates != -1)
            {
                throw new InvalidOperationException("Map quick announcement idle path must stay cheap and leave diagnostics unchanged.");
            }
        }

        private static void MapQuickAnnouncementRuntimeRecordsBlockedTriggerDiagnostics()
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();

            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            input.LegacyUiVisible = true;
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
            if (result.Triggered ||
                snapshot.LastTriggered ||
                probe.ResolveCount != 0 ||
                probe.ConsumeCount != 0 ||
                probe.DeliverCount != 0 ||
                !snapshot.LastDecisionUtc.HasValue)
            {
                throw new InvalidOperationException("Map quick announcement blocked trigger diagnostics must not alter blocked runtime semantics.");
            }

            AssertStringEquals(snapshot.LastResultCode, "skipped", "map quick announcement blocked diagnostic result");
            AssertStringEquals(snapshot.LastFailureReason, "legacyUiVisible", "map quick announcement blocked diagnostic reason");
            AssertStringEquals(snapshot.LastHotkeySummary, "Alt|Shift|MouseLeft", "map quick announcement blocked diagnostic hotkey");
            AssertStringEquals(snapshot.LastInputConsumeResult, "notAttempted", "map quick announcement blocked diagnostic input consume");
        }

        private static void DiagnosticSnapshotWritesMapQuickAnnouncementState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                MapQuickAnnouncementLastTriggered = true,
                MapQuickAnnouncementLastResultCode = "cooldown",
                MapQuickAnnouncementLastTargetKind = "Air",
                MapQuickAnnouncementLastTargetName = "空气",
                MapQuickAnnouncementLastTargetSummary = "这里只有空气",
                MapQuickAnnouncementLastTargetCount = 0,
                MapQuickAnnouncementLastResolveDetail = "air",
                MapQuickAnnouncementLastTargetSource = "air",
                MapQuickAnnouncementLastUiHoverSource = string.Empty,
                MapQuickAnnouncementLastUiHoverState = "hookNotInstalled",
                MapQuickAnnouncementLastUiHoverHookStatus = "hookNotInstalled:harmonyMissing",
                MapQuickAnnouncementLastPendingState = "expiredFallback",
                MapQuickAnnouncementLastHoverCacheAgeUpdates = -1,
                MapQuickAnnouncementLastPlacementLookupSource = string.Empty,
                MapQuickAnnouncementLastFallbackReason = "noTarget:air",
                MapQuickAnnouncementLastIsAir = true,
                MapQuickAnnouncementLastCooldownBlocked = true,
                MapQuickAnnouncementLastSendSucceeded = false,
                MapQuickAnnouncementLastFailureReason = "cooldown remaining",
                MapQuickAnnouncementLastHotkeySummary = "Alt|Shift|MouseRight",
                MapQuickAnnouncementLastInputConsumed = true,
                MapQuickAnnouncementLastInputConsumeResult = "consumed:consumed",
                MapQuickAnnouncementLastDecisionUtc = new DateTime(2026, 6, 11, 2, 3, 4, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"MapQuickAnnouncementLastTriggered\": true");
            AssertContains(json, "\"MapQuickAnnouncementLastResultCode\": \"cooldown\"");
            AssertContains(json, "\"MapQuickAnnouncementLastTargetKind\": \"Air\"");
            AssertContains(json, "\"MapQuickAnnouncementLastTargetName\": \"空气\"");
            AssertContains(json, "\"MapQuickAnnouncementLastTargetSummary\": \"这里只有空气\"");
            AssertContains(json, "\"MapQuickAnnouncementLastTargetCount\": 0");
            AssertContains(json, "\"MapQuickAnnouncementLastResolveDetail\": \"air\"");
            AssertContains(json, "\"MapQuickAnnouncementLastTargetSource\": \"air\"");
            AssertContains(json, "\"MapQuickAnnouncementLastUiHoverSource\": \"\"");
            AssertContains(json, "\"MapQuickAnnouncementLastUiHoverState\": \"hookNotInstalled\"");
            AssertContains(json, "\"MapQuickAnnouncementLastUiHoverHookStatus\": \"hookNotInstalled:harmonyMissing\"");
            AssertContains(json, "\"MapQuickAnnouncementLastPendingState\": \"expiredFallback\"");
            AssertContains(json, "\"MapQuickAnnouncementLastHoverCacheAgeUpdates\": -1");
            AssertContains(json, "\"MapQuickAnnouncementLastPlacementLookupSource\": \"\"");
            AssertContains(json, "\"MapQuickAnnouncementLastFallbackReason\": \"noTarget:air\"");
            AssertContains(json, "\"MapQuickAnnouncementLastIsAir\": true");
            AssertContains(json, "\"MapQuickAnnouncementLastCooldownBlocked\": true");
            AssertContains(json, "\"MapQuickAnnouncementLastSendSucceeded\": false");
            AssertContains(json, "\"MapQuickAnnouncementLastFailureReason\": \"cooldown remaining\"");
            AssertContains(json, "\"MapQuickAnnouncementLastHotkeySummary\": \"Alt|Shift|MouseRight\"");
            AssertContains(json, "\"MapQuickAnnouncementLastInputConsumed\": true");
            AssertContains(json, "\"MapQuickAnnouncementLastInputConsumeResult\": \"consumed:consumed\"");
            AssertContains(json, "\"MapQuickAnnouncementLastDecisionUtc\": \"2026-06-11T02:03:04.0000000Z\"");
        }

        private static void MapQuickAnnouncementRuntimeKeyboardTriggerDoesNotConsumeMouse()
        {
            var input = CreateQuickAnnouncementRuntimeInput("K");
            input.Hotkey = new MapQuickAnnouncementHotkey("H", "J", "K");
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (!result.Triggered ||
                result.InputConsumeAttempted ||
                probe.ConsumeCount != 0 ||
                probe.ResolveCount != 1 ||
                probe.DeliverCount != 1)
            {
                throw new InvalidOperationException("Map quick announcement runtime must not use the mouse-consume port for keyboard trigger keys.");
            }
        }

        private static void MapQuickAnnouncementRuntimeConsumesRightAndSideMouseTriggers()
        {
            AssertRuntimeConsumesMouseTrigger("MouseRight");
            AssertRuntimeConsumesMouseTrigger("Mouse5");
        }

        private static void MapQuickAnnouncementMouseTriggerCompatClearsMatchingMainPulse()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetQuickAnnouncementMouseInputState();

                string message;
                if (!TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("MouseLeft", out message))
                {
                    throw new InvalidOperationException("Expected MouseLeft trigger input consume to succeed: " + message);
                }

                var player = (Terraria.Player)Terraria.Main.LocalPlayer;
                if (Terraria.Main.mouseLeft ||
                    Terraria.Main.mouseLeftRelease ||
                    !Terraria.Main.mouseRight ||
                    !Terraria.Main.mouseRightRelease ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse ||
                    !player.mouseInterface ||
                    player.controlUseItem ||
                    !player.releaseUseItem ||
                    player.channel ||
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft ||
                    !Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight)
                {
                    throw new InvalidOperationException("MouseLeft trigger consume must clear left UI/player input and mark Terraria UI mouse capture.");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                player.controlUseItem = true;
                player.releaseUseItem = false;
                player.channel = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionPrefixGuard();
                if (Terraria.Main.mouseLeft ||
                    Terraria.Main.mouseLeftRelease ||
                    player.controlUseItem ||
                    !player.releaseUseItem ||
                    player.channel ||
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
                {
                    throw new InvalidOperationException("Active MouseLeft trigger suppression must keep the consumed click blocked until release.");
                }

                ResetQuickAnnouncementMouseInputState();
                if (!TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("MouseRight", out message))
                {
                    throw new InvalidOperationException("Expected MouseRight trigger input consume to succeed: " + message);
                }

                player = (Terraria.Player)Terraria.Main.LocalPlayer;
                if (!Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRight ||
                    Terraria.Main.mouseRightRelease ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse ||
                    !player.mouseInterface ||
                    !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight ||
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight)
                {
                    throw new InvalidOperationException("MouseRight trigger consume must clear only the right pulse and mark Terraria UI mouse capture.");
                }

                ResetQuickAnnouncementMouseInputState();
                if (!TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("Mouse5", out message))
                {
                    throw new InvalidOperationException("Expected side mouse trigger input consume to succeed: " + message);
                }

                player = (Terraria.Player)Terraria.Main.LocalPlayer;
                if (!Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    !Terraria.Main.mouseRight ||
                    !Terraria.Main.mouseRightRelease ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse ||
                    !player.mouseInterface)
                {
                    throw new InvalidOperationException("Side mouse trigger consume must mark capture without inventing nonexistent Main mouse4/mouse5 fields.");
                }
            }
            finally
            {
                ResetQuickAnnouncementMouseInputState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static MapQuickAnnouncementResolveContext CreateQuickAnnouncementContext(float mouseWorldX, float mouseWorldY)
        {
            return new MapQuickAnnouncementResolveContext
            {
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY,
                MouseScreenX = (int)Math.Floor(mouseWorldX),
                MouseScreenY = (int)Math.Floor(mouseWorldY),
                MouseTileX = (int)Math.Floor(mouseWorldX / 16f),
                MouseTileY = (int)Math.Floor(mouseWorldY / 16f),
                GameUpdateCount = 123
            };
        }

        private static MapQuickAnnouncementTriggerContext CreateQuickAnnouncementTriggerContext(
            int mouseScreenX,
            int mouseScreenY,
            ulong gameUpdateCount)
        {
            return new MapQuickAnnouncementTriggerContext
            {
                MouseWorldX = mouseScreenX,
                MouseWorldY = mouseScreenY,
                MouseScreenX = mouseScreenX,
                MouseScreenY = mouseScreenY,
                MouseTileX = (int)Math.Floor(mouseScreenX / 16f),
                MouseTileY = (int)Math.Floor(mouseScreenY / 16f),
                GameUpdateCount = gameUpdateCount
            };
        }

        private static MapQuickAnnouncementWorldItemTarget CreateQuickAnnouncementWorldItem(
            int type,
            int stack,
            string name,
            float x,
            float y,
            int tileX,
            int tileY)
        {
            return new MapQuickAnnouncementWorldItemTarget
            {
                ItemType = type,
                Stack = stack,
                Name = name,
                HitboxX = x,
                HitboxY = y,
                HitboxWidth = 16f,
                HitboxHeight = 16f,
                TileX = tileX,
                TileY = tileY
            };
        }

        private static MapQuickAnnouncementResolveResult CreateTileResolveResult(
            int tileType,
            int tileStyle,
            int frameX,
            int frameY)
        {
            string source;
            var name = MapQuickAnnouncementNameResolver.ResolveTileName(tileType, tileStyle, string.Empty, out source);
            return MapQuickAnnouncementTargetResolver.Resolve(new MapQuickAnnouncementResolveContext
            {
                Tile = new MapQuickAnnouncementTileTarget
                {
                    Active = true,
                    TileType = tileType,
                    TileStyle = tileStyle,
                    FrameX = frameX,
                    FrameY = frameY,
                    TileName = name,
                    NameSource = source
                }
            });
        }

        private static MapQuickAnnouncementResolveResult CreateWallResolveResult(int wallType)
        {
            string source;
            var name = MapQuickAnnouncementNameResolver.ResolveWallName(wallType, string.Empty, out source);
            return MapQuickAnnouncementTargetResolver.Resolve(new MapQuickAnnouncementResolveContext
            {
                Wall = new MapQuickAnnouncementWallTarget
                {
                    Active = true,
                    WallType = wallType,
                    WallName = name,
                    NameSource = source
                }
            });
        }

        private static MapQuickAnnouncementResolveResult CreateTileResultWithSource(string name, string source)
        {
            return MapQuickAnnouncementTargetResolver.Resolve(new MapQuickAnnouncementResolveContext
            {
                Tile = new MapQuickAnnouncementTileTarget
                {
                    Active = true,
                    TileType = 999,
                    TileStyle = 0,
                    TileName = name,
                    NameSource = source
                }
            });
        }

        private static MapQuickAnnouncementDiagnosticsSnapshot RecordQuickAnnouncementDiagnosticsForResult(
            MapQuickAnnouncementResolveResult resolveResult)
        {
            MapQuickAnnouncementDiagnostics.ResetForTesting();
            var input = CreateQuickAnnouncementRuntimeInput("K");
            var probe = new RecordingQuickAnnouncementRuntimeProbe
            {
                ResolveResult = resolveResult
            };

            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (!result.Triggered || probe.ResolveCount != 1 || probe.DeliverCount != 1)
            {
                throw new InvalidOperationException("Expected quick announcement runtime to publish diagnostic source state.");
            }

            return MapQuickAnnouncementDiagnostics.GetSnapshot();
        }

        private static MapQuickAnnouncementResolveResult CreateQuickAnnouncementResult(
            MapQuickAnnouncementTargetKind kind,
            string body)
        {
            return new MapQuickAnnouncementResolveResult
            {
                Kind = kind,
                Body = body ?? string.Empty,
                Detail = kind.ToString(),
                TargetName = body ?? string.Empty,
                TargetCount = kind == MapQuickAnnouncementTargetKind.Air ? 0 : 1
            };
        }

        private static MapQuickAnnouncementRuntimeInput CreateQuickAnnouncementRuntimeInput(string triggerKey)
        {
            triggerKey = string.IsNullOrWhiteSpace(triggerKey) ? "MouseLeft" : triggerKey;
            return new MapQuickAnnouncementRuntimeInput
            {
                FeatureEnabled = true,
                IsInWorld = true,
                GameInputAvailable = true,
                Hotkey = new MapQuickAnnouncementHotkey(
                    string.Equals(triggerKey, "K", StringComparison.Ordinal) ? "H" : "Alt",
                    string.Equals(triggerKey, "K", StringComparison.Ordinal) ? "J" : "Shift",
                    triggerKey),
                ColorHex = "#FFD966",
                CooldownMilliseconds = 500,
                AirCooldownMilliseconds = 2000,
                GameUpdateCount = 100
            };
        }

        private static void AssertRuntimeBlockedContext(string expectedReason, Action<MapQuickAnnouncementRuntimeInput> mutate)
        {
            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            if (mutate != null)
            {
                mutate(input);
            }

            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (result.Triggered ||
                result.InputConsumeAttempted ||
                probe.ResolveCount != 0 ||
                probe.DeliverCount != 0 ||
                !string.Equals(result.SkipReason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement runtime must block " + expectedReason + " before consuming input or resolving.");
            }
        }

        private static void AssertRuntimeConsumesMouseTrigger(string triggerKey)
        {
            var input = CreateQuickAnnouncementRuntimeInput(triggerKey);
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (!result.Triggered ||
                !result.InputConsumeAttempted ||
                !result.InputConsumed ||
                probe.ConsumeCount != 1 ||
                !string.Equals(probe.LastConsumedToken, triggerKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map quick announcement runtime must consume " + triggerKey + " mouse trigger input.");
            }
        }

        private static void ResetQuickAnnouncementMouseInputState()
        {
            TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();
            var player = new Terraria.Player();
            Terraria.Main.LocalPlayer = player;
            Terraria.Main.player[0] = player;
            Terraria.Main.myPlayer = 0;
            Terraria.Main.mouseLeft = true;
            Terraria.Main.mouseLeftRelease = true;
            Terraria.Main.mouseRight = true;
            Terraria.Main.mouseRightRelease = true;
            Terraria.Main.mouseInterface = false;
            Terraria.Main.blockMouse = false;
            Terraria.Main.mouseText = true;
            Terraria.Main.hoverItemName = "vanilla";
            Terraria.Main.hoverItemName2 = "vanilla";
            player.mouseInterface = false;
            player.controlUseItem = true;
            player.releaseUseItem = false;
            player.channel = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = true;
        }

        private static void ResetQuickAnnouncementPlacementNameFakes()
        {
            MapQuickAnnouncementPlacementNameCache.ResetForTesting();
            MapQuickAnnouncementTileStyleResolver.ResetForTesting();
            Terraria.ID.ItemID.Sets.ResetPlacementDetailsForTesting();
            Terraria.ID.ContentSamples.ItemsByType.Clear();
            Terraria.Map.MapHelper.ResetForTesting();
            Terraria.Lang.MapObjectNames.Clear();
            Terraria.Lang.ItemNames.Remove(11);
            Terraria.Lang.ItemNames.Remove(170);
            Terraria.Lang.ItemNames.Remove(171);
            Terraria.Lang.ItemNames.Remove(1366);
            Terraria.Lang.ItemNames.Remove(1474);
            Terraria.Lang.ItemNames.Remove(2865);
            Terraria.Lang.ItemNames.Remove(4934);
            Terraria.Lang.ItemNames.Remove(4945);
        }

        private sealed class RecordingQuickAnnouncementSink :
            IMapQuickAnnouncementChatSink,
            IMapQuickAnnouncementCooldownPromptSink
        {
            public readonly List<string> ChatMessages = new List<string>();
            public readonly List<string> PromptMessages = new List<string>();
            public bool FailNextChat;

            public bool TrySendChat(string text, out string failureReason)
            {
                failureReason = string.Empty;
                if (FailNextChat)
                {
                    FailNextChat = false;
                    failureReason = "forced failure";
                    return false;
                }

                ChatMessages.Add(text ?? string.Empty);
                return true;
            }

            public bool TryShowCooldownPrompt(string text, out string failureReason)
            {
                failureReason = string.Empty;
                PromptMessages.Add(text ?? string.Empty);
                return true;
            }
        }

        private sealed class TestQuickAnnouncementHoverItem
        {
            public int type;
            public int stack;
            public int prefix;
            public string Name = string.Empty;
            public string HoverName = string.Empty;
        }

        private static class TestItemSlotMouseHoverOverloads
        {
            public static void MouseHover(int context)
            {
            }

            public static void MouseHover(TestQuickAnnouncementHoverItem item, int context)
            {
            }

            public static void MouseHover(TestQuickAnnouncementHoverItem[] inv, int context, int slot)
            {
            }
        }

        private sealed class RecordingQuickAnnouncementRuntimeProbe
        {
            public int ResolveCount;
            public int ConsumeCount;
            public int DeliverCount;
            public string LastConsumedToken = string.Empty;
            public bool ConsumeSucceeded = true;
            public bool ResolveSucceeded = true;
            public string ResolveFailureReason = string.Empty;
            public Func<MapQuickAnnouncementPendingRequest, ulong, MapQuickAnnouncementResolveAttempt> ResolvePendingUiOverride;
            public Func<MapQuickAnnouncementPendingRequest, ulong, MapQuickAnnouncementResolveAttempt> ResolvePendingFallbackOverride;
            public MapQuickAnnouncementTriggerContext TriggerContext =
                new MapQuickAnnouncementTriggerContext
                {
                    MouseWorldX = 64f,
                    MouseWorldY = 96f,
                    MouseScreenX = 64,
                    MouseScreenY = 96,
                    MouseTileX = 4,
                    MouseTileY = 6,
                    GameUpdateCount = 100
                };
            public MapQuickAnnouncementResolveResult ResolveResult =
                CreateQuickAnnouncementResult(MapQuickAnnouncementTargetKind.Tile, "这里有 石块");
            public MapQuickAnnouncementDeliveryResult DeliveryResult =
                new MapQuickAnnouncementDeliveryResult
                {
                    Sent = true,
                    ResultCode = "sent",
                    ChatText = "[c/FFD966:这里有 石块]"
                };

            public MapQuickAnnouncementRuntimePorts ToPorts()
            {
                return new MapQuickAnnouncementRuntimePorts
                {
                    UtcNow = new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc),
                    IsTokenDown = token => true,
                    CaptureTriggerContext = () => TriggerContext,
                    ResolvePendingUi = (pending, currentGameUpdateCount) =>
                    {
                        ResolveCount++;
                        if (ResolvePendingUiOverride != null)
                        {
                            return ResolvePendingUiOverride(pending, currentGameUpdateCount);
                        }

                        return ResolveSucceeded
                            ? MapQuickAnnouncementResolveAttempt.Success(ResolveResult)
                            : MapQuickAnnouncementResolveAttempt.Failed(ResolveFailureReason);
                    },
                    ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                    {
                        ResolveCount++;
                        if (ResolvePendingFallbackOverride != null)
                        {
                            return ResolvePendingFallbackOverride(pending, currentGameUpdateCount);
                        }

                        return ResolveSucceeded
                            ? MapQuickAnnouncementResolveAttempt.Success(ResolveResult)
                            : MapQuickAnnouncementResolveAttempt.Failed(ResolveFailureReason);
                    },
                    ConsumeTriggerInput = token =>
                    {
                        ConsumeCount++;
                        LastConsumedToken = token ?? string.Empty;
                        return ConsumeSucceeded
                            ? MapQuickAnnouncementInputConsumeResult.Success("consumed")
                            : MapQuickAnnouncementInputConsumeResult.Failed("forced consume failure");
                    },
                    ResolveCurrent = () =>
                    {
                        ResolveCount++;
                        return ResolveSucceeded
                            ? MapQuickAnnouncementResolveAttempt.Success(ResolveResult)
                            : MapQuickAnnouncementResolveAttempt.Failed(ResolveFailureReason);
                    },
                    Deliver = (resolveResult, options, utcNow) =>
                    {
                        DeliverCount++;
                        return DeliveryResult;
                    }
                };
            }
        }
    }
}
