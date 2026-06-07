using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TravelMenuCompat
    {
        // TravelMenu state writes are limited to scoped Journey menu guards;
        // native CreativeUI observation must not arm travel fallbacks.
        public const int JourneyGameMode = 3;
        public const byte JourneyPlayerDifficulty = 3;

        private const int VkLeftButton = 0x01;
        private const int VkRightButton = 0x02;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly object CreativeResearchCacheSync = new object();
        private static bool _creativeUiLeftReleaseConsumedForHold;
        private static bool _creativeUiRightReleaseConsumedForHold;
        private static Func<int, bool> _creativeUiMouseButtonDownFallbackOverride;
        private static int[] _creativeResearchItemIdsCache;

        public static bool TryCaptureCurrentContext(out TravelMenuContext context, out string message)
        {
            context = new TravelMenuContext();
            message = string.Empty;

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            object playerFile;
            if (!TryGetActivePlayerFileData(mainType, out playerFile) || playerFile == null)
            {
                message = "ActivePlayerFileData unavailable.";
                return false;
            }

            object worldFile;
            if (!TryGetActiveWorldFileData(mainType, out worldFile) || worldFile == null)
            {
                message = "ActiveWorldFileData unavailable.";
                return false;
            }

            var filePlayer = GetMember(playerFile, "Player");
            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            var player = localPlayer ?? filePlayer;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            int netMode;
            TryReadStaticInt(mainType, "netMode", out netMode);

            int mainGameMode;
            if (!TryReadStaticInt(mainType, "GameMode", out mainGameMode))
            {
                message = "Main.GameMode unavailable.";
                return false;
            }

            int worldGameMode;
            if (!TryReadMemberInt(worldFile, "GameMode", out worldGameMode))
            {
                message = "ActiveWorldFileData.GameMode unavailable.";
                return false;
            }

            int difficulty;
            if (!TryReadMemberInt(player, "difficulty", out difficulty))
            {
                message = "Player.difficulty unavailable.";
                return false;
            }

            context.PlayerPath = ReadString(playerFile, "Path");
            context.WorldPath = ReadString(worldFile, "Path");
            context.PlayerName = FirstNonEmpty(ReadString(playerFile, "Name"), ReadString(player, "name"));
            context.WorldName = ReadString(worldFile, "Name");
            context.PlayerDifficulty = difficulty;
            context.WorldGameMode = worldGameMode;
            context.MainGameMode = mainGameMode;
            context.NetMode = netMode;

            if (string.IsNullOrWhiteSpace(context.PlayerPath) || string.IsNullOrWhiteSpace(context.WorldPath))
            {
                message = "Player/world path unavailable.";
                return false;
            }

            return true;
        }

        public static bool TryReadCurrentJourneyMode(out bool isJourney, out string message)
        {
            isJourney = false;
            TravelMenuContext context;
            if (!TryCaptureCurrentContext(out context, out message))
            {
                return false;
            }

            isJourney = (context.MainGameMode == JourneyGameMode || context.WorldGameMode == JourneyGameMode) &&
                        context.PlayerDifficulty == JourneyPlayerDifficulty;
            message = "Current journey mode read: " + (isJourney ? "journey." : "not journey.");
            return true;
        }

        // Native CreativeUI scope only observes Terraria's already-open Journey
        // menu; it must not apply JueMingZ travel-state fallback writes.
        public static bool TryBeginNativeJourneyCreativeUiScope(string scope, out TravelMenuScopedJourneyState state, out string message)
        {
            state = new TravelMenuScopedJourneyState
            {
                Scope = scope ?? string.Empty,
                Source = TravelMenuScopedJourneySource.NativeJourneyCreativeUiScope
            };
            message = string.Empty;

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                state.Message = message;
                return false;
            }

            object playerFile;
            object worldFile;
            object player;
            if (!TryResolveCurrentObjects(mainType, out playerFile, out worldFile, out player, out message))
            {
                state.Message = message;
                return false;
            }

            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            var filePlayer = GetMember(playerFile, "Player");

            state.WorldFile = worldFile;
            state.Player = player;
            state.FilePlayer = filePlayer;
            state.LocalPlayer = localPlayer;
            state.HasMainGameMode = TryReadStaticInt(mainType, "GameMode", out var mainGameMode);
            state.MainGameMode = mainGameMode;
            state.HasWorldGameMode = TryReadMemberInt(worldFile, "GameMode", out var worldGameMode);
            state.WorldGameMode = worldGameMode;
            state.HasPlayerDifficulty = TryReadMemberInt(player, "difficulty", out var playerDifficulty);
            state.PlayerDifficulty = playerDifficulty;
            if (filePlayer == null || ReferenceEquals(filePlayer, player))
            {
                state.HasFilePlayerDifficulty = state.HasPlayerDifficulty;
                state.FilePlayerDifficulty = state.PlayerDifficulty;
            }
            else
            {
                state.HasFilePlayerDifficulty = TryReadMemberInt(filePlayer, "difficulty", out var filePlayerDifficulty);
                state.FilePlayerDifficulty = filePlayerDifficulty;
            }

            if (localPlayer == null || ReferenceEquals(localPlayer, player) || ReferenceEquals(localPlayer, filePlayer))
            {
                state.HasLocalPlayerDifficulty = state.HasPlayerDifficulty;
                state.LocalPlayerDifficulty = state.PlayerDifficulty;
            }
            else
            {
                state.HasLocalPlayerDifficulty = TryReadMemberInt(localPlayer, "difficulty", out var localPlayerDifficulty);
                state.LocalPlayerDifficulty = localPlayerDifficulty;
            }

            if (!state.HasMainGameMode || !state.HasWorldGameMode || !state.HasPlayerDifficulty)
            {
                message = "Native journey state capture incomplete: main=" + state.HasMainGameMode + ", world=" + state.HasWorldGameMode + ", player=" + state.HasPlayerDifficulty + ".";
                state.Message = message;
                return false;
            }

            var playerJourney = state.PlayerDifficulty == JourneyPlayerDifficulty ||
                                (state.HasLocalPlayerDifficulty && state.LocalPlayerDifficulty == JourneyPlayerDifficulty) ||
                                (state.HasFilePlayerDifficulty && state.FilePlayerDifficulty == JourneyPlayerDifficulty);
            var worldJourney = state.MainGameMode == JourneyGameMode || state.WorldGameMode == JourneyGameMode;
            if (!playerJourney || !worldJourney)
            {
                message = "Native Journey CreativeUI scope skipped: current context is not Journey.";
                state.Message = message;
                return false;
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            if (!TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) || !creativeMenuEnabled)
            {
                message = string.IsNullOrWhiteSpace(creativeMenuMessage)
                    ? "Native Journey CreativeUI scope skipped: CreativeUI is closed."
                    : creativeMenuMessage;
                state.Message = message;
                return false;
            }

            message = "Native Journey CreativeUI input scope captured for " + state.Scope + ".";
            state.Message = message;
            return true;
        }

        public static bool TryApplyJourneyState(TravelMenuContext original, out string message)
        {
            message = string.Empty;
            if (original == null)
            {
                message = "Original context unavailable.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            object playerFile;
            object worldFile;
            object player;
            if (!TryResolveCurrentObjects(mainType, out playerFile, out worldFile, out player, out message))
            {
                return false;
            }

            if (!MatchesCurrentContext(original, playerFile, worldFile))
            {
                message = "Current player/world does not match captured travel menu marker.";
                return false;
            }

            var mainSet = TrySetStaticMember(mainType, "GameMode", JourneyGameMode);
            var worldSet = TrySetMember(worldFile, "GameMode", JourneyGameMode);
            var playerSet = TrySetMember(player, "difficulty", JourneyPlayerDifficulty);
            var filePlayer = GetMember(playerFile, "Player");
            var filePlayerSet = filePlayer == null || ReferenceEquals(filePlayer, player) || TrySetMember(filePlayer, "difficulty", JourneyPlayerDifficulty);
            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            var localPlayerSet = localPlayer == null || ReferenceEquals(localPlayer, player) || ReferenceEquals(localPlayer, filePlayer) || TrySetMember(localPlayer, "difficulty", JourneyPlayerDifficulty);

            if (mainSet && worldSet && playerSet && filePlayerSet && localPlayerSet)
            {
                message = "Journey travel state applied.";
                return true;
            }

            message = "Journey travel state incomplete: main=" + mainSet + ", world=" + worldSet + ", player=" + playerSet + ", filePlayer=" + filePlayerSet + ", localPlayer=" + localPlayerSet + ".";
            return false;
        }

        public static bool TryRestoreOriginalState(TravelMenuContext original, out string message)
        {
            message = string.Empty;
            if (original == null)
            {
                message = "Original context unavailable.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            object playerFile;
            object worldFile;
            object player;
            if (!TryResolveCurrentObjects(mainType, out playerFile, out worldFile, out player, out message))
            {
                return false;
            }

            if (!MatchesCurrentContext(original, playerFile, worldFile))
            {
                message = "Current player/world does not match captured travel menu marker.";
                return false;
            }

            var mainSet = TrySetStaticMember(mainType, "GameMode", original.MainGameMode);
            var worldSet = TrySetMember(worldFile, "GameMode", original.WorldGameMode);
            var playerSet = TrySetMember(player, "difficulty", (byte)original.PlayerDifficulty);
            var filePlayer = GetMember(playerFile, "Player");
            var filePlayerSet = filePlayer == null || ReferenceEquals(filePlayer, player) || TrySetMember(filePlayer, "difficulty", (byte)original.PlayerDifficulty);
            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            var localPlayerSet = localPlayer == null || ReferenceEquals(localPlayer, player) || ReferenceEquals(localPlayer, filePlayer) || TrySetMember(localPlayer, "difficulty", (byte)original.PlayerDifficulty);

            if (mainSet && worldSet && playerSet && filePlayerSet && localPlayerSet)
            {
                message = "Original travel menu state restored.";
                return true;
            }

            message = "Travel menu restore incomplete: main=" + mainSet + ", world=" + worldSet + ", player=" + playerSet + ", filePlayer=" + filePlayerSet + ", localPlayer=" + localPlayerSet + ".";
            return false;
        }

        // JueMingZ travel scope temporarily writes Journey state and therefore
        // must retain enough original context for fail-closed restore.
        public static bool TryBeginScopedJourneyState(TravelMenuContext original, string scope, out TravelMenuScopedJourneyState state, out string message)
        {
            state = new TravelMenuScopedJourneyState
            {
                Scope = scope ?? string.Empty,
                Source = TravelMenuScopedJourneySource.JueMingZTravelMenuScope
            };
            message = string.Empty;
            if (original == null)
            {
                message = "Original context unavailable.";
                state.Message = message;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                state.Message = message;
                return false;
            }

            object playerFile;
            object worldFile;
            object player;
            if (!TryResolveCurrentObjects(mainType, out playerFile, out worldFile, out player, out message))
            {
                state.Message = message;
                return false;
            }

            if (!MatchesCurrentContext(original, playerFile, worldFile))
            {
                message = "Current player/world does not match captured travel menu marker.";
                state.Message = message;
                return false;
            }

            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            var filePlayer = GetMember(playerFile, "Player");

            state.WorldFile = worldFile;
            state.Player = player;
            state.FilePlayer = filePlayer;
            state.LocalPlayer = localPlayer;
            state.HasMainGameMode = TryReadStaticInt(mainType, "GameMode", out var mainGameMode);
            state.MainGameMode = mainGameMode;
            state.HasWorldGameMode = TryReadMemberInt(worldFile, "GameMode", out var worldGameMode);
            state.WorldGameMode = worldGameMode;
            state.HasPlayerDifficulty = TryReadMemberInt(player, "difficulty", out var playerDifficulty);
            state.PlayerDifficulty = playerDifficulty;
            if (filePlayer == null || ReferenceEquals(filePlayer, player))
            {
                state.HasFilePlayerDifficulty = true;
                state.FilePlayerDifficulty = state.PlayerDifficulty;
            }
            else
            {
                state.HasFilePlayerDifficulty = TryReadMemberInt(filePlayer, "difficulty", out var filePlayerDifficulty);
                state.FilePlayerDifficulty = filePlayerDifficulty;
            }

            if (localPlayer == null || ReferenceEquals(localPlayer, player) || ReferenceEquals(localPlayer, filePlayer))
            {
                state.HasLocalPlayerDifficulty = true;
                state.LocalPlayerDifficulty = state.PlayerDifficulty;
            }
            else
            {
                state.HasLocalPlayerDifficulty = TryReadMemberInt(localPlayer, "difficulty", out var localPlayerDifficulty);
                state.LocalPlayerDifficulty = localPlayerDifficulty;
            }

            if (!state.HasMainGameMode || !state.HasWorldGameMode || !state.HasPlayerDifficulty || !state.HasFilePlayerDifficulty || !state.HasLocalPlayerDifficulty)
            {
                message = "Scoped journey state capture incomplete: main=" + state.HasMainGameMode + ", world=" + state.HasWorldGameMode + ", player=" + state.HasPlayerDifficulty + ", filePlayer=" + state.HasFilePlayerDifficulty + ", localPlayer=" + state.HasLocalPlayerDifficulty + ".";
                state.Message = message;
                return false;
            }

            state.Applied = true;
            var mainSet = TrySetStaticMember(mainType, "GameMode", JourneyGameMode);
            var worldSet = TrySetMember(worldFile, "GameMode", JourneyGameMode);
            var playerSet = TrySetMember(player, "difficulty", JourneyPlayerDifficulty);
            var filePlayerSet = filePlayer == null || ReferenceEquals(filePlayer, player) || TrySetMember(filePlayer, "difficulty", JourneyPlayerDifficulty);
            var localPlayerSet = localPlayer == null || ReferenceEquals(localPlayer, player) || ReferenceEquals(localPlayer, filePlayer) || TrySetMember(localPlayer, "difficulty", JourneyPlayerDifficulty);

            if (mainSet && worldSet && playerSet && filePlayerSet && localPlayerSet)
            {
                message = "Scoped journey travel state applied for " + state.Scope + ".";
                state.Message = message;
                return true;
            }

            string restoreMessage;
            TryRestoreScopedJourneyState(state, out restoreMessage);
            message = "Scoped journey travel state incomplete: main=" + mainSet + ", world=" + worldSet + ", player=" + playerSet + ", filePlayer=" + filePlayerSet + ", localPlayer=" + localPlayerSet + ". " + restoreMessage;
            state.Message = message;
            return false;
        }

        public static bool TryRestoreScopedJourneyState(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null)
            {
                message = "Scoped journey state unavailable.";
                return false;
            }

            if (!state.Applied)
            {
                message = "Scoped journey state was not applied.";
                return true;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            var mainSet = !state.HasMainGameMode || TrySetStaticMember(mainType, "GameMode", state.MainGameMode);
            var worldSet = !state.HasWorldGameMode || TrySetMember(state.WorldFile, "GameMode", state.WorldGameMode);
            var playerSet = !state.HasPlayerDifficulty || TrySetMember(state.Player, "difficulty", state.PlayerDifficulty);
            var filePlayerSet = !state.HasFilePlayerDifficulty || state.FilePlayer == null || ReferenceEquals(state.FilePlayer, state.Player) || TrySetMember(state.FilePlayer, "difficulty", state.FilePlayerDifficulty);
            var localPlayerSet = !state.HasLocalPlayerDifficulty || state.LocalPlayer == null || ReferenceEquals(state.LocalPlayer, state.Player) || ReferenceEquals(state.LocalPlayer, state.FilePlayer) || TrySetMember(state.LocalPlayer, "difficulty", state.LocalPlayerDifficulty);

            state.Applied = !(mainSet && worldSet && playerSet && filePlayerSet && localPlayerSet);
            if (!state.Applied)
            {
                message = "Scoped journey travel state restored for " + state.Scope + ".";
                state.Message = message;
                return true;
            }

            message = "Scoped journey restore incomplete: main=" + mainSet + ", world=" + worldSet + ", player=" + playerSet + ", filePlayer=" + filePlayerSet + ", localPlayer=" + localPlayerSet + ".";
            state.Message = message;
            return false;
        }

        public static bool TryCurrentStateMatchesOriginal(TravelMenuContext original, out bool matches, out string message)
        {
            matches = false;
            message = string.Empty;
            if (original == null)
            {
                message = "Original context unavailable.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            object playerFile;
            object worldFile;
            object player;
            if (!TryResolveCurrentObjects(mainType, out playerFile, out worldFile, out player, out message))
            {
                return false;
            }

            if (!MatchesCurrentContext(original, playerFile, worldFile))
            {
                message = "Current player/world does not match captured travel menu marker.";
                return false;
            }

            int mainGameMode;
            int worldGameMode;
            int playerDifficulty;
            if (!TryReadStaticInt(mainType, "GameMode", out mainGameMode) ||
                !TryReadMemberInt(worldFile, "GameMode", out worldGameMode) ||
                !TryReadMemberInt(player, "difficulty", out playerDifficulty))
            {
                message = "Current travel menu state read incomplete.";
                return false;
            }

            matches = mainGameMode == original.MainGameMode &&
                      worldGameMode == original.WorldGameMode &&
                      playerDifficulty == original.PlayerDifficulty;

            var filePlayer = GetMember(playerFile, "Player");
            if (filePlayer != null && !ReferenceEquals(filePlayer, player))
            {
                int filePlayerDifficulty;
                if (!TryReadMemberInt(filePlayer, "difficulty", out filePlayerDifficulty))
                {
                    message = "File player difficulty unavailable.";
                    return false;
                }

                matches = matches && filePlayerDifficulty == original.PlayerDifficulty;
            }

            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            if (localPlayer != null && !ReferenceEquals(localPlayer, player) && !ReferenceEquals(localPlayer, filePlayer))
            {
                int localPlayerDifficulty;
                if (!TryReadMemberInt(localPlayer, "difficulty", out localPlayerDifficulty))
                {
                    message = "Local player difficulty unavailable.";
                    return false;
                }

                matches = matches && localPlayerDifficulty == original.PlayerDifficulty;
            }

            message = matches ? "Current travel menu state matches original." : "Current travel menu state is still temporary journey.";
            return true;
        }

        public static bool TryOpenCreativeMenu(out string message)
        {
            message = string.Empty;
            try
            {
                if (TryReadCreativeMenuEnabled(out var enabled, out message) && enabled)
                {
                    message = "Creative menu already open.";
                    return true;
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    message = "Terraria.Main unavailable.";
                    return false;
                }

                object player;
                if (!TryGetLocalPlayer(mainType, out player) || player == null)
                {
                    message = "Local player unavailable.";
                    return false;
                }

                var method = player.GetType().GetMethod("ToggleCreativeMenu", InstanceFlags, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    message = "Player.ToggleCreativeMenu unavailable.";
                    return false;
                }

                method.Invoke(player, new object[0]);
                bool afterEnabled;
                string afterMessage;
                if (TryReadCreativeMenuEnabled(out afterEnabled, out afterMessage) && afterEnabled)
                {
                    message = "Creative menu opened.";
                    return true;
                }

                message = "Creative menu toggle invoked but open state was not confirmed. " + afterMessage;
                return false;
            }
            catch (Exception error)
            {
                var inner = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                message = "Creative menu open failed: " + (inner == null ? error.Message : inner.Message);
                RuntimeDiagnostics.RecordError("TravelMenuCompat.TryOpenCreativeMenu", inner ?? error);
                return false;
            }
        }

        public static bool TryCloseCreativeMenu(out string message)
        {
            message = string.Empty;
            try
            {
                var creativeMenu = GetCreativeMenu();
                if (creativeMenu == null)
                {
                    message = "Main.CreativeMenu unavailable.";
                    return false;
                }

                var method = creativeMenu.GetType().GetMethod("CloseMenu", InstanceFlags, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    message = "CreativeUI.CloseMenu unavailable.";
                    return false;
                }

                method.Invoke(creativeMenu, new object[0]);
                message = "Creative menu closed.";
                return true;
            }
            catch (Exception error)
            {
                var inner = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                message = "Creative menu close failed: " + (inner == null ? error.Message : inner.Message);
                RuntimeDiagnostics.RecordError("TravelMenuCompat.TryCloseCreativeMenu", inner ?? error);
                return false;
            }
        }

        public static bool TryReadPlayerInventoryOpen(out bool open, out string message)
        {
            open = false;
            message = string.Empty;
            if (!TerrariaInputCompat.TryReadPlayerInventoryOpen(out open))
            {
                message = string.IsNullOrWhiteSpace(TerrariaInputCompat.LastInputCompatError)
                    ? "Cannot read Main.playerInventory."
                    : TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            message = "Main.playerInventory read: " + (open ? "open." : "closed.");
            return true;
        }

        public static bool TrySetPlayerInventoryOpen(bool open, out string message)
        {
            return TerrariaInputCompat.TrySetPlayerInventoryOpen(open, out message);
        }

        public static bool TryBeginScopedCreativeUiWorldItemUseGuard(object player, out TerrariaInputCompat.ScopedUseItemTakeover takeover, out string message)
        {
            takeover = null;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable for creative UI item use guard.";
                return false;
            }

            if (TerrariaInputCompat.TryBeginScopedUseItemClickTakeover(player, false, "TravelMenu.CreativeUI.ItemCheck", out takeover))
            {
                message = "Creative UI item use guard applied for Player.ItemCheck.";
                return true;
            }

            message = "Creative UI item use guard failed: " + TerrariaInputCompat.LastInputCompatError;
            return false;
        }

        public static bool TryRestoreScopedCreativeUiWorldItemUseGuard(TerrariaInputCompat.ScopedUseItemTakeover takeover, out string message)
        {
            message = string.Empty;
            if (takeover == null)
            {
                message = "Creative UI item use guard was not applied.";
                return true;
            }

            var restored = TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
            message = restored
                ? "Creative UI item use guard restored after Player.ItemCheck."
                : "Creative UI item use guard restore failed: " + TerrariaInputCompat.LastInputCompatError;
            return restored;
        }

        public static bool TryBeginScopedCreativeUiInputBypass(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null)
            {
                message = "Scoped journey state unavailable for creative UI input bypass.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable for creative UI input bypass.";
                return false;
            }

            var player = state.LocalPlayer ?? state.Player;
            var playerMouseInterfaceBefore = false;
            state.HasPlayerMouseInterface = player != null && TryReadMemberBool(player, "mouseInterface", out playerMouseInterfaceBefore);
            state.PlayerMouseInterface = playerMouseInterfaceBefore;
            state.HasMainMouseInterface = TryReadStaticBool(mainType, "mouseInterface", out var mainMouseInterfaceBefore);
            state.MainMouseInterface = mainMouseInterfaceBefore;
            state.HasMainBlockMouse = TryReadStaticBool(mainType, "blockMouse", out var mainBlockMouseBefore);
            state.MainBlockMouse = mainBlockMouseBefore;
            state.HasMainMouseLeftRelease = TryReadStaticBool(mainType, "mouseLeftRelease", out var mainMouseLeftReleaseBefore);
            state.MainMouseLeftRelease = mainMouseLeftReleaseBefore;
            state.HasMainMouseRightRelease = TryReadStaticBool(mainType, "mouseRightRelease", out var mainMouseRightReleaseBefore);
            state.MainMouseRightRelease = mainMouseRightReleaseBefore;
            state.HasMainMouseLeft = TryReadStaticBool(mainType, "mouseLeft", out var mainMouseLeftBefore);
            state.MainMouseLeft = mainMouseLeftBefore;
            state.MainMouseLeftRestore = mainMouseLeftBefore;
            state.HasMainMouseRight = TryReadStaticBool(mainType, "mouseRight", out var mainMouseRightBefore);
            state.MainMouseRight = mainMouseRightBefore;
            state.MainMouseRightRestore = mainMouseRightBefore;
            state.HasPlayerInputCurrentMouseLeft = TryReadPlayerInputTrigger("Current", "MouseLeft", out var playerInputCurrentMouseLeftBefore);
            state.PlayerInputCurrentMouseLeft = playerInputCurrentMouseLeftBefore;
            state.PlayerInputCurrentMouseLeftRestore = playerInputCurrentMouseLeftBefore;
            state.HasPlayerInputCurrentMouseRight = TryReadPlayerInputTrigger("Current", "MouseRight", out var playerInputCurrentMouseRightBefore);
            state.PlayerInputCurrentMouseRight = playerInputCurrentMouseRightBefore;
            state.PlayerInputCurrentMouseRightRestore = playerInputCurrentMouseRightBefore;
            var hasPlayerInputJustPressedMouseLeft = TryReadPlayerInputTrigger("JustPressed", "MouseLeft", out var playerInputJustPressedMouseLeft);
            var hasPlayerInputJustPressedMouseRight = TryReadPlayerInputTrigger("JustPressed", "MouseRight", out var playerInputJustPressedMouseRight);
            var physicalLeftAvailable = TryReadMouseButtonDownFallback(VkLeftButton, out var physicalMouseLeft);
            var physicalRightAvailable = TryReadMouseButtonDownFallback(VkRightButton, out var physicalMouseRight);
            var isCreativeUiUpdateScope = string.Equals(state.Scope, "CreativeUI.Update", StringComparison.Ordinal);
            var isCreativeUiDrawScope = string.Equals(state.Scope, "CreativeUI.Draw", StringComparison.Ordinal);
            var mainMouseLeftDuringScope = mainMouseLeftBefore;
            var mainMouseRightDuringScope = mainMouseRightBefore;
            var playerInputCurrentMouseLeftDuringScope = playerInputCurrentMouseLeftBefore;
            var playerInputCurrentMouseRightDuringScope = playerInputCurrentMouseRightBefore;
            if (isCreativeUiUpdateScope || isCreativeUiDrawScope)
            {
                // CreativeUI reads a mix of Main.mouseLeft and PlayerInput.Triggers.Current.MouseLeft after world-use paths may already consume Main.mouseLeft.
                var synthesizeLeft = mainMouseLeftBefore ||
                                     playerInputCurrentMouseLeftBefore ||
                                     (hasPlayerInputJustPressedMouseLeft && playerInputJustPressedMouseLeft) ||
                                     physicalMouseLeft;
                var synthesizeRight = mainMouseRightBefore ||
                                      playerInputCurrentMouseRightBefore ||
                                      (hasPlayerInputJustPressedMouseRight && playerInputJustPressedMouseRight) ||
                                      physicalMouseRight;
                mainMouseLeftDuringScope = synthesizeLeft;
                mainMouseRightDuringScope = synthesizeRight;
                playerInputCurrentMouseLeftDuringScope = synthesizeLeft;
                playerInputCurrentMouseRightDuringScope = synthesizeRight;
            }

            bool mainMouseLeftReleaseDuringScope;
            bool mainMouseLeftReleaseRestore;
            bool mainMouseRightReleaseDuringScope;
            bool mainMouseRightReleaseRestore;
            var leftButtonStateAvailable = state.HasMainMouseLeft || state.HasPlayerInputCurrentMouseLeft || hasPlayerInputJustPressedMouseLeft || physicalLeftAvailable;
            var rightButtonStateAvailable = state.HasMainMouseRight || state.HasPlayerInputCurrentMouseRight || hasPlayerInputJustPressedMouseRight || physicalRightAvailable;
            if (isCreativeUiDrawScope && IsMouseOverCreativeUiToggleButton(mainType))
            {
                PlanCreativeUiReleasePulse(
                    leftButtonStateAvailable,
                    mainMouseLeftDuringScope,
                    state.HasMainMouseLeftRelease,
                    mainMouseLeftReleaseBefore,
                    ref _creativeUiLeftReleaseConsumedForHold,
                    out mainMouseLeftReleaseDuringScope,
                    out mainMouseLeftReleaseRestore);
                PlanCreativeUiReleasePulse(
                    state.HasMainMouseRight || state.HasPlayerInputCurrentMouseRight || hasPlayerInputJustPressedMouseRight || physicalRightAvailable,
                    mainMouseRightDuringScope,
                    state.HasMainMouseRightRelease,
                    mainMouseRightReleaseBefore,
                    ref _creativeUiRightReleaseConsumedForHold,
                    out mainMouseRightReleaseDuringScope,
                    out mainMouseRightReleaseRestore);
            }
            else
            {
                mainMouseLeftReleaseDuringScope = mainMouseLeftReleaseBefore;
                mainMouseLeftReleaseRestore = mainMouseLeftReleaseBefore;
                mainMouseRightReleaseDuringScope = mainMouseRightReleaseBefore;
                mainMouseRightReleaseRestore = mainMouseRightReleaseBefore;
                ResetCreativeUiReleasePulseIfButtonUp(leftButtonStateAvailable, mainMouseLeftDuringScope, ref _creativeUiLeftReleaseConsumedForHold);
                ResetCreativeUiReleasePulseIfButtonUp(rightButtonStateAvailable, mainMouseRightDuringScope, ref _creativeUiRightReleaseConsumedForHold);
            }

            state.MainMouseLeftReleaseRestore = mainMouseLeftReleaseRestore;
            state.MainMouseRightReleaseRestore = mainMouseRightReleaseRestore;
            var playerItemAnimationBefore = 0;
            state.HasPlayerItemAnimation = player != null && TryReadMemberInt(player, "itemAnimation", out playerItemAnimationBefore);
            state.PlayerItemAnimation = playerItemAnimationBefore;
            var playerReuseDelayBefore = 0;
            state.HasPlayerReuseDelay = player != null && TryReadMemberInt(player, "reuseDelay", out playerReuseDelayBefore);
            state.PlayerReuseDelay = playerReuseDelayBefore;
            var playerChannelBefore = false;
            state.HasPlayerChannel = player != null && TryReadMemberBool(player, "channel", out playerChannelBefore);
            state.PlayerChannel = playerChannelBefore;
            var playerPendingItemReuseBefore = false;
            state.HasPlayerPendingItemReuse = player != null && TryReadMemberBool(player, "pendingItemReuse", out playerPendingItemReuseBefore);
            state.PlayerPendingItemReuse = playerPendingItemReuseBefore;

            var playerSet = player == null || !state.HasPlayerMouseInterface || TrySetMember(player, "mouseInterface", false);
            var mainSet = !state.HasMainMouseInterface || TrySetStaticMember(mainType, "mouseInterface", false);
            var blockSet = !state.HasMainBlockMouse || TrySetStaticMember(mainType, "blockMouse", false);
            var leftSet = !state.HasMainMouseLeft || TrySetStaticMember(mainType, "mouseLeft", mainMouseLeftDuringScope);
            var rightSet = !state.HasMainMouseRight || TrySetStaticMember(mainType, "mouseRight", mainMouseRightDuringScope);
            var playerInputLeftSet = !state.HasPlayerInputCurrentMouseLeft || TrySetPlayerInputTrigger("Current", "MouseLeft", playerInputCurrentMouseLeftDuringScope);
            var playerInputRightSet = !state.HasPlayerInputCurrentMouseRight || TrySetPlayerInputTrigger("Current", "MouseRight", playerInputCurrentMouseRightDuringScope);
            var leftReleaseSet = !state.HasMainMouseLeftRelease || TrySetStaticMember(mainType, "mouseLeftRelease", mainMouseLeftReleaseDuringScope);
            var rightReleaseSet = !state.HasMainMouseRightRelease || TrySetStaticMember(mainType, "mouseRightRelease", mainMouseRightReleaseDuringScope);
            var itemAnimationSet = player == null || !state.HasPlayerItemAnimation || TrySetMember(player, "itemAnimation", 0);
            var reuseDelaySet = player == null || !state.HasPlayerReuseDelay || TrySetMember(player, "reuseDelay", 0);
            var channelSet = player == null || !state.HasPlayerChannel || TrySetMember(player, "channel", false);
            var pendingReuseSet = player == null || !state.HasPlayerPendingItemReuse || TrySetMember(player, "pendingItemReuse", false);

            state.InputBypassApplied = playerSet &&
                                       mainSet &&
                                       blockSet &&
                                       leftSet &&
                                       rightSet &&
                                       playerInputLeftSet &&
                                       playerInputRightSet &&
                                       leftReleaseSet &&
                                       rightReleaseSet &&
                                       itemAnimationSet &&
                                       reuseDelaySet &&
                                       channelSet &&
                                       pendingReuseSet;
            if (state.InputBypassApplied)
            {
                message = "Scoped creative UI input bypass applied.";
                return true;
            }

            message = "Scoped creative UI input bypass incomplete: player=" + playerSet +
                      ", main=" + mainSet +
                      ", block=" + blockSet +
                      ", left=" + leftSet +
                      ", right=" + rightSet +
                      ", playerInputLeft=" + playerInputLeftSet +
                      ", playerInputRight=" + playerInputRightSet +
                      ", leftRelease=" + leftReleaseSet +
                      ", rightRelease=" + rightReleaseSet +
                      ", itemAnimation=" + itemAnimationSet +
                      ", reuseDelay=" + reuseDelaySet +
                      ", channel=" + channelSet +
                      ", pendingItemReuse=" + pendingReuseSet + ".";
            return false;
        }

        public static bool TryRestoreScopedCreativeUiInputBypass(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null)
            {
                message = "Scoped journey state unavailable for creative UI input bypass restore.";
                return false;
            }

            if (!state.InputBypassApplied && !HasCreativeUiInputBypassSnapshot(state))
            {
                message = "Scoped creative UI input bypass was not applied.";
                return true;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable for creative UI input bypass restore.";
                return false;
            }

            var player = state.LocalPlayer ?? state.Player;
            var playerMouseInterfaceRestore = state.PlayerMouseInterface;
            if (state.HasPlayerMouseInterface && player != null && TryReadMemberBool(player, "mouseInterface", out var playerMouseInterfaceAtRestore))
            {
                playerMouseInterfaceRestore = state.PlayerMouseInterface || playerMouseInterfaceAtRestore;
            }

            var playerSet = !state.HasPlayerMouseInterface || player == null || TrySetMember(player, "mouseInterface", playerMouseInterfaceRestore);
            var mainSet = !state.HasMainMouseInterface || TrySetStaticMember(mainType, "mouseInterface", state.MainMouseInterface);
            var blockSet = !state.HasMainBlockMouse || TrySetStaticMember(mainType, "blockMouse", state.MainBlockMouse);
            var leftSet = !state.HasMainMouseLeft || TrySetStaticMember(mainType, "mouseLeft", state.MainMouseLeftRestore);
            var rightSet = !state.HasMainMouseRight || TrySetStaticMember(mainType, "mouseRight", state.MainMouseRightRestore);
            var playerInputLeftSet = !state.HasPlayerInputCurrentMouseLeft || TrySetPlayerInputTrigger("Current", "MouseLeft", state.PlayerInputCurrentMouseLeftRestore);
            var playerInputRightSet = !state.HasPlayerInputCurrentMouseRight || TrySetPlayerInputTrigger("Current", "MouseRight", state.PlayerInputCurrentMouseRightRestore);
            var leftReleaseSet = !state.HasMainMouseLeftRelease || TrySetStaticMember(mainType, "mouseLeftRelease", state.MainMouseLeftReleaseRestore);
            var rightReleaseSet = !state.HasMainMouseRightRelease || TrySetStaticMember(mainType, "mouseRightRelease", state.MainMouseRightReleaseRestore);
            var itemAnimationSet = !state.HasPlayerItemAnimation || player == null || TrySetMember(player, "itemAnimation", state.PlayerItemAnimation);
            var reuseDelaySet = !state.HasPlayerReuseDelay || player == null || TrySetMember(player, "reuseDelay", state.PlayerReuseDelay);
            var channelSet = !state.HasPlayerChannel || player == null || TrySetMember(player, "channel", state.PlayerChannel);
            var pendingReuseSet = !state.HasPlayerPendingItemReuse || player == null || TrySetMember(player, "pendingItemReuse", state.PlayerPendingItemReuse);

            state.InputBypassApplied = false;
            if (playerSet &&
                mainSet &&
                blockSet &&
                leftSet &&
                rightSet &&
                playerInputLeftSet &&
                playerInputRightSet &&
                leftReleaseSet &&
                rightReleaseSet &&
                itemAnimationSet &&
                reuseDelaySet &&
                channelSet &&
                pendingReuseSet)
            {
                message = "Scoped creative UI input bypass restored.";
                return true;
            }

            message = "Scoped creative UI input bypass restore incomplete: player=" + playerSet +
                      ", main=" + mainSet +
                      ", block=" + blockSet +
                      ", left=" + leftSet +
                      ", right=" + rightSet +
                      ", playerInputLeft=" + playerInputLeftSet +
                      ", playerInputRight=" + playerInputRightSet +
                      ", leftRelease=" + leftReleaseSet +
                      ", rightRelease=" + rightReleaseSet +
                      ", itemAnimation=" + itemAnimationSet +
                      ", reuseDelay=" + reuseDelaySet +
                      ", channel=" + channelSet +
                      ", pendingItemReuse=" + pendingReuseSet + ".";
            return false;
        }

        private static bool HasCreativeUiInputBypassSnapshot(TravelMenuScopedJourneyState state)
        {
            return state != null &&
                   (state.HasPlayerMouseInterface ||
                    state.HasMainMouseInterface ||
                    state.HasMainBlockMouse ||
                    state.HasMainMouseLeft ||
                    state.HasMainMouseRight ||
                    state.HasPlayerInputCurrentMouseLeft ||
                    state.HasPlayerInputCurrentMouseRight ||
                    state.HasMainMouseLeftRelease ||
                    state.HasMainMouseRightRelease ||
                    state.HasPlayerItemAnimation ||
                    state.HasPlayerReuseDelay ||
                    state.HasPlayerChannel ||
                    state.HasPlayerPendingItemReuse);
        }

        public static void ResetCreativeUiReleasePulseState()
        {
            _creativeUiLeftReleaseConsumedForHold = false;
            _creativeUiRightReleaseConsumedForHold = false;
        }

        public static void SetCreativeUiMouseButtonDownFallbackOverrideForTests(Func<int, bool> readButtonDown)
        {
            _creativeUiMouseButtonDownFallbackOverride = readButtonDown;
        }

        private static void PlanCreativeUiReleasePulse(
            bool hasButtonDown,
            bool buttonDown,
            bool hasRelease,
            bool releaseBefore,
            ref bool consumedForHold,
            out bool releaseDuringScope,
            out bool releaseAfterScope)
        {
            releaseDuringScope = releaseBefore;
            releaseAfterScope = releaseBefore;
            if (!hasButtonDown || !buttonDown)
            {
                consumedForHold = false;
                return;
            }

            if (!consumedForHold)
            {
                consumedForHold = true;
                releaseDuringScope = hasRelease;
                releaseAfterScope = false;
                return;
            }

            releaseDuringScope = false;
            releaseAfterScope = false;
        }

        private static void ResetCreativeUiReleasePulseIfButtonUp(
            bool hasButtonDown,
            bool buttonDown,
            ref bool consumedForHold)
        {
            if (!hasButtonDown || !buttonDown)
            {
                consumedForHold = false;
            }
        }

        private static bool IsMouseOverCreativeUiToggleButton(Type mainType)
        {
            if (mainType == null ||
                !TryReadStaticInt(mainType, "mouseX", out var mouseX) ||
                !TryReadStaticInt(mainType, "mouseY", out var mouseY))
            {
                return false;
            }

            var x = 28f;
            const float y = 267f;
            var inventoryScale = 1f;
            TryReadStaticFloat(mainType, "inventoryScale", out inventoryScale);
            if (TryReadStaticInt(mainType, "screenHeight", out var screenHeight) &&
                screenHeight < 650 &&
                TryReadCreativeMenuEnabled(out var enabled, out _) &&
                enabled)
            {
                x += 52f * inventoryScale;
            }

            var width = 44f;
            var height = 44f;
            if (TryReadCreativeUiButtonTextureSize(out var textureWidth, out var textureHeight))
            {
                width = textureWidth;
                height = textureHeight;
            }

            const float margin = 4f;
            return mouseX >= x - margin &&
                   mouseX <= x + width + margin &&
                   mouseY >= y - margin &&
                   mouseY <= y + height + margin;
        }

        private static bool TryReadCreativeUiButtonTextureSize(out float width, out float height)
        {
            width = 0f;
            height = 0f;
            var creativeMenu = GetCreativeMenu();
            var asset = GetMember(creativeMenu, "_buttonTexture");
            var texture = asset == null ? null : GetMember(asset, "Value");
            if (texture == null ||
                !TryReadMemberInt(texture, "Width", out var textureWidth) ||
                !TryReadMemberInt(texture, "Height", out var textureHeight) ||
                textureWidth <= 0 ||
                textureHeight <= 0)
            {
                return false;
            }

            width = textureWidth;
            height = textureHeight;
            return true;
        }

        private static bool TryReadMouseButtonDownFallback(int virtualKey, out bool down)
        {
            down = false;
            var overrideReader = _creativeUiMouseButtonDownFallbackOverride;
            if (overrideReader != null)
            {
                try
                {
                    down = overrideReader(virtualKey);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return true;
            }

            try
            {
                down = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadPlayerInputTrigger(string packName, string triggerName, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(triggerName))
            {
                return false;
            }

            var triggersPack = GetPlayerInputTriggersPack();
            var triggersSet = triggersPack == null ? null : GetMember(triggersPack, packName);
            return triggersSet != null && TryReadMemberBool(triggersSet, triggerName, out value);
        }

        private static bool TrySetPlayerInputTrigger(string packName, string triggerName, bool value)
        {
            if (string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(triggerName))
            {
                return false;
            }

            var triggersPack = GetPlayerInputTriggersPack();
            var triggersSet = triggersPack == null ? null : GetMember(triggersPack, packName);
            return triggersSet != null && TrySetMember(triggersSet, triggerName, value);
        }

        private static object GetPlayerInputTriggersPack()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            var assembly = mainType == null ? null : mainType.Assembly;
            var playerInputType = assembly == null ? null : assembly.GetType("Terraria.GameInput.PlayerInput", false);
            return playerInputType == null ? null : GetStatic(playerInputType, "Triggers");
        }

        public static bool TryApplyCreativeUiWorldInputGuard(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null)
            {
                message = "Scoped journey state unavailable for creative UI world input guard.";
                return false;
            }

            if (state.Source != TravelMenuScopedJourneySource.JueMingZTravelMenuScope)
            {
                message = "Creative UI world input guard skipped outside JueMing-Z travel menu scope.";
                return false;
            }

            if (!string.Equals(state.Scope, "CreativeUI.Update", StringComparison.Ordinal))
            {
                message = "Creative UI world input guard skipped outside CreativeUI.Update.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable for creative UI world input guard.";
                return false;
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            if (!TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) || !creativeMenuEnabled)
            {
                message = string.IsNullOrWhiteSpace(creativeMenuMessage)
                    ? "Creative UI world input guard skipped because CreativeUI is closed."
                    : creativeMenuMessage;
                return false;
            }

            bool inventoryOpen;
            string inventoryMessage;
            if (!TryReadPlayerInventoryOpen(out inventoryOpen, out inventoryMessage) || !inventoryOpen)
            {
                message = string.IsNullOrWhiteSpace(inventoryMessage)
                    ? "Creative UI world input guard skipped because Main.playerInventory is closed."
                    : inventoryMessage;
                return false;
            }

            var player = state.LocalPlayer ?? state.Player;
            if (player == null)
            {
                message = "Local player unavailable for creative UI world input guard.";
                return false;
            }

            // Keep this guard scoped to world-use flags only.
            // CreativeUI.Update runs before PlayerInput.UpdateInput in Terraria's main loop; forcing Main.mouseLeft/Right
            // here can clobber the next UI click edge and cause intermittent missed travel-menu clicks.
            var controlSet = TrySetMember(player, "controlUseItem", false);
            var releaseSet = TrySetMember(player, "releaseUseItem", true);

            if (controlSet && releaseSet)
            {
                message = "Creative UI world input guard applied for current tick.";
                return true;
            }

            message = "Creative UI world input guard incomplete: controlUseItem=" + controlSet + ", releaseUseItem=" + releaseSet + ".";
            return false;
        }

        public static bool IsSamePlayerWorld(TravelMenuContext context, TravelMenuContext current)
        {
            if (context == null || current == null)
            {
                return false;
            }

            return SamePath(context.PlayerPath, current.PlayerPath) &&
                   SamePath(context.WorldPath, current.WorldPath);
        }

        private static bool MatchesCurrentContext(TravelMenuContext context, object playerFile, object worldFile)
        {
            if (context == null || playerFile == null || worldFile == null)
            {
                return false;
            }

            return SamePath(context.PlayerPath, ReadString(playerFile, "Path")) &&
                   SamePath(context.WorldPath, ReadString(worldFile, "Path"));
        }

        private static bool TryResolveCurrentObjects(Type mainType, out object playerFile, out object worldFile, out object player, out string message)
        {
            playerFile = null;
            worldFile = null;
            player = null;
            message = string.Empty;

            if (!TryGetActivePlayerFileData(mainType, out playerFile) || playerFile == null)
            {
                message = "ActivePlayerFileData unavailable.";
                return false;
            }

            if (!TryGetActiveWorldFileData(mainType, out worldFile) || worldFile == null)
            {
                message = "ActiveWorldFileData unavailable.";
                return false;
            }

            var filePlayer = GetMember(playerFile, "Player");
            object localPlayer;
            TryGetLocalPlayer(mainType, out localPlayer);
            player = localPlayer ?? filePlayer;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            return player != null;
        }

        private static bool TryGetActivePlayerFileData(Type mainType, out object fileData)
        {
            fileData = GetStatic(mainType, "ActivePlayerFileData");
            return fileData != null;
        }

        private static bool TryGetActiveWorldFileData(Type mainType, out object fileData)
        {
            fileData = GetStatic(mainType, "ActiveWorldFileData");
            return fileData != null;
        }

        private static bool TryGetLocalPlayer(Type mainType, out object player)
        {
            player = GetStatic(mainType, "LocalPlayer");
            if (player != null)
            {
                return true;
            }

            int myPlayer;
            if (!TryReadStaticInt(mainType, "myPlayer", out myPlayer))
            {
                return false;
            }

            var players = GetStatic(mainType, "player");
            player = GetIndexed(players, myPlayer);
            return player != null;
        }

        private static object GetCreativeMenu()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            return mainType == null ? null : GetStatic(mainType, "CreativeMenu");
        }

        public static bool TryReadCreativeMenuEnabled(out bool enabled, out string message)
        {
            enabled = false;
            message = string.Empty;
            var creativeMenu = GetCreativeMenu();
            if (creativeMenu == null)
            {
                message = "Main.CreativeMenu unavailable.";
                return false;
            }

            object raw = GetMember(creativeMenu, "Enabled");
            if (raw == null)
            {
                message = "CreativeUI.Enabled unavailable.";
                return false;
            }

            try
            {
                enabled = Convert.ToBoolean(raw);
                return true;
            }
            catch (Exception error)
            {
                message = "CreativeUI.Enabled convert failed: " + error.Message;
                return false;
            }
        }

        public static bool TryReadGodmodePowerEnabled(out bool enabled, out string message)
        {
            enabled = false;
            message = string.Empty;

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            int playerIndex;
            if (!TryResolveLocalPlayerIndex(mainType, out playerIndex))
            {
                message = "Local player index unavailable for godmode power check.";
                return false;
            }

            string managerMessage;
            if (TryReadGodmodePowerEnabledByManager(mainType, playerIndex, out enabled, out managerMessage))
            {
                message = managerMessage;
                return true;
            }

            object localPlayer;
            if (TryGetLocalPlayer(mainType, out localPlayer) &&
                localPlayer != null &&
                TryReadMemberBool(localPlayer, "creativeGodMode", out enabled))
            {
                message = string.IsNullOrWhiteSpace(managerMessage)
                    ? "Godmode power fallback read from LocalPlayer.creativeGodMode."
                    : managerMessage + " Fallback read from LocalPlayer.creativeGodMode.";
                return true;
            }

            message = string.IsNullOrWhiteSpace(managerMessage)
                ? "Unable to read journey godmode power state."
                : managerMessage;
            return false;
        }

        public static bool TryGetCreativeResearchSacrificeCap(int itemId, out int amountNeeded)
        {
            amountNeeded = 0;
            if (itemId <= 0)
            {
                return false;
            }

            object catalogInstance;
            Type catalogType;
            if (!TryResolveCreativeItemSacrificesCatalog(out catalogInstance, out catalogType))
            {
                return false;
            }

            var method = catalogType.GetMethod(
                "TryGetSacrificeCountCapToUnlockInfiniteItems",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int).MakeByRefType() },
                null);
            if (method == null)
            {
                return false;
            }

            try
            {
                object[] arguments = { itemId, 0 };
                var handled = method.Invoke(catalogInstance, arguments);
                if (!(handled is bool) || !(bool)handled)
                {
                    return false;
                }

                amountNeeded = Convert.ToInt32(arguments[1]);
                return amountNeeded > 0;
            }
            catch
            {
                amountNeeded = 0;
                return false;
            }
        }

        public static bool TryInvokeCreativeResearchProgressForAllItems(Action<int> action, out int emittedCount, out string message)
        {
            emittedCount = 0;
            message = string.Empty;
            if (action == null)
            {
                message = "Creative research progress callback unavailable.";
                return false;
            }

            int[] itemIds;
            if (!TryGetCreativeResearchItemIds(out itemIds, out message))
            {
                return false;
            }

            for (var i = 0; i < itemIds.Length; i++)
            {
                action(itemIds[i]);
                emittedCount++;
            }

            message = "Virtual creative research emitted " + emittedCount + " unlockable item ids.";
            return true;
        }

        private static bool TryGetCreativeResearchItemIds(out int[] itemIds, out string message)
        {
            lock (CreativeResearchCacheSync)
            {
                if (_creativeResearchItemIdsCache != null && _creativeResearchItemIdsCache.Length > 0)
                {
                    itemIds = _creativeResearchItemIdsCache;
                    message = "Virtual creative research item cache hit.";
                    return true;
                }
            }

            if (!TryBuildCreativeResearchItemIds(out itemIds, out message))
            {
                return false;
            }

            lock (CreativeResearchCacheSync)
            {
                _creativeResearchItemIdsCache = itemIds;
            }

            message = "Virtual creative research item cache built; count=" + itemIds.Length + ".";
            return true;
        }

        private static bool TryBuildCreativeResearchItemIds(out int[] itemIds, out string message)
        {
            itemIds = Array.Empty<int>();
            message = string.Empty;

            var mainType = TerrariaRuntimeTypes.MainType;
            var assembly = mainType == null ? null : mainType.Assembly;
            if (assembly == null)
            {
                message = "Terraria assembly unavailable while building virtual creative research list.";
                return false;
            }

            var itemIdType = assembly.GetType("Terraria.ID.ItemID", false);
            if (itemIdType == null || !TryReadStaticInt(itemIdType, "Count", out var itemCount) || itemCount <= 0)
            {
                message = "ItemID.Count unavailable while building virtual creative research list.";
                return false;
            }

            IDictionary researchOverrideMap;
            TryReadCreativeResearchOverrideMap(out researchOverrideMap);

            var uniqueResearchableIds = new HashSet<int>();
            var list = new List<int>(itemCount);
            for (var itemId = 1; itemId < itemCount; itemId++)
            {
                var normalizedItemId = NormalizeCreativeResearchItemId(itemId, researchOverrideMap);
                if (normalizedItemId <= 0 || !uniqueResearchableIds.Add(normalizedItemId))
                {
                    continue;
                }

                int amountNeeded;
                if (!TryGetCreativeResearchSacrificeCap(normalizedItemId, out amountNeeded) || amountNeeded <= 0)
                {
                    continue;
                }

                list.Add(normalizedItemId);
            }

            if (list.Count == 0)
            {
                message = "Virtual creative research list is empty.";
                return false;
            }

            itemIds = list.ToArray();
            return true;
        }

        private static int NormalizeCreativeResearchItemId(int itemId, IDictionary researchOverrideMap)
        {
            if (itemId <= 0 || researchOverrideMap == null)
            {
                return itemId;
            }

            try
            {
                if (!researchOverrideMap.Contains(itemId))
                {
                    return itemId;
                }

                var overridden = researchOverrideMap[itemId];
                return overridden == null ? itemId : Convert.ToInt32(overridden);
            }
            catch
            {
                return itemId;
            }
        }

        private static bool TryReadCreativeResearchOverrideMap(out IDictionary overrideMap)
        {
            overrideMap = null;
            var mainType = TerrariaRuntimeTypes.MainType;
            var assembly = mainType == null ? null : mainType.Assembly;
            var contentSamplesType = assembly == null ? null : assembly.GetType("Terraria.ContentSamples", false);
            var rawMap = contentSamplesType == null ? null : GetStatic(contentSamplesType, "CreativeResearchItemPersistentIdOverride");
            overrideMap = rawMap as IDictionary;
            return overrideMap != null;
        }

        private static bool TryResolveCreativeItemSacrificesCatalog(out object catalogInstance, out Type catalogType)
        {
            catalogInstance = null;
            catalogType = null;

            var mainType = TerrariaRuntimeTypes.MainType;
            var assembly = mainType == null ? null : mainType.Assembly;
            catalogType = assembly == null ? null : assembly.GetType("Terraria.GameContent.Creative.CreativeItemSacrificesCatalog", false);
            if (catalogType == null)
            {
                return false;
            }

            catalogInstance = GetStatic(catalogType, "Instance");
            return catalogInstance != null;
        }

        private static object GetStatic(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TrySetStaticMember(Type type, string name, object value)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    field.SetValue(null, ConvertValue(value, field.FieldType));
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanWrite)
                {
                    property.SetValue(null, ConvertValue(value, property.PropertyType), null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadGodmodePowerEnabledByManager(Type mainType, int playerIndex, out bool enabled, out string message)
        {
            enabled = false;
            message = string.Empty;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            var assembly = mainType.Assembly;
            if (assembly == null)
            {
                message = "Terraria assembly unavailable.";
                return false;
            }

            var managerType = assembly.GetType("Terraria.GameContent.Creative.CreativePowerManager", false);
            var godmodeType = assembly.GetType("Terraria.GameContent.Creative.CreativePowers+GodmodePower", false);
            if (managerType == null || godmodeType == null)
            {
                message = "CreativePowerManager.GodmodePower type unavailable.";
                return false;
            }

            try
            {
                var initializeMethod = managerType.GetMethod(
                    "Initialize",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
                initializeMethod?.Invoke(null, new object[0]);
            }
            catch
            {
            }

            var managerInstance = GetStatic(managerType, "Instance");
            if (managerInstance == null)
            {
                message = "CreativePowerManager.Instance unavailable.";
                return false;
            }

            MethodInfo getPowerMethod = null;
            try
            {
                foreach (var method in managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!string.Equals(method.Name, "GetPower", StringComparison.Ordinal) ||
                        !method.IsGenericMethodDefinition)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        getPowerMethod = method;
                        break;
                    }
                }
            }
            catch
            {
                getPowerMethod = null;
            }

            if (getPowerMethod == null)
            {
                message = "CreativePowerManager.GetPower<T>() unavailable.";
                return false;
            }

            object power;
            try
            {
                var closedMethod = getPowerMethod.MakeGenericMethod(godmodeType);
                power = closedMethod.Invoke(managerInstance, new object[0]);
            }
            catch (Exception error)
            {
                message = "CreativePowerManager.GetPower<GodmodePower>() failed: " + error.Message;
                return false;
            }

            if (power == null)
            {
                message = "CreativePowerManager.GodmodePower instance unavailable.";
                return false;
            }

            var isEnabledMethod = power.GetType().GetMethod(
                "IsEnabledForPlayer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(int) },
                null);
            if (isEnabledMethod == null)
            {
                message = "GodmodePower.IsEnabledForPlayer(int) unavailable.";
                return false;
            }

            try
            {
                var raw = isEnabledMethod.Invoke(power, new object[] { playerIndex });
                enabled = raw is bool && (bool)raw;
                message = "Godmode power state read via CreativePowerManager.";
                return true;
            }
            catch (Exception error)
            {
                message = "GodmodePower.IsEnabledForPlayer invoke failed: " + error.Message;
                return false;
            }
        }

        private static bool TryResolveLocalPlayerIndex(Type mainType, out int playerIndex)
        {
            playerIndex = -1;
            if (mainType == null)
            {
                return false;
            }

            if (TryReadStaticInt(mainType, "myPlayer", out playerIndex) && playerIndex >= 0)
            {
                return true;
            }

            object localPlayer;
            if (TryGetLocalPlayer(mainType, out localPlayer) &&
                localPlayer != null &&
                TryReadMemberInt(localPlayer, "whoAmI", out playerIndex) &&
                playerIndex >= 0)
            {
                return true;
            }

            playerIndex = -1;
            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TrySetMember(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    field.SetValue(instance, ConvertValue(value, field.FieldType));
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    property.SetValue(instance, ConvertValue(value, property.PropertyType), null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static object GetIndexed(object collection, int index)
        {
            if (collection == null || index < 0)
            {
                return null;
            }

            var array = collection as Array;
            if (array != null && index < array.Length)
            {
                return array.GetValue(index);
            }

            var list = collection as IList;
            if (list != null && index < list.Count)
            {
                return list[index];
            }

            return null;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
            return TryConvertInt(raw, out value);
        }

        private static bool TryReadStaticFloat(Type type, string name, out float value)
        {
            value = 0f;
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw);
                return true;
            }
            catch
            {
                value = 0f;
                return false;
            }
        }

        private static bool TryReadMemberInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            return TryConvertInt(raw, out value);
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            var raw = GetStatic(type, name);
            return TryConvertBool(raw, out value);
        }

        private static bool TryReadMemberBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            return TryConvertBool(raw, out value);
        }

        private static bool TryConvertInt(object raw, out int value)
        {
            value = 0;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadString(object instance, string name)
        {
            var raw = GetMember(instance, name);
            return raw == null ? string.Empty : raw.ToString();
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType == null || value == null)
            {
                return value;
            }

            if (targetType == typeof(byte))
            {
                return Convert.ToByte(value);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }

            return value;
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) ? left : right ?? string.Empty;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
