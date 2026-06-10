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
        public static bool TryReadTextInputFocus(out bool focused, out string reason)
        {
            focused = false;
            reason = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                focused = true;
                reason = "mainTypeUnavailable";
                return false;
            }

            bool boolValue;
            if ((TryGetStaticBool(mainType, "chatMode", out boolValue) && boolValue) ||
                (TryGetStaticBool(mainType, "drawingPlayerChat", out boolValue) && boolValue))
            {
                focused = true;
                reason = "chat";
                return true;
            }

            var playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (TryGetStaticBool(playerInputType, "WritingText", out boolValue) && boolValue)
            {
                focused = true;
                reason = "playerInputWritingText";
                return true;
            }

            if (GetStatic(mainType, "CurrentInputTextTakerOverride") != null)
            {
                focused = true;
                reason = "currentInputTextTakerOverride";
                return true;
            }

            if (IsStaticTextEditActive(mainType, "editSign"))
            {
                focused = true;
                reason = "editSign";
                return true;
            }

            if (IsStaticTextEditActive(mainType, "editChest"))
            {
                focused = true;
                reason = "editChest";
                return true;
            }

            return true;
        }

        private static bool IsStaticTextEditActive(Type type, string name)
        {
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                return (bool)raw;
            }

            try
            {
                return Convert.ToInt32(raw) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
