using System;
using JueMingZ.Records;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapFootprintPlaybackState
    {
        internal static readonly int[] AllowedRates = { 1, 10, 60, 300, 1800 };
        private static readonly object SyncRoot = new object();
        private static bool _visible;
        private static bool _paused = true;
        private static int _playbackRate = 1;
        private static long _cursorTicks;
        private static bool _isAtLatest = true;
        private static bool _dragging;
        private static bool _lastLeftDown;
        private static DateTime _lastAdvanceUtc = DateTime.MinValue;
        private static string _pairId = string.Empty;
        private static int _dataSignature;
        private static long _timelineStartTicks;
        private static long _timelineEndTicks;
        private static long _displayTimelineEndTicks;
        private static string _lastInteraction = "hidden";

        public static MapFootprintPlaybackSnapshot Advance(
            MapFootprintRenderSnapshot renderSnapshot,
            bool visible,
            DateTime utcNow)
        {
            lock (SyncRoot)
            {
                if (!visible)
                {
                    HideLocked();
                    return BuildSnapshotLocked(renderSnapshot, "hidden");
                }

                EnsureVisibleLocked(renderSnapshot, utcNow);
                var latest = _timelineEndTicks;
                if (_isAtLatest)
                {
                    _cursorTicks = latest;
                    _displayTimelineEndTicks = latest;
                }

                if (!_paused && !_dragging)
                {
                    _displayTimelineEndTicks = latest;
                    var elapsedSeconds = _lastAdvanceUtc == DateTime.MinValue
                        ? 0d
                        : Math.Max(0d, (utcNow.ToUniversalTime() - _lastAdvanceUtc).TotalSeconds);
                    var advanceTicks = (long)Math.Floor(elapsedSeconds * PlayerWorldFootprintConstants.TicksPerSecond * Math.Max(1, _playbackRate));
                    if (advanceTicks > 0L)
                    {
                        _cursorTicks = ClampTicks(SafeAdd(_cursorTicks, advanceTicks), _timelineStartTicks, latest);
                        if (_cursorTicks >= latest)
                        {
                            _cursorTicks = latest;
                            _paused = true;
                            _isAtLatest = true;
                        }
                        else
                        {
                            _isAtLatest = false;
                        }
                    }
                }

                _lastAdvanceUtc = utcNow.ToUniversalTime();
                return BuildSnapshotLocked(renderSnapshot, "advanced");
            }
        }

        public static MapFootprintPlaybackSnapshot GetSnapshotForRender(
            MapFootprintRenderSnapshot renderSnapshot,
            bool visible,
            DateTime utcNow)
        {
            lock (SyncRoot)
            {
                if (!visible)
                {
                    HideLocked();
                    return BuildSnapshotLocked(renderSnapshot, "hidden");
                }

                EnsureVisibleLocked(renderSnapshot, utcNow);
                return BuildSnapshotLocked(renderSnapshot, "ready");
            }
        }

        public static MapFootprintPlaybackInteraction HandleInput(
            MapFootprintPlaybackLayout layout,
            LegacyMouseSnapshot mouse,
            MapFootprintRenderSnapshot renderSnapshot,
            bool visible,
            DateTime utcNow)
        {
            lock (SyncRoot)
            {
                var interaction = new MapFootprintPlaybackInteraction();
                if (!visible)
                {
                    HideLocked();
                    _lastLeftDown = false;
                    interaction.State = BuildSnapshotLocked(renderSnapshot, "hidden");
                    return interaction;
                }

                EnsureVisibleLocked(renderSnapshot, utcNow);
                mouse = mouse ?? new LegacyMouseSnapshot();
                var hit = HitTest(layout, mouse.X, mouse.Y);
                var leftDown = mouse.LeftDown;
                var leftPressed = leftDown && !_lastLeftDown;
                var leftReleased = !leftDown && _lastLeftDown;
                interaction.HitTarget = hit.Target;
                interaction.BarHovered = hit.BarHovered;
                interaction.LeftPressed = leftPressed;
                interaction.LeftReleased = leftReleased;
                interaction.ScrollDelta = mouse.ScrollDelta;

                if (leftPressed)
                {
                    if (string.Equals(hit.Target, MapFootprintPlaybackHitTargets.PlayButton, StringComparison.Ordinal))
                    {
                        _paused = !_paused;
                        _displayTimelineEndTicks = _timelineEndTicks;
                        _lastInteraction = _paused ? "pauseClicked" : "playClicked";
                        interaction.ClickConsumed = true;
                    }
                    else if (hit.Rate > 0)
                    {
                        _playbackRate = NormalizeRate(hit.Rate);
                        _lastInteraction = "rateClicked";
                        interaction.ClickConsumed = true;
                    }
                    else if (string.Equals(hit.Target, MapFootprintPlaybackHitTargets.Track, StringComparison.Ordinal))
                    {
                        _dragging = true;
                        _paused = true;
                        _displayTimelineEndTicks = _timelineEndTicks;
                        SetCursorFromTrackXLocked(layout.Track, mouse.X);
                        _lastInteraction = "dragStarted";
                        interaction.ClickConsumed = true;
                    }
                    else if (hit.BarHovered)
                    {
                        _lastInteraction = "barClicked";
                        interaction.ClickConsumed = true;
                    }
                }

                if (_dragging && leftDown)
                {
                    SetCursorFromTrackXLocked(layout.Track, mouse.X);
                    _lastInteraction = "dragging";
                }

                if (_dragging && leftReleased)
                {
                    SetCursorFromTrackXLocked(layout.Track, mouse.X);
                    _dragging = false;
                    _lastInteraction = "dragReleased";
                }

                interaction.Dragging = _dragging;
                interaction.MouseCaptured = hit.BarHovered || _dragging || (_lastLeftDown && leftReleased);
                interaction.ScrollConsumed = interaction.MouseCaptured && mouse.ScrollDelta != 0;
                _lastLeftDown = leftDown;
                _lastAdvanceUtc = utcNow.ToUniversalTime();
                interaction.State = BuildSnapshotLocked(renderSnapshot, _lastInteraction);
                return interaction;
            }
        }

        public static MapFootprintPlaybackLayout CalculateLayout(int screenWidth, int screenHeight)
        {
            var safeWidth = Math.Max(1, screenWidth);
            var safeHeight = Math.Max(1, screenHeight);
            var horizontalMargin = safeWidth < 640 ? 16 : 48;
            var width = Math.Max(280, safeWidth - horizontalMargin * 2);
            width = Math.Min(width, 980);
            if (width > safeWidth - 16)
            {
                width = Math.Max(1, safeWidth - 16);
            }

            var height = safeHeight < 520 ? 48 : 56;
            var bottomMargin = safeHeight < 520 ? 18 : 34;
            var x = Math.Max(8, (safeWidth - width) / 2);
            var y = Math.Max(8, safeHeight - bottomMargin - height);
            var bar = new LegacyUiRect(x, y, width, height);
            var innerY = y + (height - 32) / 2;
            var gap = safeWidth < 640 ? 4 : 6;
            var playWidth = safeWidth < 640 ? 54 : 64;
            var rateWidth = safeWidth < 640 ? 38 : 50;
            var rateCount = AllowedRates.Length;
            var ratesWidth = rateCount * rateWidth + Math.Max(0, rateCount - 1) * gap;
            var trackX = x + 10 + playWidth + gap * 2;
            var trackRight = x + width - 10 - ratesWidth - gap * 2;
            var trackWidth = Math.Max(48, trackRight - trackX);
            var ratesX = trackX + trackWidth + gap * 2;

            var layout = new MapFootprintPlaybackLayout
            {
                Bar = bar,
                PlayButton = new LegacyUiRect(x + 10, innerY, playWidth, 32),
                Track = new LegacyUiRect(trackX, innerY + 10, trackWidth, 12),
                KnobSize = 16,
                RateButtons = new LegacyUiRect[rateCount],
                RateValues = new int[rateCount]
            };

            for (var index = 0; index < rateCount; index++)
            {
                layout.RateValues[index] = AllowedRates[index];
                layout.RateButtons[index] = new LegacyUiRect(ratesX + index * (rateWidth + gap), innerY, rateWidth, 32);
            }

            return layout;
        }

        public static MapFootprintPlaybackHitTest HitTest(MapFootprintPlaybackLayout layout, int mouseX, int mouseY)
        {
            var result = new MapFootprintPlaybackHitTest();
            if (layout == null || layout.Bar.Width <= 0 || layout.Bar.Height <= 0)
            {
                result.Target = MapFootprintPlaybackHitTargets.Outside;
                return result;
            }

            result.BarHovered = layout.Bar.Contains(mouseX, mouseY);
            if (!result.BarHovered)
            {
                result.Target = MapFootprintPlaybackHitTargets.Outside;
                return result;
            }

            if (layout.PlayButton.Contains(mouseX, mouseY))
            {
                result.Target = MapFootprintPlaybackHitTargets.PlayButton;
                return result;
            }

            for (var index = 0; layout.RateButtons != null && index < layout.RateButtons.Length; index++)
            {
                if (layout.RateButtons[index].Contains(mouseX, mouseY))
                {
                    result.Target = MapFootprintPlaybackHitTargets.RateButton;
                    result.Rate = layout.RateValues == null || index >= layout.RateValues.Length ? 0 : layout.RateValues[index];
                    return result;
                }
            }

            var trackHit = new LegacyUiRect(
                layout.Track.X - 6,
                layout.Track.Y - 10,
                layout.Track.Width + 12,
                layout.Track.Height + 20);
            if (trackHit.Contains(mouseX, mouseY))
            {
                result.Target = MapFootprintPlaybackHitTargets.Track;
                return result;
            }

            result.Target = MapFootprintPlaybackHitTargets.Bar;
            return result;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _visible = false;
                _paused = true;
                _playbackRate = 1;
                _cursorTicks = 0L;
                _isAtLatest = true;
                _dragging = false;
                _lastLeftDown = false;
                _lastAdvanceUtc = DateTime.MinValue;
                _pairId = string.Empty;
                _dataSignature = 0;
                _timelineStartTicks = 0L;
                _timelineEndTicks = 0L;
                _displayTimelineEndTicks = 0L;
                _lastInteraction = "hidden";
            }
        }

        private static void EnsureVisibleLocked(MapFootprintRenderSnapshot renderSnapshot, DateTime utcNow)
        {
            var pairId = renderSnapshot == null ? string.Empty : renderSnapshot.PairId ?? string.Empty;
            var dataSignature = renderSnapshot == null ? 0 : renderSnapshot.DataSignature;
            var timelineStart = renderSnapshot == null ? 0L : Math.Max(0L, renderSnapshot.TimelineStartTicks);
            var timelineEnd = renderSnapshot == null ? 0L : Math.Max(timelineStart, renderSnapshot.TimelineEndTicks);
            var firstVisible = !_visible;
            var dataChanged =
                !string.Equals(_pairId, pairId, StringComparison.Ordinal) ||
                _dataSignature != dataSignature ||
                _timelineStartTicks != timelineStart ||
                _timelineEndTicks != timelineEnd;

            _visible = true;
            if (firstVisible || !string.Equals(_pairId, pairId, StringComparison.Ordinal))
            {
                _paused = true;
                _playbackRate = 1;
                _cursorTicks = timelineEnd;
                _isAtLatest = true;
                _dragging = false;
                _lastLeftDown = false;
                _displayTimelineEndTicks = timelineEnd;
                _lastInteraction = "opened";
            }
            else if (dataChanged)
            {
                if (_isAtLatest)
                {
                    _cursorTicks = timelineEnd;
                    _displayTimelineEndTicks = timelineEnd;
                }
                else
                {
                    _cursorTicks = ClampTicks(_cursorTicks, timelineStart, timelineEnd);
                    _isAtLatest = _cursorTicks >= timelineEnd;
                    if (_isAtLatest || !_paused)
                    {
                        _displayTimelineEndTicks = timelineEnd;
                    }
                    else
                    {
                        _displayTimelineEndTicks = ClampTicks(
                            Math.Max(_displayTimelineEndTicks, _cursorTicks),
                            timelineStart,
                            timelineEnd);
                    }
                }
            }

            _pairId = pairId;
            _dataSignature = dataSignature;
            _timelineStartTicks = timelineStart;
            _timelineEndTicks = timelineEnd;
            if (_lastAdvanceUtc == DateTime.MinValue)
            {
                _lastAdvanceUtc = utcNow.ToUniversalTime();
            }
        }

        private static void HideLocked()
        {
            _visible = false;
            _dragging = false;
            _lastLeftDown = false;
            _lastAdvanceUtc = DateTime.MinValue;
            _lastInteraction = "hidden";
        }

        private static void SetCursorFromTrackXLocked(LegacyUiRect track, int mouseX)
        {
            var seekEnd = ResolveDisplayTimelineEndLocked(_timelineStartTicks, _timelineEndTicks, _cursorTicks);
            var duration = Math.Max(0L, seekEnd - _timelineStartTicks);
            if (duration <= 0L || track.Width <= 0)
            {
                _cursorTicks = seekEnd;
                _isAtLatest = true;
                return;
            }

            var clampedX = ClampInt(mouseX, track.X, track.Right);
            var fraction = (clampedX - track.X) / (double)Math.Max(1, track.Width);
            _cursorTicks = ClampTicks(_timelineStartTicks + (long)Math.Round(duration * fraction), _timelineStartTicks, seekEnd);
            _isAtLatest = _cursorTicks >= _timelineEndTicks;
        }

        private static MapFootprintPlaybackSnapshot BuildSnapshotLocked(MapFootprintRenderSnapshot renderSnapshot, string status)
        {
            var timelineStart = renderSnapshot == null ? _timelineStartTicks : Math.Max(0L, renderSnapshot.TimelineStartTicks);
            var timelineEnd = renderSnapshot == null ? _timelineEndTicks : Math.Max(timelineStart, renderSnapshot.TimelineEndTicks);
            var cursor = ClampTicks(_cursorTicks, timelineStart, timelineEnd);
            var displayEnd = ResolveDisplayTimelineEndLocked(timelineStart, timelineEnd, cursor);
            return new MapFootprintPlaybackSnapshot
            {
                Visible = _visible,
                Paused = _paused,
                PlaybackRate = _playbackRate,
                CursorTicks = cursor,
                IsAtLatest = _isAtLatest || cursor >= timelineEnd,
                Dragging = _dragging,
                TimelineStartTicks = timelineStart,
                TimelineEndTicks = timelineEnd,
                DisplayTimelineEndTicks = displayEnd,
                PairId = renderSnapshot == null ? _pairId : renderSnapshot.PairId ?? string.Empty,
                DataSignature = renderSnapshot == null ? _dataSignature : renderSnapshot.DataSignature,
                Status = status ?? string.Empty,
                LastInteraction = _lastInteraction ?? string.Empty
            };
        }

        private static long ResolveDisplayTimelineEndLocked(long timelineStart, long timelineEnd, long cursor)
        {
            if (timelineEnd < timelineStart)
            {
                timelineEnd = timelineStart;
            }

            if (!_paused || _isAtLatest || cursor >= timelineEnd)
            {
                return timelineEnd;
            }

            var displayEnd = _displayTimelineEndTicks <= 0L ? timelineEnd : _displayTimelineEndTicks;
            displayEnd = ClampTicks(displayEnd, timelineStart, timelineEnd);
            if (displayEnd < cursor)
            {
                displayEnd = cursor;
            }

            return displayEnd;
        }

        private static int NormalizeRate(int rate)
        {
            for (var index = 0; index < AllowedRates.Length; index++)
            {
                if (AllowedRates[index] == rate)
                {
                    return rate;
                }
            }

            return 1;
        }

        private static long ClampTicks(long value, long min, long max)
        {
            if (max < min)
            {
                max = min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static long SafeAdd(long left, long right)
        {
            if (right <= 0L)
            {
                return left;
            }

            return long.MaxValue - left < right ? long.MaxValue : left + right;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    internal static class MapFootprintPlaybackHitTargets
    {
        public const string Outside = "outside";
        public const string Bar = "bar";
        public const string PlayButton = "play";
        public const string RateButton = "rate";
        public const string Track = "track";
    }

    internal sealed class MapFootprintPlaybackSnapshot
    {
        public bool Visible { get; set; }
        public bool Paused { get; set; }
        public int PlaybackRate { get; set; }
        public long CursorTicks { get; set; }
        public bool IsAtLatest { get; set; }
        public bool Dragging { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public long DisplayTimelineEndTicks { get; set; }
        public string PairId { get; set; }
        public int DataSignature { get; set; }
        public string Status { get; set; }
        public string LastInteraction { get; set; }

        public MapFootprintPlaybackSnapshot()
        {
            PairId = string.Empty;
            Status = string.Empty;
            LastInteraction = string.Empty;
            PlaybackRate = 1;
        }
    }

    internal sealed class MapFootprintPlaybackLayout
    {
        public LegacyUiRect Bar { get; set; }
        public LegacyUiRect PlayButton { get; set; }
        public LegacyUiRect Track { get; set; }
        public int KnobSize { get; set; }
        public LegacyUiRect[] RateButtons { get; set; }
        public int[] RateValues { get; set; }
    }

    internal sealed class MapFootprintPlaybackHitTest
    {
        public bool BarHovered { get; set; }
        public string Target { get; set; }
        public int Rate { get; set; }

        public MapFootprintPlaybackHitTest()
        {
            Target = MapFootprintPlaybackHitTargets.Outside;
        }
    }

    internal sealed class MapFootprintPlaybackInteraction
    {
        public bool MouseCaptured { get; set; }
        public bool ClickConsumed { get; set; }
        public bool ScrollConsumed { get; set; }
        public bool BarHovered { get; set; }
        public bool Dragging { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
        public int ScrollDelta { get; set; }
        public string HitTarget { get; set; }
        public MapFootprintPlaybackSnapshot State { get; set; }

        public MapFootprintPlaybackInteraction()
        {
            HitTarget = MapFootprintPlaybackHitTargets.Outside;
            State = new MapFootprintPlaybackSnapshot();
        }
    }
}
