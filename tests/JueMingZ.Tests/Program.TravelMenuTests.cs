using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void TravelMenuDiagnosticsCloneKeepsScopedHookFields()
        {
            var info = new TravelMenuDiagnosticInfo
            {
                ScopedPowerHookInstalled = true,
                ScopedPowerHookMessage = "installed",
                ScopedApplyCount = 7,
                ScopedRestoreCount = 6,
                ScopedCleanupCount = 1
            };

            var clone = info.Clone();
            if (!clone.ScopedPowerHookInstalled ||
                !string.Equals(clone.ScopedPowerHookMessage, "installed", StringComparison.Ordinal) ||
                clone.ScopedApplyCount != 7 ||
                clone.ScopedRestoreCount != 6 ||
                clone.ScopedCleanupCount != 1)
            {
                throw new InvalidOperationException("Travel menu scoped diagnostics were not preserved by Clone().");
            }
        }

        private static void TravelMenuItemCheckGuardSuppressesWorldUseAndRestoresClick()
        {
            ResetFakeMainMouse(true, true);
            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            string message;
            if (!TravelMenuCompat.TryBeginScopedCreativeUiWorldItemUseGuard(player, out takeover, out message))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to apply: " + message);
            }

            if (player.controlUseItem ||
                !player.releaseUseItem ||
                !player.channel ||
                Terraria.Main.mouseLeft ||
                !Terraria.Main.mouseLeftRelease ||
                takeover == null ||
                !takeover.Applied ||
                takeover.Pressed ||
                !string.Equals(takeover.ScopeName, "TravelMenu.CreativeUI.ItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to suppress only scoped world use input.");
            }

            if (!TravelMenuCompat.TryRestoreScopedCreativeUiWorldItemUseGuard(takeover, out message))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to restore: " + message);
            }

            if (!player.controlUseItem ||
                player.releaseUseItem ||
                !player.channel ||
                !Terraria.Main.mouseLeft ||
                !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard restore to preserve the original click for CreativeUI.Update.");
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardDoesNotOverrideMouseState()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = true;
                ResetFakeMainMouse(true, true);
                Terraria.Main.mouseRight = true;
                Terraria.Main.mouseRightRelease = true;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.JueMingZTravelMenuScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected travel menu CreativeUI world input guard to apply: " + message);
                }

                if (player.controlUseItem ||
                    !player.releaseUseItem ||
                    !Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    !Terraria.Main.mouseRight ||
                    !Terraria.Main.mouseRightRelease)
                {
                    throw new InvalidOperationException("Expected travel menu CreativeUI world input guard to preserve Main mouse button state while suppressing use-item controls.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardSkipsNativeJourneyScope()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = true;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.NativeJourneyCreativeUiScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected native Journey CreativeUI world input guard to skip.");
                }

                if (!player.controlUseItem || player.releaseUseItem)
                {
                    throw new InvalidOperationException("Expected native Journey CreativeUI scope to leave use-item flags untouched.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardRequiresInventory()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = false;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.JueMingZTravelMenuScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI world input guard to skip while inventory is closed.");
                }

                if (!player.controlUseItem || player.releaseUseItem)
                {
                    throw new InvalidOperationException("Expected closed-inventory CreativeUI scope to leave use-item flags untouched.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiDrawRestorePreservesVanillaMouseInterface()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(true, true);
                var player = new FakePlayer { mouseInterface = false };
                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to apply: " + message);
                }

                player.mouseInterface = true;

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to restore: " + message);
                }

                if (!player.mouseInterface)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw restore to preserve vanilla mouseInterface=true set by the hovered toggle.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiDrawReleasePulseIgnoresOtherInventoryButtons()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseAwayFromTravelMenuToggle();
                ResetFakeMainMouse(true, true);
                var player = new FakePlayer();
                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to apply away from toggle: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw to preserve mouseLeftRelease while the cursor is away from the travel menu toggle.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to restore away from toggle: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw restore to leave non-travel-menu inventory button clicks usable.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiInputBypassClearsUsingItemGate()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                ResetFakeMainMouse(false, false);
                Terraria.Main.mouseRightRelease = false;
                Terraria.Main.mouseInterface = true;
                Terraria.Main.blockMouse = true;
                var player = new FakePlayer
                {
                    mouseInterface = true,
                    itemAnimation = 12,
                    reuseDelay = 3,
                    channel = true,
                    pendingItemReuse = true
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to apply: " + message);
                }

                if (player.mouseInterface ||
                    player.itemAnimation != 0 ||
                    player.reuseDelay != 0 ||
                    player.channel ||
                    player.pendingItemReuse ||
                    Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRightRelease ||
                    Terraria.Main.mouseInterface ||
                    Terraria.Main.blockMouse)
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to clear PlayerInput.IgnoreMouseInterface sources without inventing a click when mouse is up.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to restore: " + message);
                }

                if (!player.mouseInterface ||
                    player.itemAnimation != 12 ||
                    player.reuseDelay != 3 ||
                    !player.channel ||
                    !player.pendingItemReuse ||
                    Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRightRelease ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse)
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass restore to preserve original using-item state.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiReleasePulseIsOncePerMouseHold()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(true, false);
                Terraria.Main.mouseRight = false;
                Terraria.Main.mouseRightRelease = false;
                var player = new FakePlayer();

                var firstState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(firstState, out message))
                {
                    throw new InvalidOperationException("Expected first CreativeUI release pulse to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected first held-frame CreativeUI scope to expose one release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(firstState, out message))
                {
                    throw new InvalidOperationException("Expected first CreativeUI release pulse to restore: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected release pulse to be consumed after first scope while mouse remains held.");
                }

                var heldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(heldState, out message))
                {
                    throw new InvalidOperationException("Expected held CreativeUI scope to apply: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected held mouse to stay release-suppressed until physical mouse up.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(heldState, out message))
                {
                    throw new InvalidOperationException("Expected held CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(false, true);
                var resetState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(resetState, out message))
                {
                    throw new InvalidOperationException("Expected mouse-up CreativeUI scope to apply: " + message);
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(resetState, out message))
                {
                    throw new InvalidOperationException("Expected mouse-up CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(true, false);
                var secondClickState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(secondClickState, out message))
                {
                    throw new InvalidOperationException("Expected second click CreativeUI scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected a new physical press after mouse-up to expose another release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(secondClickState, out message))
                {
                    throw new InvalidOperationException("Expected second click CreativeUI scope to restore: " + message);
                }

                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(false, false);
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(virtualKey => virtualKey == 0x01);
                var physicalHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(physicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected physical mouse fallback to expose one release pulse when Main.mouseLeft was consumed.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(physicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI scope to restore: " + message);
                }

                var physicalStillHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(physicalStillHeldState, out message))
                {
                    throw new InvalidOperationException("Expected still-physical-held CreativeUI scope to apply: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected physical mouse fallback to keep release suppressed while Main.mouseLeft stays consumed but the physical button is still held.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(physicalStillHeldState, out message))
                {
                    throw new InvalidOperationException("Expected still-physical-held CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(false, false);
                var updatePhysicalHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(updatePhysicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI.Update scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update to synthesize held mouse down without inventing a release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(updatePhysicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI.Update scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update physical mouse synthesis to restore original Main mouse state.");
                }

                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(false, false);
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                var triggerHeldDrawState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(triggerHeldDrawState, out message))
                {
                    throw new InvalidOperationException("Expected PlayerInput-triggered CreativeUI.Draw scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Draw to synthesize click state from PlayerInput.Triggers.Current when Main.mouseLeft was consumed.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(triggerHeldDrawState, out message))
                {
                    throw new InvalidOperationException("Expected PlayerInput-triggered CreativeUI.Draw scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Draw PlayerInput-trigger synthesis to restore original trigger and Main mouse state.");
                }

                ResetFakeMainMouse(false, false);
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = false;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                var justPressedUpdateState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(justPressedUpdateState, out message))
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update to preserve a consumed short click from PlayerInput.Triggers.JustPressed without inventing release.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(justPressedUpdateState, out message))
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update synthesis to restore original trigger and Main mouse state.");
                }
            }
            finally
            {
                ResetFakeMainMouse(false, true);
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuGodmodePowerReadUsesCreativeFlagFallback()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            try
            {
                var player = new FakePlayer
                {
                    whoAmI = 7,
                    creativeGodMode = true
                };

                Terraria.Main.myPlayer = player.whoAmI;
                Terraria.Main.LocalPlayer = player;

                bool enabled;
                string message;
                if (!TravelMenuCompat.TryReadGodmodePowerEnabled(out enabled, out message))
                {
                    throw new InvalidOperationException("Expected godmode power read to fall back to LocalPlayer.creativeGodMode: " + message);
                }

                if (!enabled)
                {
                    throw new InvalidOperationException("Expected godmode power read to report enabled when LocalPlayer.creativeGodMode=true.");
                }
            }
            finally
            {
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuGodmodePowerReadDisabledWhenCreativeFlagOff()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            try
            {
                var player = new FakePlayer
                {
                    whoAmI = 11,
                    creativeGodMode = false
                };

                Terraria.Main.myPlayer = player.whoAmI;
                Terraria.Main.LocalPlayer = player;

                bool enabled;
                string message;
                if (!TravelMenuCompat.TryReadGodmodePowerEnabled(out enabled, out message))
                {
                    throw new InvalidOperationException("Expected godmode power read fallback to succeed when LocalPlayer.creativeGodMode=false: " + message);
                }

                if (enabled)
                {
                    throw new InvalidOperationException("Expected godmode power read to report disabled when LocalPlayer.creativeGodMode=false.");
                }
            }
            finally
            {
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuStateStoreRecoversActiveMarkerFromBackup()
        {
            var restoreConfigDirectory = PushTemporaryConfigDirectory("travel-menu-state-store");
            try
            {
                ResetTravelMenuStateStoreCache();
                var context = new TravelMenuContext
                {
                    PlayerPath = @"C:\Players\TestPlayer.plr",
                    WorldPath = @"C:\Worlds\TestWorld.wld",
                    PlayerName = "TestPlayer",
                    WorldName = "TestWorld",
                    PlayerDifficulty = 0,
                    WorldGameMode = 0,
                    MainGameMode = 0
                };

                TravelMenuStateStore.UpsertActiveMarker(context, "enabled");

                TravelMenuRestoreMarker marker;
                if (!TravelMenuStateStore.TryFindActiveMarker(context, out marker) || marker == null)
                {
                    throw new InvalidOperationException("Expected active travel menu restore marker after enable.");
                }

                var statePath = TravelMenuStateStore.StatePath;
                var backupPath = statePath + ".bak";
                if (!File.Exists(statePath) || !File.Exists(backupPath))
                {
                    throw new InvalidOperationException("Expected travel menu state primary and backup files to exist.");
                }

                File.WriteAllText(statePath, "{corrupt");
                ResetTravelMenuStateStoreCache();

                if (!TravelMenuStateStore.TryFindActiveMarker(context, out marker) || marker == null)
                {
                    throw new InvalidOperationException("Expected active travel menu restore marker to recover from backup when primary is corrupt.");
                }

                if (marker.OriginalPlayerDifficulty != context.PlayerDifficulty ||
                    marker.OriginalWorldGameMode != context.WorldGameMode ||
                    marker.OriginalMainGameMode != context.MainGameMode)
                {
                    throw new InvalidOperationException("Expected recovered travel menu marker to preserve original player and world modes.");
                }

                TravelMenuStateStore.MarkRestored(context, "restored");
                ResetTravelMenuStateStoreCache();
                if (TravelMenuStateStore.TryFindActiveMarker(context, out marker))
                {
                    throw new InvalidOperationException("Expected restored travel menu marker to become inactive.");
                }
            }
            finally
            {
                ResetTravelMenuStateStoreCache();
                restoreConfigDirectory();
            }
        }

        private static void SetFakeMouseOverTravelMenuToggle()
        {
            Terraria.Main.mouseX = 40;
            Terraria.Main.mouseY = 280;
            Terraria.Main.screenHeight = 800;
            Terraria.Main.inventoryScale = 1f;
        }

        private static void SetFakeMouseAwayFromTravelMenuToggle()
        {
            Terraria.Main.mouseX = 500;
            Terraria.Main.mouseY = 500;
            Terraria.Main.screenHeight = 800;
            Terraria.Main.inventoryScale = 1f;
        }

        private static Action PushFakeTerrariaMainType()
        {
            var gameModeMainField = typeof(GameMode).GetField("_cachedMainType", BindingFlags.Static | BindingFlags.NonPublic);
            var runtimeMainField = typeof(TerrariaRuntimeTypes).GetField("_mainType", BindingFlags.Static | BindingFlags.NonPublic);
            if (gameModeMainField == null || runtimeMainField == null)
            {
                throw new InvalidOperationException("Terraria runtime type cache field missing.");
            }

            var previousGameModeMain = gameModeMainField.GetValue(null);
            var previousRuntimeMain = runtimeMainField.GetValue(null);
            gameModeMainField.SetValue(null, typeof(Terraria.Main));
            runtimeMainField.SetValue(null, null);
            return () =>
            {
                runtimeMainField.SetValue(null, previousRuntimeMain);
                gameModeMainField.SetValue(null, previousGameModeMain);
            };
        }

        private static void ResetTravelMenuStateStoreCache()
        {
            var stateField = typeof(TravelMenuStateStore).GetField("_state", BindingFlags.Static | BindingFlags.NonPublic);
            if (stateField == null)
            {
                throw new InvalidOperationException("TravelMenuStateStore state cache field missing.");
            }

            stateField.SetValue(null, null);
        }

    }
}
