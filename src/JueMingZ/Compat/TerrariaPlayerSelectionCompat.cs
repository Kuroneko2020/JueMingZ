using System;
using System.IO;
using System.Reflection;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TerrariaPlayerSelectionCompat
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly object SyncRoot = new object();

        private static Type _playerType;
        private static bool _resolved;
        private static bool _diagnosticsWritten;
        private static string _lastError = string.Empty;

        private static MethodInfo _selectedItemGetterMethod;
        private static FieldInfo _selectedItemGetterField;
        private static PropertyInfo _selectedItemGetterProperty;
        private static FieldInfo _selectedItemWritableField;
        private static PropertyInfo _selectedItemWritableProperty;

        private static MethodInfo _changeItemMethod;
        private static FieldInfo _changeItemField;
        private static FieldInfo _selectedItemStateField;
        private static PropertyInfo _selectedItemStateProperty;
        private static MethodInfo _selectedItemStateSelectMethod;
        private static FieldInfo _selectedItemStateSelectedField;
        private static FieldInfo _selectedItemStateHotbarField;
        private static FieldInfo _selectedItemStateBufferedField;
        private static FieldInfo _selectedItemStateOverriddenField;

        public static bool SelectedItemGetterReady { get; private set; }
        public static bool SelectedItemSelectorReady { get; private set; }
        public static bool SelectedItemAccessorReady { get { return SelectedItemGetterReady && SelectedItemSelectorReady; } }
        public static string LastError { get { return _lastError; } }
        public static string LastSelectionMethod { get; private set; } = string.Empty;

        public static bool TryGetSelectedItem(object player, out int selectedItem)
        {
            selectedItem = 0;
            if (!EnsureResolved(player))
            {
                return false;
            }

            if (!SelectedItemGetterReady)
            {
                WriteDiagnosticsOnce(_playerType, "selectedItem int getter unavailable.");
                return Fail("selected item getter unavailable.");
            }

            try
            {
                object raw = null;
                if (_selectedItemGetterMethod != null)
                {
                    raw = _selectedItemGetterMethod.Invoke(player, null);
                }
                else if (_selectedItemGetterField != null)
                {
                    raw = _selectedItemGetterField.GetValue(player);
                }
                else if (_selectedItemGetterProperty != null)
                {
                    raw = _selectedItemGetterProperty.GetValue(player, null);
                }

                if (raw == null)
                {
                    return Fail("selected item getter returned null.");
                }

                selectedItem = Convert.ToInt32(raw);
                _lastError = string.Empty;
                return true;
            }
            catch (Exception error)
            {
                return Fail("selected item getter failed: " + error.Message);
            }
        }

        public static bool TrySelectInventorySlot(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            if (!EnsureResolved(player))
            {
                return false;
            }

            if (_changeItemMethod != null && TryInvokeChangeItem(player, slot) && TryVerifySelectedSlot(player, slot))
            {
                _lastError = string.Empty;
                LastSelectionMethod = "Player.changeItem(int)";
                return true;
            }

            if (TrySelectBySelectedItemState(player, slot) && TryVerifySelectedSlot(player, slot))
            {
                _lastError = string.Empty;
                LastSelectionMethod = "selectedItemState.Select(int)";
                return true;
            }

            if (TryWriteSelectedItemInt(player, slot) && TryVerifySelectedSlot(player, slot))
            {
                _lastError = string.Empty;
                LastSelectionMethod = "selectedItem int write fallback";
                return true;
            }

            WriteDiagnosticsOnce(_playerType, "Selected item changer unavailable or failed verification.");
            return string.IsNullOrWhiteSpace(_lastError)
                ? Fail("No selected item changer is available.")
                : false;
        }

        public static bool TryRequestInventorySlotSelection(object player, int slot, out bool selectedImmediately)
        {
            selectedImmediately = false;
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            if (!EnsureResolved(player))
            {
                return false;
            }

            int selectedSlot;
            if (TryGetSelectedItem(player, out selectedSlot) && selectedSlot == slot)
            {
                selectedImmediately = true;
                LastSelectionMethod = "already selected";
                _lastError = string.Empty;
                return true;
            }

            if (_changeItemMethod != null && TryInvokeChangeItem(player, slot) &&
                VerifyRequestedSelection(player, slot, "Player.changeItem(int)", true, out selectedImmediately))
            {
                return true;
            }

            if (_changeItemField != null && TrySetChangeItemField(player, slot) &&
                VerifyRequestedSelection(player, slot, "Player.changeItem field", true, out selectedImmediately))
            {
                return true;
            }

            if (TrySelectBySelectedItemState(player, slot) &&
                VerifyRequestedSelection(player, slot, "selectedItemState.Select(int)", true, out selectedImmediately))
            {
                return true;
            }

            if (TryWriteSelectedItemInt(player, slot) &&
                VerifyRequestedSelection(player, slot, "selectedItem int write fallback", false, out selectedImmediately))
            {
                return true;
            }

            WriteDiagnosticsOnce(_playerType, "Selected item changer unavailable or failed verification.");
            return string.IsNullOrWhiteSpace(_lastError)
                ? Fail("No selected item changer is available.")
                : false;
        }

        public static bool TryForceInventorySlotSelection(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            if (!EnsureResolved(player))
            {
                return false;
            }

            int selectedSlot;
            if (TryGetSelectedItem(player, out selectedSlot) && selectedSlot == slot)
            {
                LastSelectionMethod = "already selected";
                _lastError = string.Empty;
                return true;
            }

            if (TryWriteSelectedItemInt(player, slot) && TryVerifySelectedSlot(player, slot))
            {
                LastSelectionMethod = "selectedItem int write immediate";
                _lastError = string.Empty;
                return true;
            }

            if (TryForceSelectedItemStateSelection(player, slot))
            {
                LastSelectionMethod = "selectedItemState direct hotbar selection";
                _lastError = string.Empty;
                return true;
            }

            return string.IsNullOrWhiteSpace(_lastError)
                ? Fail("Writable selectedItem immediate selection is not available.")
                : false;
        }

        private static bool TryVerifySelectedSlot(object player, int expectedSlot)
        {
            int selectedSlot;
            if (!TryGetSelectedItem(player, out selectedSlot))
            {
                return false;
            }

            return selectedSlot == expectedSlot || Fail(
                "Selected slot verification failed. selectedSlot=" +
                selectedSlot +
                ", expectedSlot=" +
                expectedSlot +
                ".");
        }

        private static bool VerifyRequestedSelection(
            object player,
            int expectedSlot,
            string method,
            bool allowDeferred,
            out bool selectedImmediately)
        {
            selectedImmediately = false;
            int selectedSlot;
            if (!TryGetSelectedItem(player, out selectedSlot))
            {
                return false;
            }

            if (selectedSlot == expectedSlot)
            {
                selectedImmediately = true;
                LastSelectionMethod = method;
                _lastError = string.Empty;
                return true;
            }

            if (allowDeferred)
            {
                LastSelectionMethod = method + " (pending)";
                _lastError = string.Empty;
                return true;
            }

            return Fail(
                "Selected slot verification failed. selectedSlot=" +
                selectedSlot +
                ", expectedSlot=" +
                expectedSlot +
                ".");
        }

        private static bool IsSupportedItemUseSlot(int slot)
        {
            return (slot >= 0 && slot < 50) || slot == 58;
        }

        private static bool EnsureResolved(object player)
        {
            if (player == null)
            {
                return Fail("Local player unavailable.");
            }

            var type = player.GetType();
            lock (SyncRoot)
            {
                if (_resolved && _playerType == type)
                {
                    return true;
                }

                _playerType = type;
                _resolved = true;
                _lastError = string.Empty;
                ResolveGetter(type);
                ResolveSelector(type);

                if (!SelectedItemGetterReady || !SelectedItemSelectorReady)
                {
                    WriteDiagnosticsOnce(type, "selected item getter/selector incomplete.");
                }

                return true;
            }
        }

        private static void ResolveGetter(Type type)
        {
            SelectedItemGetterReady = false;
            _selectedItemGetterMethod = null;
            _selectedItemGetterField = null;
            _selectedItemGetterProperty = null;

            var getter = type.GetMethod("get_selectedItem", InstanceFlags, null, Type.EmptyTypes, null);
            if (getter != null && IsIntLike(getter.ReturnType))
            {
                _selectedItemGetterMethod = getter;
                SelectedItemGetterReady = true;
                return;
            }

            var field = type.GetField("selectedItem", InstanceFlags) ?? FindFieldIgnoreCase(type, "selectedItem");
            if (field != null && IsIntLike(field.FieldType))
            {
                _selectedItemGetterField = field;
                SelectedItemGetterReady = true;
                return;
            }

            var property = FindSelectedItemIntProperty(type, requireWritable: false);
            if (property != null)
            {
                _selectedItemGetterProperty = property;
                SelectedItemGetterReady = true;
            }
        }

        private static void ResolveSelector(Type type)
        {
            SelectedItemSelectorReady = false;
            _changeItemMethod = FindChangeItemMethod(type);
            _changeItemField = FindChangeItemField(type);
            _selectedItemStateField = type.GetField("selectedItemState", InstanceFlags);
            _selectedItemStateProperty = _selectedItemStateField == null
                ? type.GetProperty("selectedItemState", InstanceFlags)
                : null;
            var selectedItemStateType = GetSelectedItemStateType();
            _selectedItemStateSelectMethod = FindSelectedItemStateSelectMethod(selectedItemStateType);
            _selectedItemStateSelectedField = FindSelectedItemStateIntField(selectedItemStateType, "selected");
            _selectedItemStateHotbarField = FindSelectedItemStateIntField(selectedItemStateType, "hotbar");
            _selectedItemStateBufferedField = FindSelectedItemStateIntField(selectedItemStateType, "buffered");
            _selectedItemStateOverriddenField = FindSelectedItemStateIntField(selectedItemStateType, "overridden");

            _selectedItemWritableField = null;
            _selectedItemWritableProperty = null;
            var field = type.GetField("selectedItem", InstanceFlags) ?? FindFieldIgnoreCase(type, "selectedItem");
            if (field != null && IsIntLike(field.FieldType) && !field.IsInitOnly)
            {
                _selectedItemWritableField = field;
            }

            if (_selectedItemWritableField == null)
            {
                _selectedItemWritableProperty = FindSelectedItemIntProperty(type, requireWritable: true);
            }

            SelectedItemSelectorReady = _changeItemMethod != null ||
                                        _changeItemField != null ||
                                        _selectedItemStateSelectMethod != null ||
                                        _selectedItemWritableField != null ||
                                        _selectedItemWritableProperty != null;
        }

        private static MethodInfo FindChangeItemMethod(Type type)
        {
            var method = type.GetMethod("changeItem", InstanceFlags, null, new[] { typeof(int) }, null);
            if (method != null)
            {
                return method;
            }

            var methods = type.GetMethods(InstanceFlags);
            for (var index = 0; index < methods.Length; index++)
            {
                method = methods[index];
                if (!string.Equals(method.Name, "changeItem", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static FieldInfo FindChangeItemField(Type type)
        {
            var field = type.GetField("changeItem", InstanceFlags) ?? FindFieldIgnoreCase(type, "changeItem");
            return field != null && IsIntLike(field.FieldType) && !field.IsInitOnly ? field : null;
        }

        private static Type GetSelectedItemStateType()
        {
            if (_selectedItemStateField != null)
            {
                return _selectedItemStateField.FieldType;
            }

            return _selectedItemStateProperty != null && _selectedItemStateProperty.GetIndexParameters().Length == 0
                ? _selectedItemStateProperty.PropertyType
                : null;
        }

        private static MethodInfo FindSelectedItemStateSelectMethod(Type stateType)
        {
            if (stateType == null)
            {
                return null;
            }

            return stateType.GetMethod("Select", InstanceFlags, null, new[] { typeof(int) }, null);
        }

        private static FieldInfo FindSelectedItemStateIntField(Type stateType, string name)
        {
            if (stateType == null)
            {
                return null;
            }

            var field = stateType.GetField(name, InstanceFlags) ?? FindFieldIgnoreCase(stateType, name);
            return field != null && IsIntLike(field.FieldType) && !field.IsInitOnly ? field : null;
        }

        private static bool TryInvokeChangeItem(object player, int slot)
        {
            try
            {
                _changeItemMethod.Invoke(player, new object[] { slot });
                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "change-item-invoke-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "Player.changeItem(int) failed; fallback will be tried: " + error.Message);
                return false;
            }
        }

        private static bool TrySetChangeItemField(object player, int slot)
        {
            try
            {
                _changeItemField.SetValue(player, slot);
                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "change-item-field-set-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "Player.changeItem field write failed; fallback will be tried: " + error.Message);
                return false;
            }
        }

        private static bool TrySelectBySelectedItemState(object player, int slot)
        {
            if (_selectedItemStateField == null && _selectedItemStateProperty == null)
            {
                return false;
            }

            try
            {
                var state = _selectedItemStateField != null
                    ? _selectedItemStateField.GetValue(player)
                    : _selectedItemStateProperty.GetValue(player, null);
                if (state == null)
                {
                    return false;
                }

                var stateType = state.GetType();
                if (_selectedItemStateSelectMethod == null ||
                    _selectedItemStateSelectMethod.DeclaringType != stateType)
                {
                    _selectedItemStateSelectMethod = FindSelectedItemStateSelectMethod(stateType);
                }

                if (_selectedItemStateSelectMethod == null)
                {
                    return false;
                }

                _selectedItemStateSelectMethod.Invoke(state, new object[] { slot });

                if (stateType.IsValueType)
                {
                    if (_selectedItemStateField != null)
                    {
                        _selectedItemStateField.SetValue(player, state);
                    }
                    else if (_selectedItemStateProperty != null && _selectedItemStateProperty.CanWrite)
                    {
                        _selectedItemStateProperty.SetValue(player, state, null);
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "selected-item-state-select-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "selectedItemState.Select(int) failed; fallback will be tried: " + error.Message);
                return false;
            }
        }

        private static bool TryForceSelectedItemStateSelection(object player, int slot)
        {
            if (slot < 0 || slot > 9)
            {
                return Fail("SelectedItemState immediate selection fallback only supports hotbar slots.");
            }

            if ((_selectedItemStateField == null && _selectedItemStateProperty == null) ||
                _selectedItemStateSelectedField == null)
            {
                return false;
            }

            try
            {
                var state = _selectedItemStateField != null
                    ? _selectedItemStateField.GetValue(player)
                    : _selectedItemStateProperty.GetValue(player, null);
                if (state == null)
                {
                    return false;
                }

                _selectedItemStateSelectedField.SetValue(state, slot);
                if (_selectedItemStateHotbarField != null)
                {
                    _selectedItemStateHotbarField.SetValue(state, slot);
                }

                if (_selectedItemStateBufferedField != null)
                {
                    _selectedItemStateBufferedField.SetValue(state, -1);
                }

                if (_selectedItemStateOverriddenField != null)
                {
                    _selectedItemStateOverriddenField.SetValue(state, -1);
                }

                var stateType = state.GetType();
                if (stateType.IsValueType)
                {
                    if (_selectedItemStateField != null)
                    {
                        _selectedItemStateField.SetValue(player, state);
                    }
                    else if (_selectedItemStateProperty != null && _selectedItemStateProperty.CanWrite)
                    {
                        _selectedItemStateProperty.SetValue(player, state, null);
                    }
                    else
                    {
                        return Fail("SelectedItemState immediate selection fallback cannot write value-type state back.");
                    }
                }

                return TryVerifySelectedSlot(player, slot);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "selected-item-state-force-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "SelectedItemState immediate selection fallback failed: " + error.Message);
                return false;
            }
        }

        private static bool TryWriteSelectedItemInt(object player, int slot)
        {
            try
            {
                if (_selectedItemWritableField != null)
                {
                    // Controlled input write: selectedItem = target item-use slot.
                    _selectedItemWritableField.SetValue(player, slot);
                    return true;
                }

                if (_selectedItemWritableProperty != null)
                {
                    // Controlled input write: selectedItem = target item-use slot.
                    _selectedItemWritableProperty.SetValue(player, slot, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "selected-item-write-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "Writable selectedItem fallback failed: " + error.Message);
            }

            return false;
        }

        private static FieldInfo FindFieldIgnoreCase(Type type, string name)
        {
            var fields = type.GetFields(InstanceFlags);
            for (var index = 0; index < fields.Length; index++)
            {
                if (string.Equals(fields[index].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return fields[index];
                }
            }

            return null;
        }

        private static PropertyInfo FindSelectedItemIntProperty(Type type, bool requireWritable)
        {
            var properties = type.GetProperties(InstanceFlags);
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (!string.Equals(property.Name, "selectedItem", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.GetIndexParameters().Length != 0 || !property.CanRead || !IsIntLike(property.PropertyType))
                {
                    continue;
                }

                if (requireWritable && !property.CanWrite)
                {
                    continue;
                }

                return property;
            }

            return null;
        }

        private static bool IsIntLike(Type type)
        {
            if (type == typeof(int))
            {
                return true;
            }

            if (type == null || type == typeof(string) || !typeof(IConvertible).IsAssignableFrom(type))
            {
                return false;
            }

            var code = Type.GetTypeCode(type);
            return code == TypeCode.Byte ||
                   code == TypeCode.SByte ||
                   code == TypeCode.Int16 ||
                   code == TypeCode.UInt16 ||
                   code == TypeCode.Int32 ||
                   code == TypeCode.UInt32 ||
                   code == TypeCode.Int64 ||
                   code == TypeCode.UInt64;
        }

        private static bool IsCandidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.IndexOf("selected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("hotbar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Fail(string message)
        {
            _lastError = message ?? string.Empty;
            return false;
        }

        private static void WriteDiagnosticsOnce(Type playerType, string reason)
        {
            if (_diagnosticsWritten || playerType == null)
            {
                return;
            }

            _diagnosticsWritten = true;
            try
            {
                Directory.CreateDirectory(DiagnosticSnapshotWriter.DiagnosticsDirectory);
                var path = Path.Combine(DiagnosticSnapshotWriter.DiagnosticsDirectory, "input-compat-diagnostics.txt");
                var builder = new StringBuilder();
                builder.AppendLine("Input compat selected item diagnostics");
                builder.AppendLine("Reason: " + (reason ?? string.Empty));
                builder.AppendLine("PlayerTypeName: " + (playerType.FullName ?? playerType.Name));
                builder.AppendLine("GetterReady: " + SelectedItemGetterReady);
                builder.AppendLine("SelectorReady: " + SelectedItemSelectorReady);
                builder.AppendLine("LastInputCompatError: " + LastError);
                builder.AppendLine("Note: SelectedItem returning Terraria.Item is ignored because it is not an int slot index.");
                builder.AppendLine();

                builder.AppendLine("Found changeItem methods:");
                var methods = playerType.GetMethods(InstanceFlags);
                for (var index = 0; index < methods.Length; index++)
                {
                    if (string.Equals(methods[index].Name, "changeItem", StringComparison.Ordinal))
                    {
                        builder.AppendLine("- " + FormatMethod(methods[index]));
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Found selectedItemState fields/properties:");
                var stateField = playerType.GetField("selectedItemState", InstanceFlags);
                if (stateField != null)
                {
                    builder.AppendLine("- field " + stateField.FieldType.FullName + " " + stateField.Name +
                                       ", HasSelectInt=" + (FindSelectedItemStateSelectMethod(stateField.FieldType) != null));
                }

                var stateProperty = playerType.GetProperty("selectedItemState", InstanceFlags);
                if (stateProperty != null)
                {
                    builder.AppendLine("- property " + stateProperty.PropertyType.FullName + " " + stateProperty.Name +
                                       ", CanRead=" + stateProperty.CanRead +
                                       ", CanWrite=" + stateProperty.CanWrite +
                                       ", HasSelectInt=" + (FindSelectedItemStateSelectMethod(stateProperty.PropertyType) != null));
                }

                builder.AppendLine();
                builder.AppendLine("Candidate selected/item/hotbar fields:");
                var fields = playerType.GetFields(InstanceFlags);
                for (var index = 0; index < fields.Length; index++)
                {
                    if (IsCandidateName(fields[index].Name))
                    {
                        builder.AppendLine("- " + fields[index].FieldType.FullName + " " + fields[index].Name +
                                           ", IsInitOnly=" + fields[index].IsInitOnly);
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Candidate selected/item/hotbar properties:");
                var properties = playerType.GetProperties(InstanceFlags);
                for (var index = 0; index < properties.Length; index++)
                {
                    if (IsCandidateName(properties[index].Name))
                    {
                        builder.AppendLine("- " + properties[index].PropertyType.FullName + " " + properties[index].Name +
                                           ", CanRead=" + properties[index].CanRead +
                                           ", CanWrite=" + properties[index].CanWrite);
                    }
                }

                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                Logger.Warn("TerrariaPlayerSelectionCompat", "selected item diagnostics written: " + path);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "selected-item-diagnostics-write-failed",
                    TimeSpan.FromSeconds(30),
                    "TerrariaPlayerSelectionCompat",
                    "selected item diagnostics write failed: " + error.Message);
            }
        }

        private static string FormatMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append(method.ReturnType.FullName).Append(" ").Append(method.Name).Append("(");
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[index].ParameterType.FullName).Append(" ").Append(parameters[index].Name);
            }

            builder.Append(")");
            return builder.ToString();
        }
    }
}
