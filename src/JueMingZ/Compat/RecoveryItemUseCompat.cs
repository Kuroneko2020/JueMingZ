using System;
using System.Reflection;

namespace JueMingZ.Compat
{
    public static class RecoveryItemUseCompat
    {
        private static bool _resolved;
        private static MethodInfo _tryStartUseMethod;
        private static MethodInfo _applyPotionDelayMethod;
        private static MethodInfo _applyLifeAndOrManaMethod;
        private static MethodInfo _canConsumeConsumableItemMethod;
        private static MethodInfo _tryResetHungerMethod;
        private static string _resolveMessage = string.Empty;

        public static string LastMessage { get; private set; } = string.Empty;

        public static bool TryStartUse(object player, object item, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (!EnsureResolved(player, item))
            {
                message = _resolveMessage;
                LastMessage = message;
                return false;
            }

            if (_tryStartUseMethod == null)
            {
                message = "Player.ItemCheck_TryStartUse was not found.";
                LastMessage = message;
                return false;
            }

            try
            {
                var value = _tryStartUseMethod.Invoke(player, new[] { item, (object)false });
                invoked = true;
                var allowed = value is bool && (bool)value;
                message = allowed ? "ItemCheck_TryStartUse accepted recovery item." : "ItemCheck_TryStartUse rejected recovery item.";
                LastMessage = message;
                return allowed;
            }
            catch (Exception error)
            {
                message = "ItemCheck_TryStartUse failed: " + Unwrap(error);
                LastMessage = message;
                return false;
            }
        }

        public static bool TryApplyPotionDelay(object player, object item, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (!EnsureResolved(player, item) || _applyPotionDelayMethod == null)
            {
                message = _applyPotionDelayMethod == null ? "Player.ApplyPotionDelay was not found." : _resolveMessage;
                LastMessage = message;
                return false;
            }

            try
            {
                _applyPotionDelayMethod.Invoke(player, new[] { item });
                invoked = true;
                message = "Player.ApplyPotionDelay invoked.";
                LastMessage = message;
                return true;
            }
            catch (Exception error)
            {
                message = "Player.ApplyPotionDelay failed: " + Unwrap(error);
                LastMessage = message;
                return false;
            }
        }

        public static bool TryApplyLifeAndOrMana(object player, object item, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (!EnsureResolved(player, item) || _applyLifeAndOrManaMethod == null)
            {
                message = _applyLifeAndOrManaMethod == null ? "Player.ApplyLifeAndOrMana was not found." : _resolveMessage;
                LastMessage = message;
                return false;
            }

            try
            {
                _applyLifeAndOrManaMethod.Invoke(player, new[] { item });
                invoked = true;
                message = "Player.ApplyLifeAndOrMana invoked.";
                LastMessage = message;
                return true;
            }
            catch (Exception error)
            {
                message = "Player.ApplyLifeAndOrMana failed: " + Unwrap(error);
                LastMessage = message;
                return false;
            }
        }

        public static bool TryCanConsume(object player, object item, out bool canConsume, out string message)
        {
            canConsume = false;
            message = string.Empty;
            if (!EnsureResolved(player, item) || _canConsumeConsumableItemMethod == null)
            {
                message = _canConsumeConsumableItemMethod == null ? "Player.CanConsumeConsumableItem was not found." : _resolveMessage;
                LastMessage = message;
                return false;
            }

            try
            {
                var value = _canConsumeConsumableItemMethod.Invoke(player, new[] { item });
                canConsume = value is bool && (bool)value;
                message = "Player.CanConsumeConsumableItem invoked.";
                LastMessage = message;
                return true;
            }
            catch (Exception error)
            {
                message = "Player.CanConsumeConsumableItem failed: " + Unwrap(error);
                LastMessage = message;
                return false;
            }
        }

        public static void TryResetHungerToNeutral(object player)
        {
            if (player == null || _tryResetHungerMethod == null)
            {
                return;
            }

            try
            {
                _tryResetHungerMethod.Invoke(player, new object[0]);
            }
            catch
            {
            }
        }

        private static bool EnsureResolved(object player, object item)
        {
            if (_resolved)
            {
                return string.IsNullOrEmpty(_resolveMessage);
            }

            _resolved = true;
            if (player == null || item == null)
            {
                _resolveMessage = "Cannot resolve recovery item use methods: player or item unavailable.";
                return false;
            }

            var playerType = player.GetType();
            var itemType = item.GetType();
            _tryStartUseMethod = FindMethod(playerType, "ItemCheck_TryStartUse", itemType, typeof(bool), typeof(bool));
            _applyPotionDelayMethod = FindMethod(playerType, "ApplyPotionDelay", itemType, null, typeof(void));
            _applyLifeAndOrManaMethod = FindMethod(playerType, "ApplyLifeAndOrMana", itemType, null, typeof(void));
            _canConsumeConsumableItemMethod = FindMethod(playerType, "CanConsumeConsumableItem", itemType, null, typeof(bool));
            _tryResetHungerMethod = FindZeroParameterMethod(playerType, "TryToResetHungerToNeutral");

            if (_tryStartUseMethod == null || _applyPotionDelayMethod == null || _applyLifeAndOrManaMethod == null || _canConsumeConsumableItemMethod == null)
            {
                _resolveMessage = "One or more original recovery item methods were not found on " + playerType.FullName + ".";
                return false;
            }

            _resolveMessage = string.Empty;
            return true;
        }

        private static MethodInfo FindMethod(Type playerType, string name, Type itemType, Type secondParameterType, Type returnType)
        {
            var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (returnType != null && method.ReturnType != returnType)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (secondParameterType == null)
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(itemType))
                    {
                        return method;
                    }
                }
                else if (parameters.Length == 2 &&
                         parameters[0].ParameterType.IsAssignableFrom(itemType) &&
                         parameters[1].ParameterType == secondParameterType)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindZeroParameterMethod(Type playerType, string name)
        {
            var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (string.Equals(method.Name, name, StringComparison.Ordinal) && method.GetParameters().Length == 0)
                {
                    return method;
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
