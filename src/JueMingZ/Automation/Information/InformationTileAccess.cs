using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace JueMingZ.Automation.Information
{
    internal static class InformationTileAccess
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, TileAccessor> TileAccessors = new Dictionary<Type, TileAccessor>();
        private static readonly Dictionary<Type, TileCollectionAccessor> TileCollectionAccessors = new Dictionary<Type, TileCollectionAccessor>();

        public static object GetTileAt(object tileCollection, int x, int y)
        {
            if (tileCollection == null || x < 0 || y < 0)
            {
                return null;
            }

            try
            {
                var accessor = GetTileCollectionAccessor(tileCollection.GetType());
                return accessor == null ? InformationReflection.GetTileAt(tileCollection, x, y) : accessor.GetTile(tileCollection, x, y);
            }
            catch
            {
                return InformationReflection.GetTileAt(tileCollection, x, y);
            }
        }

        public static bool IsActive(object tile)
        {
            bool active;
            return TryReadActive(tile, out active) && active;
        }

        public static int ReadType(object tile)
        {
            int type;
            return TryReadType(tile, out type) ? type : -1;
        }

        public static int ReadFrameX(object tile)
        {
            int value;
            return TryReadFrameX(tile, out value) ? value : 0;
        }

        public static int ReadFrameY(object tile)
        {
            int value;
            return TryReadFrameY(tile, out value) ? value : 0;
        }

        public static int ReadLiquidAmount(object tile)
        {
            int value;
            return TryReadLiquidAmount(tile, out value) ? Math.Max(0, value) : 0;
        }

        public static int ReadLiquidType(object tile)
        {
            int value;
            return TryReadLiquidType(tile, out value) ? value : 0;
        }

        public static bool TryReadActiveTypeAndFrame(object tile, out bool active, out int type, out int frameX, out int frameY)
        {
            active = false;
            type = -1;
            frameX = 0;
            frameY = 0;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            if (accessor == null)
            {
                return false;
            }

            var hasActive = accessor.TryReadActive(tile, out active);
            var hasType = accessor.TryReadType(tile, out type);
            accessor.TryReadFrameX(tile, out frameX);
            accessor.TryReadFrameY(tile, out frameY);
            return hasActive && hasType;
        }

        private static bool TryReadActive(object tile, out bool active)
        {
            active = false;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadActive(tile, out active);
        }

        private static bool TryReadType(object tile, out int type)
        {
            type = -1;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadType(tile, out type);
        }

        private static bool TryReadFrameX(object tile, out int frameX)
        {
            frameX = 0;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadFrameX(tile, out frameX);
        }

        private static bool TryReadFrameY(object tile, out int frameY)
        {
            frameY = 0;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadFrameY(tile, out frameY);
        }

        private static bool TryReadLiquidAmount(object tile, out int liquid)
        {
            liquid = 0;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadLiquidAmount(tile, out liquid);
        }

        private static bool TryReadLiquidType(object tile, out int liquidType)
        {
            liquidType = 0;
            if (tile == null)
            {
                return false;
            }

            var accessor = GetTileAccessor(tile.GetType());
            return accessor != null && accessor.TryReadLiquidType(tile, out liquidType);
        }

        private static TileAccessor GetTileAccessor(Type type)
        {
            if (type == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                TileAccessor cached;
                if (TileAccessors.TryGetValue(type, out cached))
                {
                    return cached;
                }

                var resolved = TileAccessor.Create(type);
                TileAccessors[type] = resolved;
                return resolved;
            }
        }

        private static TileCollectionAccessor GetTileCollectionAccessor(Type type)
        {
            if (type == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                TileCollectionAccessor cached;
                if (TileCollectionAccessors.TryGetValue(type, out cached))
                {
                    return cached;
                }

                var resolved = TileCollectionAccessor.Create(type);
                TileCollectionAccessors[type] = resolved;
                return resolved;
            }
        }

        private sealed class TileCollectionAccessor
        {
            private readonly Func<object, int, int, object> _getter;
            private readonly int _arrayRank;

            private TileCollectionAccessor(Func<object, int, int, object> getter, int arrayRank)
            {
                _getter = getter;
                _arrayRank = arrayRank;
            }

            public static TileCollectionAccessor Create(Type type)
            {
                if (type == null)
                {
                    return null;
                }

                if (type.IsArray)
                {
                    var rank = type.GetArrayRank();
                    if (rank == 2)
                    {
                        var getter = CompileTwoDimensionalArrayGetter(type);
                        return getter == null ? null : new TileCollectionAccessor(getter, rank);
                    }
                }

                var indexer = FindTwoIntIndexer(type);
                if (indexer != null)
                {
                    var getter = CompileIndexerGetter(type, indexer);
                    return getter == null ? null : new TileCollectionAccessor(getter, 0);
                }

                return null;
            }

            public object GetTile(object collection, int x, int y)
            {
                if (_getter == null || collection == null || x < 0 || y < 0)
                {
                    return null;
                }

                if (_arrayRank == 2)
                {
                    var array = collection as Array;
                    if (array == null ||
                        x >= array.GetLength(0) ||
                        y >= array.GetLength(1))
                    {
                        return null;
                    }
                }

                return _getter(collection, x, y);
            }

            private static PropertyInfo FindTwoIntIndexer(Type type)
            {
                try
                {
                    var properties = type.GetProperties(InstanceFlags);
                    for (var index = 0; index < properties.Length; index++)
                    {
                        var property = properties[index];
                        if (!property.CanRead)
                        {
                            continue;
                        }

                        var parameters = property.GetIndexParameters();
                        if (parameters.Length == 2 &&
                            parameters[0].ParameterType == typeof(int) &&
                            parameters[1].ParameterType == typeof(int))
                        {
                            return property;
                        }
                    }
                }
                catch
                {
                }

                return null;
            }

            private static Func<object, int, int, object> CompileTwoDimensionalArrayGetter(Type type)
            {
                try
                {
                    var collection = Expression.Parameter(typeof(object), "collection");
                    var x = Expression.Parameter(typeof(int), "x");
                    var y = Expression.Parameter(typeof(int), "y");
                    var body = Expression.ArrayIndex(Expression.Convert(collection, type), x, y);
                    var boxed = Expression.Convert(body, typeof(object));
                    return Expression.Lambda<Func<object, int, int, object>>(boxed, collection, x, y).Compile();
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, int, int, object> CompileIndexerGetter(Type type, PropertyInfo property)
            {
                try
                {
                    var collection = Expression.Parameter(typeof(object), "collection");
                    var x = Expression.Parameter(typeof(int), "x");
                    var y = Expression.Parameter(typeof(int), "y");
                    var body = Expression.Property(Expression.Convert(collection, type), property, x, y);
                    var boxed = Expression.Convert(body, typeof(object));
                    return Expression.Lambda<Func<object, int, int, object>>(boxed, collection, x, y).Compile();
                }
                catch
                {
                    return null;
                }
            }
        }

        private sealed class TileAccessor
        {
            private readonly MemberReader[] _activeReaders;
            private readonly MemberReader[] _typeReaders;
            private readonly MemberReader[] _frameXReaders;
            private readonly MemberReader[] _frameYReaders;
            private readonly MemberReader[] _liquidAmountReaders;
            private readonly MemberReader[] _liquidTypeReaders;

            private TileAccessor(MemberReader[] activeReaders, MemberReader[] typeReaders, MemberReader[] frameXReaders, MemberReader[] frameYReaders, MemberReader[] liquidAmountReaders, MemberReader[] liquidTypeReaders)
            {
                _activeReaders = activeReaders ?? new MemberReader[0];
                _typeReaders = typeReaders ?? new MemberReader[0];
                _frameXReaders = frameXReaders ?? new MemberReader[0];
                _frameYReaders = frameYReaders ?? new MemberReader[0];
                _liquidAmountReaders = liquidAmountReaders ?? new MemberReader[0];
                _liquidTypeReaders = liquidTypeReaders ?? new MemberReader[0];
            }

            public static TileAccessor Create(Type type)
            {
                if (type == null)
                {
                    return null;
                }

                return new TileAccessor(
                    BuildReaders(type, new[] { "HasTile", "IsActive", "active" }),
                    BuildReaders(type, new[] { "TileType", "type" }),
                    BuildReaders(type, new[] { "TileFrameX", "frameX" }),
                    BuildReaders(type, new[] { "TileFrameY", "frameY" }),
                    BuildReaders(type, new[] { "liquid", "LiquidAmount" }),
                    BuildReaders(type, new[] { "LiquidType", "liquidType" }));
            }

            public bool TryReadActive(object tile, out bool active)
            {
                active = false;
                object raw;
                if (!TryReadFirst(_activeReaders, tile, out raw))
                {
                    return false;
                }

                try
                {
                    active = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public bool TryReadType(object tile, out int type)
            {
                type = -1;
                object raw;
                if (!TryReadFirst(_typeReaders, tile, out raw))
                {
                    return false;
                }

                try
                {
                    type = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public bool TryReadFrameX(object tile, out int frameX)
            {
                return TryReadInt(_frameXReaders, tile, out frameX);
            }

            public bool TryReadFrameY(object tile, out int frameY)
            {
                return TryReadInt(_frameYReaders, tile, out frameY);
            }

            public bool TryReadLiquidAmount(object tile, out int liquid)
            {
                return TryReadInt(_liquidAmountReaders, tile, out liquid);
            }

            public bool TryReadLiquidType(object tile, out int liquidType)
            {
                return TryReadInt(_liquidTypeReaders, tile, out liquidType);
            }

            private static bool TryReadInt(MemberReader[] readers, object instance, out int value)
            {
                value = 0;
                object raw;
                if (!TryReadFirst(readers, instance, out raw))
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

            private static bool TryReadFirst(MemberReader[] readers, object instance, out object value)
            {
                value = null;
                if (readers == null || instance == null)
                {
                    return false;
                }

                for (var index = 0; index < readers.Length; index++)
                {
                    var reader = readers[index];
                    if (reader != null && reader.TryRead(instance, out value))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static MemberReader[] BuildReaders(Type type, string[] names)
            {
                var readers = new List<MemberReader>();
                for (var index = 0; names != null && index < names.Length; index++)
                {
                    var reader = ResolveReader(type, names[index]);
                    if (reader != null)
                    {
                        readers.Add(reader);
                    }
                }

                return readers.ToArray();
            }

            private static MemberReader ResolveReader(Type type, string name)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                try
                {
                    var field = type.GetField(name, InstanceFlags);
                    if (field != null)
                    {
                        return MemberReader.FromField(field);
                    }

                    var property = type.GetProperty(name, InstanceFlags);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        return MemberReader.FromProperty(property);
                    }

                    var method = type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        return MemberReader.FromMethod(method);
                    }
                }
                catch
                {
                }

                return null;
            }
        }

        private sealed class MemberReader
        {
            private readonly Func<object, object> _reader;

            private MemberReader(Func<object, object> reader)
            {
                _reader = reader;
            }

            public static MemberReader FromField(FieldInfo field)
            {
                if (field == null)
                {
                    return null;
                }

                return new MemberReader(CompileFieldReader(field) ?? field.GetValue);
            }

            public static MemberReader FromProperty(PropertyInfo property)
            {
                if (property == null)
                {
                    return null;
                }

                return new MemberReader(CompilePropertyReader(property) ?? (instance => property.GetValue(instance, null)));
            }

            public static MemberReader FromMethod(MethodInfo method)
            {
                if (method == null)
                {
                    return null;
                }

                return new MemberReader(CompileMethodReader(method) ?? (instance => method.Invoke(instance, null)));
            }

            public bool TryRead(object instance, out object value)
            {
                value = null;
                if (_reader == null || instance == null)
                {
                    return false;
                }

                try
                {
                    value = _reader(instance);
                    return value != null;
                }
                catch
                {
                    return false;
                }
            }

            private static Func<object, object> CompileFieldReader(FieldInfo field)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Field(Expression.Convert(instance, field.DeclaringType), field);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompilePropertyReader(PropertyInfo property)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Property(Expression.Convert(instance, property.DeclaringType), property);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompileMethodReader(MethodInfo method)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Call(Expression.Convert(instance, method.DeclaringType), method);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompileBoxedReader(ParameterExpression instance, Expression body)
            {
                Expression boxed;
                if (body.Type == typeof(void))
                {
                    boxed = Expression.Constant(null, typeof(object));
                }
                else
                {
                    boxed = Expression.Convert(body, typeof(object));
                }

                return Expression.Lambda<Func<object, object>>(boxed, instance).Compile();
            }
        }
    }
}
