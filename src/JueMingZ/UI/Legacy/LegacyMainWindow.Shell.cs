using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static void DrawFrame(object spriteBatch, LegacyUiRect window, LegacyUiRect titleRect, LegacyUiRect resizeRect)
        {
            LegacyUiTheme.DrawPanel(spriteBatch, window);
            LegacyUiTheme.DrawTitleBar(spriteBatch, titleRect);
            UiTextRenderer.DrawText(spriteBatch, "决明Z", window.X + 14, window.Y + 8, 244, 242, 228, 255, 1f);
            if (LegacyUiMetrics.AllowResize)
            {
                LegacyUiTheme.DrawResizeGrip(spriteBatch, resizeRect);
            }
        }

        private static void DrawTabs(LegacyUiContext context)
        {
            new LegacyTabBarControl
            {
                Bounds = context.WindowRect,
                SelectedPageId = context.SelectedPageId
            }.Draw(context);
        }
    }
}
