using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainUiState
    {
        public static void SetScrollOffset(int offset, int max)
        {
            lock (SyncRoot)
            {
                _maxScroll = Math.Max(0, max);
                _scrollOffset = Clamp(offset, 0, _maxScroll);
            }
        }

        public static int ScrollBy(int delta, int max)
        {
            lock (SyncRoot)
            {
                _maxScroll = Math.Max(0, max);
                _scrollOffset = Clamp(_scrollOffset + delta, 0, _maxScroll);
                return _scrollOffset;
            }
        }

        public static int ScrollByKnownMax(int delta)
        {
            lock (SyncRoot)
            {
                _scrollOffset = Clamp(_scrollOffset + delta, 0, _maxScroll);
                return _scrollOffset;
            }
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
