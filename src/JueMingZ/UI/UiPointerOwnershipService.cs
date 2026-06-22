using System;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    public static class UiPointerOwnershipService
    {
        private static readonly object SyncRoot = new object();
        private static UiPointerOwnershipSnapshot _snapshot = UiPointerOwnershipSnapshot.Empty;

        public static void RegisterPointerOwnerForCurrentFrame(
            string ownerId,
            string ownerKind,
            LegacyUiRect bounds,
            bool leftOwned,
            bool leftConsumed,
            bool scrollOwned,
            string reason)
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            if (!frameKey.IsValid)
            {
                return;
            }

            lock (SyncRoot)
            {
                var current = IsCurrentFrameLocked(frameKey) ? _snapshot : UiPointerOwnershipSnapshot.Empty;
                _snapshot = UiPointerOwnershipSnapshot.Create(
                    frameKey,
                    ownerId,
                    ownerKind,
                    true,
                    leftOwned || current.LeftOwned,
                    leftConsumed || current.LeftConsumed,
                    scrollOwned || current.ScrollOwned,
                    true,
                    bounds,
                    reason);
            }
        }

        internal static void EnsureOperationWindowPointerOwned(string reason)
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            if (!frameKey.IsValid)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (IsCurrentFrameLocked(frameKey) && _snapshot.PointerOwned)
                {
                    return;
                }

                _snapshot = UiPointerOwnershipSnapshot.Create(
                    frameKey,
                    "operation-window",
                    "OperationWindow",
                    true,
                    true,
                    false,
                    false,
                    false,
                    new LegacyUiRect(0, 0, 0, 0),
                    reason);
            }
        }

        internal static void MarkOperationWindowLeftConsumed(string reason)
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            if (!frameKey.IsValid)
            {
                return;
            }

            lock (SyncRoot)
            {
                var current = IsCurrentFrameLocked(frameKey) && _snapshot.PointerOwned
                    ? _snapshot
                    : UiPointerOwnershipSnapshot.Create(
                        frameKey,
                        "operation-window",
                        "OperationWindow",
                        true,
                        true,
                        false,
                        false,
                        false,
                        new LegacyUiRect(0, 0, 0, 0),
                        reason);
                _snapshot = current.WithLeftConsumed(reason);
            }
        }

        public static bool IsPointerOwnedThisFrame()
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (SyncRoot)
            {
                return IsCurrentFrameLocked(frameKey) && _snapshot.PointerOwned;
            }
        }

        public static bool IsLeftConsumedThisFrame()
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (SyncRoot)
            {
                return IsCurrentFrameLocked(frameKey) && _snapshot.LeftConsumed;
            }
        }

        public static UiPointerOwnershipDetails ResolveWorldPointerOwnership(DiagnosticMouseState raw)
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            UiPointerOwnershipSnapshot snapshot;
            lock (SyncRoot)
            {
                snapshot = IsCurrentFrameLocked(frameKey) ? _snapshot : UiPointerOwnershipSnapshot.Empty;
            }

            return UiPointerOwnershipDetails.Create(snapshot, raw);
        }

        public static bool IsPointerOwnerBoundsHitThisFrame(DiagnosticMouseState raw)
        {
            return ResolveWorldPointerOwnership(raw).BoundsHit;
        }

        public static bool ResolveWorldUiOwned(bool legacyUiOwned, bool vanillaUiOwned)
        {
            return legacyUiOwned || vanillaUiOwned || IsPointerOwnedThisFrame();
        }

        public static bool ResolveWorldLeftDown(DiagnosticMouseState raw)
        {
            if (raw == null || !raw.GameInputAvailable)
            {
                return false;
            }

            // OS physical buttons cannot be cleared by Terraria input consume. Once
            // UI consumes this frame's left click, later world overlays must not
            // revive it from either OS or stale cached Terraria state.
            if (IsLeftConsumedThisFrame())
            {
                return false;
            }

            return raw.TerrariaLeftDown || raw.OsLeftDown;
        }

        internal static UiPointerOwnershipSnapshot GetSnapshotForTesting()
        {
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (SyncRoot)
            {
                return IsCurrentFrameLocked(frameKey) ? _snapshot : UiPointerOwnershipSnapshot.Empty;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = UiPointerOwnershipSnapshot.Empty;
            }
        }

        private static bool IsCurrentFrameLocked(UiInputFrameKey frameKey)
        {
            return frameKey.IsValid &&
                   _snapshot.FrameKeyValid &&
                   _snapshot.FrameKey.Equals(frameKey);
        }
    }

    public sealed class UiPointerOwnershipDetails
    {
        private UiPointerOwnershipDetails(
            string ownerId,
            string ownerKind,
            string reason,
            bool pointerOwned,
            bool leftOwned,
            bool leftConsumed,
            bool scrollOwned,
            bool hasBounds,
            LegacyUiRect bounds,
            bool mouseAvailable,
            int mouseX,
            int mouseY,
            string mouseSource,
            bool boundsHit)
        {
            OwnerId = ownerId ?? string.Empty;
            OwnerKind = ownerKind ?? string.Empty;
            Reason = reason ?? string.Empty;
            PointerOwned = pointerOwned;
            LeftOwned = leftOwned;
            LeftConsumed = leftConsumed;
            ScrollOwned = scrollOwned;
            HasBounds = hasBounds;
            Bounds = bounds;
            MouseAvailable = mouseAvailable;
            MouseX = mouseX;
            MouseY = mouseY;
            MouseSource = mouseSource ?? string.Empty;
            BoundsHit = boundsHit;
        }

        internal static UiPointerOwnershipDetails Create(UiPointerOwnershipSnapshot snapshot, DiagnosticMouseState raw)
        {
            snapshot = snapshot ?? UiPointerOwnershipSnapshot.Empty;
            int mouseX;
            int mouseY;
            string mouseSource;
            var mouseAvailable = TryResolveMouse(raw, out mouseX, out mouseY, out mouseSource);
            // Pointer ownership only means a UI layer observed or captured this frame's pointer.
            // LeftConsumed is the hard OS-left revival blocker; BoundsHit is the separate
            // same-coordinate-domain query future world overlay gates can use.
            var boundsHit = snapshot.PointerOwned &&
                            snapshot.HasBounds &&
                            mouseAvailable &&
                            snapshot.Bounds.Contains(mouseX, mouseY);
            return new UiPointerOwnershipDetails(
                snapshot.OwnerId,
                snapshot.OwnerKind,
                snapshot.Reason,
                snapshot.PointerOwned,
                snapshot.LeftOwned,
                snapshot.LeftConsumed,
                snapshot.ScrollOwned,
                snapshot.HasBounds,
                snapshot.Bounds,
                mouseAvailable,
                mouseX,
                mouseY,
                mouseSource,
                boundsHit);
        }

        private static bool TryResolveMouse(DiagnosticMouseState raw, out int mouseX, out int mouseY, out string source)
        {
            mouseX = -1;
            mouseY = -1;
            source = string.Empty;
            if (raw == null)
            {
                return false;
            }

            if (raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0 && raw.TerrariaMouseY >= 0)
            {
                mouseX = raw.TerrariaMouseX;
                mouseY = raw.TerrariaMouseY;
                source = "Terraria";
                return true;
            }

            if (raw.OsReadAvailable && raw.OsClientMouseX >= 0 && raw.OsClientMouseY >= 0)
            {
                mouseX = raw.OsClientMouseX;
                mouseY = raw.OsClientMouseY;
                source = "OsClient";
                return true;
            }

            return false;
        }

        public string OwnerId { get; private set; }

        public string OwnerKind { get; private set; }

        public string Reason { get; private set; }

        public bool PointerOwned { get; private set; }

        public bool LeftOwned { get; private set; }

        public bool LeftConsumed { get; private set; }

        public bool ScrollOwned { get; private set; }

        public bool HasBounds { get; private set; }

        public LegacyUiRect Bounds { get; private set; }

        public bool MouseAvailable { get; private set; }

        public int MouseX { get; private set; }

        public int MouseY { get; private set; }

        public string MouseSource { get; private set; }

        public bool BoundsHit { get; private set; }
    }

    public sealed class UiPointerOwnershipSnapshot
    {
        private UiPointerOwnershipSnapshot(
            UiInputFrameKey frameKey,
            bool frameKeyValid,
            string ownerId,
            string ownerKind,
            bool pointerOwned,
            bool leftOwned,
            bool leftConsumed,
            bool scrollOwned,
            bool hasBounds,
            LegacyUiRect bounds,
            string reason)
        {
            FrameKey = frameKey;
            FrameKeyValid = frameKeyValid;
            OwnerId = ownerId ?? string.Empty;
            OwnerKind = ownerKind ?? string.Empty;
            PointerOwned = pointerOwned;
            LeftOwned = leftOwned;
            LeftConsumed = leftConsumed;
            ScrollOwned = scrollOwned;
            HasBounds = hasBounds;
            Bounds = bounds;
            Reason = reason ?? string.Empty;
        }

        public static UiPointerOwnershipSnapshot Empty
        {
            get
            {
                return new UiPointerOwnershipSnapshot(
                    UiInputFrameKey.None,
                    false,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    false,
                    false,
                    false,
                    new LegacyUiRect(0, 0, 0, 0),
                    string.Empty);
            }
        }

        internal static UiPointerOwnershipSnapshot Create(
            UiInputFrameKey frameKey,
            string ownerId,
            string ownerKind,
            bool pointerOwned,
            bool leftOwned,
            bool leftConsumed,
            bool scrollOwned,
            bool hasBounds,
            LegacyUiRect bounds,
            string reason)
        {
            return new UiPointerOwnershipSnapshot(
                frameKey,
                frameKey.IsValid,
                ownerId,
                ownerKind,
                pointerOwned,
                leftOwned,
                leftConsumed,
                scrollOwned,
                hasBounds,
                bounds,
                reason);
        }

        internal UiPointerOwnershipSnapshot WithLeftConsumed(string reason)
        {
            return new UiPointerOwnershipSnapshot(
                FrameKey,
                FrameKeyValid,
                OwnerId,
                OwnerKind,
                PointerOwned,
                true,
                true,
                ScrollOwned,
                HasBounds,
                Bounds,
                reason);
        }

        internal UiInputFrameKey FrameKey { get; private set; }

        internal bool FrameKeyValid { get; private set; }

        public string OwnerId { get; private set; }

        public string OwnerKind { get; private set; }

        public bool PointerOwned { get; private set; }

        public bool LeftOwned { get; private set; }

        public bool LeftConsumed { get; private set; }

        public bool ScrollOwned { get; private set; }

        public bool HasBounds { get; private set; }

        public LegacyUiRect Bounds { get; private set; }

        public string Reason { get; private set; }
    }
}
