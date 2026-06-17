using System;

namespace JueMingZ.Compat
{
    internal sealed class TerrariaUiMouseCaptureDiagnosticsSnapshot
    {
        public bool MainMouseLeft { get; set; }
        public bool MainMouseLeftRelease { get; set; }
        public bool MainMouseInterface { get; set; }
        public bool MainBlockMouse { get; set; }
        public bool PlayerMouseInterface { get; set; }
    }

    public static partial class TerrariaUiMouseCompat
    {
        internal static TerrariaUiMouseCaptureDiagnosticsSnapshot ReadCaptureDiagnosticsSnapshot()
        {
            var snapshot = new TerrariaUiMouseCaptureDiagnosticsSnapshot();
            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType != null)
                {
                    EnsureUiMouseAccessors(mainType);
                    bool value;
                    if (_mainMouseLeftAccessor.TryGet(null, out value))
                    {
                        snapshot.MainMouseLeft = value;
                    }

                    if (_mainMouseLeftReleaseAccessor.TryGet(null, out value))
                    {
                        snapshot.MainMouseLeftRelease = value;
                    }

                    if (_mainMouseInterfaceAccessor.TryGet(null, out value))
                    {
                        snapshot.MainMouseInterface = value;
                    }

                    if (_mainBlockMouseAccessor.TryGet(null, out value))
                    {
                        snapshot.MainBlockMouse = value;
                    }
                }

                object player;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                    TryReadLocalPlayerMouseInterface(player, out var playerMouseInterface))
                {
                    snapshot.PlayerMouseInterface = playerMouseInterface;
                }
            }
            catch
            {
            }

            return snapshot;
        }

        private static bool TryReadLocalPlayerMouseInterface(object player, out bool value)
        {
            value = false;
            if (player == null)
            {
                return false;
            }

            try
            {
                var playerType = player.GetType();
                if (_localPlayerMouseInterfaceType != playerType)
                {
                    lock (UiMouseAccessorSyncRoot)
                    {
                        if (_localPlayerMouseInterfaceType != playerType)
                        {
                            _localPlayerMouseInterfaceType = playerType;
                            _localPlayerMouseInterfaceAccessor = BooleanMemberAccessor.Resolve(playerType, "mouseInterface", false);
                        }
                    }
                }

                return _localPlayerMouseInterfaceAccessor.TryGet(player, out value);
            }
            catch
            {
                value = false;
                return false;
            }
        }
    }
}
