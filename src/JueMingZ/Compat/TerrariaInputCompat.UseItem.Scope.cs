using System;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public sealed class ScopedUseItemTakeover : IDisposable
        {
            // Scoped takeover is the combat-facing contract for temporary
            // controlUseItem/Main mouse writes; callers must restore or dispose.
            private bool _disposed;

            internal ScopedUseItemTakeover(object player, bool pressed, string scopeName)
            {
                Player = player;
                Pressed = pressed;
                ScopeName = scopeName ?? string.Empty;
            }

            internal object Player { get; private set; }
            public bool Captured { get; internal set; }
            public bool Applied { get; internal set; }
            public bool Pressed { get; private set; }
            public string ScopeName { get; private set; }
            public bool PlayerControlUseItemCaptured { get; internal set; }
            public bool PlayerControlUseItem { get; internal set; }
            public bool PlayerReleaseUseItemCaptured { get; internal set; }
            public bool PlayerReleaseUseItem { get; internal set; }
            public bool PlayerChannelCaptured { get; internal set; }
            public bool PlayerChannel { get; internal set; }
            public bool MainMouseLeftCaptured { get; internal set; }
            public bool MainMouseLeft { get; internal set; }
            public bool MainMouseLeftReleaseCaptured { get; internal set; }
            public bool MainMouseLeftRelease { get; internal set; }
            public bool MainMouseRightCaptured { get; internal set; }
            public bool MainMouseRight { get; internal set; }
            public bool MainMouseRightReleaseCaptured { get; internal set; }
            public bool MainMouseRightRelease { get; internal set; }
            public bool SuppressedPhysicalInput { get; internal set; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(this);
            }
        }

        public static bool TryApplyUseItemTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed);

            return ok ? ClearInputError() : Fail("Cannot apply use item takeover input override.");
        }

        public static bool TryApplyUseItemClickTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item click takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed, false);

            return ok ? ClearInputError() : Fail("Cannot apply use item click takeover input override.");
        }

        public static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, true, false, out takeover);
        }

        public static bool TryBeginScopedUseItemClickTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, false, false, out takeover);
        }

        public static bool TryBeginScopedUseItemClickTakeoverSuppressingRightClick(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, false, true, out takeover);
        }

        private static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, bool applyChannel, bool suppressRightClick, out ScopedUseItemTakeover takeover)
        {
            takeover = null;
            if (player == null)
            {
                return Fail("Cannot begin scoped use item takeover: player unavailable.");
            }

            var state = new ScopedUseItemTakeover(player, pressed, scopeName);
            // Capture before writing so failed or completed takeover can fail
            // closed back to vanilla input state.
            bool value;
            if (TryGetBool(player, "controlUseItem", out value))
            {
                state.PlayerControlUseItemCaptured = true;
                state.PlayerControlUseItem = value;
            }

            if (TryGetBool(player, "releaseUseItem", out value))
            {
                state.PlayerReleaseUseItemCaptured = true;
                state.PlayerReleaseUseItem = value;
            }

            if (applyChannel && TryGetBool(player, "channel", out value))
            {
                state.PlayerChannelCaptured = true;
                state.PlayerChannel = value;
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (TryGetStaticBool(mainType, "mouseLeft", out value))
                {
                    state.MainMouseLeftCaptured = true;
                    state.MainMouseLeft = value;
                }

                if (TryGetStaticBool(mainType, "mouseLeftRelease", out value))
                {
                    state.MainMouseLeftReleaseCaptured = true;
                    state.MainMouseLeftRelease = value;
                }

                if (suppressRightClick && TryGetStaticBool(mainType, "mouseRight", out value))
                {
                    state.MainMouseRightCaptured = true;
                    state.MainMouseRight = value;
                }

                if (suppressRightClick && TryGetStaticBool(mainType, "mouseRightRelease", out value))
                {
                    state.MainMouseRightReleaseCaptured = true;
                    state.MainMouseRightRelease = value;
                }
            }

            state.Captured = state.PlayerControlUseItemCaptured ||
                             state.PlayerReleaseUseItemCaptured ||
                             state.PlayerChannelCaptured ||
                             state.MainMouseLeftCaptured ||
                             state.MainMouseLeftReleaseCaptured ||
                             state.MainMouseRightCaptured ||
                             state.MainMouseRightReleaseCaptured;
            state.SuppressedPhysicalInput = !pressed &&
                                            ((state.PlayerControlUseItemCaptured && state.PlayerControlUseItem) ||
                                             (state.MainMouseLeftCaptured && state.MainMouseLeft));

            if (!state.PlayerControlUseItemCaptured || !state.PlayerReleaseUseItemCaptured)
            {
                return Fail("Cannot begin scoped use item takeover: player use item fields unavailable.");
            }

            if (!ApplyUseItemTakeoverFields(player, pressed, applyChannel, suppressRightClick))
            {
                TryRestoreScopedUseItemTakeover(state);
                return Fail("Cannot begin scoped use item takeover: " + LastInputCompatError);
            }

            state.Applied = true;
            takeover = state;
            return ClearInputError();
        }

        public static bool TryRestoreScopedUseItemTakeover(ScopedUseItemTakeover takeover)
        {
            if (takeover == null || !takeover.Captured || takeover.Player == null)
            {
                return false;
            }

            var ok = true;
            if (takeover.PlayerControlUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "controlUseItem", takeover.PlayerControlUseItem);
            }

            if (takeover.PlayerReleaseUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "releaseUseItem", takeover.PlayerReleaseUseItem);
            }

            if (takeover.PlayerChannelCaptured)
            {
                ok &= TrySetMemberIfExists(takeover.Player, "channel", takeover.PlayerChannel);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (takeover.MainMouseLeftCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeft", takeover.MainMouseLeft);
                }

                if (takeover.MainMouseLeftReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeftRelease", takeover.MainMouseLeftRelease);
                }

                if (takeover.MainMouseRightCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRight", takeover.MainMouseRight);
                }

                if (takeover.MainMouseRightReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRightRelease", takeover.MainMouseRightRelease);
                }
            }

            // Restore every captured Player/Main field; partial restore reports
            // failure instead of pretending the scoped click succeeded.
            return ok ? ClearInputError() : Fail("Cannot restore scoped use item takeover.");
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed)
        {
            return ApplyUseItemTakeoverFields(player, pressed, true);
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed, bool applyChannel)
        {
            return ApplyUseItemTakeoverFields(player, pressed, applyChannel, false);
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed, bool applyChannel, bool suppressRightClick)
        {
            // Controlled input write: combat takeover must override both Player and Main mouse use state for this tick/scope.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", true);
            if (applyChannel)
            {
                TrySetMemberIfExists(player, "channel", pressed);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", pressed);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
                if (suppressRightClick)
                {
                    TrySetStaticIfExists(mainType, "mouseRight", false);
                    TrySetStaticIfExists(mainType, "mouseRightRelease", false);
                }
            }

            return ok;
        }

        private static Type ResolveMainTypeForInputWrite()
        {
            return FindType("Terraria.Main");
        }
    }
}
