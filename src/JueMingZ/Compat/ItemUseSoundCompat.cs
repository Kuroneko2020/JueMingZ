using System;
using System.Reflection;

namespace JueMingZ.Compat
{
    public static class ItemUseSoundCompat
    {
        // Use sounds are best-effort feedback after item use; missing SoundEngine
        // reflection must not block or imply item-use success.
        private static MethodInfo _playSoundMethod;
        private static ConstructorInfo _vector2Constructor;

        public static bool TryPlayUseSound(object player, object item, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (player == null || item == null)
            {
                message = "Player or item unavailable for use sound.";
                return false;
            }

            var useSound = GetMember(item, "UseSound");
            if (useSound == null)
            {
                message = "Item.UseSound is empty.";
                return false;
            }

            if (!EnsureResolved(useSound.GetType()))
            {
                message = "SoundEngine.PlaySound(LegacySoundStyle, Vector2, float, float) is unavailable.";
                return false;
            }

            var position = GetMember(player, "Center") ?? CreateVector2(ReadCenterX(player), ReadCenterY(player));
            if (position == null)
            {
                message = "Cannot create player center position for use sound.";
                return false;
            }

            try
            {
                _playSoundMethod.Invoke(null, new[] { useSound, position, (object)0f, (object)1f });
                invoked = true;
                message = "Item.UseSound played through SoundEngine.";
                return true;
            }
            catch (Exception error)
            {
                message = "Item use sound failed: " + Unwrap(error);
                return false;
            }
        }

        private static bool EnsureResolved(Type soundStyleType)
        {
            if (_playSoundMethod != null && _vector2Constructor != null)
            {
                return true;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                return false;
            }

            var vector2Type = TerrariaRuntimeTypes.Vector2Type;
            var soundEngineType = FindType("Terraria.Audio.SoundEngine");
            if (vector2Type == null || soundEngineType == null || soundStyleType == null)
            {
                return false;
            }

            _vector2Constructor = vector2Type.GetConstructor(new[] { typeof(float), typeof(float) });
            var methods = soundEngineType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "PlaySound", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 4 &&
                    parameters[0].ParameterType.IsAssignableFrom(soundStyleType) &&
                    parameters[1].ParameterType.FullName == vector2Type.FullName &&
                    parameters[2].ParameterType == typeof(float) &&
                    parameters[3].ParameterType == typeof(float))
                {
                    _playSoundMethod = method;
                    break;
                }
            }

            return _playSoundMethod != null && _vector2Constructor != null;
        }

        private static object CreateVector2(float x, float y)
        {
            return _vector2Constructor == null ? null : _vector2Constructor.Invoke(new object[] { x, y });
        }

        private static float ReadCenterX(object instance)
        {
            return ReadFloat(instance, "position", "X") + ReadInt(instance, "width", 0) / 2f;
        }

        private static float ReadCenterY(object instance)
        {
            return ReadFloat(instance, "position", "Y") + ReadInt(instance, "height", 0) / 2f;
        }

        private static float ReadFloat(object instance, string vectorName, string componentName)
        {
            var vector = GetMember(instance, vectorName);
            var raw = GetMember(vector, componentName);
            try { return raw == null ? 0f : Convert.ToSingle(raw); }
            catch { return 0f; }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw); }
            catch { return fallback; }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
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

        private static string Unwrap(Exception error)
        {
            return error == null ? string.Empty : (error.InnerException == null ? error.Message : error.InnerException.Message);
        }
    }
}
