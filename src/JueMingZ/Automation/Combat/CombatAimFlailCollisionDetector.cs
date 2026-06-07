using System;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    internal sealed class CombatAimFlailCollisionDetector
    {
        private readonly Func<Type> _collisionTypeResolver;
        private MethodInfo _tileCollisionMethod;
        private bool _tileCollisionResolved;

        public CombatAimFlailCollisionDetector()
            : this(DefaultCollisionTypeResolver)
        {
        }

        internal CombatAimFlailCollisionDetector(Func<Type> collisionTypeResolver)
        {
            _collisionTypeResolver = collisionTypeResolver ?? DefaultCollisionTypeResolver;
        }

        public bool DetectTileCollision(CombatAimFlailControlService.FlailProjectileSnapshot projectile)
        {
            if (projectile == null || Math.Abs(projectile.Ai0 - 1f) > 0.001f)
            {
                return false;
            }

            float velocityX;
            float velocityY;
            if (!GameStateReflection.TryReadVector2(projectile.Velocity, out velocityX, out velocityY) ||
                Math.Abs(velocityX) < 0.001f && Math.Abs(velocityY) < 0.001f)
            {
                return false;
            }

            var method = ResolveTileCollisionMethod();
            if (method == null)
            {
                return false;
            }

            // TileCollision is reflection-backed; when the signature or call
            // cannot be proven, collision stays false instead of guessing Tile state.
            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (index == 0)
                    {
                        args[index] = projectile.Position;
                    }
                    else if (index == 1)
                    {
                        args[index] = projectile.Velocity;
                    }
                    else if (index == 2)
                    {
                        args[index] = projectile.Width;
                    }
                    else if (index == 3)
                    {
                        args[index] = projectile.Height;
                    }
                    else if (parameters[index].ParameterType == typeof(bool))
                    {
                        args[index] = false;
                    }
                    else if (parameters[index].ParameterType == typeof(int))
                    {
                        args[index] = 1;
                    }
                    else
                    {
                        args[index] = parameters[index].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[index].ParameterType)
                            : null;
                    }
                }

                var result = method.Invoke(null, args);
                float resultX;
                float resultY;
                if (!GameStateReflection.TryReadVector2(result, out resultX, out resultY))
                {
                    return false;
                }

                return Math.Abs(resultX - velocityX) > 0.001f ||
                       Math.Abs(resultY - velocityY) > 0.001f;
            }
            catch
            {
                return false;
            }
        }

        private static Type DefaultCollisionTypeResolver()
        {
            return TerrariaTypeCache.Find("Terraria.Collision");
        }

        private MethodInfo ResolveTileCollisionMethod()
        {
            if (_tileCollisionResolved)
            {
                return _tileCollisionMethod;
            }

            _tileCollisionResolved = true;
            var type = _collisionTypeResolver();
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TileCollision", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 4)
                {
                    continue;
                }

                if (string.Equals(parameters[0].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    string.Equals(parameters[1].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    parameters[2].ParameterType == typeof(int) &&
                    parameters[3].ParameterType == typeof(int))
                {
                    _tileCollisionMethod = method;
                    return _tileCollisionMethod;
                }
            }

            return null;
        }
    }
}
