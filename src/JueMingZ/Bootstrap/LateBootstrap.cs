using System;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Hooks;
using JueMingZ.Runtime;

namespace JueMingZ.Bootstrap
{
    public static class LateBootstrap
    {
        private static readonly object SyncRoot = new object();
        private static bool _completed;
        private static bool _inProgress;

        public static bool Completed
        {
            get
            {
                lock (SyncRoot)
                {
                    return _completed;
                }
            }
        }

        public static bool TryLoadAfterMainAlive()
        {
            lock (SyncRoot)
            {
                if (_completed)
                {
                    return true;
                }

                if (_inProgress)
                {
                    return false;
                }

                _inProgress = true;
            }

            try
            {
                Logger.Info("LateBootstrap", "LateBootstrap begin.");
                JueMingZBootstrap.Start();

                int netMode;
                if (GameMode.TryReadNetModeLateOnly(out netMode))
                {
                    Logger.Info("LateBootstrap", "netMode late read result: " + netMode);
                }
                else
                {
                    Logger.Warn("LateBootstrap", "netMode late read result: unavailable");
                }

                Logger.Info("LateBootstrap", "Late game mode: " + GameMode.GetDescriptionLateOnly());
                Logger.Info("LateBootstrap", "Installing runtime update hook...");

                // Late bootstrap owns hook installation after Main is alive; failed hooks must remain diagnostic, not fatal.
                var hookResult = HookInstaller.InstallUpdateHook();
                Logger.Info("LateBootstrap", "Runtime hook handoff result: " + hookResult.Message);

                if (!hookResult.Succeeded)
                {
                    return false;
                }

                Logger.Info("LateBootstrap", "Installing ItemCheck hook...");
                var itemCheckHookResult = ItemUseHookInstaller.Install();
                Logger.Info("LateBootstrap", "ItemCheck hook handoff result: " + itemCheckHookResult.Message);
                if (!itemCheckHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "ItemCheck hook did not install; diagnostics will continue, but ItemUseAction bridge tests will fail.");
                }

                Logger.Info("LateBootstrap", "Installing goblin execution hit gate hooks...");
                var goblinExecutionHookResult = GoblinExecutionHookInstaller.Install();
                Logger.Info("LateBootstrap", "Goblin execution hook handoff result: " + goblinExecutionHookResult.Message);
                if (!goblinExecutionHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Goblin execution hooks did not install fully; feature will remain fail-safe original behavior.");
                }

                Logger.Info("LateBootstrap", "Installing quick bag ItemSlot.RightClick hook...");
                var quickBagOpenItemSlotHookResult = QuickBagOpenItemSlotHookInstaller.Install();
                Logger.Info("LateBootstrap", "Quick bag ItemSlot.RightClick hook handoff result: " + quickBagOpenItemSlotHookResult.Message);
                if (!quickBagOpenItemSlotHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Quick bag ItemSlot.RightClick hook did not install; quick bag opening will rely on runtime fallback only.");
                }

                Logger.Info("LateBootstrap", "Installing quick announcement ItemSlot.MouseHover hook...");
                var quickAnnouncementItemSlotHoverHookResult = MapQuickAnnouncementItemSlotHoverHookInstaller.Install();
                Logger.Info("LateBootstrap", "Quick announcement ItemSlot.MouseHover hook handoff result: " + quickAnnouncementItemSlotHoverHookResult.Message);
                if (!quickAnnouncementItemSlotHoverHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Quick announcement ItemSlot.MouseHover hook did not install; UI hover item announcements will safely fall back to world target resolution.");
                }

                Logger.Info("LateBootstrap", "Installing Player.KillMe death record hook...");
                var playerDeathHookResult = PlayerDeathHookInstaller.Install();
                Logger.Info("LateBootstrap", "Player death hook handoff result: " + playerDeathHookResult.Message);
                if (!playerDeathHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Player death hook did not install; player-world death records will fail closed.");
                }

                Logger.Info("LateBootstrap", "Installing player-world footprint map layer...");
                var playerWorldFootprintLayerResult = PlayerWorldFootprintMapLayerInstaller.Install();
                Logger.Info("LateBootstrap", "Player-world footprint map layer handoff result: " + playerWorldFootprintLayerResult.Message);
                if (!playerWorldFootprintLayerResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Player-world footprint map layer did not install; map footprints will not draw.");
                }

                Logger.Info("LateBootstrap", "Installing player-world death marker map layer...");
                var playerWorldDeathMarkerLayerResult = PlayerWorldDeathMarkerMapLayerInstaller.Install();
                Logger.Info("LateBootstrap", "Player-world death marker map layer handoff result: " + playerWorldDeathMarkerLayerResult.Message);
                if (!playerWorldDeathMarkerLayerResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Player-world death marker map layer did not install; persistent death markers will not draw.");
                }

                Logger.Info("LateBootstrap", "Installing player-world map marker map layer...");
                var playerWorldMapMarkerLayerResult = PlayerWorldMapMarkerMapLayerInstaller.Install();
                Logger.Info("LateBootstrap", "Player-world map marker layer handoff result: " + playerWorldMapMarkerLayerResult.Message);
                if (!playerWorldMapMarkerLayerResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Player-world map marker layer did not install; custom map markers will not draw.");
                }

                Logger.Info("LateBootstrap", "Installing map marker fullscreen picker draw hook...");
                var mapMarkerFullscreenPickerDrawResult = MapCustomMarkerFullscreenMapDrawInstaller.Install();
                Logger.Info("LateBootstrap", "Map marker fullscreen picker draw hook handoff result: " + mapMarkerFullscreenPickerDrawResult.Message);
                if (!mapMarkerFullscreenPickerDrawResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Map marker fullscreen picker draw hook did not install; right-click style picker will not draw on the fullscreen map.");
                }

                Logger.Info("LateBootstrap", "Installing map footprint playback overlay draw hook...");
                var mapFootprintPlaybackOverlayResult = MapFootprintFullscreenOverlayInstaller.Install();
                Logger.Info("LateBootstrap", "Map footprint playback overlay hook handoff result: " + mapFootprintPlaybackOverlayResult.Message);
                if (!mapFootprintPlaybackOverlayResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Map footprint playback overlay hook did not install; fullscreen map footprints playback UI will not draw.");
                }

                Logger.Info("LateBootstrap", "Installing auto mining PickTile hook...");
                var autoMiningPickTileHookResult = AutoMiningPickTileHookInstaller.Install();
                Logger.Info("LateBootstrap", "Auto mining PickTile hook handoff result: " + autoMiningPickTileHookResult.Message);
                if (!autoMiningPickTileHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Auto mining PickTile hook did not install; automatic auto-mining trigger will remain unavailable.");
                }

                Logger.Info("LateBootstrap", "Installing teleport rod hook...");
                var teleportRodHookResult = TeleportRodHookInstaller.Install();
                Logger.Info("LateBootstrap", "Teleport rod hook handoff result: " + teleportRodHookResult.Message);
                if (!teleportRodHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Teleport rod hook did not install; movement teleport correction will record hookMissing and skip correction.");
                }

                Logger.Info("LateBootstrap", "Installing DashMovement hook...");
                var dashMovementHookResult = MovementDashHookInstaller.Install();
                Logger.Info("LateBootstrap", "DashMovement hook handoff result: " + dashMovementHookResult.Message);
                if (!dashMovementHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "DashMovement hook did not install; continuous dash will use Runtime tick fallback timing.");
                }

                Logger.Info("LateBootstrap", "Installing safe landing Player.Update hook...");
                var safeLandingPlayerUpdateHookResult = MovementSafeLandingPlayerUpdateHookInstaller.Install();
                Logger.Info("LateBootstrap", "Safe landing Player.Update hook handoff result: " + safeLandingPlayerUpdateHookResult.Message);
                if (!safeLandingPlayerUpdateHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Safe landing Player.Update hook did not install; smart fall protection jump takeover will not have reliable input timing.");
                }

                Logger.Info("LateBootstrap", "Installing Projectile AI hook...");
                var projectileAiHookResult = ProjectileAiHookInstaller.Install();
                Logger.Info("LateBootstrap", "Projectile AI hook handoff result: " + projectileAiHookResult.Message);
                if (!projectileAiHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Projectile AI hook did not install; persistent cursor aim will use Main.Update fallback.");
                }

                Logger.Info("LateBootstrap", "Installing FishingBobber hook...");
                var fishingBobberHookResult = FishingBobberHookInstaller.Install();
                Logger.Info("LateBootstrap", "FishingBobber hook handoff result: " + fishingBobberHookResult.Message);
                if (!fishingBobberHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "FishingBobber hook did not install; fishing automation will use throttled local bobber scan fallback.");
                }

                Logger.Info("LateBootstrap", "Installing buff removal hook...");
                var buffRemovalHookResult = BuffRemovalHookInstaller.Install();
                Logger.Info("LateBootstrap", "Buff removal hook handoff result: " + buffRemovalHookResult.Message);
                if (!buffRemovalHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "Buff removal hook did not install; AutoBuff follow-remove will not observe manual buff cancellation.");
                }

                Logger.Info("LateBootstrap", "Installing TileInteraction hooks...");
                var tileInteractionHookResult = TileInteractionHookInstaller.Install();
                Logger.Info("LateBootstrap", "TileInteraction hook handoff result: " + tileInteractionHookResult.Message);
                if (!tileInteractionHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "TileInteraction hooks did not install; TileInteract will keep using direct mouse target override as fallback.");
                }

                Logger.Info("LateBootstrap", "Installing interface layer hook...");
                var interfaceLayerHookResult = InterfaceLayerHookInstaller.Install();
                Logger.Info("LateBootstrap", "Interface layer hook handoff result: " + interfaceLayerHookResult.Message);
                if (!interfaceLayerHookResult.Succeeded)
                {
                    return false;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (ShouldInstallDebugUiLocalizationHooks(settings))
                {
                    Logger.Info(
                        "LateBootstrap",
                        "Installing debug UI localization hooks: worldGenViewer=" + settings.DiagnosticsWorldGenDebugViewerEnabled +
                        ", developerDebugCommands=" + settings.DiagnosticsDeveloperDebugCommandsEnabled + ".");
                    var debugUiLocalizationHookResult = DebugUiLocalizationHookInstaller.Install();
                    Logger.Info("LateBootstrap", "Debug UI localization hook handoff result: " + debugUiLocalizationHookResult.Message);
                    if (!debugUiLocalizationHookResult.Succeeded)
                    {
                        Logger.Warn("LateBootstrap", "Debug UI localization hooks did not install; WorldGen Debug and /hh list will keep vanilla English text.");
                    }
                }
                else
                {
                    Logger.Info("LateBootstrap", "Skipping debug UI localization hooks: WorldGen Debug Viewer and developer debug commands are both disabled by config.");
                }

                if (TravelMenuService.IsSuspended)
                {
                    Logger.Info("LateBootstrap", "Skipping travel menu hooks: " + TravelMenuService.SuspendedReason);
                    TravelMenuService.MarkSuspendedAtBootstrap();
                }
                else
                {
                    Logger.Info("LateBootstrap", "Installing travel menu save guard hooks...");
                    var travelMenuSaveGuardHookResult = TravelMenuSaveGuardHookInstaller.Install();
                    Logger.Info("LateBootstrap", "Travel menu save guard hook handoff result: " + travelMenuSaveGuardHookResult.Message);
                    if (!travelMenuSaveGuardHookResult.Succeeded)
                    {
                        Logger.Warn("LateBootstrap", "Travel menu save guard hook did not install; travel menu UI enable will be rejected.");
                    }

                    Logger.Info("LateBootstrap", "Installing travel menu CreativeUI hooks...");
                    var travelMenuCreativeUiHookResult = TravelMenuCreativeUiHookInstaller.Install();
                    Logger.Info("LateBootstrap", "Travel menu CreativeUI hook handoff result: " + travelMenuCreativeUiHookResult.Message);
                    if (!travelMenuCreativeUiHookResult.Succeeded)
                    {
                        Logger.Warn("LateBootstrap", "Travel menu CreativeUI hook did not install; internal travel menu clicks may remain blocked.");
                    }

                    Logger.Info("LateBootstrap", "Installing travel menu scoped journey hooks...");
                    var travelMenuScopedHookResult = TravelMenuScopedJourneyHookInstaller.Install();
                    Logger.Info("LateBootstrap", "Travel menu scoped journey hook handoff result: " + travelMenuScopedHookResult.Message);
                    if (!travelMenuScopedHookResult.Succeeded)
                    {
                        Logger.Warn("LateBootstrap", "Travel menu scoped journey hook did not install; creative powers may not affect non-Journey worlds.");
                    }
                }

                Logger.Info("LateBootstrap", "Installing ScrollHotbar hook...");
                var scrollHotbarHookResult = ScrollHotbarHookInstaller.Install();
                Logger.Info("LateBootstrap", "ScrollHotbar hook handoff result: " + scrollHotbarHookResult.Message);
                if (!scrollHotbarHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "ScrollHotbar hook did not install; PlayerInput/Main scroll suppression will remain active as fallback.");
                }

                Logger.Info("LateBootstrap", "Installing PlayerInput scroll hook...");
                var playerInputScrollHookResult = PlayerInputScrollHookInstaller.Install();
                Logger.Info("LateBootstrap", "PlayerInput scroll hook handoff result: " + playerInputScrollHookResult.Message);
                if (!playerInputScrollHookResult.Succeeded)
                {
                    Logger.Warn("LateBootstrap", "PlayerInput scroll hook did not install; Main prefix/draw suppression and ScrollHotbar hook will remain active as fallback.");
                }

                JueMingZRuntime.MarkLateBootstrapCompleted();
                Logger.Info("LateBootstrap", "runtime handoff success.");

                // Completion is published only after the runtime and UI hook handoff, so services do not read early state.
                lock (SyncRoot)
                {
                    _completed = true;
                }

                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("LateBootstrap", error);
                Logger.Error("LateBootstrap", "LateBootstrap failed; exception swallowed.", error);
                return false;
            }
            finally
            {
                lock (SyncRoot)
                {
                    _inProgress = false;
                }
            }
        }

        public static bool ShouldInstallDebugUiLocalizationHooks(AppSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return settings.DiagnosticsWorldGenDebugViewerEnabled ||
                   settings.DiagnosticsDeveloperDebugCommandsEnabled;
        }
    }
}
