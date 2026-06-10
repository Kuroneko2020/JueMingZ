using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Movement;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        public static MovementSafeLandingEquipmentItemSignature CreateSignature(object item)
        {
            var signature = new MovementSafeLandingEquipmentItemSignature();
            if (item == null)
            {
                return signature;
            }

            int value;
            signature.Type = TryReadItemInt(item, "type", out value) ? value : 0;
            signature.Stack = TryReadItemInt(item, "stack", out value) ? value : 0;
            signature.Prefix = TryReadItemInt(item, "prefix", out value) ? value : 0;
            var rawName = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            signature.Name = rawName == null ? string.Empty : rawName.ToString();
            return signature;
        }

        private static object CreateAirLike(object item)
        {
            Type itemType = item == null ? null : item.GetType();
            if (itemType == null)
            {
                itemType = FindType("Terraria.Item");
            }

            if (itemType == null)
            {
                return null;
            }

            try
            {
                var empty = Activator.CreateInstance(itemType);
                TryTurnToAir(empty);
                return empty;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.CreateAirLike", error);
                return null;
            }
        }

        private static bool TryTurnToAir(object item)
        {
            if (item == null)
            {
                return false;
            }

            var methods = item.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TurnToAir", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                    {
                        method.Invoke(item, new object[] { false });
                        return true;
                    }

                    if (parameters.Length == 0)
                    {
                        method.Invoke(item, new object[0]);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = instance.GetType();
            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property;
            return TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property != null && property.CanRead
                ? property.GetValue(instance, null)
                : null;
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
                if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field != null)
                {
                    object converted;
                    if (!TryConvertMemberValue(value, field.FieldType, out converted))
                    {
                        return false;
                    }

                    field.SetValue(instance, converted);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property != null && property.CanWrite)
                {
                    object converted;
                    if (!TryConvertMemberValue(value, property.PropertyType, out converted))
                    {
                        return false;
                    }

                    property.SetValue(instance, converted, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.TrySetMember:" + name, error);
            }

            return false;
        }

        private static bool TryConvertMemberValue(object value, Type targetType, out object converted)
        {
            converted = value;
            if (targetType == null)
            {
                return false;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (value == null)
            {
                return !targetType.IsValueType || nullableType != null;
            }

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                converted = value;
                return true;
            }

            try
            {
                if (targetType.IsEnum)
                {
                    converted = Enum.ToObject(targetType, value);
                    return true;
                }

                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.TryConvertMemberValue:" + targetType.FullName, error);
                converted = value;
                return false;
            }
        }

        private static object GetStatic(Type type, string name)
        {
            object value;
            return TryGetStaticMember(type, name, out value) ? value : null;
        }

        private static bool TryGetStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field != null)
                {
                    value = field.GetValue(null);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property != null && property.CanRead)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadItemInt(object item, string name, out int value)
        {
            value = 0;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemSByte(object item, string name, out sbyte value)
        {
            value = 0;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSByte(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemBool(object item, string name, out bool value)
        {
            value = false;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemIntByNames(object item, out int value, params string[] names)
        {
            value = 0;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryReadItemInt(item, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static int TryReadIntOrDefault(object instance, string name, int fallback)
        {
            int value;
            return TryReadInt(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadIntByNames(object instance, out int value, params string[] names)
        {
            value = 0;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryReadInt(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadBoolByNames(object instance, out bool value, params string[] names)
        {
            value = false;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var raw = GetMember(instance, names[index]);
                if (raw == null)
                {
                    continue;
                }

                try
                {
                    value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadFloatByNames(object instance, out float value, params string[] names)
        {
            value = 0f;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var raw = GetMember(instance, names[index]);
                if (raw == null)
                {
                    continue;
                }

                try
                {
                    value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            if (!TryGetStaticMember(type, name, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadVectorMember(object instance, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var vector = GetMember(instance, name);
            if (vector == null)
            {
                return false;
            }

            return TryReadFloatByNames(vector, out x, "X", "x") &&
                   TryReadFloatByNames(vector, out y, "Y", "y");
        }

        private static bool SetIndexed(object source, int index, object value)
        {
            if (source == null || index < 0 || value == null)
            {
                return false;
            }

            try
            {
                var list = source as IList;
                if (list != null)
                {
                    if (index >= list.Count)
                    {
                        return false;
                    }

                    list[index] = value;
                    return true;
                }

                var array = source as Array;
                if (array != null && array.Rank == 1 && index < array.GetLength(0))
                {
                    array.SetValue(value, index);
                    return true;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.SetIndexed", error);
            }

            return false;
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                return type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    parameterTypes ?? Type.EmptyTypes,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

    }
}
