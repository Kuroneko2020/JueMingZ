using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace JueMingZ.UI
{
    public static class UiEmbeddedTextureLoader
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, object> TextureCache = new Dictionary<string, object>(StringComparer.Ordinal);

        private static object _graphicsDevice;
        private static Type _texture2DType;
        private static PropertyInfo _graphicsDeviceProperty;
        private static MethodInfo _fromStreamMethod;

        public static bool TryGetTexture(object spriteBatch, string resourceName, out object texture)
        {
            texture = null;
            if (spriteBatch == null || string.IsNullOrWhiteSpace(resourceName))
            {
                return false;
            }

            var graphicsDevice = ReadGraphicsDevice(spriteBatch);
            if (graphicsDevice == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!ReferenceEquals(_graphicsDevice, graphicsDevice))
                {
                    TextureCache.Clear();
                    _graphicsDevice = graphicsDevice;
                }

                if (!EnsureReflection(spriteBatch, graphicsDevice))
                {
                    return false;
                }

                var key = resourceName.Trim();
                if (TextureCache.TryGetValue(key, out texture))
                {
                    return texture != null;
                }

                texture = CreateTextureFromResource(graphicsDevice, key);
                TextureCache[key] = texture;
                return texture != null;
            }
        }

        public static void InvalidateCachedResources(string reason)
        {
            lock (SyncRoot)
            {
                TextureCache.Clear();
                _graphicsDevice = null;
            }
        }

        private static object CreateTextureFromResource(object graphicsDevice, string resourceName)
        {
            if (_fromStreamMethod == null)
            {
                return null;
            }

            try
            {
                var assembly = typeof(UiEmbeddedTextureLoader).Assembly;
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    return _fromStreamMethod.Invoke(null, new object[] { graphicsDevice, stream });
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool EnsureReflection(object spriteBatch, object graphicsDevice)
        {
            if (_texture2DType != null && _graphicsDeviceProperty != null && _fromStreamMethod != null)
            {
                return true;
            }

            _graphicsDeviceProperty = spriteBatch.GetType().GetProperty(
                "GraphicsDevice",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _texture2DType = FindType("Microsoft.Xna.Framework.Graphics.Texture2D");
            if (_graphicsDeviceProperty == null || _texture2DType == null)
            {
                return false;
            }

            _fromStreamMethod = FindFromStreamMethod(_texture2DType, graphicsDevice.GetType());
            return _fromStreamMethod != null;
        }

        private static object ReadGraphicsDevice(object spriteBatch)
        {
            try
            {
                var property = _graphicsDeviceProperty ?? spriteBatch.GetType().GetProperty(
                    "GraphicsDevice",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return property == null || !property.CanRead
                    ? null
                    : property.GetValue(spriteBatch, null);
            }
            catch
            {
                return null;
            }
        }

        private static MethodInfo FindFromStreamMethod(Type textureType, Type graphicsDeviceType)
        {
            var methods = textureType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "FromStream", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    IsGraphicsDeviceParameter(parameters[0].ParameterType, graphicsDeviceType) &&
                    parameters[1].ParameterType.IsAssignableFrom(typeof(Stream)))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool IsGraphicsDeviceParameter(Type parameterType, Type graphicsDeviceType)
        {
            return parameterType.IsAssignableFrom(graphicsDeviceType) ||
                   string.Equals(parameterType.FullName, "Microsoft.Xna.Framework.Graphics.GraphicsDevice", StringComparison.Ordinal);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
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
