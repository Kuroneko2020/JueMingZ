using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using JueMingZ.UI;

namespace JueMingZ.UI.Legacy.Controls
{
    internal static class LegacyVectorIconRenderer
    {
        private const int TextureSize = 18;
        private const int Oversample = 5;
        private const double DefaultStroke = 1.35d;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, object> TextureCache = new Dictionary<string, object>(StringComparer.Ordinal);

        private static object _graphicsDevice;
        private static Type _texture2DType;
        private static Type _colorType;
        private static ConstructorInfo _textureConstructor;
        private static MethodInfo _setDataMethod;
        private static PropertyInfo _graphicsDeviceProperty;
        private static bool _textureConstructorUsesSurfaceFormat;
        private static object _surfaceFormatColorValue;

        private delegate double SignedDistance(double x, double y);

        public static void Draw(object spriteBatch, string iconId, LegacyUiRect bounds, LegacyUiRect clip, bool selected, bool enabled)
        {
            if (spriteBatch == null || string.IsNullOrWhiteSpace(iconId) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            object texture;
            if (!TryGetTexture(spriteBatch, iconId, out texture))
            {
                return;
            }

            var alpha = enabled ? (selected ? 238 : 226) : 138;
            var r = selected ? LegacyUiTheme.SelectedTextR : 224;
            var g = selected ? LegacyUiTheme.SelectedTextG : 228;
            var b = selected ? LegacyUiTheme.SelectedTextB : 238;
            UiPrimitiveRenderer.DrawTextureRectClipped(
                spriteBatch,
                texture,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                r,
                g,
                b,
                alpha);
        }

        private static bool TryGetTexture(object spriteBatch, string iconId, out object texture)
        {
            texture = null;
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

                var key = NormalizeIconId(iconId);
                if (TextureCache.TryGetValue(key, out texture))
                {
                    return texture != null;
                }

                texture = CreateTexture(graphicsDevice, BuildMask(key));
                if (texture == null)
                {
                    return false;
                }

                TextureCache[key] = texture;
                return true;
            }
        }

        private static object CreateTexture(object graphicsDevice, double[] mask)
        {
            if (_textureConstructor == null || _setDataMethod == null || _colorType == null)
            {
                return null;
            }

            try
            {
                object texture;
                if (_textureConstructorUsesSurfaceFormat)
                {
                    texture = _textureConstructor.Invoke(new[] { graphicsDevice, TextureSize, TextureSize, false, _surfaceFormatColorValue });
                }
                else
                {
                    texture = _textureConstructor.Invoke(new[] { graphicsDevice, TextureSize, TextureSize });
                }

                var data = Array.CreateInstance(_colorType, TextureSize * TextureSize);
                for (var index = 0; index < data.Length; index++)
                {
                    var alpha = (int)Math.Round(Math.Max(0d, Math.Min(1d, mask[index])) * 255d);
                    data.SetValue(CreateColor(alpha, alpha, alpha, alpha), index);
                }

                _setDataMethod.Invoke(texture, new object[] { data });
                return texture;
            }
            catch
            {
                return null;
            }
        }

        private static bool EnsureReflection(object spriteBatch, object graphicsDevice)
        {
            if (_texture2DType != null &&
                _colorType != null &&
                _textureConstructor != null &&
                _setDataMethod != null &&
                _graphicsDeviceProperty != null)
            {
                return true;
            }

            _graphicsDeviceProperty = spriteBatch.GetType().GetProperty(
                "GraphicsDevice",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _texture2DType = FindType("Microsoft.Xna.Framework.Graphics.Texture2D");
            _colorType = FindType("Microsoft.Xna.Framework.Color");
            if (_texture2DType == null || _colorType == null || _graphicsDeviceProperty == null)
            {
                return false;
            }

            _textureConstructor = FindTextureConstructor(_texture2DType, graphicsDevice.GetType(), out _textureConstructorUsesSurfaceFormat, out _surfaceFormatColorValue);
            _setDataMethod = FindSetDataMethod(_texture2DType, _colorType);
            return _textureConstructor != null && _setDataMethod != null;
        }

        private static object ReadGraphicsDevice(object spriteBatch)
        {
            if (spriteBatch == null)
            {
                return null;
            }

            try
            {
                var property = _graphicsDeviceProperty ?? spriteBatch.GetType().GetProperty(
                    "GraphicsDevice",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property == null || !property.CanRead)
                {
                    return null;
                }

                return property.GetValue(spriteBatch, null);
            }
            catch
            {
                return null;
            }
        }

        private static ConstructorInfo FindTextureConstructor(Type textureType, Type graphicsDeviceType, out bool usesSurfaceFormat, out object surfaceFormatColorValue)
        {
            usesSurfaceFormat = false;
            surfaceFormatColorValue = null;
            var constructors = textureType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (var index = 0; index < constructors.Length; index++)
            {
                var ctor = constructors[index];
                var parameters = ctor.GetParameters();
                if (parameters.Length == 3 &&
                    IsGraphicsDeviceParameter(parameters[0].ParameterType, graphicsDeviceType) &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(int))
                {
                    return ctor;
                }
            }

            for (var index = 0; index < constructors.Length; index++)
            {
                var ctor = constructors[index];
                var parameters = ctor.GetParameters();
                if (parameters.Length == 5 &&
                    IsGraphicsDeviceParameter(parameters[0].ParameterType, graphicsDeviceType) &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(int) &&
                    parameters[3].ParameterType == typeof(bool) &&
                    parameters[4].ParameterType.IsEnum)
                {
                    usesSurfaceFormat = true;
                    surfaceFormatColorValue = ResolveSurfaceFormatColor(parameters[4].ParameterType);
                    return ctor;
                }
            }

            return null;
        }

        private static bool IsGraphicsDeviceParameter(Type parameterType, Type graphicsDeviceType)
        {
            return parameterType.IsAssignableFrom(graphicsDeviceType) ||
                   string.Equals(parameterType.FullName, "Microsoft.Xna.Framework.Graphics.GraphicsDevice", StringComparison.Ordinal);
        }

        private static object ResolveSurfaceFormatColor(Type surfaceFormatType)
        {
            try
            {
                return Enum.Parse(surfaceFormatType, "Color");
            }
            catch
            {
                return Activator.CreateInstance(surfaceFormatType);
            }
        }

        private static MethodInfo FindSetDataMethod(Type textureType, Type colorType)
        {
            var methods = textureType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "SetData", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsArray)
                {
                    return method.MakeGenericMethod(colorType);
                }
            }

            return null;
        }

        private static ConstructorInfo FindColorConstructor(Type colorType)
        {
            var constructors = colorType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (var index = 0; index < constructors.Length; index++)
            {
                var ctor = constructors[index];
                var parameters = ctor.GetParameters();
                if (parameters.Length != 4)
                {
                    continue;
                }

                if (IsColorComponent(parameters[0].ParameterType) &&
                    IsColorComponent(parameters[1].ParameterType) &&
                    IsColorComponent(parameters[2].ParameterType) &&
                    IsColorComponent(parameters[3].ParameterType))
                {
                    return ctor;
                }
            }

            return null;
        }

        private static bool IsColorComponent(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(int) ||
                   type == typeof(float) ||
                   type == typeof(double);
        }

        private static object CreateColor(int r, int g, int b, int a)
        {
            return new Color(ClampColorComponent(r), ClampColorComponent(g), ClampColorComponent(b), ClampColorComponent(a));
        }

        private static int ClampColorComponent(int value)
        {
            return Math.Max(0, Math.Min(255, value));
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

        private static string NormalizeIconId(string iconId)
        {
            return string.IsNullOrWhiteSpace(iconId) ? string.Empty : iconId.Trim();
        }

        private static double[] BuildMask(string iconId)
        {
            var mask = new double[TextureSize * TextureSize];
            switch (iconId)
            {
                case "home":
                    DrawHome(mask);
                    break;
                case "item_bag":
                    DrawItemBag(mask);
                    break;
                case "grid":
                    DrawGrid(mask);
                    break;
                case "map_pin":
                    DrawMapPin(mask);
                    break;
                case "search":
                    DrawSearch(mask);
                    break;
                case "keyboard":
                    DrawKeyboard(mask);
                    break;
                case "blueprint":
                    DrawBlueprint(mask);
                    break;
                case "info":
                    DrawInfo(mask);
                    break;
                case "fish":
                    DrawFish(mask);
                    break;
                case "sword":
                    DrawSword(mask);
                    break;
                case "status_panel":
                    DrawStatusPanel(mask);
                    break;
                case "flask":
                    DrawFlask(mask);
                    break;
                case "movement":
                    DrawMovement(mask);
                    break;
                default:
                    DrawInfo(mask);
                    break;
            }

            return mask;
        }

        private static void DrawHome(double[] mask)
        {
            AddPolyline(mask, DefaultStroke, false, 3.3, 9.0, 9.0, 3.5, 14.7, 9.0);
            AddPolyline(mask, DefaultStroke, false, 5.0, 8.4, 5.0, 14.2, 13.0, 14.2, 13.0, 8.4);
            AddPolyline(mask, 1.1d, false, 7.6, 14.1, 7.6, 10.5, 10.4, 10.5, 10.4, 14.1);
        }

        private static void DrawItemBag(double[] mask)
        {
            AddRoundedRectOutline(mask, 4.2, 6.3, 9.6, 8.0, 1.35, 1.2d);
            AddPolyline(mask, 1.15d, false, 6.7, 6.5, 6.7, 5.4, 7.5, 4.5, 10.5, 4.5, 11.3, 5.4, 11.3, 6.5);
            AddSegment(mask, 5.6, 7.8, 12.4, 7.8, 1.05d);
            AddPolyline(mask, 1.05d, true, 7.1, 10.4, 8.9, 9.0, 10.7, 10.4, 8.9, 11.8);
            AddFilledCircle(mask, 11.8, 12.1, 0.55);
        }

        private static void DrawGrid(double[] mask)
        {
            AddRoundedRectOutline(mask, 3.8, 3.8, 3.8, 3.8, 0.6, 1.15d);
            AddRoundedRectOutline(mask, 10.4, 3.8, 3.8, 3.8, 0.6, 1.15d);
            AddRoundedRectOutline(mask, 3.8, 10.4, 3.8, 3.8, 0.6, 1.15d);
            AddRoundedRectOutline(mask, 10.4, 10.4, 3.8, 3.8, 0.6, 1.15d);
        }

        private static void DrawMapPin(double[] mask)
        {
            AddCircleOutline(mask, 9.0, 6.7, 3.4, 1.25d);
            AddFilledCircle(mask, 9.0, 6.7, 0.75);
            AddPolyline(mask, 1.25d, false, 6.8, 9.4, 9.0, 14.9, 11.2, 9.4);
        }

        private static void DrawSearch(double[] mask)
        {
            AddCircleOutline(mask, 7.5, 7.4, 4.1, 1.35d);
            AddSegment(mask, 10.6, 10.5, 14.4, 14.3, 1.45d);
        }

        private static void DrawKeyboard(double[] mask)
        {
            AddRoundedRectOutline(mask, 2.9, 5.2, 12.2, 7.7, 1.2, 1.2d);
            AddSegment(mask, 5.0, 7.5, 5.9, 7.5, 1.1d);
            AddSegment(mask, 8.0, 7.5, 8.9, 7.5, 1.1d);
            AddSegment(mask, 11.0, 7.5, 11.9, 7.5, 1.1d);
            AddSegment(mask, 5.0, 10.2, 13.0, 10.2, 1.05d);
        }

        private static void DrawBlueprint(double[] mask)
        {
            AddRoundedRectOutline(mask, 4.0, 3.6, 10.0, 10.8, 0.8, 1.15d);
            AddSegment(mask, 7.3, 3.9, 7.3, 14.1, 1.0d);
            AddSegment(mask, 10.7, 3.9, 10.7, 14.1, 1.0d);
            AddSegment(mask, 4.2, 7.2, 13.8, 7.2, 1.0d);
            AddSegment(mask, 4.2, 10.8, 13.8, 10.8, 1.0d);
        }

        private static void DrawInfo(double[] mask)
        {
            AddCircleOutline(mask, 9.0, 9.0, 6.0, 1.25d);
            AddFilledCircle(mask, 9.0, 5.8, 0.75);
            AddSegment(mask, 9.0, 8.1, 9.0, 12.4, 1.35d);
        }

        private static void DrawFish(double[] mask)
        {
            AddEllipseOutline(mask, 8.4, 9.1, 4.5, 3.1, 1.25d);
            AddPolyline(mask, 1.25d, false, 12.3, 9.1, 15.4, 6.7, 15.4, 11.5, 12.3, 9.1);
            AddFilledCircle(mask, 6.5, 8.3, 0.55);
            AddSegment(mask, 4.8, 9.1, 3.3, 7.8, 1.15d);
            AddSegment(mask, 4.8, 9.1, 3.3, 10.4, 1.15d);
        }

        private static void DrawSword(double[] mask)
        {
            AddSegment(mask, 5.2, 13.1, 13.6, 4.7, 1.35d);
            AddPolyline(mask, 1.2d, false, 12.2, 3.7, 14.4, 3.4, 14.1, 5.6);
            AddSegment(mask, 5.4, 10.1, 7.9, 12.6, 1.2d);
            AddSegment(mask, 4.3, 13.9, 5.8, 12.4, 1.5d);
        }

        private static void DrawStatusPanel(double[] mask)
        {
            AddFilledCircle(mask, 4.5, 5.4, 0.62);
            AddFilledCircle(mask, 4.5, 9.0, 0.62);
            AddFilledCircle(mask, 4.5, 12.6, 0.62);
            AddSegment(mask, 6.4, 5.4, 14.0, 5.4, 1.12d);
            AddSegment(mask, 6.4, 9.0, 14.0, 9.0, 1.12d);
            AddSegment(mask, 6.4, 12.6, 14.0, 12.6, 1.12d);
        }

        private static void DrawFlask(double[] mask)
        {
            AddSegment(mask, 7.2, 3.5, 10.8, 3.5, 1.15d);
            AddSegment(mask, 7.7, 3.9, 7.7, 7.5, 1.15d);
            AddSegment(mask, 10.3, 3.9, 10.3, 7.5, 1.15d);
            AddPolyline(mask, 1.25d, false, 7.7, 7.4, 4.5, 14.0, 13.5, 14.0, 10.3, 7.4);
            AddSegment(mask, 6.2, 11.7, 11.8, 11.7, 1.05d);
        }

        private static void DrawMovement(double[] mask)
        {
            AddSegment(mask, 3.7, 6.0, 13.4, 6.0, 1.25d);
            AddPolyline(mask, 1.25d, false, 10.9, 3.7, 13.5, 6.0, 10.9, 8.3);
            AddSegment(mask, 14.3, 12.0, 4.6, 12.0, 1.25d);
            AddPolyline(mask, 1.25d, false, 7.1, 9.7, 4.5, 12.0, 7.1, 14.3);
        }

        private static void AddRoundedRectOutline(double[] mask, double x, double y, double width, double height, double radius, double stroke)
        {
            var right = x + width;
            var bottom = y + height;
            AddSegment(mask, x + radius, y, right - radius, y, stroke);
            AddSegment(mask, x + radius, bottom, right - radius, bottom, stroke);
            AddSegment(mask, x, y + radius, x, bottom - radius, stroke);
            AddSegment(mask, right, y + radius, right, bottom - radius, stroke);
            AddArc(mask, x + radius, y + radius, radius, 180d, 270d, stroke);
            AddArc(mask, right - radius, y + radius, radius, 270d, 360d, stroke);
            AddArc(mask, right - radius, bottom - radius, radius, 0d, 90d, stroke);
            AddArc(mask, x + radius, bottom - radius, radius, 90d, 180d, stroke);
        }

        private static void AddCircleOutline(double[] mask, double cx, double cy, double radius, double stroke)
        {
            Apply(mask, delegate(double x, double y)
            {
                var distance = Math.Sqrt(Square(x - cx) + Square(y - cy));
                return Math.Abs(distance - radius) - stroke * 0.5d;
            });
        }

        private static void AddEllipseOutline(double[] mask, double cx, double cy, double rx, double ry, double stroke)
        {
            const int steps = 40;
            var previousX = cx + rx;
            var previousY = cy;
            for (var index = 1; index <= steps; index++)
            {
                var angle = Math.PI * 2d * index / steps;
                var x = cx + Math.Cos(angle) * rx;
                var y = cy + Math.Sin(angle) * ry;
                AddSegment(mask, previousX, previousY, x, y, stroke);
                previousX = x;
                previousY = y;
            }
        }

        private static void AddFilledCircle(double[] mask, double cx, double cy, double radius)
        {
            Apply(mask, delegate(double x, double y)
            {
                return Math.Sqrt(Square(x - cx) + Square(y - cy)) - radius;
            });
        }

        private static void AddArc(double[] mask, double cx, double cy, double radius, double fromDegrees, double toDegrees, double stroke)
        {
            const int steps = 8;
            var previousX = cx + Math.Cos(ToRadians(fromDegrees)) * radius;
            var previousY = cy + Math.Sin(ToRadians(fromDegrees)) * radius;
            for (var index = 1; index <= steps; index++)
            {
                var degrees = fromDegrees + (toDegrees - fromDegrees) * index / steps;
                var x = cx + Math.Cos(ToRadians(degrees)) * radius;
                var y = cy + Math.Sin(ToRadians(degrees)) * radius;
                AddSegment(mask, previousX, previousY, x, y, stroke);
                previousX = x;
                previousY = y;
            }
        }

        private static void AddPolyline(double[] mask, double stroke, bool closed, params double[] points)
        {
            if (points == null || points.Length < 4)
            {
                return;
            }

            for (var index = 0; index <= points.Length - 4; index += 2)
            {
                AddSegment(mask, points[index], points[index + 1], points[index + 2], points[index + 3], stroke);
            }

            if (closed)
            {
                AddSegment(mask, points[points.Length - 2], points[points.Length - 1], points[0], points[1], stroke);
            }
        }

        private static void AddSegment(double[] mask, double x1, double y1, double x2, double y2, double stroke)
        {
            Apply(mask, delegate(double x, double y)
            {
                return DistanceToSegment(x, y, x1, y1, x2, y2) - stroke * 0.5d;
            });
        }

        private static void Apply(double[] mask, SignedDistance distance)
        {
            var sampleCount = Oversample * Oversample;
            for (var py = 0; py < TextureSize; py++)
            {
                for (var px = 0; px < TextureSize; px++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < Oversample; sy++)
                    {
                        for (var sx = 0; sx < Oversample; sx++)
                        {
                            var sampleX = px + (sx + 0.5d) / Oversample;
                            var sampleY = py + (sy + 0.5d) / Oversample;
                            if (distance(sampleX, sampleY) <= 0d)
                            {
                                covered++;
                            }
                        }
                    }

                    if (covered <= 0)
                    {
                        continue;
                    }

                    var index = py * TextureSize + px;
                    var coverage = covered / (double)sampleCount;
                    if (coverage > mask[index])
                    {
                        mask[index] = coverage;
                    }
                }
            }
        }

        private static double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0.0001d)
            {
                return Math.Sqrt(Square(px - x1) + Square(py - y1));
            }

            var t = ((px - x1) * dx + (py - y1) * dy) / lengthSquared;
            t = Math.Max(0d, Math.Min(1d, t));
            var x = x1 + t * dx;
            var y = y1 + t * dy;
            return Math.Sqrt(Square(px - x) + Square(py - y));
        }

        private static double Square(double value)
        {
            return value * value;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180d;
        }
    }
}
