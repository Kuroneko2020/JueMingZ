using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        // This is the controlled input-write Compat boundary; scoped captures
        // must restore every Terraria/Main flag they touch.
        // Keep this layer to shared primitives; feature policy belongs in Actions/services.
        private const BindingFlags InstanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int VkLeftButton = 0x01;
        private const int VkRightButton = 0x02;
        private static string _lastError = string.Empty;
        private static bool _playerInputMouseResolved;
        private static Type _playerInputType;
        private static FieldInfo _playerInputMouseXField;
        private static FieldInfo _playerInputMouseYField;
        private static PropertyInfo _playerInputMouseXProperty;
        private static PropertyInfo _playerInputMouseYProperty;
        private static bool _tileTargetResolved;
        private static FieldInfo _tileTargetXField;
        private static FieldInfo _tileTargetYField;
        private static bool _tileInteractionUseResolved;
        private static MethodInfo _tileInteractionUseMethod;
        private static bool _tileInteractionCheckResolved;
        private static MethodInfo _tileInteractionCheckMethod;
        private static bool _changeDirResolved;
        private static MethodInfo _changeDirMethod;
        private static bool _airJumpFieldsResolved;
        private static FieldInfo[] _airJumpFields = new FieldInfo[0];
        private static bool _bootFlyingMethodResolved;
        private static MethodInfo _bootFlyingMethod;
        private static bool _playerInputJumpTriggersResolved;
        private static FieldInfo _playerInputTriggersField;
        private static PropertyInfo _playerInputTriggersProperty;
        private static readonly object TileInteractionOverrideSync = new object();
        private static Guid _tileInteractionOverrideRequestId = Guid.Empty;
        private static int _tileInteractionOverrideTileX = -1;
        private static int _tileInteractionOverrideTileY = -1;
        private static DateTime _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
        private static string _lastTileInteractionOverrideMessage = string.Empty;
        private static readonly object AutoFacingOverrideSync = new object();
        private static Guid _autoFacingOverrideRequestId = Guid.Empty;
        private static int _autoFacingOverrideDirection;
        private static int _autoFacingOverrideSelectedSlot = -1;
        private static int _autoFacingOverrideItemType;
        private static DateTime _autoFacingOverrideExpiresUtc = DateTime.MinValue;
        private static string _lastAutoFacingOverrideMessage = string.Empty;
        private static readonly object AutoClickerUseItemSuppressionSync = new object();
        private static DateTime _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;

        public static bool InputCompatReady { get; private set; }
        public static bool SelectedItemGetterReady { get { return TerrariaPlayerSelectionCompat.SelectedItemGetterReady; } }
        public static bool SelectedItemSelectorReady { get { return TerrariaPlayerSelectionCompat.SelectedItemSelectorReady; } }
        public static bool SelectedItemAccessorReady { get { return TerrariaPlayerSelectionCompat.SelectedItemAccessorReady; } }
        public static string LastSelectionMethod { get { return TerrariaPlayerSelectionCompat.LastSelectionMethod; } }
        public static string PlayerTypeName { get; private set; } = string.Empty;
        public static string LastInputCompatError { get { return _lastError; } }
        public static string LastTileInteractionOverrideMessage { get { return _lastTileInteractionOverrideMessage; } }
        public static string LastAutoFacingOverrideMessage { get { return _lastAutoFacingOverrideMessage; } }

        public static bool TryGetLocalPlayer(out object player)
        {
            player = null;
            try
            {
                if (!TerrariaMemberCache.EnsureInitializedLateOnly())
                {
                    return Fail(TerrariaMemberCache.LastError);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                player = GetStatic(mainType, "LocalPlayer");
                if (IsTerrariaPlayer(player))
                {
                    InputCompatReady = true;
                    return true;
                }

                var players = GetStatic(mainType, "player") as System.Collections.IList;
                var myPlayer = 0;
                var rawMyPlayer = GetStatic(mainType, "myPlayer");
                if (rawMyPlayer != null)
                {
                    myPlayer = Convert.ToInt32(rawMyPlayer);
                }

                if (players == null || myPlayer < 0 || myPlayer >= players.Count)
                {
                    return Fail("Local player unavailable.");
                }

                player = players[myPlayer];
                if (!IsTerrariaPlayer(player))
                {
                    return Fail("Local player object is not Terraria.Player.");
                }

                InputCompatReady = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }
    }
}
